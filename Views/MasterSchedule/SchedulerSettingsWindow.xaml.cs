using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Services;
using UniversityScheduler.Data; 
using System.ComponentModel;    
using System.Windows.Data;      
using System.Windows.Input;


namespace UniversityScheduler.Views
{
    public partial class SchedulerSettingsWindow : Window
    {
        private SchedulerSettings _settings = new SchedulerSettings();
        private List<string> _allCourseCodes = new List<string>();
        private int _detectedCores;

        // Observable collections bind to the Lists in the UI
        public ObservableCollection<DayConstraint> TempDayRules { get; set; } = new ObservableCollection<DayConstraint>();
        public ObservableCollection<TimeConstraint> TempTimeRules { get; set; } = new ObservableCollection<TimeConstraint>();
        public ObservableCollection<string> TempExclusions { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> TempSplitExceptions { get; set; } = new ObservableCollection<string>();

        public SchedulerSettingsWindow()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            // 1. Load Settings & Hardware Info
            _settings = SchedulerSettings.Load(); 
            _detectedCores = _settings.DetectedCores;

            // 2. Bind General Constraints
            // Note: We reference the CheckBox by name 'LunchBreakCheck' defined in XAML
            if (LunchBreakCheck != null)
                LunchBreakCheck.IsChecked = _settings.AvoidLunchBreak;

            // 3. Bind Hours Dropdowns
            StartHourCombo.SelectedIndex = _settings.DayStartHour == 6 ? 0 : (_settings.DayStartHour == 8 ? 2 : 1);
            EndHourCombo.SelectedIndex = _settings.DayEndHour == 19 ? 0 : (_settings.DayEndHour == 21 ? 2 : 1);

            // 4. Bind Time Limit Slider
            TimeLimitSlider.Value = _settings.MaxCalculationTimeSeconds / 60;
            TimeLimitLabel.Text = $"{TimeLimitSlider.Value} mins";

            // 5. Load Flexible Rules into Observable Collections (UI Binding)
            TempDayRules = new ObservableCollection<DayConstraint>(_settings.DayRules ?? new List<DayConstraint>());
            TempTimeRules = new ObservableCollection<TimeConstraint>(_settings.TimeRules ?? new List<TimeConstraint>());
            TempExclusions = new ObservableCollection<string>(_settings.ExcludedCourses ?? new List<string>());

            // Set ItemSource for the ListBoxes
            if (DayRulesList != null) DayRulesList.ItemsSource = TempDayRules;
            if (TimeRulesList != null) TimeRulesList.ItemsSource = TempTimeRules;
            if (ExclusionList != null) ExclusionList.ItemsSource = TempExclusions;

            // 6. Bind Performance Tab
            HardwareInfoTxt.Text = _settings.SystemInfoSummary;

            if (_settings.MaxSearchWorkers <= 1) PerformanceSlider.Value = 1;
            else if (_settings.MaxSearchWorkers >= _detectedCores - 1) PerformanceSlider.Value = 3;
            else PerformanceSlider.Value = 2;

            // --- NEW PATTERN BINDINGS ---
            BlockSplitCheck.IsChecked = _settings.EnableBlockSplitting;
            
            // Bind Exceptions List
            TempSplitExceptions = new ObservableCollection<string>(_settings.SplittingExceptions ?? new List<string>());
            SplitExceptionList.ItemsSource = TempSplitExceptions;

            // Bind Sibling Combo
            foreach (ComboBoxItem item in SiblingPatternCombo.Items)
            {
                if (item.Tag.ToString() == _settings.SiblingPattern)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            UpdatePerformanceText();

            using (var db = new AppDbContext())
            {
                // Safety check if database exists
                if (db.Database.CanConnect())
                {
                    _allCourseCodes = db.Courses
                        .Select(c => c.Code)
                        .Distinct()
                        .OrderBy(c => c)
                        .ToList();
                }
            }

            // Assign the list to your inputs (These must be ComboBoxes in XAML now)
            if (DayRuleInput != null) DayRuleInput.ItemsSource = _allCourseCodes;
            if (TimeRuleInput != null) TimeRuleInput.ItemsSource = _allCourseCodes;
            if (ExclusionInput != null) ExclusionInput.ItemsSource = _allCourseCodes;
            if (SplitExceptionInput != null) SplitExceptionInput.ItemsSource = _allCourseCodes;
        }

        private void CourseInput_KeyUp(object sender, KeyEventArgs e)
        {
            var cmb = sender as ComboBox;
            if (cmb == null || cmb.ItemsSource == null) return;

            // Get the default view of the list we bound earlier
            var view = CollectionViewSource.GetDefaultView(cmb.ItemsSource);
            if (view == null) return;

            // Filter logic: Show items that contain the typed text
            string text = cmb.Text.ToUpper();
            
            view.Filter = item =>
            {
                if (string.IsNullOrEmpty(text)) return true;
                return item.ToString()!.Contains(text); 
            };

            // Auto-open the dropdown to show results
            cmb.IsDropDownOpen = true;

            // Fix Cursor Position (WPF ComboBox quirk)
            // Without this, the cursor jumps to the start after every keystroke
            var textBox = (TextBox)cmb.Template.FindName("PART_EditableTextBox", cmb);
            if (textBox != null)
            {
                textBox.SelectionStart = text.Length;
            }
        }

        // --- PATTERN TAB HANDLERS ---
        private void AddSplitException_Click(object sender, RoutedEventArgs e)
        {
            if (SplitExceptionInput == null) return;
            string course = SplitExceptionInput.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(course)) return;

            if (!TempSplitExceptions.Contains(course))
            {
                TempSplitExceptions.Add(course);
                // FIX: ComboBox doesn't have .Clear(), use .Text = ""
                SplitExceptionInput.Text = string.Empty;
            }
        }
        
