using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Views
{
    public class SelectableProgram
    {
        public string Name { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class TimeBlockItem
    {
        public string DisplayString { get; set; } = string.Empty;
        public string ValueString { get; set; } = string.Empty;
    }
    public class PreferredCourseItem
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayString { get; set; } = string.Empty;
        public string FullDisplay => $"{Code} - {DisplayString}";
    }

    public partial class AddInstructorWindow : Window
    {
        private ObservableCollection<TimeBlockItem> _timeBlocks = new ObservableCollection<TimeBlockItem>();
        private ObservableCollection<PreferredCourseItem> _preferredCourses = new ObservableCollection<PreferredCourseItem>();
        private List<PreferredCourseItem> _allDatabaseCourses = new List<PreferredCourseItem>();
        public ObservableCollection<SelectableProgram> AvailablePrograms { get; set; } = new ObservableCollection<SelectableProgram>();
        private int _editingId = 0; 

        public AddInstructorWindow()
        {
            InitializeComponent();
            InitializeLists();
            LoadPrograms(null);
            LoadRooms();    
        }

        // Auto-Generate Initials
        private void NameTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_editingId == 0)
            {
                // Safely grab the text from the new boxes (using ? in case they are initializing)
                string first = FirstNameTxt?.Text?.Trim() ?? "";
                string middle = MiddleNameTxt?.Text?.Trim() ?? "";
                string last = SurnameTxt?.Text?.Trim() ?? "";

                string combinedName = $"{first} {middle} {last}";
                var parts = combinedName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                string initials = "";
                foreach (var part in parts)
                {
                    if (part.Length > 0 && char.IsLetter(part[0]))
                        initials += char.ToUpper(part[0]);
                }
                
                if (InitialsTxt != null) InitialsTxt.Text = initials;
            }
        }

        public AddInstructorWindow(Instructor instructorToEdit)
        {
            InitializeComponent();
            InitializeLists();
            
            _editingId = instructorToEdit.Id;
            this.Title = "Edit Instructor";
            
            TitleCombo.Text = instructorToEdit.Title;
            SurnameTxt.Text = instructorToEdit.Surname;
            FirstNameTxt.Text = instructorToEdit.FirstName;
            MiddleNameTxt.Text = instructorToEdit.MiddleName;
            SuffixTxt.Text = instructorToEdit.Suffix;
            InitialsTxt.Text = instructorToEdit.Initials;
            UnitsTxt.Text = instructorToEdit.MaxUnits.ToString();
            AddressTxt.Text = instructorToEdit.HomeAddress;
            BaccTxt.Text = instructorToEdit.BaccalaureateDegree;
            MasterTxt.Text = instructorToEdit.MastersDegree;
            DoctorTxt.Text = instructorToEdit.DoctoralDegree;
            ExpPublicTxt.Text = instructorToEdit.ExperiencePublic.ToString();
            ExpPrivateTxt.Text = instructorToEdit.ExperiencePrivate.ToString();
            
            // 1. Set Assigned Room
            if (instructorToEdit.AssignedRoomId != null)
            {
                AssignedRoomCombo.SelectedValue = instructorToEdit.AssignedRoomId;
            }

            // 2. Set Preferred Years (Parse "1,3" string)
            if (!string.IsNullOrEmpty(instructorToEdit.PreferredYearLevels))
            {
                var years = instructorToEdit.PreferredYearLevels.Split(',');
                CbYear1.IsChecked = years.Contains("1");
                CbYear2.IsChecked = years.Contains("2");
                CbYear3.IsChecked = years.Contains("3");
                CbYear4.IsChecked = years.Contains("4");
            }

            foreach (ComboBoxItem item in StatusCombo.Items)
                if (item.Content.ToString() == instructorToEdit.Status) 
                    StatusCombo.SelectedItem = item;

            LoadPrograms(instructorToEdit.Program);
            LoadRooms();

            if (instructorToEdit.AssignedRoomId != null)
            {
                AssignedRoomCombo.SelectedValue = instructorToEdit.AssignedRoomId;
            }

            if (!string.IsNullOrEmpty(instructorToEdit.PreferredYearLevels))
            {
                var years = instructorToEdit.PreferredYearLevels.Split(',');
                CbYear1.IsChecked = years.Contains("1");
                CbYear2.IsChecked = years.Contains("2");
                CbYear3.IsChecked = years.Contains("3");
                CbYear4.IsChecked = years.Contains("4");
            }

            if (!string.IsNullOrEmpty(instructorToEdit.SchedulePreferences))
            {
                var blocks = instructorToEdit.SchedulePreferences.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var block in blocks)
                {
                    var parts = block.Split('|');
                    if (parts.Length == 2)
                    {
                        _timeBlocks.Add(new TimeBlockItem 
                        { 
                            DisplayString = $"{parts[0]} : {parts[1]}", 
                            ValueString = block 
                        });
                    }
                }
            }

            if (!string.IsNullOrEmpty(instructorToEdit.PreferredCourseCodes))
            {
                var codes = instructorToEdit.PreferredCourseCodes.Split(',', StringSplitOptions.RemoveEmptyEntries);
                using (var db = new AppDbContext())
                {
                    foreach (var code in codes)
                    {
                        var c = db.Courses.FirstOrDefault(x => x.Code == code);
                        if (c != null)
                            _preferredCourses.Add(new PreferredCourseItem { Code = c.Code, DisplayString = c.Name });
                    }
                }
            }

        }

        private void InitializeLists()
        {
            PreferencesList.ItemsSource = _timeBlocks;
            ProgramCheckList.ItemsSource = AvailablePrograms;
            PreferredCoursesList.ItemsSource = _preferredCourses;
        }

        private void LoadRooms()
        {
            using (var db = new AppDbContext())
            {
                // Get all rooms, sorted by Name
                var rooms = db.Rooms.OrderBy(r => r.Name).ToList();
                
                // Add a "None" option (optional, or just allow null selection)
                // For simplicity, we just bind the list. The user can leave it empty.
                AssignedRoomCombo.ItemsSource = rooms;
            }
        }

        private void LoadPrograms(string? existingPrograms)
        {
            AvailablePrograms.Clear();
            using (var db = new AppDbContext())
            {
                if (db.Database.CanConnect())
                {
                    var programs = db.Programs
                        .Select(p => p.Code)
                        .OrderBy(p => p)
                        .ToList();

                    foreach (var p in programs)
                    {
                        if (p != "General Education")
                            AvailablePrograms.Add(new SelectableProgram { Name = p, IsSelected = false });
                    }
                }
            }

            // Re-check the boxes if we are editing an existing item
            if (!string.IsNullOrEmpty(existingPrograms))
            {
                var currentTags = existingPrograms.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in AvailablePrograms)
                {
                    if (currentTags.Contains(item.Name)) item.IsSelected = true;
                }
            }
        }

        private void PreferredCourseCombo_DropDownOpened(object sender, EventArgs e)
        {
            // 1. Figure out what programs and years the user has selected
            var selectedProgs = AvailablePrograms.Where(p => p.IsSelected).Select(p => p.Name).ToList();
            
            List<int> selectedYears = new List<int>();
            if (CbYear1.IsChecked == true) selectedYears.Add(1);
            if (CbYear2.IsChecked == true) selectedYears.Add(2);
            if (CbYear3.IsChecked == true) selectedYears.Add(3);
            if (CbYear4.IsChecked == true) selectedYears.Add(4);

            using (var db = new AppDbContext())
            {
                // Find all curriculums that match the selected programs AND selected years
                var validCourses = db.Curriculums
                    .Include(c => c.Course)
                    .Where(c => selectedProgs.Contains(c.Program) && selectedYears.Contains(c.YearLevel))
                    .Select(c => c.Course)
                    .Distinct()
                    .Where(c => c != null)
                    .ToList();

                // Convert to our UI item
                _allDatabaseCourses = validCourses.Select(c => new PreferredCourseItem 
                { 
                    Code = c!.Code, 
                    DisplayString = c.Name 
                }).OrderBy(c => c.Code).ToList();

                PreferredCourseCombo.ItemsSource = _allDatabaseCourses;
            }

            if (_allDatabaseCourses.Count == 0)
            {
                MessageBox.Show("No courses found. Please ensure you have selected at least one Program and one Year Level above.", "Filter Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddPreferredCourse_Click(object sender, RoutedEventArgs e)
        {
            if (PreferredCourseCombo.SelectedItem is PreferredCourseItem selectedCourse)
            {
                if (_preferredCourses.Any(c => c.Code == selectedCourse.Code))
                {
                    MessageBox.Show("This course is already in the preferred list.");
                    return;
                }

                _preferredCourses.Add(selectedCourse);
                PreferredCourseCombo.SelectedItem = null;
                PreferredCourseCombo.Text = "";
            }
            else
            {
                MessageBox.Show("Please select a valid course from the dropdown.");
            }
        }

        private void RemovePreferredCourse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PreferredCourseItem item)
            {
                _preferredCourses.Remove(item);
            }
        }

        // --- NEW HELPER METHODS FOR SELECT ALL/NONE ---
        private void SelectAllDays_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in DayCheckboxes.Children)
                if (child is CheckBox cb) cb.IsChecked = true;
        }

        private void SelectNoDays_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in DayCheckboxes.Children)
                if (child is CheckBox cb) cb.IsChecked = false;
        }

        // --- NEW ADD BLOCK LOGIC (USING COMBOBOXES) ---
        private void AddBlock_Click(object sender, RoutedEventArgs e)
        {
            // 1. Build time strings from dropdowns
            string startStr = $"{StartHourCombo.Text}:{StartMinCombo.Text} {StartAmPmCombo.Text}";
            string endStr   = $"{EndHourCombo.Text}:{EndMinCombo.Text} {EndAmPmCombo.Text}";

            // 2. Parse
            if (!DateTime.TryParse(startStr, out DateTime startDt) || 
                !DateTime.TryParse(endStr, out DateTime endDt))
            {
                MessageBox.Show("Invalid Time Selection");
                return;
            }

            // 3. Auto-Correct: If End Time is before Start Time, assume +12 hours (e.g., 11AM to 1PM)
            if (endDt <= startDt)
            {
                if (endDt.AddHours(12) > startDt)
                    endDt = endDt.AddHours(12);
                else
                {
                    MessageBox.Show("End time must be after Start time.");
                    return;
                }
            }

            // 4. Gather Days
            List<string> selectedDays = new List<string>();
            foreach (var child in DayCheckboxes.Children)
            {
                if (child is CheckBox cb && cb.IsChecked == true)
                {
                    string day = cb.Content?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(day)) selectedDays.Add(day);
                }
            }

            if (selectedDays.Count == 0) { MessageBox.Show("Select at least one day."); return; }

            string daysStr = string.Join(",", selectedDays);
            string timeStr = $"{startDt.ToString("h:mm tt")} - {endDt.ToString("h:mm tt")}";
            string newValueString = $"{daysStr}|{timeStr}";

            // 5. Prevent Duplicates
            if (_timeBlocks.Any(b => b.ValueString == newValueString))
            {
                MessageBox.Show("This time block is already added.");
                return;
            }

            _timeBlocks.Add(new TimeBlockItem
            {
                DisplayString = $"{daysStr} : {timeStr}",
                ValueString = newValueString
            });
        }

        private void RemoveBlock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TimeBlockItem item)
            {
                _timeBlocks.Remove(item);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SurnameTxt.Text) || string.IsNullOrWhiteSpace(FirstNameTxt.Text)) 
            { 
                MessageBox.Show("Surname and First Name are required."); 
                return; 
            }
            if (!int.TryParse(UnitsTxt.Text, out int units)) { MessageBox.Show("Invalid Units."); return; }

            var selectedProgs = AvailablePrograms.Where(p => p.IsSelected).Select(p => p.Name).ToList();
            if (selectedProgs.Count == 0) { MessageBox.Show("Please check at least one program."); return; }
            
            // 1. Get Selected Room ID
            int? roomId = null;
            if (AssignedRoomCombo.SelectedValue != null)
            {
                roomId = (int)AssignedRoomCombo.SelectedValue;
            }

            // 2. Get Preferred Years String (e.g., "1,2,4")
            List<string> selectedYears = new List<string>();
            if (CbYear1.IsChecked == true) selectedYears.Add("1");
            if (CbYear2.IsChecked == true) selectedYears.Add("2");
            if (CbYear3.IsChecked == true) selectedYears.Add("3");
            if (CbYear4.IsChecked == true) selectedYears.Add("4");
            
            string yearString = string.Join(",", selectedYears);

            string finalProgramStr = string.Join(", ", selectedProgs);
            string finalSchedule = string.Join(";", _timeBlocks.Select(b => b.ValueString));
            string finalCoursePrefs = string.Join(",", _preferredCourses.Select(c => c.Code));

            int expPub = int.TryParse(ExpPublicTxt.Text, out int ep) ? ep : 0;
            int expPriv = int.TryParse(ExpPrivateTxt.Text, out int epr) ? epr : 0;

            using (var db = new AppDbContext())
            {
                if (_editingId == 0)
                {
                    var newInstructor = new Instructor
                    {
                        Title = TitleCombo.Text.Trim(),
                        Surname = SurnameTxt.Text.Trim(),
                        FirstName = FirstNameTxt.Text.Trim(),
                        MiddleName = MiddleNameTxt.Text.Trim(),
                        Suffix = SuffixTxt.Text.Trim(),
                        Initials = InitialsTxt.Text,
                        Status = (StatusCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Full-time",
                        Program = finalProgramStr,
                        MaxUnits = units,
                        SchedulePreferences = finalSchedule,
                        AssignedRoomId = roomId,
                        PreferredYearLevels = yearString,
                        PreferredCourseCodes = finalCoursePrefs,
                        HomeAddress = AddressTxt.Text,
                        BaccalaureateDegree = BaccTxt.Text,
                        MastersDegree = MasterTxt.Text,
                        DoctoralDegree = DoctorTxt.Text,
                        ExperiencePublic = expPub,
                        ExperiencePrivate = expPriv
                    };
                    db.Instructors.Add(newInstructor);
                }
                else
                {
                    var existing = db.Instructors.Find(_editingId);
                    if (existing != null)
                    {
                        existing.Title = TitleCombo.Text.Trim();
                        existing.Surname = SurnameTxt.Text.Trim();
                        existing.FirstName = FirstNameTxt.Text.Trim();
                        existing.MiddleName = MiddleNameTxt.Text.Trim();
                        existing.Suffix = SuffixTxt.Text.Trim();
                        existing.Initials = InitialsTxt.Text;
                        existing.Status = (StatusCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Full-time";
                        existing.Program = finalProgramStr;
                        existing.MaxUnits = units;
                        existing.SchedulePreferences = finalSchedule;
                        existing.AssignedRoomId = roomId;
                        existing.PreferredYearLevels = yearString;
                        existing.PreferredCourseCodes = finalCoursePrefs;
                        existing.HomeAddress = AddressTxt.Text;
                        existing.BaccalaureateDegree = BaccTxt.Text;
                        existing.MastersDegree = MasterTxt.Text;
                        existing.DoctoralDegree = DoctorTxt.Text;
                        existing.ExperiencePublic = expPub;
                        existing.ExperiencePrivate = expPriv;
                    }
                }
                db.SaveChanges();
            }
            
            MessageBox.Show("Instructor Saved!");
            this.Close();
        }
    }
}