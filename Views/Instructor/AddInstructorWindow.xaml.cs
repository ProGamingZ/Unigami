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

    public class AssignedSectionItem
    {
        public int Id { get; set; }
        public string FullDisplay { get; set; } = string.Empty;
    }

    public partial class AddInstructorWindow : Window
    {
        // Semester 1 Collections
        private ObservableCollection<TimeBlockItem> _timeBlocksSem1 = new ObservableCollection<TimeBlockItem>();
        private ObservableCollection<PreferredCourseItem> _preferredCoursesSem1 = new ObservableCollection<PreferredCourseItem>();
        private ObservableCollection<AssignedSectionItem> _assignedSectionsSem1 = new ObservableCollection<AssignedSectionItem>();
        public ObservableCollection<SelectableProgram> AvailableProgramsSem1 { get; set; } = new ObservableCollection<SelectableProgram>();

        // Semester 2 Collections
        private ObservableCollection<TimeBlockItem> _timeBlocksSem2 = new ObservableCollection<TimeBlockItem>();
        private ObservableCollection<PreferredCourseItem> _preferredCoursesSem2 = new ObservableCollection<PreferredCourseItem>();
        private ObservableCollection<AssignedSectionItem> _assignedSectionsSem2 = new ObservableCollection<AssignedSectionItem>();
        public ObservableCollection<SelectableProgram> AvailableProgramsSem2 { get; set; } = new ObservableCollection<SelectableProgram>();

        private List<PreferredCourseItem> _allDatabaseCourses = new List<PreferredCourseItem>();
        private List<AssignedSectionItem> _allDatabaseSections = new List<AssignedSectionItem>();
        private int _editingId = 0;

        public AddInstructorWindow()
        {
            InitializeComponent();
            InitializeLists();
            LoadRooms();
            LoadPrograms(AvailableProgramsSem1, null);
            LoadPrograms(AvailableProgramsSem2, null);
        }

        public AddInstructorWindow(Instructor instructorToEdit)
        {
            InitializeComponent();
            InitializeLists();
            LoadRooms();

            _editingId = instructorToEdit.Id;
            this.Title = "Edit Instructor (Global & Semesters)";

            // 1. Load Global Data
            TitleCombo.Text = instructorToEdit.Title;
            SurnameTxt.Text = instructorToEdit.Surname;
            FirstNameTxt.Text = instructorToEdit.FirstName;
            MiddleNameTxt.Text = instructorToEdit.MiddleName;
            SuffixTxt.Text = instructorToEdit.Suffix;
            InitialsTxt.Text = instructorToEdit.Initials;

            AddressTxt.Text = instructorToEdit.HomeAddress;
            BaccTxt.Text = instructorToEdit.BaccalaureateDegree;
            MasterTxt.Text = instructorToEdit.MastersDegree;
            DoctorTxt.Text = instructorToEdit.DoctoralDegree;
            ExpPublicTxt.Text = instructorToEdit.ExperiencePublic.ToString();
            ExpPrivateTxt.Text = instructorToEdit.ExperiencePrivate.ToString();

            // 2. Load Semester 1 Data
            LoadSemesterData(1, instructorToEdit);
            LoadPrograms(AvailableProgramsSem1, instructorToEdit.ProgramSem1);

            // 3. Load Semester 2 Data
            LoadSemesterData(2, instructorToEdit);
            LoadPrograms(AvailableProgramsSem2, instructorToEdit.ProgramSem2);
        }

        private void InitializeLists()
        {
            PreferencesListSem1.ItemsSource = _timeBlocksSem1;
            PreferredCoursesListSem1.ItemsSource = _preferredCoursesSem1;
            AssignedSectionsListSem1.ItemsSource = _assignedSectionsSem1;
            ProgramCheckListSem1.ItemsSource = AvailableProgramsSem1;

            PreferencesListSem2.ItemsSource = _timeBlocksSem2;
            PreferredCoursesListSem2.ItemsSource = _preferredCoursesSem2;
            AssignedSectionsListSem2.ItemsSource = _assignedSectionsSem2;
            ProgramCheckListSem2.ItemsSource = AvailableProgramsSem2;
        }

        private void LoadRooms()
        {
            using (var db = new AppDbContext())
            {
                var rooms = db.Rooms.OrderBy(r => r.Name).ToList();
                AssignedRoomComboSem1.ItemsSource = rooms;
                AssignedRoomComboSem2.ItemsSource = rooms.ToList();
            }
        }

        private void LoadPrograms(ObservableCollection<SelectableProgram> targetList, string? existingPrograms)
        {
            targetList.Clear();
            using (var db = new AppDbContext())
            {
                if (db.Database.CanConnect())
                {
                    var programs = db.Programs.Select(p => p.Code).OrderBy(p => p).ToList();
                    foreach (var p in programs)
                    {
                        if (p != "General Education")
                            targetList.Add(new SelectableProgram { Name = p, IsSelected = false });
                    }
                }
            }

            if (!string.IsNullOrEmpty(existingPrograms))
            {
                var currentTags = existingPrograms.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in targetList)
                {
                    if (currentTags.Contains(item.Name)) item.IsSelected = true;
                }
            }
        }

        private void LoadSemesterData(int semester, Instructor inst)
        {
            if (semester == 1)
            {
                UnitsTxtSem1.Text = inst.MaxUnitsSem1.ToString();
                if (inst.AssignedRoomIdSem1 != null) AssignedRoomComboSem1.SelectedValue = inst.AssignedRoomIdSem1;
                foreach (ComboBoxItem item in StatusComboSem1.Items) if (item.Content.ToString() == inst.StatusSem1) StatusComboSem1.SelectedItem = item;

                if (!string.IsNullOrEmpty(inst.PreferredYearLevelsSem1))
                {
                    var years = inst.PreferredYearLevelsSem1.Split(',');
                    CbYear1Sem1.IsChecked = years.Contains("1"); CbYear2Sem1.IsChecked = years.Contains("2");
                    CbYear3Sem1.IsChecked = years.Contains("3"); CbYear4Sem1.IsChecked = years.Contains("4");
                }
                LoadTimeBlocks(inst.SchedulePreferencesSem1, _timeBlocksSem1);
                LoadPreferredCourses(inst.PreferredCourseCodesSem1, _preferredCoursesSem1);
                LoadAssignedSections(inst.AssignedSectionsSem1, _assignedSectionsSem1);
            }
            else
            {
                UnitsTxtSem2.Text = inst.MaxUnitsSem2.ToString();
                if (inst.AssignedRoomIdSem2 != null) AssignedRoomComboSem2.SelectedValue = inst.AssignedRoomIdSem2;
                foreach (ComboBoxItem item in StatusComboSem2.Items) if (item.Content.ToString() == inst.StatusSem2) StatusComboSem2.SelectedItem = item;

                if (!string.IsNullOrEmpty(inst.PreferredYearLevelsSem2))
                {
                    var years = inst.PreferredYearLevelsSem2.Split(',');
                    CbYear1Sem2.IsChecked = years.Contains("1"); CbYear2Sem2.IsChecked = years.Contains("2");
                    CbYear3Sem2.IsChecked = years.Contains("3"); CbYear4Sem2.IsChecked = years.Contains("4");
                }
                LoadTimeBlocks(inst.SchedulePreferencesSem2, _timeBlocksSem2);
                LoadPreferredCourses(inst.PreferredCourseCodesSem2, _preferredCoursesSem2);
                LoadAssignedSections(inst.AssignedSectionsSem2, _assignedSectionsSem2);
            }
        }

        private void LoadTimeBlocks(string dbString, ObservableCollection<TimeBlockItem> targetList)
        {
            if (string.IsNullOrEmpty(dbString)) return;
            var blocks = dbString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var block in blocks)
            {
                var parts = block.Split('|');
                if (parts.Length == 2) targetList.Add(new TimeBlockItem { DisplayString = $"{parts[0]} : {parts[1]}", ValueString = block });
            }
        }

        private void LoadPreferredCourses(string dbString, ObservableCollection<PreferredCourseItem> targetList)
        {
            if (string.IsNullOrWhiteSpace(dbString)) return;
            
            // Added .Select(s => s.Trim().ToUpper()) to bulletproof the loading
            var codes = dbString.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToUpper());
            using (var db = new AppDbContext())
            {
                foreach (var code in codes)
                {
                    var c = db.Courses.FirstOrDefault(x => x.Code.ToUpper() == code);
                    if (c != null) targetList.Add(new PreferredCourseItem { Code = c.Code, DisplayString = c.Name });
                }
            }
        }

        private void LoadAssignedSections(string dbString, ObservableCollection<AssignedSectionItem> targetList)
        {
            if (string.IsNullOrWhiteSpace(dbString)) return;
            
            // Added .Trim() to prevent FormatExceptions on hidden spaces
            var ids = dbString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                              .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                              .Where(id => id > 0).ToList();
                              
            using (var db = new AppDbContext())
            {
                foreach (var id in ids)
                {
                    var s = db.Sections.FirstOrDefault(x => x.Id == id);
                    if (s != null) targetList.Add(new AssignedSectionItem { Id = s.Id, FullDisplay = $"{s.Program} {s.YearLevel}-{s.Name}" });
                }
            }
        }

        private void NameTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_editingId == 0)
            {
                string first = FirstNameTxt?.Text?.Trim() ?? "";
                string middle = MiddleNameTxt?.Text?.Trim() ?? "";
                string last = SurnameTxt?.Text?.Trim() ?? "";

                string combinedName = $"{first} {middle} {last}";
                var parts = combinedName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                string initials = "";
                foreach (var part in parts)
                    if (part.Length > 0 && char.IsLetter(part[0]))
                        initials += char.ToUpper(part[0]);
                
                if (InitialsTxt != null) InitialsTxt.Text = initials;
            }
        }

        // --- ASSIGNED SECTIONS HANDLERS ---
        private void AssignedSectionCombo_DropDownOpened(object sender, EventArgs e)
        {
            bool isSem1 = SemesterTabs.SelectedIndex == 0;
            var targetProgs = isSem1 ? AvailableProgramsSem1 : AvailableProgramsSem2;
            var selectedProgs = targetProgs.Where(p => p.IsSelected).Select(p => p.Name).ToList();

            // 1. Gather Selected Year Levels
            List<int> selectedYears = new List<int>();
            if (isSem1)
            {
                if (CbYear1Sem1.IsChecked == true) selectedYears.Add(1);
                if (CbYear2Sem1.IsChecked == true) selectedYears.Add(2);
                if (CbYear3Sem1.IsChecked == true) selectedYears.Add(3);
                if (CbYear4Sem1.IsChecked == true) selectedYears.Add(4);
            }
            else
            {
                if (CbYear1Sem2.IsChecked == true) selectedYears.Add(1);
                if (CbYear2Sem2.IsChecked == true) selectedYears.Add(2);
                if (CbYear3Sem2.IsChecked == true) selectedYears.Add(3);
                if (CbYear4Sem2.IsChecked == true) selectedYears.Add(4);
            }

            // 2. Filter Sections by BOTH Program and Year Level
            using (var db = new AppDbContext())
            {
                var validSections = db.Sections
                    .Where(s => selectedProgs.Contains(s.Program) && selectedYears.Contains(s.YearLevel))
                    .ToList();

                _allDatabaseSections = validSections.Select(s => new AssignedSectionItem { Id = s.Id, FullDisplay = $"{s.Program} {s.YearLevel}-{s.Name}" }).OrderBy(s => s.FullDisplay).ToList();

                if (isSem1) AssignedSectionComboSem1.ItemsSource = _allDatabaseSections;
                else AssignedSectionComboSem2.ItemsSource = _allDatabaseSections;
            }

            // 3. Show alert if nothing matches
            if (_allDatabaseSections.Count == 0) 
                MessageBox.Show("No sections found for the selected Programs and Year Levels.", "Filter Notice", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddAssignedSection_Click(object sender, RoutedEventArgs e)
        {
            bool isSem1 = SemesterTabs.SelectedIndex == 0;
            var combo = isSem1 ? AssignedSectionComboSem1 : AssignedSectionComboSem2;
            var targetList = isSem1 ? _assignedSectionsSem1 : _assignedSectionsSem2;

            if (combo.SelectedItem is AssignedSectionItem selectedItem)
            {
                if (targetList.Any(s => s.Id == selectedItem.Id)) { MessageBox.Show("Section already added."); return; }
                targetList.Add(selectedItem);
                combo.SelectedItem = null;
                combo.Text = "";
            }
            else MessageBox.Show("Select a valid section first.");
        }

        private void RemoveAssignedSection_Click(object sender, RoutedEventArgs e)
        {
            bool isSem1 = SemesterTabs.SelectedIndex == 0;
            if (sender is Button btn && btn.Tag is AssignedSectionItem item)
            {
                if (isSem1) _assignedSectionsSem1.Remove(item);
                else _assignedSectionsSem2.Remove(item);
            }
        }

        // --- PREFERRED COURSES HANDLERS ---
        private void PreferredCourseCombo_DropDownOpened(object sender, EventArgs e)
        {
            bool isSem1 = SemesterTabs.SelectedIndex == 0;
            var targetProgs = isSem1 ? AvailableProgramsSem1 : AvailableProgramsSem2;
            var selectedProgs = targetProgs.Where(p => p.IsSelected).Select(p => p.Name).ToList();
            
            List<int> selectedYears = new List<int>();
            if (isSem1)
            {
                if (CbYear1Sem1.IsChecked == true) selectedYears.Add(1);
                if (CbYear2Sem1.IsChecked == true) selectedYears.Add(2);
                if (CbYear3Sem1.IsChecked == true) selectedYears.Add(3);
                if (CbYear4Sem1.IsChecked == true) selectedYears.Add(4);
            }
            else
            {
                if (CbYear1Sem2.IsChecked == true) selectedYears.Add(1);
                if (CbYear2Sem2.IsChecked == true) selectedYears.Add(2);
                if (CbYear3Sem2.IsChecked == true) selectedYears.Add(3);
                if (CbYear4Sem2.IsChecked == true) selectedYears.Add(4);
            }

            using (var db = new AppDbContext())
            {
                var validCourses = db.Curriculums
                    .Include(c => c.Course)
                    .Where(c => selectedProgs.Contains(c.Program) && selectedYears.Contains(c.YearLevel) && c.Semester == (isSem1 ? 1 : 2))
                    .Select(c => c.Course)
                    .Distinct()
                    .Where(c => c != null)
                    .ToList();

                _allDatabaseCourses = validCourses.Select(c => new PreferredCourseItem { Code = c!.Code, DisplayString = c.Name }).OrderBy(c => c.Code).ToList();

                if (isSem1) PreferredCourseComboSem1.ItemsSource = _allDatabaseCourses;
                else PreferredCourseComboSem2.ItemsSource = _allDatabaseCourses;
            }

            if (_allDatabaseCourses.Count == 0) MessageBox.Show("No courses found for the selected Programs and Year Levels in this semester.", "Filter Notice", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddPreferredCourse_Click(object sender, RoutedEventArgs e)
        {
            bool isSem1 = SemesterTabs.SelectedIndex == 0;
            var combo = isSem1 ? PreferredCourseComboSem1 : PreferredCourseComboSem2;
            var targetList = isSem1 ? _preferredCoursesSem1 : _preferredCoursesSem2;

            if (combo.SelectedItem is PreferredCourseItem selectedCourse)
            {
                if (targetList.Any(c => c.Code == selectedCourse.Code)) { MessageBox.Show("Course already added."); return; }
                targetList.Add(selectedCourse);
                combo.SelectedItem = null;
                combo.Text = "";
            }
            else MessageBox.Show("Select a valid course first.");
        }

        private void RemovePreferredCourse_Click(object sender, RoutedEventArgs e)
        {
            bool isSem1 = SemesterTabs.SelectedIndex == 0;
            if (sender is Button btn && btn.Tag is PreferredCourseItem item)
            {
                if (isSem1) _preferredCoursesSem1.Remove(item);
                else _preferredCoursesSem2.Remove(item);
            }
        }

        // --- TIME BLOCK HANDLERS ---
        private void SelectAllDays_Click(object sender, RoutedEventArgs e)
        {
            var panel = SemesterTabs.SelectedIndex == 0 ? DayCheckboxesSem1 : DayCheckboxesSem2;
            foreach (var child in panel.Children) if (child is CheckBox cb) cb.IsChecked = true;
        }

        private void SelectNoDays_Click(object sender, RoutedEventArgs e)
        {
            var panel = SemesterTabs.SelectedIndex == 0 ? DayCheckboxesSem1 : DayCheckboxesSem2;
            foreach (var child in panel.Children) if (child is CheckBox cb) cb.IsChecked = false;
        }

        private void AddBlock_Click(object sender, RoutedEventArgs e)
        {
            bool isSem1 = SemesterTabs.SelectedIndex == 0;
            
            string startStr = isSem1 ? $"{StartHourComboSem1.Text}:{StartMinComboSem1.Text} {StartAmPmComboSem1.Text}" : $"{StartHourComboSem2.Text}:{StartMinComboSem2.Text} {StartAmPmComboSem2.Text}";
            string endStr = isSem1 ? $"{EndHourComboSem1.Text}:{EndMinComboSem1.Text} {EndAmPmComboSem1.Text}" : $"{EndHourComboSem2.Text}:{EndMinComboSem2.Text} {EndAmPmComboSem2.Text}";

            if (!DateTime.TryParse(startStr, out DateTime startDt) || !DateTime.TryParse(endStr, out DateTime endDt))
            {
                MessageBox.Show("Invalid Time Selection");
                return;
            }

            if (endDt <= startDt)
            {
                if (endDt.AddHours(12) > startDt) endDt = endDt.AddHours(12);
                else { MessageBox.Show("End time must be after Start time."); return; }
            }

            var dayPanel = isSem1 ? DayCheckboxesSem1 : DayCheckboxesSem2;
            List<string> selectedDays = new List<string>();
            foreach (var child in dayPanel.Children)
            {
                if (child is CheckBox cb && cb.IsChecked == true) selectedDays.Add(cb.Content?.ToString() ?? "");
            }

            if (selectedDays.Count == 0) { MessageBox.Show("Select at least one day."); return; }

            string daysStr = string.Join(",", selectedDays);
            string timeStr = $"{startDt.ToString("h:mm tt")} - {endDt.ToString("h:mm tt")}";
            string newValueString = $"{daysStr}|{timeStr}";

            var targetList = isSem1 ? _timeBlocksSem1 : _timeBlocksSem2;
            if (targetList.Any(b => b.ValueString == newValueString)) { MessageBox.Show("Time block already added."); return; }

            targetList.Add(new TimeBlockItem { DisplayString = $"{daysStr} : {timeStr}", ValueString = newValueString });
        }

        private void RemoveBlock_Click(object sender, RoutedEventArgs e)
        {
            bool isSem1 = SemesterTabs.SelectedIndex == 0;
            if (sender is Button btn && btn.Tag is TimeBlockItem item)
            {
                if (isSem1) _timeBlocksSem1.Remove(item);
                else _timeBlocksSem2.Remove(item);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SurnameTxt.Text) || string.IsNullOrWhiteSpace(FirstNameTxt.Text)) 
            { MessageBox.Show("Surname and First Name are required."); return; }

            // --- GATHER SEMESTER 1 DATA ---
            var progs1 = AvailableProgramsSem1.Where(p => p.IsSelected).Select(p => p.Name).ToList();
            List<string> yrs1 = new List<string>();
            if (CbYear1Sem1.IsChecked == true) yrs1.Add("1"); if (CbYear2Sem1.IsChecked == true) yrs1.Add("2");
            if (CbYear3Sem1.IsChecked == true) yrs1.Add("3"); if (CbYear4Sem1.IsChecked == true) yrs1.Add("4");
            int units1 = int.TryParse(UnitsTxtSem1.Text, out int u1) ? u1 : 24;

            // --- GATHER SEMESTER 2 DATA ---
            var progs2 = AvailableProgramsSem2.Where(p => p.IsSelected).Select(p => p.Name).ToList();
            List<string> yrs2 = new List<string>();
            if (CbYear1Sem2.IsChecked == true) yrs2.Add("1"); if (CbYear2Sem2.IsChecked == true) yrs2.Add("2");
            if (CbYear3Sem2.IsChecked == true) yrs2.Add("3"); if (CbYear4Sem2.IsChecked == true) yrs2.Add("4");
            int units2 = int.TryParse(UnitsTxtSem2.Text, out int u2) ? u2 : 24;

            using (var db = new AppDbContext())
            {
                Instructor target;
                if (_editingId == 0)
                {
                    target = new Instructor();
                    db.Instructors.Add(target);
                }
                else
                {
                    target = db.Instructors.Find(_editingId);
                    if (target == null) return;
                }

                // Global
                target.Title = TitleCombo.Text.Trim();
                target.Surname = SurnameTxt.Text.Trim();
                target.FirstName = FirstNameTxt.Text.Trim();
                target.MiddleName = MiddleNameTxt.Text.Trim();
                target.Suffix = SuffixTxt.Text.Trim();
                target.Initials = InitialsTxt.Text;
                target.HomeAddress = AddressTxt.Text;
                target.BaccalaureateDegree = BaccTxt.Text;
                target.MastersDegree = MasterTxt.Text;
                target.DoctoralDegree = DoctorTxt.Text;
                target.ExperiencePublic = int.TryParse(ExpPublicTxt.Text, out int ep) ? ep : 0;
                target.ExperiencePrivate = int.TryParse(ExpPrivateTxt.Text, out int epr) ? epr : 0;

                // Sem 1
                target.StatusSem1 = (StatusComboSem1.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Full-time";
                target.MaxUnitsSem1 = units1;
                target.ProgramSem1 = string.Join(", ", progs1);
                target.SchedulePreferencesSem1 = string.Join(";", _timeBlocksSem1.Select(b => b.ValueString));
                target.PreferredYearLevelsSem1 = string.Join(",", yrs1);
                target.PreferredCourseCodesSem1 = string.Join(",", _preferredCoursesSem1.Select(c => c.Code));
                target.AssignedSectionsSem1 = string.Join(",", _assignedSectionsSem1.Select(s => s.Id));
                target.AssignedRoomIdSem1 = AssignedRoomComboSem1.SelectedValue as int?;

                // Sem 2
                target.StatusSem2 = (StatusComboSem2.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Full-time";
                target.MaxUnitsSem2 = units2;
                target.ProgramSem2 = string.Join(", ", progs2);
                target.SchedulePreferencesSem2 = string.Join(";", _timeBlocksSem2.Select(b => b.ValueString));
                target.PreferredYearLevelsSem2 = string.Join(",", yrs2);
                target.PreferredCourseCodesSem2 = string.Join(",", _preferredCoursesSem2.Select(c => c.Code));
                target.AssignedSectionsSem2 = string.Join(",", _assignedSectionsSem2.Select(s => s.Id));
                target.AssignedRoomIdSem2 = AssignedRoomComboSem2.SelectedValue as int?;

                db.SaveChanges();
            }
            
            MessageBox.Show("Instructor Saved Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}