        private void RemoveSplitException_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is string s) TempSplitExceptions.Remove(s);
        }

        // --- RULE ENGINE EVENT HANDLERS ---
        private void AddDayRule_Click(object sender, RoutedEventArgs e)
        {
            if (DayRuleInput == null) return;
            string course = DayRuleInput.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(course)) return;

            var days = new List<string>();
            if (DayCbMon.IsChecked == true) days.Add("Mon");
            if (DayCbTue.IsChecked == true) days.Add("Tue");
            if (DayCbWed.IsChecked == true) days.Add("Wed");
            if (DayCbThu.IsChecked == true) days.Add("Thu");
            if (DayCbFri.IsChecked == true) days.Add("Fri");
            if (DayCbSat.IsChecked == true) days.Add("Sat");

            if (days.Count == 0) { MessageBox.Show("Select at least one day."); return; }

            TempDayRules.Add(new DayConstraint { CoursePrefix = course, AllowedDays = days });
            
            // FIX: ComboBox doesn't have .Clear(), use .Text = ""
            DayRuleInput.Text = string.Empty; 
        }
        
        private void RemoveDayRule_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is DayConstraint rule) TempDayRules.Remove(rule);
        }

        private void AddTimeRule_Click(object sender, RoutedEventArgs e)
        {
            if (TimeRuleInput == null) return;
            string course = TimeRuleInput.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(course)) return;

            if (TimeRuleCombo.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                int hour = int.Parse(item.Tag.ToString() ?? "16");
                TempTimeRules.Add(new TimeConstraint { CoursePrefix = course, LatestEndHour = hour });
                
                // FIX: ComboBox doesn't have .Clear(), use .Text = ""
                TimeRuleInput.Text = string.Empty; 
            }
        }

        private void RemoveTimeRule_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is TimeConstraint rule) TempTimeRules.Remove(rule);
        }

        private void AddExclusion_Click(object sender, RoutedEventArgs e)
        {
            if (ExclusionInput == null) return;
            string course = ExclusionInput.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(course)) return;
            
            if (!TempExclusions.Contains(course))
            {
                TempExclusions.Add(course);
                // FIX: ComboBox doesn't have .Clear(), use .Text = ""
                ExclusionInput.Text = string.Empty; 
            }
        }        
        
        private void RemoveExclusion_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is string s) TempExclusions.Remove(s);
        }

        // --- STANDARD EVENTS ---
        private void TimeLimitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TimeLimitLabel != null)
                TimeLimitLabel.Text = $"{(int)e.NewValue} mins";
        }

        private void PerformanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Ensure UI is loaded before running logic
            if (PerformanceDescTxt == null) return;
            UpdatePerformanceText();
        }

        private void UpdatePerformanceText()
        {
            if (PerformanceDescTxt == null) return;

            int val = (int)PerformanceSlider.Value;
            switch (val)
            {
                case 1:
                    PerformanceDescTxt.Text = "Eco Mode: Uses 1 CPU thread. Slowest, but uses minimal RAM. Best for older laptops.";
                    break;
                case 2:
                    PerformanceDescTxt.Text = $"Balanced: Uses ~50% of CPU ({Math.Max(1, _detectedCores / 2)} threads). Good balance of speed and stability.";
                    break;
                case 3:
                    PerformanceDescTxt.Text = $"Max Power: Uses almost all cores ({Math.Max(1, _detectedCores - 1)} threads). Fastest, but high RAM usage. PC may lag during generation.";
                    break;
            }

            // --- LOW RAM WARNING LOGIC ---
            // Check if user forces 'Max Power' on a low RAM system
            bool isMaxPower = (val == 3);
            
            // Safety Check: LowRamWarning might be null if XAML isn't updated
            if (LowRamWarning != null && HardwareInfoTxt != null)
            {
                bool hasLowRam = HardwareInfoTxt.Text.Contains("Free RAM") &&
                                 (HardwareInfoTxt.Text.Contains(" 3.") || 
                                  HardwareInfoTxt.Text.Contains(" 2.") || 
                                  HardwareInfoTxt.Text.Contains(" 1.")); // < 4GB detected

                if (isMaxPower && hasLowRam)
                    LowRamWarning.Visibility = Visibility.Visible;
                else
                    LowRamWarning.Visibility = Visibility.Collapsed;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. Save General
            if (LunchBreakCheck != null)
                _settings.AvoidLunchBreak = LunchBreakCheck.IsChecked == true;

            _settings.MaxCalculationTimeSeconds = (int)TimeLimitSlider.Value * 60;

            if (StartHourCombo.SelectedItem is ComboBoxItem startItem)
                _settings.DayStartHour = int.Parse(startItem.Tag.ToString() ?? "7");
            
            if (EndHourCombo.SelectedItem is ComboBoxItem endItem)
                _settings.DayEndHour = int.Parse(endItem.Tag.ToString() ?? "20");

            // 2. Save Rule Lists (Convert ObservableCollection back to List)
            _settings.DayRules = TempDayRules.ToList();
            _settings.TimeRules = TempTimeRules.ToList();
            _settings.ExcludedCourses = TempExclusions.ToList();

            // 3. Save Performance
            int sliderVal = (int)PerformanceSlider.Value;
            if (sliderVal == 1) _settings.MaxSearchWorkers = 1; 
            else if (sliderVal == 3) _settings.MaxSearchWorkers = Math.Max(1, _detectedCores - 1); 
            else _settings.MaxSearchWorkers = Math.Max(1, _detectedCores / 2); 

            // --- SAVE PATTERNS ---
            if (BlockSplitCheck != null)
                _settings.EnableBlockSplitting = BlockSplitCheck.IsChecked == true;
            
            _settings.SplittingExceptions = TempSplitExceptions.ToList();

            if (SiblingPatternCombo.SelectedItem is ComboBoxItem patternItem)
                _settings.SiblingPattern = patternItem.Tag.ToString() ?? "Strict";


            // 4. Write to Disk
            _settings.Save();

            MessageBox.Show("Settings saved! They will apply to the next generation task.", "Saved");
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}