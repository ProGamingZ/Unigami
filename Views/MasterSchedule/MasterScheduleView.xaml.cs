using System;
using ClosedXML.Excel;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using UniversityScheduler.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Views
{
    public partial class MasterScheduleView : UserControl
    {
        private bool _isLoaded = false;
        private bool _isUpdatingFilters = false; 

        #region Initialization & Lifecycle
        // Constructors
            public MasterScheduleView()
            {
                InitializeComponent();
                this.Loaded += MasterScheduleView_Loaded;
            }

            private void MasterScheduleView_Loaded(object sender, RoutedEventArgs e)
            {
                this.Loaded -= MasterScheduleView_Loaded;
                LoadInitialData();
                _isLoaded = true;
                RefreshSchedule();
            }
        #endregion


        #region UI Event Handlers
        // Syncs headers with grids
            private void MainScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
            {
                HeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
                TimeScroll.ScrollToVerticalOffset(e.VerticalOffset);
            }
        // Dropdown Updates
            private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (!_isLoaded || _isUpdatingFilters) return;
                using (var db = new AppDbContext())
                {
                    UpdateDynamicFilters(db); 
                }
                RefreshSchedule(); 
            }
        // Buttons
            private async void GenerateSchedule_Click(object sender, RoutedEventArgs e)
            {
                if (MessageBox.Show("Delete current schedule and generate new?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                await RunGenerationProcess(false);
            }
            private async void AssignInstructors_Click(object sender, RoutedEventArgs e)
            {
                if (MessageBox.Show("Overwrite instructors?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                await RunGenerationProcess(true);
            }
            private void Settings_Click(object sender, RoutedEventArgs e)
            {
                var settingsWin = new SchedulerSettingsWindow();
                settingsWin.ShowDialog(); // Use ShowDialog so they must close it before clicking Generate
            }
            private void WipeData_Click(object sender, RoutedEventArgs e)
            {
                var result = MessageBox.Show(
                    "WARNING: This will completely wipe ALL generated schedules and instructor assignments from the database. This action cannot be undone.\n\nAre you sure you want to reset data?",
                    "Confirm Data Wipe",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var db = new AppDbContext())
                        {
                            db.Schedules.RemoveRange(db.Schedules);
                            db.SaveChanges();
                        }
                        
                        RefreshSchedule();
                        UniversityScheduler.MainWindow.TriggerDatabaseUpdated();
                        
                        MessageBox.Show("All schedules and assignments have been wiped clean. You can now generate a new schedule.", "Data Wiped", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to wipe data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        #endregion
        

        #region Data Loading & Filter Logic
        //Populates dropdowns on startup
            private void LoadInitialData()
            {
                using var db = new AppDbContext();
                if (!db.Database.CanConnect()) return;

                var programs = db.Programs
                    .Select(p => p.Code)
                    .OrderBy(p => p)
                    .ToList();

                ProgramSelector.Items.Clear();
                ProgramSelector.Items.Add(new ComboBoxItem { Content = "All", Tag = "All", IsSelected = true });

                foreach (var p in programs)
                {
                    ProgramSelector.Items.Add(new ComboBoxItem { Content = p, Tag = p });
                }

                UpdateDynamicFilters(db);
            }
        //Cascading dropdown logic
            private void UpdateDynamicFilters(AppDbContext db)
            {
                _isUpdatingFilters = true;

                string selSem = GetTag(SemSelector);
                string selProg = GetTag(ProgramSelector);
                string selYear = GetTag(YearSelector);
                string selClass = GetTag(ClassSelector);
                string selInstr = GetTag(InstructorSelector);
                string selCourse = GetTag(CourseSelector);

                int sem = int.Parse(selSem);

                var allSections = db.Sections.ToList();
                var allInstructors = db.Instructors.ToList();
                var curriculum = db.Curriculums.Include(c => c.Course).Where(c => c.Semester == sem).ToList();

                // A. FILTER CLASSES
                var filteredSections = allSections.Where(s => 
                    (selProg == "All" || s.Program == selProg) &&
                    (selYear == "All" || s.YearLevel.ToString() == selYear)
                ).ToList();

                if (selCourse != "All")
                {
                    var validSectionIds = curriculum
                        .Where(c => c.Course != null && c.CourseId.ToString() == selCourse)
                        .Select(c => $"{c.Program}-{c.YearLevel}") 
                        .Distinct().ToList();
                    
                    filteredSections = filteredSections
                        .Where(s => validSectionIds.Contains($"{s.Program}-{s.YearLevel}"))
                        .ToList();
                }

                // B. FILTER INSTRUCTORS
                // FIX: Use (i.Program ?? "") to prevent CS8602 warnings safely
                var filteredInstructors = allInstructors.Where(i => {
                    string prog = (sem == 1 ? i.ProgramSem1 : i.ProgramSem2) ?? "";
                    return selProg == "All" || prog.Contains(selProg);
                }).ToList();

                if (selClass != "All")
                {
                    var section = allSections.FirstOrDefault(s => s.Id.ToString() == selClass);
                    
                    if (section != null && section.Program != null)
                    {
                        string secProg = section.Program; 
                        filteredInstructors = filteredInstructors
                            .Where(i => {
                                string prog = (sem == 1 ? i.ProgramSem1 : i.ProgramSem2) ?? "";
                                return prog.Contains(secProg) || prog.Contains("General Education");
                            })
                            .ToList();
                    }
                }

                // C. FILTER COURSES
                var filteredCurriculum = curriculum.AsQueryable();

                if (selProg != "All") filteredCurriculum = filteredCurriculum.Where(c => c.Program == selProg);
                if (selYear != "All") filteredCurriculum = filteredCurriculum.Where(c => c.YearLevel.ToString() == selYear);
                
                if (selClass != "All")
                {
                    var section = allSections.FirstOrDefault(s => s.Id.ToString() == selClass);
                    if (section != null)
                    {
                        filteredCurriculum = filteredCurriculum.Where(c => c.Program == section.Program && c.YearLevel == section.YearLevel);
                    }
                }

                var finalCourses = filteredCurriculum
                    .Where(c => c.Course != null)
                    .Select(c => c.Course!)
                    .Distinct()
                    .OrderBy(c => c.Code)
                    .ToList();

                // UPDATE UI
                var sortedSections = filteredSections
                    .OrderBy(s => s.Program)
                    .ThenBy(s => s.YearLevel)
                    .ThenBy(s => s.Name);


                UpdateCombo(ClassSelector, sortedSections.Select(s => new { Id = s.Id.ToString(), Name = $"{s.Program} {s.YearLevel}{s.Name}" }), selClass, "All Classes");                
                UpdateCombo(InstructorSelector, filteredInstructors.Select(i => new { Id = i.Id.ToString(), Name = i.FullName }), selInstr, "All Instructors");
                UpdateCombo(CourseSelector, finalCourses.Select(c => new { Id = c.Id.ToString(), Name = $"{c.Code}" }), selCourse, "All Courses");

                _isUpdatingFilters = false;
            }
        #endregion
        
        
        #region Core Rendering
        //Wrapper
            private void RefreshSchedule()
            {
                string dayCode = GetTag(DaySelector);
                LoadSchedule(dayCode);
            }
        //Draws the grid, headers, and cards
            private void LoadSchedule(string dayCode)
            {
                if (ScheduleGrid == null) return;

                TimeSpan StartTime = new TimeSpan(GlobalSettings.StartTimeHour, 0, 0);
                TimeSpan EndTime = new TimeSpan(GlobalSettings.EndTimeHour, 0, 0);
                int IntervalMinutes = 30;

                try 
                {
                    ScheduleGrid.Children.Clear(); 
                    ScheduleGrid.RowDefinitions.Clear(); 
                    ScheduleGrid.ColumnDefinitions.Clear();
                    RoomHeaderGrid.Children.Clear(); 
                    RoomHeaderGrid.ColumnDefinitions.Clear();
                    TimeHeaderGrid.Children.Clear(); 
                    TimeHeaderGrid.RowDefinitions.Clear();

                    RoomHeaderGrid.Margin = new Thickness(0, 0, SystemParameters.VerticalScrollBarWidth, 0);
                    TimeHeaderGrid.Margin = new Thickness(0, 0, 0, SystemParameters.HorizontalScrollBarHeight);

                    var totalMinutes = (EndTime - StartTime).TotalMinutes;
                    int totalTimeSlots = (int)(totalMinutes / IntervalMinutes);

                    // --- GET FILTERS ---
                    string fSem = GetTag(SemSelector);
                    string fProg = GetTag(ProgramSelector);
                    string fYear = GetTag(YearSelector);
                    string fClass = GetTag(ClassSelector); 
                    string fInstr = GetTag(InstructorSelector); 
                    string fCourse = GetTag(CourseSelector);    

                    using (var db = new AppDbContext())
                    {
                        if (!db.Database.CanConnect()) return;

                        int sem = int.Parse(fSem);
                        var rooms = db.Rooms.OrderBy(r => r.FloorLevel).ThenBy(r => r.Name).ToList();
                        
                        var schedules = db.Schedules
                            .Include(s => s.Course)
                            .Include(s => s.Section)
                            .Include(s => s.Instructor)
                            .Where(s => s.Day == dayCode && s.Semester == sem)
                            .ToList();

                        // --- 1. PRE-CALCULATE INSTRUCTOR NAMES (Smart Initials) ---
                        var allInstructors = db.Instructors.ToList();
                        var instructorNames = GetSmartInstructorNames(allInstructors);

                        // --- DRAW ROOM HEADERS ---
                        foreach (var room in rooms)
                        {
                            RoomHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) }); // Slightly wider for size 12 text
                            ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });

                            var headerBorder = new Border 
                            { 
                                Background = (Brush)Application.Current.Resources["SidebarBrush"], 
                                BorderBrush = (Brush)Application.Current.Resources["TextDarkBrush"],
                                BorderThickness = new Thickness(0,0,1,1),
                                Height = 35 
                            };
                            headerBorder.Child = new TextBlock 
                            { 
                                Text = room.Name, 
                                Foreground = (Brush)Application.Current.Resources["BackgroundBrush"], 
                                FontWeight = FontWeights.Bold, 
                                FontSize = 13, // <--- UPDATED FONT SIZE
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                TextAlignment = TextAlignment.Center
                            };
                            Grid.SetColumn(headerBorder, RoomHeaderGrid.ColumnDefinitions.Count - 1);
                            RoomHeaderGrid.Children.Add(headerBorder);
                        }

                        // --- DRAW TIME SLOTS ---
                        DateTime baseDate = DateTime.Today;
                        TimeSpan currentTime = StartTime;

                        for (int i = 0; i < totalTimeSlots; i++)
                        {
                            TimeHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Taller rows for bigger text
                            ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

                            var timeBorder = new Border 
                            { 
                                Background = Brushes.White,
                                BorderBrush = (Brush)Application.Current.Resources["TableLineBrush"],
                                BorderThickness = new Thickness(0,0,1,1)
                            };
                            
                            DateTime startDt = baseDate.Add(currentTime);
                            DateTime endDt = startDt.AddMinutes(IntervalMinutes);
                            string timeString = $"{startDt:h:mm}-{endDt:h:mm}{startDt:tt}".ToLower();

                            timeBorder.Child = new TextBlock 
                            { 
                                Text = timeString, 
                                FontSize = 13, // <--- UPDATED FONT SIZE
                                FontWeight = FontWeights.Bold,
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center
                            };
                            Grid.SetRow(timeBorder, i);
                            TimeHeaderGrid.Children.Add(timeBorder);

                            // Draw empty grid cells
                            for (int col = 0; col < rooms.Count; col++)
                            {
                                var cell = new Border 
                                { 
                                    BorderBrush = (Brush)Application.Current.Resources["TableLineBrush"], 
                                    BorderThickness = new Thickness(0,0,1,1) 
                                };
                                Grid.SetRow(cell, i);
                                Grid.SetColumn(cell, col);
                                ScheduleGrid.Children.Add(cell);
                            }
                            currentTime = currentTime.Add(TimeSpan.FromMinutes(IntervalMinutes));
                        }

                        // --- DRAW SCHEDULE CARDS ---
                        foreach (var cls in schedules)
                        {
                            if (cls.StartTime == null || cls.EndTime == null) continue;
                            if (!TimeSpan.TryParse(cls.StartTime, out TimeSpan start) || !TimeSpan.TryParse(cls.EndTime, out TimeSpan end)) continue;

                            int roomIndex = rooms.FindIndex(r => r.Id == cls.RoomId);
                            if (roomIndex == -1) continue;

                            double minutesFromStart = (start - StartTime).TotalMinutes;
                            int startRow = (int)Math.Floor(minutesFromStart / IntervalMinutes);
                            double durationMinutes = (end - start).TotalMinutes;
                            int rowSpan = (int)Math.Ceiling(durationMinutes / IntervalMinutes);

                            if (startRow >= 0 && rowSpan > 0)
                            {
                                // Filter Logic
                                bool isMatch = true;
                                if (fProg != "All" && (cls.Section?.Program ?? "") != fProg) isMatch = false;
                                if (isMatch && fYear != "All" && (cls.Section?.YearLevel.ToString() ?? "") != fYear) isMatch = false;
                                if (isMatch && fClass != "All" && cls.SectionId.ToString() != fClass) isMatch = false;
                                if (isMatch && fInstr != "All" && (cls.InstructorId?.ToString() ?? "") != fInstr) isMatch = false;
                                if (isMatch && fCourse != "All" && cls.CourseId.ToString() != fCourse) isMatch = false;

                                double opacity = isMatch ? 1.0 : 0.15; 

                                // --- COLOR LOGIC: RED FOR TBA ---
                                Brush cardBackground = (Brush)Application.Current.Resources["PrimaryBrush"];
                                if (cls.InstructorId == null) 
                                {
                                    cardBackground = (Brush)Application.Current.Resources["DangerBrush"]; 
                                }

                                var card = new Border 
                                { 
                                    Background = cardBackground,
                                    CornerRadius = new CornerRadius(4),
                                    Margin = new Thickness(1, 0, 1, 1),
                                    Padding = new Thickness(2),
                                    Opacity = opacity
                                };
                                
                                var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                                
                                // Course Code
                                string courseCode = cls.Course?.Code ?? "Subject";
                                // Optional: Append component if needed, e.g., " (Lab)"
                                // if (cls.Component == "Lab") courseCode += " (Lab)";
                                
                                stack.Children.Add(new TextBlock 
                                { 
                                    Text = courseCode, 
                                    FontWeight = FontWeights.SemiBold, 
                                    FontSize = 13, 
                                    TextAlignment = TextAlignment.Center, 
                                    Foreground = (Brush)Application.Current.Resources["BackgroundBrush"] 
                                });

                                // Section Name
                                stack.Children.Add(new TextBlock 
                                { 
                                    Text = $"{cls.Section?.Program} {cls.Section?.YearLevel}{cls.Section?.Name}", 
                                    FontSize = 13, // <--- UPDATED SIZE
                                    FontWeight = FontWeights.SemiBold, 
                                    TextAlignment = TextAlignment.Center, 
                                    Foreground = (Brush)Application.Current.Resources["BackgroundBrush"] 
                                });
                                
                                // Instructor Name (Using Smart Logic)
                                string instrName = "TBA";
                                if (cls.InstructorId != null && instructorNames.ContainsKey(cls.InstructorId.Value))
                                {
                                    instrName = instructorNames[cls.InstructorId.Value];
                                }

                                stack.Children.Add(new TextBlock 
                                { 
                                    Text = instrName, 
                                    FontSize = 13, // <--- UPDATED SIZE
                                    FontWeight = FontWeights.SemiBold, 
                                    TextAlignment = TextAlignment.Center, 
                                    Foreground = (Brush)Application.Current.Resources["BackgroundBrush"], 
                                });

                                card.Child = stack;
                                // Tooltip for full details
                                card.ToolTip = $"{courseCode}\n{start:h\\:mm} - {end:h\\:mm}\n{cls.Section?.Program}-{cls.Section?.YearLevel}{cls.Section?.Name}\nFull Instructor: {(cls.Instructor?.FullName ?? "TBA")}";

                                Grid.SetColumn(card, roomIndex);
                                Grid.SetRow(card, startRow);
                                Grid.SetRowSpan(card, rowSpan);
                                ScheduleGrid.Children.Add(card);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
        #endregion


        #region Backend Integration
        //Runs the Solver/Assigner in background
            private async System.Threading.Tasks.Task RunGenerationProcess(bool assignInstructors)
            {
                var logWindow = new GenerationLogWindow();
                logWindow.Show(); 
                Action<string> logger = (msg) => logWindow.AddLog(msg);

                try
                {
                    int targetSem = int.Parse(GetTag(SemSelector));
                    await System.Threading.Tasks.Task.Run(() => 
                    {
                        using (var db = new AppDbContext())
                        {
                            var solver = new Services.ScheduleSolver();
                            if (!assignInstructors)
                            {
                                // Load the settings freshly from the file/memory
                                var settings = UniversityScheduler.Services.SchedulerSettings.Load();

                                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                                sb.AppendLine("========== SCHEDULER CONFIGURATION ==========");

                                // 1. Time & Breaks
                                sb.AppendLine($"[Time Settings]");
                                sb.AppendLine($" • Range:       {GlobalSettings.StartTimeHour}:00am - {GlobalSettings.EndTimeHour}:00pm");
                                sb.AppendLine($" • Lunch Break: {(settings.AvoidLunchBreak ? "YES (12:00pm - 1:00 PM blocked)" : "NO")}");

                                // 2. Constraints
                                sb.AppendLine($"[Constraints]");
                                sb.AppendLine($" • Day Limits:  {settings.DayRules.Count} active rules");
                                foreach (var r in settings.DayRules) sb.AppendLine($"   - {r.DisplayString}");

                                sb.AppendLine($" • Time Limits: {settings.TimeRules.Count} active rules");
                                foreach (var r in settings.TimeRules) sb.AppendLine($"   - {r.DisplayString}");

                                string excluded = settings.ExcludedCourses.Count > 0 ? string.Join(", ", settings.ExcludedCourses) : "None";
                                sb.AppendLine($" • Exclusions:  {excluded}");

                                // 3. Structure & Spacing
                                sb.AppendLine($"[Structure]");
                                sb.AppendLine($" • Block Split: {(settings.EnableBlockSplitting ? "YES (3hr -> 1.5 + 1.5)" : "NO")}");
                                if (settings.EnableBlockSplitting && settings.SplittingExceptions.Count > 0)
                                {
                                    sb.AppendLine($"   - Exceptions: {string.Join(", ", settings.SplittingExceptions)}");
                                }
                                sb.AppendLine($" • Day Spacing: {settings.SiblingPattern} Mode");

                                // 4. Performance
                                sb.AppendLine($"[Performance]");
                                sb.AppendLine($" • Max Time:    {settings.MaxCalculationTimeSeconds} seconds");
                                sb.AppendLine($" • Workers:    {settings.MaxSearchWorkers} Workers (Detected {settings.DetectedCores} Cores)");
                                sb.AppendLine($" • System Status:  {settings.SystemInfoSummary}");
                                sb.AppendLine("=============================================");

                                // Write the big block to the log window
                                logger(sb.ToString());
                                
                                var schedulesToDelete = db.Schedules
                                    .Include(s => s.Instructor)
                                    .Where(s => s.Semester == targetSem && 
                                            (s.InstructorId == null || !s.Instructor!.IsScheduleLocked))
                                    .ToList();

                                db.Schedules.RemoveRange(schedulesToDelete);
                                db.SaveChanges();

                                var rooms = db.Rooms.ToList();
                                var instructors = db.Instructors.ToList();
                                var preparer = new Services.ScheduleDataPreparer(db);
                                var tasks = preparer.GenerateTasks(targetSem, logger); 
                                var newSchedules = solver.Solve(tasks, rooms, instructors, targetSem, logger);

                                if (newSchedules.Count > 0)
                                {
                                    db.Schedules.AddRange(newSchedules);
                                    db.SaveChanges();
                                    logger("Schedule Saved.");
                                }
                            }
                            else
                            {
                                logger($"Assigning Instructors Semester {targetSem}...");
                                var existingSchedules = db.Schedules
                                    .Include(s => s.Course)
                                    .Include(s => s.Section) // <--- Add this
                                    .Where(s => s.Semester == targetSem)
                                    .ToList();
                                if (existingSchedules.Count == 0) { logger("No schedule found."); return; }

                                logger("Clearing old instructor assignments...");
                                
                                // Option A: Fast & Clean (Using EF Core objects in memory)
                                foreach (var schedule in existingSchedules)
                                {
                                    if (schedule.Instructor != null && schedule.Instructor.IsScheduleLocked) continue;
                                }

                                var instructors = db.Instructors.ToList();
                                var rooms = db.Rooms.ToList();
                                solver.AssignInstructors(existingSchedules, instructors, logger);
                                
                                db.SaveChanges();
                                logger("Assignments Complete.");
                            }
                        }
                    });
                    RefreshSchedule();
                    UniversityScheduler.MainWindow.TriggerDatabaseUpdated();
                    logger("DONE.");
                }
                catch (Exception ex) { logger($"ERROR: {ex.Message}"); }
                finally { System.Media.SystemSounds.Exclamation.Play(); }
            }
        #endregion


        #region Helpers and Utilities
        //Helper for ComboBoxes
            private void UpdateCombo(ComboBox cb, IEnumerable<dynamic> items, string currentTag, string defaultText)
            {
                cb.Items.Clear();
                cb.Items.Add(new ComboBoxItem { Content = defaultText, Tag = "All" });
                
                bool found = false;
                foreach (var item in items)
                {
                    var cbi = new ComboBoxItem { Content = item.Name, Tag = item.Id };
                    cb.Items.Add(cbi);
                    if (item.Id == currentTag) 
                    {
                        cbi.IsSelected = true;
                        found = true;
                    }
                }

                if (!found) cb.SelectedIndex = 0; 
            }
        //Helper for reading Tags
            private string GetTag(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        //The new name formatter
            private Dictionary<int, string> GetSmartInstructorNames(List<Instructor> instructors)
            {
                var result = new Dictionary<int, string>();
                var parsedList = new List<(int Id, string LastName, string FirstInitial)>();

                foreach (var instr in instructors)
                {
                    string lastName = string.IsNullOrWhiteSpace(instr.Surname) ? instr.FullName : instr.Surname;
                    string initial = !string.IsNullOrWhiteSpace(instr.FirstName) ? instr.FirstName.Substring(0, 1) : "";

                    parsedList.Add((instr.Id, lastName, initial));
                }

                var groups = parsedList.GroupBy(p => p.LastName);

                foreach (var group in groups)
                {
                    if (group.Count() == 1)
                    {
                        var item = group.First();
                        result[item.Id] = item.LastName;
                    }
                    else
                    {
                        foreach (var item in group)
                        {
                            result[item.Id] = $"{item.FirstInitial}. {item.LastName}";
                        }
                    }
                }
                return result;
            }
        #endregion     
    

        #region Import / Export Logic

            private void ExportSchedule_Click(object sender, RoutedEventArgs e)
            {
                try
                {
                    // 1. Get Semester directly from the View's dropdown
                    int targetSem = int.Parse(GetTag(SemSelector));
                    
                    // 2. Get Settings from GlobalSettings
                    string schoolYear = GlobalSettings.MasterSchoolYear;
                    string targetDate = string.IsNullOrWhiteSpace(GlobalSettings.MasterDateText) ? DateTime.Now.ToString("MM/dd/yyyy") : GlobalSettings.MasterDateText;
                    string deptName = GlobalSettings.MasterDeptName;
                    string deptAcro = GlobalSettings.MasterDeptAcronym;
                    string secName = GlobalSettings.MasterSecName;
                    string secPos = GlobalSettings.MasterSecPos;
                    string deanName = GlobalSettings.MasterDeanName;
                    string deanPos = GlobalSettings.MasterDeanPos;

                    // 3. Locate Template file
                    string templateFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Template");
                    string templatePath = System.IO.Path.Combine(templateFolder, "WHOLE _CLASS MONITORING_Template_2.xlsx");

                    if (!System.IO.File.Exists(templatePath))
                    {
                        MessageBox.Show($"Template not found!\nPlease ensure the file exists at:\n{templatePath}", "Missing Template", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                        FileName = $"WHOLE_CLASS_MONITORING_SEM{targetSem}.xlsx"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        using (var db = new AppDbContext())
                        {
                            var rooms = db.Rooms.OrderBy(r => r.FloorLevel).ThenBy(r => r.Name).ToList();
                            var allSchedules = db.Schedules
                                .Include(s => s.Course)
                                .Include(s => s.Section)
                                .Include(s => s.Instructor)
                                .Where(s => s.Semester == targetSem)
                                .ToList();
                            
                            var instructors = db.Instructors.ToList();
                            var smartInstructorNames = GetSmartInstructorNames(instructors);

                            // 4. Open the existing template workbook
                            using (var workbook = new XLWorkbook(templatePath))
                            {
                                string[] days = { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" };

                                foreach (var day in days)
                                {
                                    // Try to get the worksheet. If it doesn't exist (like Sat/Sun), clone MONDAY.
                                    IXLWorksheet ws;
                                    if (!workbook.Worksheets.TryGetWorksheet(day, out ws))
                                    {
                                        ws = workbook.Worksheet("MONDAY").CopyTo(day);
                                    }

                                    string dayCode = day.Substring(0, 3); 
                                    if (day == "THURSDAY") dayCode = "Thu"; 
                                    else if (day == "TUESDAY") dayCode = "Tue";
                                    else dayCode = char.ToUpper(day[0]) + day.Substring(1, 2).ToLower();

                                    // Update Basic Headers
                                    ws.Cell("A1").Value = day;
                                    ws.Cell("A3").Value = targetDate;

                                    // Calculate the dynamic last column boundary
                                    int lastRoomCol = rooms.Count > 0 ? rooms.Count + 1 : 4;

                                    // 1. Safely UNMERGE D1, D2, D3 and RE-MERGE to correct dynamic width
                                    for (int i = 1; i <= 3; i++)
                                    {
                                        var mergedRanges = ws.MergedRanges.Where(m => m.Contains(ws.Cell(i, 4))).ToList();
                                        foreach (var m in mergedRanges) m.Unmerge();
                                        
                                        var headerRange = ws.Range(i, 4, i, lastRoomCol);
                                        headerRange.Merge();
                                        headerRange.Style.Font.Bold = true;
                                        headerRange.Style.Font.FontName = "Bahnschrift SemiBold SemiConden"; // <-- Font Applied
                                        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                        
                                        // Apply the strong right border exactly at the dynamic edge
                                        ws.Cell(i, lastRoomCol).Style.Border.RightBorder = XLBorderStyleValues.Thin;
                                    }

                                    ws.Cell("D1").Value = deptName;
                                    ws.Cell("D2").Value = "REGULAR CLASS MONITORING";
                                    ws.Cell("D3").Value = $"{(targetSem == 1 ? "FIRST" : "SECOND")} SEMESTER {schoolYear}";

                                    // 2. Write Room Headers and draw the dynamic Vertical Grid
                                    for (int i = 0; i < rooms.Count; i++)
                                    {
                                        int colIndex = i + 2;
                                        ws.Column(colIndex).Width = 15;
                                        
                                        // Room Header (Row 4)
                                        var headerCell = ws.Cell(4, colIndex);
                                        headerCell.Value = rooms[i].Name;
                                        headerCell.Style.Font.Bold = true;
                                        headerCell.Style.Font.FontName = "Bahnschrift SemiBold SemiConden"; 
                                        headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                        headerCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin; 
                                        
                                        // Standardize empty schedule grid (Rows 5 to 32)
                                        var colRange = ws.Range(5, colIndex, 32, colIndex); 
                                        colRange.Clear(XLClearOptions.Contents);
                                        colRange.Style.Font.FontName = "Bahnschrift SemiBold SemiConden"; 
                                        colRange.Style.Border.TopBorder = XLBorderStyleValues.None;
                                        colRange.Style.Border.BottomBorder = XLBorderStyleValues.None;
                                        colRange.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                                        colRange.Style.Border.RightBorder = XLBorderStyleValues.Thin;
                                        
                                        // Cap the bottom of the grid
                                        ws.Cell(32, colIndex).Style.Border.BottomBorder = XLBorderStyleValues.Thin; 
                                    }

                                    // 3. Delete extra room columns formatting if the template was wider than the database
                                    int lastUsedCol = ws.LastColumnUsed().ColumnNumber();
                                    if (lastUsedCol > lastRoomCol)
                                    {
                                        // Only clear rows 1 to 32
                                        ws.Range(1, lastRoomCol + 1, 32, lastUsedCol).Clear(XLClearOptions.All); 
                                    }

                                    // 4. Populate Schedules inside Borders
                                    var daySchedules = allSchedules.Where(s => s.Day == dayCode).ToList();
                                    foreach (var sched in daySchedules)
                                    {
                                        if (!TimeSpan.TryParse(sched.StartTime, out TimeSpan start) || !TimeSpan.TryParse(sched.EndTime, out TimeSpan end)) continue;
                                        
                                        int roomIdx = rooms.FindIndex(r => r.Id == sched.RoomId);
                                        if (roomIdx == -1) continue;

                                        int startRow = 5 + (int)((start.TotalMinutes - 420) / 30); // 7:00 AM starts at row 5
                                        int blocks = (int)((end.TotalMinutes - start.TotalMinutes) / 30);
                                        int excelCol = roomIdx + 2;

                                        string courseSuffix = sched.Component == "Lab" ? "Lab" : "Lec";
                                        string courseStr = $"{sched.Course?.Code} {courseSuffix}";
                                        string sectionStr = $"{sched.Section?.Program} {sched.Section?.YearLevel}{sched.Section?.Name}";
                                        string instructorStr = sched.InstructorId.HasValue ? smartInstructorNames[sched.InstructorId.Value] : "TBA";

                                        for (int b = 0; b < blocks; b++)
                                        {
                                            int r = startRow + b;
                                            var cell = ws.Cell(r, excelCol);
                                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                                            if (b == 0) 
                                            {
                                                cell.Value = courseStr;
                                                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin; // Top cap of the class block
                                            }
                                            else if (b == 1) cell.Value = sectionStr;
                                            else if (b == 2) cell.Value = instructorStr;
                                            else cell.Value = "-DO-";

                                            if (b == blocks - 1) 
                                            {
                                                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin; // Bottom cap of the class block
                                            }
                                        }
                                    }

                                    // 5. Write Signatures based on exact coordinates
                                    ws.Cell(40, 2).Value = secName;
                                    ws.Cell(40, 2).Style.Font.Bold = true;
                                    ws.Cell(41, 2).Value = $"{deptAcro} {secPos}";
                                    
                                    ws.Cell(40, 7).Value = deanName;
                                    ws.Cell(40, 7).Style.Font.Bold = true;
                                    ws.Cell(41, 7).Value = $"{deptAcro} {deanPos}";
                                    ws.Range(40, 2, 41, 7).Style.Font.FontName = "Bahnschrift SemiBold SemiConden";
                                }

                                workbook.SaveAs(dialog.FileName);
                                MessageBox.Show("Master Schedule Exported Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            
            private void SaveSchedule_Click(object sender, RoutedEventArgs e)
            {
                try
                {
                    // 1. Define Path
                    string dataFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                    if (!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);

                    string baseName = "Curriculum_and_Instructor";
                    string extension = ".json";
                    string fileName = baseName + extension;
                    string fullPath = System.IO.Path.Combine(dataFolder, fileName);

                    int counter = 1;
                    // Loop: If file exists, try (1), then (2), etc.
                    while (File.Exists(fullPath))
                    {
                        fileName = $"{baseName}({counter}){extension}";
                        fullPath = System.IO.Path.Combine(dataFolder, fileName);
                        counter++;
                    }

                    // 2. Fetch Data
                    using (var db = new AppDbContext())
                    {
                        // We load as 'AsNoTracking' to avoid circular reference issues with Entity Framework proxies
                        // and we do NOT Include() navigation properties to keep the JSON clean (IDs only).
                        var backup = new ScheduleBackupData
                        {
                            Instructors = db.Instructors.AsNoTracking().ToList(),
                            Schedules = db.Schedules.AsNoTracking().ToList()
                        };

                        // 3. Serialize
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        string jsonString = JsonSerializer.Serialize(backup, options);

                        // 4. Write File
                        File.WriteAllText(fullPath, jsonString);

                        MessageBox.Show($"Schedule successfully saved to:\n{fileName}", "Backup Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving schedule: {ex.Message}", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            private void ImportSchedule_Click(object sender, RoutedEventArgs e)
            {
                // 1. Open File Dialog
                string dataFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                if (!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);

                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    InitialDirectory = dataFolder,
                    Filter = "JSON Files (*.json)|*.json",
                    Title = "Select Schedule Backup"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string filePath = openFileDialog.FileName;

                    try
                    {
                        // 2. Read & Validate
                        string jsonString = File.ReadAllText(filePath);
                        var backup = JsonSerializer.Deserialize<ScheduleBackupData>(jsonString);

                        if (backup == null || backup.Instructors == null || backup.Schedules == null)
                        {
                            MessageBox.Show("Invalid file format. Ensure this is a valid 'ScheduleAndInstructor' backup file.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        // 3. Warning Confirmation
                        var result = MessageBox.Show(
                            "WARNING: This will OVERWRITE the current schedule and instructor list in the database.\n\n" +
                            "Are you sure you want to continue?",
                            "Confirm Import",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            PerformDatabaseRestore(backup);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to import file: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            private void PerformDatabaseRestore(ScheduleBackupData data)
            {
                using (var db = new AppDbContext())
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            // 1. Clear Existing Data
                            // We remove Schedules first because they depend on Instructors
                            db.Schedules.RemoveRange(db.Schedules);
                            db.SaveChanges(); // Commit delete schedules

                            db.Instructors.RemoveRange(db.Instructors);
                            db.SaveChanges(); // Commit delete instructors

                            // 2. Insert New Data
                            // We insert Instructors first so Schedules have valid Foreign Keys
                            if (data.Instructors.Any())
                            {
                                db.Instructors.AddRange(data.Instructors);
                                db.SaveChanges();
                            }

                            if (data.Schedules.Any())
                            {
                                // Ensure IDs are cleared if you want new auto-increments, 
                                // OR keep them if you want exact restoration. 
                                // Usually for backup/restore, we keep the IDs as they are in the JSON.
                                db.Schedules.AddRange(data.Schedules);
                                db.SaveChanges();
                            }

                            transaction.Commit();
                            
                            // 3. Refresh UI
                            RefreshSchedule();
                            UniversityScheduler.MainWindow.TriggerDatabaseUpdated();
                            MessageBox.Show("Schedule imported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Database Error during restore: {ex.Message}\n\nChanges have been rolled back.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }

            // Helper Class for JSON Structure
            public class ScheduleBackupData
            {
                public List<Instructor> Instructors { get; set; } = new List<Instructor>();
                public List<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
            }

        #endregion

    }
}