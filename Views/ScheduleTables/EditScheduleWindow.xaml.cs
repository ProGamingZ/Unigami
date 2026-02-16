using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
    public partial class EditScheduleWindow : Window
    {
        private int _scheduleId;
        private AppDbContext _db = new AppDbContext();
        private bool _isLoaded = false; // Prevent event firing during init

        // Variables to hold pre-filled data for NEW mode
        private int _defaultInstructorId = 0;
        private int _defaultSectionId = 0; 
        private int _defaultRoomId = 0; 
        private string _defaultDay = "Mon";
        private string _defaultTime = "07:00";
        private int _targetSemester = 1;

        // EDIT CONSTRUCTOR
        public EditScheduleWindow(int scheduleId)
        {
            InitializeComponent();
            _scheduleId = scheduleId;
            LoadInitialData();
        }

        // ADD MODE - INSTRUCTOR CONTEXT
        public EditScheduleWindow(int instructorId, string day, string time, int semester)
        {
            InitializeComponent();
            _scheduleId = 0; 
            _defaultInstructorId = instructorId;
            _defaultDay = day;
            _defaultTime = time;
            _targetSemester = semester;
            TitleTxt.Text = "Add Class (Instructor View)";
            LoadInitialData();
        }

        // ADD MODE - SECTION CONTEXT
        public EditScheduleWindow(int sectionId, string day, string time, int semester, bool isSectionMode)
        {
            InitializeComponent();
            _scheduleId = 0;
            _defaultSectionId = sectionId;
            _defaultDay = day;
            _defaultTime = time;
            _targetSemester = semester;
            TitleTxt.Text = "Add Class (Section View)";
            LoadInitialData();
        }

        // ADD MODE - ROOM CONTEXT
        public EditScheduleWindow(string day, string time, int semester, int roomId)
        {
            InitializeComponent();
            _scheduleId = 0;
            _defaultRoomId = roomId; 
            _defaultDay = day;
            _defaultTime = time;
            _targetSemester = semester;
            TitleTxt.Text = "Add Class (Room View)";
            LoadInitialData();
        }

        private void LoadInitialData()
        {
            _isLoaded = false;

            // 1. Initialize Time Lists
            var times = GenerateTimeList();
            StartTimeCombo.ItemsSource = times;
            EndTimeCombo.ItemsSource = new List<string>(times);

            // 2. Load Sections (We need this list first)
            LoadValidSections();

            // 3. SET PRE-SELECTIONS (Day, Time, and SECTION)
            // We must select the Section NOW so dependent lists (Courses) can load correctly.
            if (_scheduleId != 0)
            {
                var s = _db.Schedules.Find(_scheduleId);
                if (s != null)
                {
                    _defaultDay = s.Day;
                    _defaultTime = s.StartTime;
                    _targetSemester = s.Semester;
                    
                    SetDay(s.Day);
                    StartTimeCombo.SelectedItem = ConvertTo12H(s.StartTime);
                    EndTimeCombo.SelectedItem = ConvertTo12H(s.EndTime);

                    // CRITICAL CHANGE: Set Section ID immediately here
                    if (s.SectionId != 0) SectionCombo.SelectedValue = s.SectionId;
                }
            }
            else
            {
                // Add Mode
                SetDay(_defaultDay);
                StartTimeCombo.SelectedItem = ConvertTo12H(_defaultTime);
                if (TimeSpan.TryParse(_defaultTime, out TimeSpan startTs))
                    EndTimeCombo.SelectedItem = ConvertTo12H(startTs.Add(new TimeSpan(1, 30, 0)).ToString(@"hh\:mm"));

                // CRITICAL CHANGE: Set Default Section ID immediately here
                if (_defaultSectionId != 0) SectionCombo.SelectedValue = _defaultSectionId;
            }

            // 4. Load Dependent Lists 
            // Now that SectionCombo.SelectedItem is set, these will work!
            LoadValidCourses();      
            LoadValidInstructors();  
            UpdateAvailableRooms();

            // 5. Set Remaining Selections (Course, Room, Instructor)
            if (_scheduleId != 0)
            {
                var s = _db.Schedules.Find(_scheduleId);
                if (s != null)
                {
                    InstructorCombo.SelectedValue = s.InstructorId;
                    CourseCombo.SelectedValue = s.CourseId; // Course list is populated now
                    RoomCombo.SelectedValue = s.RoomId;
                }
            }
            else
            {
                // Add Mode Defaults
                if (_defaultInstructorId != 0) InstructorCombo.SelectedValue = _defaultInstructorId;
                if (_defaultRoomId != 0) RoomCombo.SelectedValue = _defaultRoomId;
            }

            _isLoaded = true; // Enable Event Handlers
        }


        // 1. Load Sections (Filter by Instructor if needed)
        private void LoadValidSections()
        {
            var allSections = _db.Sections.OrderBy(s => s.Program).ThenBy(s => s.YearLevel).ThenBy(s => s.Name).ToList();

            if (_defaultInstructorId != 0)
            {
                // Context: Instructor Table. Only show sections allowed for this instructor.
                var inst = _db.Instructors.Find(_defaultInstructorId);
                if (inst != null)
                {
                    // Logic: Match Program and Year Level preference
                    // Note: Handle flexible string matching
                    var validSections = allSections.Where(s => 
                        (inst.Program.Contains(s.Program) || inst.Program.Contains("General Education")) && 
                        inst.PreferredYearLevels.Contains(s.YearLevel.ToString())
                    ).ToList();

                    SectionCombo.ItemsSource = validSections;
                }
                else SectionCombo.ItemsSource = allSections;
            }
            else
            {
                SectionCombo.ItemsSource = allSections;
            }
        }

        // 2. Load Courses (Filter by Section)
        private void LoadValidCourses()
        {
            if (SectionCombo.SelectedItem is StudentSection section)
            {
                // Get Curriculum for this Section's Program/Year/Semester
                var activeCourseIds = _db.Curriculums
                    .Where(c => c.Program == section.Program && 
                                c.YearLevel == section.YearLevel && 
                                c.Semester == _targetSemester)
                    .Select(c => c.CourseId)
                    .Distinct()
                    .ToList();

                var courses = _db.Courses.Where(c => activeCourseIds.Contains(c.Id)).OrderBy(c => c.Code).ToList();
                CourseCombo.ItemsSource = courses;
            }
            else
            {
                CourseCombo.ItemsSource = new List<Course>(); // Clear if no section
            }
        }

        // 3. Load Instructors (Filter by Course/Section/Vacancy)
        private void LoadValidInstructors()
        {
            if (_defaultInstructorId != 0)
            {
                // Locked Context: Just show the one instructor
                var inst = _db.Instructors.Where(i => i.Id == _defaultInstructorId).ToList();
                InstructorCombo.ItemsSource = inst;
                return;
            }

            var allInst = _db.Instructors.OrderBy(i => i.Name).ToList();
            
            // If Section is selected, filter by Program
            if (SectionCombo.SelectedItem is StudentSection section)
            {
                allInst = allInst.Where(i => 
                    i.Program.Contains(section.Program) 
                ).ToList();
            }

            // Filter by Vacancy (Time)
            if (IsValidTime(out string d, out string s, out string e))
            {
                var busyIds = GetBusyInstructorIds(d, s, e);
                allInst = allInst.Where(i => !busyIds.Contains(i.Id)).ToList();
            }

            InstructorCombo.ItemsSource = allInst;
        }

        // 4. Load Rooms (Filter by Vacancy)
        private void UpdateAvailableRooms()
        {
            if (_defaultRoomId != 0)
            {
                 
            }

            var allRooms = _db.Rooms.OrderBy(r => r.Name).ToList();

            if (IsValidTime(out string day, out string start, out string end))
            {
                var busyRoomIds = GetBusyRoomIds(day, start, end);
                var availableRooms = allRooms.Where(r => !busyRoomIds.Contains(r.Id)).ToList();
                
                // UX: If current selection is still valid, keep it. Else clear.
                int? currentId = (int?)RoomCombo.SelectedValue;
                RoomCombo.ItemsSource = availableRooms;
                
                if (currentId != null && availableRooms.Any(r => r.Id == currentId))
                    RoomCombo.SelectedValue = currentId;
                else
                    RoomCombo.SelectedIndex = -1; 
            }
            else
            {
                // Invalid time? Show nothing or everything? Show all to be safe.
                RoomCombo.ItemsSource = allRooms;
            }
        }



        private void SectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            
            // 1. Load Curriculum
            LoadValidCourses();
            CourseCombo.SelectedIndex = -1; // Reset dependent

            // 2. Filter Instructors (Program Match)
            LoadValidInstructors();
            if (_defaultInstructorId == 0) InstructorCombo.SelectedIndex = -1;
        }

        private void CourseCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
             LoadValidInstructors();
        }

        private void TimeFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            
            
            UpdateAvailableRooms();
            LoadValidInstructors(); 
        }

        private void InstructorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InstructorCombo.SelectedItem is Instructor selectedInstructor && !string.IsNullOrWhiteSpace(selectedInstructor.SchedulePreferences))
                PreferenceTxt.Text = selectedInstructor.SchedulePreferences.Replace("|", " @ ").Replace(";", "\n");
            else
                PreferenceTxt.Text = "-";
        }



        private bool IsValidTime(out string day, out string start, out string end)
        {
            day = (DayCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Mon";
            start = ""; end = "";
            if (StartTimeCombo.SelectedItem == null || EndTimeCombo.SelectedItem == null) return false;

            start = ConvertTo24H(StartTimeCombo.SelectedItem.ToString()!);
            end = ConvertTo24H(EndTimeCombo.SelectedItem.ToString()!);

            return ParseTime(start) < ParseTime(end);
        }

        private List<int> GetBusyRoomIds(string day, string start, string end)
        {
            TimeSpan newStart = ParseTime(start);
            TimeSpan newEnd = ParseTime(end);

            return _db.Schedules
                .Where(s => s.Semester == _targetSemester && s.Day == day && s.RoomId != null && s.Id != _scheduleId)
                .AsEnumerable() // Pull to memory for time math
                .Where(s => {
                    TimeSpan sStart = ParseTime(s.StartTime);
                    TimeSpan sEnd = ParseTime(s.EndTime);
                    return newStart < sEnd && sStart < newEnd; // Overlap check
                })
                .Select(s => s.RoomId!.Value)
                .Distinct()
                .ToList();
        }

        private List<int> GetBusyInstructorIds(string day, string start, string end)
        {
            TimeSpan newStart = ParseTime(start);
            TimeSpan newEnd = ParseTime(end);

            return _db.Schedules
                .Where(s => s.Semester == _targetSemester && s.Day == day && s.InstructorId != null && s.Id != _scheduleId)
                .AsEnumerable()
                .Where(s => {
                    TimeSpan sStart = ParseTime(s.StartTime);
                    TimeSpan sEnd = ParseTime(s.EndTime);
                    return newStart < sEnd && sStart < newEnd;
                })
                .Select(s => s.InstructorId!.Value)
                .Distinct()
                .ToList();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (CourseCombo.SelectedItem == null || SectionCombo.SelectedItem == null) { MessageBox.Show("Select Course and Section."); return; }
            if (StartTimeCombo.SelectedItem == null || EndTimeCombo.SelectedItem == null) { MessageBox.Show("Select Time."); return; }

            string start24 = ConvertTo24H(StartTimeCombo.SelectedItem.ToString()!);
            string end24 = ConvertTo24H(EndTimeCombo.SelectedItem.ToString()!);

            if (ParseTime(start24) >= ParseTime(end24)) { MessageBox.Show("End time must be after start time."); return; }

            ClassSchedule scheduleToSave;
            if (_scheduleId == 0)
            {
                scheduleToSave = new ClassSchedule();
                _db.Schedules.Add(scheduleToSave);
                scheduleToSave.Semester = _targetSemester;
            }
            else scheduleToSave = _db.Schedules.Find(_scheduleId)!;

            scheduleToSave.CourseId = (int)CourseCombo.SelectedValue;
            scheduleToSave.SectionId = (int)SectionCombo.SelectedValue;
            scheduleToSave.RoomId = (int?)RoomCombo.SelectedValue;
            scheduleToSave.InstructorId = (int?)InstructorCombo.SelectedValue;
            scheduleToSave.Day = (DayCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Mon";
            scheduleToSave.StartTime = start24;
            scheduleToSave.EndTime = end24;

            var selectedCourse = CourseCombo.SelectedItem as Course;
            if (selectedCourse != null) scheduleToSave.Component = selectedCourse.LabHours > 0 ? "Lab" : "Lec";

            // OVERLOAD CHECK 
            if (scheduleToSave.InstructorId != null) 
            { 
                 var inst = _db.Instructors.Find(scheduleToSave.InstructorId);
                 // (Simple overload check - logic omitted for brevity as per original file)
            }

            if (RoomCombo.SelectedItem is Room room && SectionCombo.SelectedItem is StudentSection sec)
            {
                if (sec.StudentCount > room.Capacity)
                {
                    if (MessageBox.Show($"Warning: Room Capacity ({room.Capacity}) is smaller than Section Size ({sec.StudentCount}).\n\nContinue anyway?", 
                        "Overcrowding Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                    {
                        return;
                    }
                }
            }
            // CONFLICT CHECK
            if (HasConflicts(scheduleToSave)) return;

            if (_scheduleId == 0) _db.Schedules.Add(scheduleToSave);   
            _db.SaveChanges();
            this.DialogResult = true;
        }

        private bool HasConflicts(ClassSchedule current)
        {
            TimeSpan newStart = ParseTime(current.StartTime);
            TimeSpan newEnd = ParseTime(current.EndTime);

            var others = _db.Schedules
                .Include(s => s.Room).Include(s => s.Section).Include(s => s.Course).Include(s => s.Instructor)
                .Where(s => s.Semester == current.Semester && s.Day == current.Day && s.Id != current.Id)
                .ToList();

            foreach (var existing in others)
            {
                TimeSpan exStart = ParseTime(existing.StartTime);
                TimeSpan exEnd = ParseTime(existing.EndTime);

                if (newStart < exEnd && exStart < newEnd)
                {
                    if (current.RoomId != null && existing.RoomId == current.RoomId)
                    {
                        ShowConflictMsg("Room Conflict", $"Room {existing.Room?.Name} is occupied by {existing.Course?.Code} ({existing.Section?.Name}).");
                        return true;
                    }
                    if (current.InstructorId != null && existing.InstructorId == current.InstructorId)
                    {
                        ShowConflictMsg("Instructor Conflict", $"{existing.Instructor?.Name} is teaching {existing.Course?.Code} in {existing.Room?.Name}.");
                        return true;
                    }
                    if (existing.SectionId == current.SectionId)
                    {
                        ShowConflictMsg("Student Conflict", $"Section {existing.Section?.Name} is attending {existing.Course?.Code} in {existing.Room?.Name}.");
                        return true;
                    }
                }
            }
            return false;
        }

        private List<string> GenerateTimeList() { var list = new List<string>(); TimeSpan t = new TimeSpan(7,0,0); while(t.Hours<=21){ list.Add(DateTime.Today.Add(t).ToString("h:mm tt")); t=t.Add(new TimeSpan(0,30,0)); } return list; }
        private void SetDay(string day) { foreach (ComboBoxItem item in DayCombo.Items) if (item.Tag.ToString() == day) DayCombo.SelectedItem = item; }
        private string ConvertTo12H(string? t) => DateTime.TryParse(t, out DateTime dt) ? dt.ToString("h:mm tt") : "07:00 AM";
        private string ConvertTo24H(string t) => DateTime.TryParse(t, out DateTime dt) ? dt.ToString("HH:mm") : t;
        private TimeSpan ParseTime(string? t) => DateTime.TryParse(t, out DateTime dt) ? dt.TimeOfDay : TimeSpan.Zero;
        private void ShowConflictMsg(string t, string m) => MessageBox.Show($"{t}!\n\n{m}", "Conflict Detected", MessageBoxButton.OK, MessageBoxImage.Warning);
        private void Delete_Click(object sender, RoutedEventArgs e) { if (MessageBox.Show("Delete?", "Confirm", MessageBoxButton.YesNo)==MessageBoxResult.Yes) { var s = _db.Schedules.Find(_scheduleId); if(s!=null) { _db.Schedules.Remove(s); _db.SaveChanges(); DialogResult=true; } } }
    }
}