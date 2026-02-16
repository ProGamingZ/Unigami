using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using UniversityScheduler.Data;
using UniversityScheduler.Views;

namespace UniversityScheduler
{
    public partial class MainWindow : Window
    {
        public static event Action? DatabaseUpdated;
        private List<Instructor> _allInstructors = new List<Instructor>();
        private List<StudentSection> _allSections = new List<StudentSection>(); 
        private List<CheckBox> _dayCheckBoxes = new List<CheckBox>();
        private List<CheckBox> _roomDayCheckBoxes = new List<CheckBox>();
        private List<Room> _allRooms = new List<Room>();
        private int _currentSemester = 1;
        private Window? _instructorsWindow = null;
        private Window? _classesWindow = null;
        private Window? _roomsWindow = null;
        private Window? _coursesWindow = null;
        private Window? _curriculumWindow = null;
        private Window? _statsWindow = null;
        private Window? _generatorWindow = null;

    #region Initialization and Startup
        
        public static void TriggerDatabaseUpdated()
        {
            DatabaseUpdated?.Invoke();
        }
        public MainWindow()
        {
            InitializeComponent();

            string dataFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!System.IO.Directory.Exists(dataFolder))
            {
                System.IO.Directory.CreateDirectory(dataFolder);
            }
            try 
            {
                this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                    new Uri("pack://application:,,,/UNIGAMIicon128.ico", UriKind.RelativeOrAbsolute)
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Icon failed to load: {ex.Message}");
            }

            try
            {
                DataSeeder.SeedCurriculum();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Error during startup:\n{ex.Message}\n\n{ex.InnerException?.Message}", 
                                "Startup Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            }


            DataSeeder.SeedCurriculum();
            this.Loaded += MainWindow_Loaded;
            
            DatabaseUpdated += () => 
            {
                // Use Dispatcher to ensure we are on the UI thread
                Dispatcher.Invoke(() => 
                {
                    RefreshDashboard();
                    RefreshSchedule();
                });
            };


            InstructorScheduleTable.SlotClicked += ScheduleTable_SlotClicked;
            ClassScheduleTable.SlotClicked += ClassScheduleTable_SlotClicked; 
            RoomScheduleTable.SlotClicked += RoomScheduleTable_SlotClicked;

            // 1. Instructor Jumps
            ClassScheduleTable.InstructorJumpRequested += HandleInstructorJump!;
            RoomScheduleTable.InstructorJumpRequested += HandleInstructorJump!;

            // 2. Room Jumps
            InstructorScheduleTable.RoomJumpRequested += HandleRoomJump!;
            ClassScheduleTable.RoomJumpRequested += HandleRoomJump!;

            // 3. Section Jumps
            InstructorScheduleTable.SectionJumpRequested += HandleSectionJump!;
            RoomScheduleTable.SectionJumpRequested += HandleSectionJump!;


        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckSubscriptionStatus();
                CheckSubscriptionAlerts();
                LoadInstructorData();
                LoadClassData();
                LoadRoomData(); 
                InitializeVacancyTools();
                await RefreshSystemHealthAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup Error: {ex.Message}\n\nDetails: {ex.InnerException?.Message}", 
                                "App Crash Report", MessageBoxButton.OK, MessageBoxImage.Error);
                
            }
        }
        private void InitializeVacancyTools()
        {
            // Generate 30-min intervals
            DateTime start = DateTime.Today.AddHours(GlobalSettings.StartTimeHour);
            DateTime end = DateTime.Today.AddHours(GlobalSettings.EndTimeHour); 
            
            VacancyTimeSlots.Clear();
            while (start <= end)
            {
                VacancyTimeSlots.Add(start.ToString("h:mm tt"));
                start = start.AddMinutes(30);
            }
            
            // --- INSTRUCTOR CONTROLS ---
            if (InstStartCombo != null) InstStartCombo.ItemsSource = VacancyTimeSlots;
            if (InstEndCombo != null)   InstEndCombo.ItemsSource = VacancyTimeSlots;
            
            
            if (InstStartCombo != null) InstStartCombo.SelectedIndex = 0; // 7:00 AM
            if (InstEndCombo != null)   InstEndCombo.SelectedIndex = 4;   // 9:00 AM

            // --- ROOM CONTROLS ---
            if (RoomStartCombo != null) RoomStartCombo.ItemsSource = VacancyTimeSlots;
            if (RoomEndCombo != null)   RoomEndCombo.ItemsSource = VacancyTimeSlots;
            
            if (RoomStartCombo != null) RoomStartCombo.SelectedIndex = 0; // 7:00 AM
            if (RoomEndCombo != null)   RoomEndCombo.SelectedIndex = 4;   // 9:00 AM
            // --- CRITERIA LIST ---
            CriteriaList.CollectionChanged += (s, e) => CheckInstVacancyComplex();
        }

    #endregion

    #region Subscription and License
        
        private void ActivateApp_Click(object sender, RoutedEventArgs e)
        {
            if (UniversityScheduler.Data.LicenseManager.IsLicenseValid())
            {
                MessageBox.Show("The application is already activated.\n\nYour license is currently valid.", 
                                "Subscription Active", MessageBoxButton.OK, MessageBoxImage.Information);
                return; 
            }

            // 1. Get the Unique ID for THIS computer
            string thisPcId = UniversityScheduler.Data.LicenseManager.GetInstallationId();

            // 2. Show it in the dialog
            var inputDialog = new Window()
            {
                Width = 450, Height = 280, // Made taller
                Title = "Activate Subscription",
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize, Topmost = true,
                Background = new SolidColorBrush(Colors.WhiteSmoke)
            };
            
            var stack = new StackPanel { Margin = new Thickness(20) };
            
            // INSTRUCTION TEXT
            stack.Children.Add(new TextBlock 
            { 
                Text = "Step 1: Send this Request Code to the Administrator:", 
                FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,5) 
            });

            // READ-ONLY BOX WITH REQUEST CODE
            var codeBox = new TextBox 
            { 
                Text = thisPcId, 
                IsReadOnly = true, 
                Background = new SolidColorBrush(Colors.LightYellow),
                Height = 30, FontSize = 14, FontWeight = FontWeights.Bold,
                Margin = new Thickness(0,0,0,15)
            };
            stack.Children.Add(codeBox);

            // INPUT BOX
            stack.Children.Add(new TextBlock 
            { 
                Text = "Step 2: Enter the Activation Key you received:", 
                FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,5) 
            });
            
            var txtInput = new TextBox { Height = 30, FontSize = 14 };
            stack.Children.Add(txtInput);

            // BUTTONS
            var btn = new Button 
            { 
                Content = "Activate Now", Height = 35, 
                Margin = new Thickness(0,20,0,0), IsDefault = true,
                Background = (Brush)FindResource("PrimaryBrush"), Foreground = Brushes.White
            };
            
            btn.Click += (s, args) => { inputDialog.DialogResult = true; inputDialog.Close(); };
            stack.Children.Add(btn);

            inputDialog.Content = stack;

            if (inputDialog.ShowDialog() == true)
            {
                string inputKey = txtInput.Text.Trim();
                if (UniversityScheduler.Data.LicenseManager.TryActivate(inputKey))
                {
                    MessageBox.Show("Success! Application Activated.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                    CheckSubscriptionStatus();
                }
                else
                {
                    MessageBox.Show("Activation Failed.\nThis key is not valid for THIS computer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void CheckSubscriptionStatus()
        {
            // 1. Check the License
            bool isValid = UniversityScheduler.Data.LicenseManager.IsLicenseValid();

            // SECTION A: THE MAIN TABLES (Bottom Half)
            if (this.FindName("AppMainContent") is Grid mainGrid)
            {
                mainGrid.IsEnabled = isValid;
                mainGrid.Opacity = isValid ? 1.0 : 0.5; // This gives the "Desaturated" look
            }
            else
            {
                // Fallback if you didn't wrap them in XAML
                InstructorScheduleTable.IsEnabled = isValid;
                ClassScheduleTable.IsEnabled = isValid;
                RoomScheduleTable.IsEnabled = isValid;
                
                double op = isValid ? 1.0 : 0.5;
                InstructorScheduleTable.Opacity = op;
                ClassScheduleTable.Opacity = op;
                RoomScheduleTable.Opacity = op;
            }

            // SECTION B: DASHBOARD & VACANCY (Top Right)
            double dimOpacity = isValid ? 1.0 : 0.5;

            if (HealthGroup != null)
            {
                HealthGroup.IsEnabled = isValid;
                HealthGroup.Opacity = dimOpacity;
            }

            if (VacancyGroup != null)
            {
                VacancyGroup.IsEnabled = isValid;
                VacancyGroup.Opacity = dimOpacity;
            }

            // SECTION C: SIDEBAR BUTTONS (Top Left)
            // ensuring 'ActivateBtn' stays bright and clickable.
            if (SidebarGrid != null)
            {
                foreach (var child in SidebarGrid.Children)
                {
                    if (child is Button btn)
                    {
                        if (btn.Name == "ActivateBtn")
                        {
                            // ALWAYS ENABLED, ALWAYS FULLY VISIBLE
                            btn.IsEnabled = true;
                            btn.Opacity = 1.0; 
                        }
                        else
                        {
                            // Other buttons get disabled and dimmed
                            btn.IsEnabled = isValid;
                            btn.Opacity = dimOpacity;
                        }
                    }
                }
            }

            // SECTION D: ACTIVATE BUTTON STYLING
            if (isValid)
            {
                ActivateBtn.Content = "Active";
                ActivateBtn.Background = (Brush)FindResource("PrimaryBrush");
                ActivateBtn.ToolTip = "License is Valid";
            }
            else
            {
                ActivateBtn.Content = "Activate";
                ActivateBtn.Background = (Brush)FindResource("DangerBrush"); 
                ActivateBtn.ToolTip = "Subscription Expired - Click to Renew";
            }
        }
        private void CheckSubscriptionAlerts()
        {
            // Ensure the license is actually valid before checking expiration dates
            if (!UniversityScheduler.Data.LicenseManager.IsLicenseValid()) return;
            DateTime expirationDate = UniversityScheduler.Data.LicenseManager.GetExpirationDate();
            
            TimeSpan timeRemaining = expirationDate - DateTime.Now;
            int daysLeft = (int)timeRemaining.TotalDays;

            // RULE 1: Final 24 Hours (Show EVERY time)
            if (daysLeft <= 1 && daysLeft >= 0)
            {
                MessageBox.Show($"URGENT: Your Unigami subscription expires in {timeRemaining.Hours} hours!\n\nPlease renew immediately to avoid interruption.", 
                                "Subscription Critical", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                // We update the date so we know we warned them, but we don't block the next warning
                GlobalSettings.LastAlertDate = DateTime.Today; 
                GlobalSettings.Save();
            }
            // RULE 2: Warning Zone (7 Days or 3 Days) - Show ONCE per day
            else if (daysLeft <= 7)
            {
                // Check if we already annoyed them today
                if (GlobalSettings.LastAlertDate.Date != DateTime.Today)
                {
                    string msg = $"Reminder: Your subscription expires in {daysLeft} days.";
                    if (daysLeft <= 3) msg += "\nIt is recommended to renew soon.";

                    MessageBox.Show(msg, "Subscription Expiry", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Save that we warned them today
                    GlobalSettings.LastAlertDate = DateTime.Today;
                    GlobalSettings.Save();
                }
            }
        }

    #endregion  

    #region Data Loading and Filtering
        public void RefreshDashboard()
        {
            LoadInstructorData();
            LoadClassData();
            LoadRoomData();
            InitializeVacancyTools(); 
        }
    
        private void LoadInstructorData()
        {
            using (var db = new AppDbContext())
            {
                _allInstructors = db.Instructors.OrderBy(i => i.Name).ToList();
                UpdateInstructorList("All");
            }
        }
        private void UpdateInstructorList(string programFilter)
        {
            if (InstructorSelector == null) return;
            InstructorSelector.SelectionChanged -= InstructorSelector_SelectionChanged;
            try 
            {
                if (programFilter == "All") InstructorSelector.ItemsSource = _allInstructors;
                else
                {
                    var filtered = _allInstructors
                        .Where(i => (i.Program ?? "").Contains(programFilter) || (i.Program ?? "").Contains("General Education"))
                        .ToList();
                    InstructorSelector.ItemsSource = filtered;
                }

                if (InstructorSelector.Items.Count > 0) InstructorSelector.SelectedIndex = 0;
                else
                {
                    InstructorSelector.SelectedIndex = -1;
                    ClearInstructorSchedule();
                }
            }
            finally { InstructorSelector.SelectionChanged += InstructorSelector_SelectionChanged; }
            
            if (InstructorSelector.SelectedItem is Instructor i) UpdateInstructorSchedule(i);
        }
        private void InstProgramCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InstructorSelector == null || !IsLoaded) return;
            if (InstProgramCombo.SelectedItem is ComboBoxItem item)
            {
                string selectedProgram = item.Tag?.ToString() ?? "All";
                UpdateInstructorList(selectedProgram);
            }
        }

        private void LoadClassData()
        {
            using (var db = new AppDbContext())
            {
                _allSections = db.Sections.OrderBy(s => s.Program).ThenBy(s => s.YearLevel).ThenBy(s => s.Name).ToList();
                UpdateClassList("All");
            }
        }
        private void UpdateClassList(string programFilter)
        {
            if (ClassSelector == null) return;
            ClassSelector.SelectionChanged -= ClassSelector_SelectionChanged;

            try
            {
                IEnumerable<StudentSection> filtered;
                if (programFilter == "All") filtered = _allSections;
                else filtered = _allSections.Where(s => s.Program == programFilter);

                var displayList = filtered.Select(s => new 
                { 
                    Id = s.Id, 
                    DisplayName = $"{s.Program} {s.YearLevel}-{s.Name}", 
                    OriginalObject = s 
                }).ToList();

                ClassSelector.ItemsSource = displayList;
                ClassSelector.SelectedValuePath = "Id";

                if (displayList.Count > 0) ClassSelector.SelectedIndex = 0;
                else
                {
                    ClassSelector.SelectedIndex = -1;
                    ClearClassSchedule();
                }
            }
            finally { ClassSelector.SelectionChanged += ClassSelector_SelectionChanged; }

            if (ClassSelector.SelectedItem != null) 
            {
                dynamic item = ClassSelector.SelectedItem;
                UpdateClassSchedule(item.OriginalObject);
            }
        }
        private void ClassProgramCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassSelector == null || !IsLoaded) return;
            if (ClassProgramCombo.SelectedItem is ComboBoxItem item)
            {
                string selectedProgram = item.Tag?.ToString() ?? "All";
                UpdateClassList(selectedProgram);
            }
        }

        private void LoadRoomData()
        {
            using (var db = new AppDbContext())
            {
                _allRooms = db.Rooms.OrderBy(r => r.Name).ToList();
                var floors = _allRooms.Select(r => r.FloorLevel.ToString())
                                      .Distinct()
                                      .OrderBy(f => f)
                                      .ToList();

                RoomFloorCombo.Items.Clear();
                
                // Add "All" option
                var allItem = new ComboBoxItem { Content = "All", Tag = "All", IsSelected = true };
                RoomFloorCombo.Items.Add(allItem);

                // Add detected floors
                foreach (var f in floors)
                {
                    RoomFloorCombo.Items.Add(new ComboBoxItem { Content = $"{f}F", Tag = f });
                }

                // 3. Show All Rooms initially
                UpdateRoomList("All");
            }
        }
        private void UpdateRoomList(string floorFilter)
        {
            // Detach event to prevent triggering "SelectionChanged" while swapping sources
            if (RoomSelector == null) return;
            RoomSelector.SelectionChanged -= RoomSelector_SelectionChanged;

            try
            {
                List<Room> filtered;
                
                if (floorFilter == "All") 
                {
                    filtered = _allRooms;
                }
                else
                {
                    // Filter: Convert FloorLevel to string to match the tag
                    filtered = _allRooms
                        .Where(r => r.FloorLevel.ToString() == floorFilter)
                        .ToList();
                }

                RoomSelector.ItemsSource = filtered;

                // Auto-select first item if available
                if (filtered.Count > 0) 
                    RoomSelector.SelectedIndex = 0;
                else
                {
                    RoomSelector.SelectedIndex = -1;
                    // Optional: Clear the schedule view if no rooms match
                     RoomScheduleTable.RefreshTable(new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, new List<ClassSchedule>());
                     RoomScheduleTable.Title = "Room Schedules";
                     RoomStatusTxt.Text = "Classes: 0";
                }
            }
            finally 
            { 
                // Re-attach event
                RoomSelector.SelectionChanged += RoomSelector_SelectionChanged; 
            }

            // Trigger update for the newly selected room (if any)
            if (RoomSelector.SelectedItem is Room r) 
                UpdateRoomSchedule(r);
        }
        private void RoomFloorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoomSelector == null || !IsLoaded) return;

            if (RoomFloorCombo.SelectedItem is ComboBoxItem item)
            {
                string selectedFloor = item.Tag?.ToString() ?? "All";
                UpdateRoomList(selectedFloor);
            }
        }

    #endregion

    #region Schedule Table logic

        private void RefreshSchedule()
        {
            if (!IsLoaded) return;

            // 1. Refresh Instructor Table (Left)
            if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
                UpdateInstructorSchedule(selectedInstructor);
            else
                ClearInstructorSchedule();

            // 2. Refresh Class Table (Middle)
            if (ClassSelector.SelectedItem != null)
            {
                dynamic item = ClassSelector.SelectedItem;
                UpdateClassSchedule(item.OriginalObject);
            }

            // 3. Refresh Room Table (Right)
            if (RoomSelector.SelectedItem is Room selectedRoom)
                UpdateRoomSchedule(selectedRoom);

            // 4. Update Dashboard Stats (Background)
            _ = RefreshSystemHealthAsync();

            // This ensures the list removes people you just booked!
            if (CriteriaList.Count > 0) CheckInstVacancyComplex();
            if (RoomCriteriaList.Count > 0) CheckRoomVacancyComplex();
        }

        // Instructor table
        private void InstructorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
            {
                LockScheduleChk.IsChecked = selectedInstructor.IsScheduleLocked;
                UpdateInstructorSchedule(selectedInstructor);
            }        
        }
        private void UpdateInstructorSchedule(Instructor instructor)
        {
            using (var db = new AppDbContext())
            {
                int currentSemester = _currentSemester; 
                
                // 1. Fetch Schedule
                var schedules = db.Schedules
                    .Include(s => s.Course)
                    .Include(s => s.Room)
                    .Include(s => s.Section)
                    .Include(s => s.Instructor)
                    .Where(s => s.InstructorId == instructor.Id && s.Semester == currentSemester)
                    .ToList();

                // 2. Update Table
                var days = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
                InstructorScheduleTable.RefreshTable(days, schedules);

                // 3. Calculate Units & Overload
                int totalUnits = schedules.Where(s => s.Course != null)
                    .Select(s => new { s.CourseId, s.SectionId, s.Course!.Units })
                    .Distinct().Sum(x => x.Units);

                int maxUnits = instructor.MaxUnits;
                int overload = Math.Max(0, totalUnits - maxUnits);

                // 4. UPDATE LEFT SIDE COUNTERS
                InstTotalUnitsTxt.Text = $"Units: {totalUnits} / {maxUnits}";
                InstOverloadTxt.Text = $"Overload: {overload}";

                // Color Logic
                if (totalUnits > maxUnits)
                {
                    InstTotalUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                    InstOverloadTxt.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                    InstOverloadTxt.FontWeight = FontWeights.SemiBold;
                }
                else if (totalUnits == maxUnits) 
                {
                    // Exact Match -> Success (Green)
                    InstTotalUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("SidebarBrush");
                    InstOverloadTxt.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
                    InstOverloadTxt.FontWeight = FontWeights.Normal;
                }
                else
                {
                    InstTotalUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("InfoBrush");
                    InstOverloadTxt.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
                    InstOverloadTxt.FontWeight = FontWeights.Normal;
                }

                
                InstructorScheduleTable.Title = instructor.Name;
            }
        }
        private void ClearInstructorSchedule()
        {
            InstructorScheduleTable.RefreshTable(new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, new List<ClassSchedule>());
            InstructorScheduleTable.Title = "Instructor Schedules";
        }
        private void ScheduleTable_SlotClicked(object? sender, ScheduleSlotArgs e)
        {
            if (e.ExistingSchedule != null)
            {
                // Edit existing class
                var editWin = new EditScheduleWindow(e.ExistingSchedule.Id);
                if (editWin.ShowDialog() == true) RefreshSchedule(); 
            }
            else
            {
                // Add new class (Pre-fill Instructor)
                if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
                {
                    var addWin = new EditScheduleWindow(
                        selectedInstructor.Id, 
                        e.Day, 
                        e.Time, 
                        _currentSemester
                    );
                    if (addWin.ShowDialog() == true) RefreshSchedule();
                }
                else
                {
                    MessageBox.Show("Please select an instructor first.");
                }
            }
        }
        private void LockScheduleChk_Click(object sender, RoutedEventArgs e)
        {
            if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
            {
                using (var db = new AppDbContext())
                {
                    var inst = db.Instructors.Find(selectedInstructor.Id);
                    if (inst != null) { inst.IsScheduleLocked = LockScheduleChk.IsChecked == true; db.SaveChanges(); selectedInstructor.IsScheduleLocked = inst.IsScheduleLocked; }
                }
            }
        }

        // Class Table
        private void ClassSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassSelector.SelectedItem != null)
            {
                dynamic selectedItem = ClassSelector.SelectedItem;
                UpdateClassSchedule(selectedItem.OriginalObject);
            }
        }
        private void UpdateClassSchedule(StudentSection section)
        {
            using (var db = new AppDbContext())
            {
                // 1. Fetch Current Schedules
                var schedules = db.Schedules
                    .Include(s => s.Course)
                    .Include(s => s.Room)
                    .Include(s => s.Section)
                    .Include(s => s.Instructor)
                    .Where(s => s.SectionId == section.Id && s.Semester == _currentSemester)
                    .ToList();

                // 2. Update the Table
                var days = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
                ClassScheduleTable.RefreshTable(days, schedules);
                ClassScheduleTable.Title = $"{section.Program} {section.YearLevel}-{section.Name}";

                // 3. Calculate ASSIGNED Units (Sum of unique courses scheduled)
                int assignedUnits = schedules
                    .Where(s => s.Course != null)
                    .Select(s => s.CourseId)
                    .Distinct()
                    .Select(id => schedules.First(s => s.CourseId == id).Course!.Units)
                    .Sum();

                // 4. Calculate MAX Units (Sum of all curriculum courses for this section/semester)
                int maxUnits = db.Curriculums
                    .Include(c => c.Course)
                    .Where(c => c.Program == section.Program && 
                                c.YearLevel == section.YearLevel && 
                                c.Semester == _currentSemester)
                    .Select(c => c.Course != null ? c.Course.Units : 0) // Handle potential null course
                    .Sum();

                // 5. Update Text & Color Logic
                ClassUnitsTxt.Text = $"Units: {assignedUnits} / {maxUnits}";

                if (assignedUnits > maxUnits)
                {
                    // Overload -> Red
                    ClassUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                    ClassUnitsTxt.ToolTip = "Warning: Exceeds curriculum requirements";
                }
                else if (assignedUnits == maxUnits && maxUnits > 0)
                {
                    // Perfect Match -> Green
                    ClassUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("SidebarBrush");
                    ClassUnitsTxt.ToolTip = "Schedule Complete";
                }
                else
                {
                    // Incomplete -> Gray/Black
                    ClassUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
                    ClassUnitsTxt.ToolTip = "Missing subjects";
                }

                // 6. Update Student Count
                ClassStudentCountTxt.Text = $"Students: {section.StudentCount}";
            }
        }
        private void ClearClassSchedule()
        {
            ClassScheduleTable.RefreshTable(new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, new List<ClassSchedule>());
            ClassScheduleTable.Title = "Class Schedules";
        }
        private void ClassScheduleTable_SlotClicked(object? sender, ScheduleSlotArgs e)
        {
            if (e.ExistingSchedule != null)
            {
                // Edit existing class (Same window, works universally)
                var editWin = new EditScheduleWindow(e.ExistingSchedule.Id);
                // Refresh both tables to be safe, or just the Class one
                if (editWin.ShowDialog() == true) 
                {
                    if (ClassSelector.SelectedItem != null)
                    {
                        dynamic selectedItem = ClassSelector.SelectedItem;
                        UpdateClassSchedule(selectedItem.OriginalObject);
                    }
                    RefreshSchedule(); // Also refresh Instructor view in case of changes
                }
            }
            else
            {
                // Add new class (Pre-fill SECTION)
                if (ClassSelector.SelectedItem != null)
                {
                    dynamic selectedItem = ClassSelector.SelectedItem;
                    StudentSection section = selectedItem.OriginalObject;

                    // Open with new Section Constructor
                    var addWin = new EditScheduleWindow(
                        section.Id,
                        e.Day,
                        e.Time,
                        _currentSemester,
                        true // isSectionMode flag
                    );

                    if (addWin.ShowDialog() == true)
                    {
                        UpdateClassSchedule(section);
                        RefreshSchedule(); // Refresh instructor table too
                    }
                }
                else
                {
                    MessageBox.Show("Please select a class (Section) first.");
                }
            }
        }
        private void ClassScheduleTable_InstructorJumpRequested(object sender, int instructorId)
        {
            // 1. Find the instructor object in our loaded list
            var targetInstructor = _allInstructors.FirstOrDefault(i => i.Id == instructorId);

            if (targetInstructor != null)
            {
                // 2. Select them in the Dropdown
                InstructorSelector.SelectedItem = targetInstructor;
            }
            else
            {
                MessageBox.Show("Could not find this instructor in the active list.");
            }
        } 

        // Room Table   
        private void RoomSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoomSelector.SelectedItem is Room selectedRoom)
            {
                UpdateRoomSchedule(selectedRoom);
            }
        }
        private void UpdateRoomSchedule(Room room)
        {
            using (var db = new AppDbContext())
            {
                var schedules = db.Schedules
                    .Include(s => s.Course)
                    .Include(s => s.Room)
                    .Include(s => s.Section)
                    .Include(s => s.Instructor)
                    .Where(s => s.RoomId == room.Id && s.Semester == _currentSemester)
                    .ToList();

                var days = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
                RoomScheduleTable.RefreshTable(days, schedules);
                RoomScheduleTable.Title = room.Name;
                RoomDetailsTxt.Text = $"Type: {room.Type}  |  Cap: {room.Capacity}";

                double totalHours = schedules.Sum(s => (ParseTime(s.EndTime) - ParseTime(s.StartTime)).TotalHours);
                double maxHours = 98.0; 
                int usagePercent = (int)((totalHours / maxHours) * 100);
                
                RoomStatusTxt.Text = $"Usage: {usagePercent}% ({schedules.Count} classes)";
                
                // Color code usage
                if (usagePercent > 80) RoomStatusTxt.Foreground = (System.Windows.Media.Brush)FindResource("SidebarBrush"); // High Usage
                else RoomStatusTxt.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
            }
        }
        private void RoomScheduleTable_SlotClicked(object? sender, ScheduleSlotArgs e)
        {
            if (e.ExistingSchedule != null)
            {
                // Edit Existing (Universal ID lookup)
                var editWin = new EditScheduleWindow(e.ExistingSchedule.Id);
                if (editWin.ShowDialog() == true) 
                {
                    // Refresh the Room view
                    if (RoomSelector.SelectedItem is Room selectedRoom)
                        UpdateRoomSchedule(selectedRoom);

                    // Optional: Refresh others if they might be affected
                    RefreshSchedule(); 
                }
            }
            else
            {
                // Add New (Room Context)
                if (RoomSelector.SelectedItem is Room selectedRoom)
                {
                    // Use the new Room Constructor we just created
                    var addWin = new EditScheduleWindow(
                        e.Day, 
                        e.Time, 
                        _currentSemester, 
                        selectedRoom.Id 
                    );

                    if (addWin.ShowDialog() == true)
                    {
                        UpdateRoomSchedule(selectedRoom);
                        RefreshSchedule();
                    }
                }
                else
                {
                    MessageBox.Show("Please select a room first.");
                }
            }
        }

        // Cross-View Jumps
        private void HandleInstructorJump(object sender, int instructorId)
        {
            // Reset filter to "All" to ensure the instructor is visible
            if (InstProgramCombo.SelectedIndex != 0) InstProgramCombo.SelectedIndex = 0;

            var target = _allInstructors.FirstOrDefault(i => i.Id == instructorId);
            if (target != null) InstructorSelector.SelectedItem = target;
        }
        private void HandleRoomJump(object sender, int roomId)
        {
            if (RoomFloorCombo.SelectedIndex != 0) RoomFloorCombo.SelectedIndex = 0;
            var target = _allRooms.FirstOrDefault(r => r.Id == roomId);
            if (target != null) RoomSelector.SelectedItem = target;
        }
        private void HandleSectionJump(object sender, int sectionId)
        {
            // Reset filter to "All" to ensure the section is visible in the list
            if (ClassProgramCombo.SelectedIndex != 0) ClassProgramCombo.SelectedIndex = 0;

            // Find the section object in our list using the ID
            // Note: ClassSelector is bound to an anonymous object, so we search the source list
            dynamic? targetItem = null;
            
            foreach (dynamic item in ClassSelector.Items)
            {
                if (item.Id == sectionId)
                {
                    targetItem = item;
                    break;
                }
            }

            if (targetItem != null)
                ClassSelector.SelectedItem = targetItem;
            else
                MessageBox.Show("Could not find the target section in the current list.");
        }

        // Helpers
        private TimeSpan ParseTime(string t) => DateTime.TryParse(t, out var dt) ? dt.TimeOfDay : TimeSpan.Zero;

    #endregion
        
    #region Dashboard and System health

        public System.Collections.ObjectModel.ObservableCollection<AlertItem> TbaAlerts { get; set; } 
         = new System.Collections.ObjectModel.ObservableCollection<AlertItem>();

        public System.Collections.ObjectModel.ObservableCollection<AlertItem> IncompleteAlerts { get; set; } 
        = new System.Collections.ObjectModel.ObservableCollection<AlertItem>();

        public System.Collections.ObjectModel.ObservableCollection<AlertItem> CrowdedAlerts { get; set; } 
         = new System.Collections.ObjectModel.ObservableCollection<AlertItem>();

        public System.Collections.ObjectModel.ObservableCollection<AlertItem> OverloadAlerts { get; set; } 
         = new System.Collections.ObjectModel.ObservableCollection<AlertItem>();

        public System.Collections.ObjectModel.ObservableCollection<AlertItem> UnderloadAlerts { get; set; } 
         = new System.Collections.ObjectModel.ObservableCollection<AlertItem>();

        public System.Collections.ObjectModel.ObservableCollection<AlertItem> ContinuousAlerts { get; set; } 
         = new System.Collections.ObjectModel.ObservableCollection<AlertItem>();

        public System.Collections.ObjectModel.ObservableCollection<AlertItem> GapAlerts { get; set; } 
         = new System.Collections.ObjectModel.ObservableCollection<AlertItem>();

        private async Task RefreshSystemHealthAsync()
        {
            // 1. Run Analysis on Background Thread
            var result = await Task.Run(() =>
            {
                using (var db = new AppDbContext())
                {
                    var tba = new List<AlertItem>();
                    var incomplete = new List<AlertItem>();
                    var crowded = new List<AlertItem>();
                    var overload = new List<AlertItem>();
                    var underload = new List<AlertItem>();
                    var continuous = new List<AlertItem>(); 
                    var gaps = new List<AlertItem>();

                    int unassignedCount = 0;

                    // Fetch ALL schedules once to use for everything
                    var allSchedules = db.Schedules
                                        .Include(s => s.Course)
                                        .Include(s => s.Section)
                                        .Include(s => s.Room)
                                        .Where(s => s.Semester == _currentSemester)
                                        .ToList();

                    // --- A. UNASSIGNED (TBA) ---
                    // We can filter the in-memory list 'allSchedules' instead of querying DB again
                    var tbaGroups = allSchedules
                        .Where(s => s.InstructorId == null)
                        .GroupBy(s => s.SectionId);

                    foreach (var group in tbaGroups)
                    {
                        var section = group.First().Section;
                        if (section == null) continue;
                        int count = group.Count();
                        unassignedCount += count;
                        
                        tba.Add(new AlertItem { 
                            Icon = "", 
                            Title = section.FullDisplayName, 
                            Description = $"{count} TBA Instructors", 
                            RelatedId = section.Id, 
                            Type = "Section" 
                        });
                    }

                    // ---  INCOMPLETE (Missing Units) --- 
                    // Fetch Sections and Curriculum for this semester
                    var allSections = db.Sections.ToList();
                    var allCurriculums = db.Curriculums
                                        .Include(c => c.Course)
                                        .Where(c => c.Semester == _currentSemester)
                                        .ToList();

                    foreach (var section in allSections)
                    {
                        // 1. Calculate Max Units (Curriculum)
                        int maxUnits = allCurriculums
                            .Where(c => c.Program == section.Program && c.YearLevel == section.YearLevel && c.Course != null)
                            .Sum(c => c.Course!.Units);

                        if (maxUnits == 0) continue; // Skip if no curriculum

                        // 2. Calculate Assigned Units
                        int assignedUnits = allSchedules
                            .Where(s => s.SectionId == section.Id && s.Course != null)
                            .Select(s => s.CourseId)
                            .Distinct()
                            .Sum(id => allSchedules.First(s => s.CourseId == id).Course!.Units);

                        // 3. Compare
                        if (assignedUnits < maxUnits)
                        {
                            incomplete.Add(new AlertItem 
                            { 
                                Icon = "⚠️", // Warning Icon
                                Title = section.FullDisplayName, 
                                Description = $"Missing: {maxUnits - assignedUnits} Units", 
                                RelatedId = section.Id, 
                                Type = "Section" 
                            });
                        }
                    }

                    // ---  CROWDED ---
                    var crowdedList = allSchedules
                        .Where(s => s.RoomId != null && s.Section != null && s.Section.StudentCount > s.Room!.Capacity)
                        .ToList();

                    foreach (var item in crowdedList)
                    {
                        crowded.Add(new AlertItem { 
                            Icon = "⚠️", 
                            Title = "Room Overcrowded", 
                            Description = $"{item.Room!.Name} ({item.Room.Capacity}) vs {item.Section!.StudentCount}", 
                            RelatedId = item.RoomId ?? 0, 
                            Type = "Room" 
                        });
                    }

                    // --- D. INSTRUCTORS (Overload/Underload/Straight/Gaps) ---
                    var instructors = db.Instructors.ToList();
                    
                    // We use the same 'allSchedules' list, just filter for instructors
                    var instructorScheds = allSchedules.Where(s => s.InstructorId != null).ToList();

                    foreach (var inst in instructors)
                    {
                        var myScheds = instructorScheds.Where(s => s.InstructorId == inst.Id).ToList();
                        if (myScheds.Count == 0) continue; 

                        // Calculate Units
                        int currentUnits = myScheds.Where(s => s.Course != null)
                            .Select(s => new { s.CourseId, s.SectionId, s.Course!.Units }) 
                            .Distinct().Sum(x => x.Units);

                        // Overload Alert
                        if (currentUnits > inst.MaxUnits)
                            overload.Add(new AlertItem { Icon = "", Title = inst.Name, Description = $" {currentUnits}/{inst.MaxUnits} Units", RelatedId = inst.Id, Type = "Instructor" });

                        // Underload Alert
                        if (currentUnits > 0 && currentUnits < (inst.MaxUnits * 0.75)) 
                            underload.Add(new AlertItem { Icon = "", Title = inst.Name, Description = $"{currentUnits}/{inst.MaxUnits} Units", RelatedId = inst.Id, Type = "Instructor" });

                        // Time Analysis (Straight/Gaps)
                        var dayGroups = myScheds.GroupBy(s => s.Day);
                        foreach (var dayGroup in dayGroups)
                        {
                            var sorted = dayGroup.OrderBy(s => DateTime.Parse(s.StartTime)).ToList();
                            double currentStreakHours = 0;
                            DateTime? lastEndTime = null;

                            foreach (var s in sorted)
                            {
                                DateTime start = DateTime.Parse(s.StartTime);
                                DateTime end = DateTime.Parse(s.EndTime);
                                double duration = (end - start).TotalHours;

                                if (lastEndTime != null)
                                {
                                    double gapHours = (start - lastEndTime.Value).TotalHours;

                                    // GAP CHECK (> 3 Hours)
                                    if (gapHours > 3)
                                    {
                                        gaps.Add(new AlertItem 
                                        { 
                                            Icon = "", Title = inst.Name, 
                                            Description = $"{s.Day}: {gapHours:0.#}hr Gap", 
                                            RelatedId = inst.Id, Type = "Instructor"
                                        });
                                    }

                                    // STREAK CHECK (Gap < 20 mins counts as continuous)
                                    if (gapHours < 0.33) currentStreakHours += duration;
                                    else currentStreakHours = duration;
                                }
                                else
                                {
                                    currentStreakHours = duration;
                                }

                                // STRAIGHT TEACHING ALERT (> 3 Hours)
                                if (currentStreakHours >= 3)
                                {
                                    string uniqueKey = $"{inst.Name}-{s.Day}";
                                    if (!continuous.Any(x => x.Title == inst.Name && x.Description.StartsWith(s.Day)))
                                    {
                                        continuous.Add(new AlertItem 
                                        { 
                                            Icon = "", Title = inst.Name, 
                                            Description = $"{s.Day}: {currentStreakHours:0.#}hrs Straight", 
                                            RelatedId = inst.Id, Type = "Instructor"
                                        });
                                    }
                                }
                                lastEndTime = end;
                            }
                        }
                    }

                    // Return everything, INCLUDING the new 'incomplete' list
                    return (tba, incomplete, crowded, overload, underload, continuous, gaps, unassignedCount);
                }
            });

            // 2. Update UI
            TbaAlerts.Clear(); foreach (var i in result.tba) TbaAlerts.Add(i);

            // NEW: Update Incomplete UI
            IncompleteAlerts.Clear(); foreach (var i in result.incomplete) IncompleteAlerts.Add(i);
            if (TabIncomplete != null) TabIncomplete.Header = $"{IncompleteAlerts.Count} Incomplete";

            CrowdedAlerts.Clear(); foreach (var i in result.crowded) CrowdedAlerts.Add(i);
            OverloadAlerts.Clear(); foreach (var i in result.overload) OverloadAlerts.Add(i);
            UnderloadAlerts.Clear(); foreach (var i in result.underload) UnderloadAlerts.Add(i);
            ContinuousAlerts.Clear(); foreach (var i in result.continuous) ContinuousAlerts.Add(i);
            GapAlerts.Clear();        foreach (var i in result.gaps)       GapAlerts.Add(i);

            // Update Tab Headers
            if (TabMissing != null) TabMissing.Header = $"{result.unassignedCount} Unassigned";
            if (TabCrowded != null) TabCrowded.Header = $"{CrowdedAlerts.Count} Crowded";
            if (TabOverload != null) TabOverload.Header = $"{OverloadAlerts.Count} Overload";
            if (TabUnderload != null) TabUnderload.Header = $"{UnderloadAlerts.Count} Underload";
            if (TabContinuous != null) TabContinuous.Header = $"{ContinuousAlerts.Count} Straight";
            if (TabGaps != null)       TabGaps.Header = $"{GapAlerts.Count} Gaps";
        }        
        private void JumpToAlert_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AlertItem alert)
            {
                // Reuse your existing jump logic from MainWindow.xaml.cs
                switch (alert.Type)
                {
                    case "Section":
                        HandleSectionJump(this, alert.RelatedId); 
                        break;
                    case "Room":
                        HandleRoomJump(this, alert.RelatedId);
                        break;
                    case "Instructor":
                        HandleInstructorJump(this, alert.RelatedId);
                        break;
                }
            }
        }


    #endregion
        
    #region Vacancy Checker Tools

        public List<string> VacancyTimeSlots { get; set; } = new List<string>();

        // Instructor Search
        public System.Collections.ObjectModel.ObservableCollection<VacancyItem> VacantInstructors { get; set; } 
         = new System.Collections.ObjectModel.ObservableCollection<VacancyItem>();
        private void InstFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            CheckInstVacancyComplex();
        }
        private void JumpToInstVacancy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int instId)
            {
                if (instId == -1) return; // Ignore "No instructors" item
                HandleInstructorJump(this, instId); // Reuse your existing jump logic
            }
        }
        public System.Collections.ObjectModel.ObservableCollection<VacancyCriteriaItem> CriteriaList { get; set; } 
         = new System.Collections.ObjectModel.ObservableCollection<VacancyCriteriaItem>();
        private void AddInstCriteria_Click(object sender, RoutedEventArgs e)
        {
            // A. Gather Days
            var selectedDays = _dayCheckBoxes.Where(cb => cb.IsChecked == true).Select(cb => cb.Content.ToString()!).ToList();
            if (selectedDays.Count == 0) { MessageBox.Show("Select at least one day."); return; }

            // B. Gather Time
            string startStr = InstStartCombo?.SelectedItem as string ?? "7:00 AM";
            string endStr = InstEndCombo?.SelectedItem as string ?? "9:00 AM";
            if (!DateTime.TryParse(startStr, out DateTime dtStart) || !DateTime.TryParse(endStr, out DateTime dtEnd)) return;
            
            if (dtStart.TimeOfDay >= dtEnd.TimeOfDay) { MessageBox.Show("Invalid Time Range"); return; }

            // C. Add to List
            var newItem = new VacancyCriteriaItem
            {
                Days = selectedDays,
                StartTime = dtStart.TimeOfDay,
                EndTime = dtEnd.TimeOfDay,
                Logic = "AND" // Default
            };

            CriteriaList.Add(newItem);
            UpdateInstLogicVisibilities();
            CheckInstVacancyComplex(); // Trigger Search
        }
        private void RemoveInstCriteria_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VacancyCriteriaItem item)
            {
                CriteriaList.Remove(item);
                UpdateInstLogicVisibilities();
                CheckInstVacancyComplex();
            }
        }
        private void ToggleInstLogic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton btn && btn.Tag is VacancyCriteriaItem item)
            {
                item.Logic = (btn.IsChecked == true) ? "OR" : "AND";
                CheckInstVacancyComplex();
            }
        }
        private void UpdateInstLogicVisibilities()
        {
            for (int i = 0; i < CriteriaList.Count; i++)
            {
                // Show toggle for everyone EXCEPT the last one
                CriteriaList[i].LogicVisibility = (i == CriteriaList.Count - 1) ? Visibility.Collapsed : Visibility.Visible;
                // Trigger UI update manually if needed, or rely on PropertyChanged
            }
            // Hack to refresh ListView bindings if they don't auto-update visibility
            var temp = CriteriaList.ToList(); CriteriaList.Clear(); foreach(var i in temp) CriteriaList.Add(i);
        }
        private void CheckInstVacancyComplex()
        {
            VacantInstructors.Clear();
            
            // If list is empty, do nothing
            if (CriteriaList.Count == 0) 
            {
                // Optional: Show everyone? or Show nobody? Let's show nobody to keep it clean.
                return; 
            }

            string programFilter = (VacancyInstProgramCombo?.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "All";

            using (var db = new UniversityScheduler.Data.AppDbContext())
            {
                // 1. Get Base Instructors
                var query = db.Instructors.Where(i => i.Status != "Inactive");
                if (programFilter != "All") query = query.Where(i => (i.Program ?? "").Contains(programFilter));
                var candidates = query.ToList(); // Start with everyone

                // 2. Load Schedules for checking
                var schedules = db.Schedules.Where(s => s.Semester == _currentSemester && s.InstructorId != null).ToList();

                // 3. APPLY LOGIC (Left-to-Right Processing)
                // We need to filter 'candidates' down based on the criteria chain.
                // "A AND B" means (Free in A) n (Free in B).
                // Simplest way: Calculate "IsFree" for every criteria for every instructor, then combine bools.
                var finalMatches = new List<Instructor>();

                foreach (var inst in candidates)
                {
                    // Calculate availability for EACH criteria item independently
                    List<bool> criteriaResults = new List<bool>();
                    foreach (var crit in CriteriaList)
                    {
                        bool isFree = true;
                        foreach (var day in crit.Days)
                        {
                            // Check conflict for this specific day/time
                            bool hasConflict = schedules.Any(s => 
                                s.InstructorId == inst.Id &&
                                s.Day == day &&
                                ParseTime(s.StartTime) < crit.EndTime && 
                                ParseTime(s.EndTime) > crit.StartTime
                            );
                            if (hasConflict) { isFree = false; break; }
                        }
                        criteriaResults.Add(isFree);
                    }

                    // Combine results using the Logic operators
                    // Result = Criteria[0]
                    // Result = Result [Op0] Criteria[1]
                    // Result = Result [Op1] Criteria[2]
                    
                    bool combinedResult = criteriaResults[0]; // Start with first result

                    for (int i = 0; i < criteriaResults.Count - 1; i++)
                    {
                        string op = CriteriaList[i].Logic; // Operator connecting i and i+1
                        bool nextVal = criteriaResults[i + 1];

                        if (op == "AND") combinedResult = combinedResult && nextVal;
                        else if (op == "OR") combinedResult = combinedResult || nextVal;
                    }

                    if (combinedResult) finalMatches.Add(inst);
                }

                // 4. Populate UI
                if (finalMatches.Count == 0) 
                    VacantInstructors.Add(new VacancyItem { Header = "No matches found", InstructorId = -1 });
                else
                {
                    var allScheds = db.Schedules.Include(s => s.Course).Where(s => s.Semester == _currentSemester).ToList();
                    foreach (var inst in finalMatches)
                    {
                        int units = allScheds.Where(s => s.InstructorId == inst.Id && s.Course != null)
                                            .Select(s => new { s.CourseId, s.SectionId, s.Course!.Units }).Distinct().Sum(x => x.Units);
                        
                        VacantInstructors.Add(new VacancyItem { 
                            Header = inst.Name, 
                            SubHeader = $"{inst.Program ?? "GenEd"}   {units}/{inst.MaxUnits}", 
                            InstructorId = inst.Id 
                        });
                    }
                }
            }
        }
        private void InstDayCb_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                if (!_dayCheckBoxes.Contains(cb)) _dayCheckBoxes.Add(cb);
            }
        }

        // Room Search
        public System.Collections.ObjectModel.ObservableCollection<RoomVacancyItem> VacantRooms { get; set; } 
         = new System.Collections.ObjectModel.ObservableCollection<RoomVacancyItem>();
        private void RoomFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded) CheckRoomVacancyComplex();
        }
        private void JumpToRoomVacancy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int roomId)
            {
                if (roomId == -1) return;
                HandleRoomJump(this, roomId);
            }
        }
        public System.Collections.ObjectModel.ObservableCollection<VacancyCriteriaItem> RoomCriteriaList { get; set; } 
         = new System.Collections.ObjectModel.ObservableCollection<VacancyCriteriaItem>();
        private void AddRoomCriteria_Click(object sender, RoutedEventArgs e)
        {
            // A. Gather Days
            var selectedDays = _roomDayCheckBoxes.Where(cb => cb.IsChecked == true).Select(cb => cb.Content.ToString()!).ToList();
            if (selectedDays.Count == 0) { MessageBox.Show("Select at least one day."); return; }

            // B. Gather Time
            string startStr = RoomStartCombo?.SelectedItem as string ?? "7:00 AM";
            string endStr = RoomEndCombo?.SelectedItem as string ?? "9:00 AM";
            if (!DateTime.TryParse(startStr, out DateTime dtStart) || !DateTime.TryParse(endStr, out DateTime dtEnd)) return;
            
            if (dtStart.TimeOfDay >= dtEnd.TimeOfDay) { MessageBox.Show("Invalid Time Range"); return; }

            // C. Add to List
            var newItem = new VacancyCriteriaItem
            {
                Days = selectedDays,
                StartTime = dtStart.TimeOfDay,
                EndTime = dtEnd.TimeOfDay,
                Logic = "AND"
            };

            RoomCriteriaList.Add(newItem);
            UpdateRoomLogicVisibilities();
            CheckRoomVacancyComplex();
        }
        private void RemoveRoomCriteria_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VacancyCriteriaItem item)
            {
                RoomCriteriaList.Remove(item);
                UpdateRoomLogicVisibilities();
                CheckRoomVacancyComplex();
            }
        }
        private void ToggleRoomLogic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton btn && btn.Tag is VacancyCriteriaItem item)
            {
                item.Logic = (btn.IsChecked == true) ? "OR" : "AND";
                CheckRoomVacancyComplex();
            }
        }
        private void UpdateRoomLogicVisibilities()
        {
            for (int i = 0; i < RoomCriteriaList.Count; i++)
            {
                RoomCriteriaList[i].LogicVisibility = (i == RoomCriteriaList.Count - 1) ? Visibility.Collapsed : Visibility.Visible;
            }
            // Refresh List View
            var temp = RoomCriteriaList.ToList(); RoomCriteriaList.Clear(); foreach(var i in temp) RoomCriteriaList.Add(i);
        }
        private void CheckRoomVacancyComplex()
        {
            VacantRooms.Clear();
            
            if (RoomCriteriaList.Count == 0) return;

            string floorFilter = (VacancyRoomFloorCombo?.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "All";

            using (var db = new UniversityScheduler.Data.AppDbContext())
            {
                // 1. Get Rooms & Filter by Floor
                var query = db.Rooms.AsQueryable();
                if (floorFilter != "All")
                {
                    if (int.TryParse(floorFilter, out int floor)) query = query.Where(r => r.FloorLevel == floor);
                }
                var candidates = query.OrderBy(r => r.Name).ToList();

                // 2. Load Schedules
                var schedules = db.Schedules.Where(s => s.Semester == _currentSemester && s.RoomId != null).ToList();

                var finalMatches = new List<Room>();

                // 3. APPLY LOGIC (Same "AND/OR" engine as Instructor side)
                foreach (var room in candidates)
                {
                    List<bool> criteriaResults = new List<bool>();
                    foreach (var crit in RoomCriteriaList)
                    {
                        bool isFree = true;
                        foreach (var day in crit.Days)
                        {
                            bool hasConflict = schedules.Any(s => 
                                s.RoomId == room.Id &&
                                s.Day == day &&
                                ParseTime(s.StartTime) < crit.EndTime && 
                                ParseTime(s.EndTime) > crit.StartTime
                            );
                            if (hasConflict) { isFree = false; break; }
                        }
                        criteriaResults.Add(isFree);
                    }

                    bool combinedResult = criteriaResults[0];
                    for (int i = 0; i < criteriaResults.Count - 1; i++)
                    {
                        string op = RoomCriteriaList[i].Logic;
                        bool nextVal = criteriaResults[i + 1];
                        if (op == "AND") combinedResult = combinedResult && nextVal;
                        else if (op == "OR") combinedResult = combinedResult || nextVal;
                    }

                    if (combinedResult) finalMatches.Add(room);
                }

                // 4. Populate UI
                if (finalMatches.Count == 0) 
                    VacantRooms.Add(new RoomVacancyItem { Header = "No rooms free", RoomId = -1 });
                else
                {
                    foreach (var r in finalMatches)
                    {
                        VacantRooms.Add(new RoomVacancyItem { 
                            Header = r.Name, 
                            SubHeader = $"{r.Type} | Cap: {r.Capacity}", 
                            RoomId = r.Id 
                        });
                    }
                }
            }
        }
        private void RoomDayCb_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                if (!_roomDayCheckBoxes.Contains(cb)) _roomDayCheckBoxes.Add(cb);
            }
        }


    #endregion   
        
    #region Export and Reports  

        private void ExporttoExcelBtn_Click(object sender, RoutedEventArgs e)
        {
            if (InstructorSelector.SelectedItem is not Instructor selectedInstructor) { MessageBox.Show("Select instructor."); return; }
            SaveFileDialog saveDialog = new SaveFileDialog { Filter = "Excel CSV (*.csv)|*.csv", FileName = $"{selectedInstructor.Name}_Schedule.csv" };
            if (saveDialog.ShowDialog() == true) ExportScheduleToCsv(saveDialog.FileName, selectedInstructor.Id, null);
        }
        private void ExportClassBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSelector.SelectedItem == null) { MessageBox.Show("Select class."); return; }
            dynamic selectedItem = ClassSelector.SelectedItem;
            StudentSection section = selectedItem.OriginalObject;
            SaveFileDialog saveDialog = new SaveFileDialog { Filter = "Excel CSV (*.csv)|*.csv", FileName = $"{section.Program}_{section.YearLevel}{section.Name}_Schedule.csv" };
            if (saveDialog.ShowDialog() == true) ExportScheduleToCsv(saveDialog.FileName, null, section.Id);
        }
        private void ExportRoomBtn_Click(object sender, RoutedEventArgs e)
        {
            if (RoomSelector.SelectedItem is not Room selectedRoom) return;

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "Excel CSV (*.csv)|*.csv",
                FileName = $"{selectedRoom.Name}_Schedule.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                // Pass null for Instructor/Section, and the Room ID
                // Note: You need to update ExportScheduleToCsv to accept a RoomId parameter!
                ExportScheduleToCsv(saveDialog.FileName, null, null, selectedRoom.Id);
            }
        }
        private void ExportScheduleToCsv(string fileName, int? instructorId, int? sectionId, int? roomId = null)
        {
            try
            {
                if (File.Exists(fileName))
                {
                    try 
                    { 
                        using (FileStream stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None)) { stream.Close(); } 
                    }
                    catch (IOException)
                    {
                        MessageBox.Show("The file is currently open in Excel.\nPlease close it and try again.", "File Locked", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                using (var db = new AppDbContext())
                {
                    var query = db.Schedules.Include(s => s.Course).Include(s => s.Room).Include(s => s.Section).Include(s => s.Instructor).Where(s => s.Semester == _currentSemester);
                    if (instructorId != null) query = query.Where(s => s.InstructorId == instructorId);
                    if (sectionId != null) query = query.Where(s => s.SectionId == sectionId);
                    if (roomId != null) query = query.Where(s => s.RoomId == roomId);
                    var schedules = query.ToList();

                    StringBuilder csv = new StringBuilder();
                    csv.AppendLine("Time,Mon,Tue,Wed,Thu,Fri,Sat,Sun");

                    TimeSpan currentTime = new TimeSpan(GlobalSettings.StartTimeHour, 0, 0);
                    TimeSpan endTime = new TimeSpan(GlobalSettings.EndTimeHour, 0, 0);

                    while (currentTime <= endTime)
                    {
                        List<string> rowData = new List<string> { DateTime.Today.Add(currentTime).ToString("h:mm tt") };
                        var days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
                        foreach (var day in days)
                        {
                            var activeClass = schedules.FirstOrDefault(s => s.Day == day && ParseTime(s.StartTime) <= currentTime && ParseTime(s.EndTime) > currentTime);
                            if (activeClass != null)
                            {
                                int blockIndex = (int)((currentTime - ParseTime(activeClass.StartTime)).TotalMinutes / 30);
                                string line1 = activeClass.Course?.Code ?? "Subject";
                                string line2 = activeClass.Room?.Name ?? "TBA";
                                string line3 = instructorId != null ? (activeClass.Section?.FullDisplayName ?? "Sec") : (activeClass.Instructor?.Name ?? "TBA");
                                
                                switch (blockIndex) {
                                    case 0: rowData.Add($"\"{line1}\""); break;
                                    case 1: rowData.Add($"\"{line2}\""); break;
                                    case 2: rowData.Add($"\"{line3}\""); break;
                                    default: rowData.Add("\"-DO-\""); break;
                                }
                            }
                            else rowData.Add("");
                        }
                        csv.AppendLine(string.Join(",", rowData));
                        currentTime = currentTime.Add(new TimeSpan(0, 30, 0));
                    }
                    File.WriteAllText(fileName, csv.ToString());
                    MessageBox.Show("Export Successful!");
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }
        private void ViewInstLoadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (InstructorSelector.SelectedItem is not Instructor instructor) return;

            using (var db = new AppDbContext())
            {
                // Fetch data again to ensure freshness
                var schedules = db.Schedules
                    .Include(s => s.Course)
                    .Include(s => s.Section)
                    .Where(s => s.InstructorId == instructor.Id && s.Semester == _currentSemester)
                    .ToList();

                if (schedules.Count == 0)
                {
                    MessageBox.Show("No classes assigned to this instructor.", "Teaching Load");
                    return;
                }

                // Group by Section
                var loadGroups = schedules
                    .Where(s => s.Section != null && s.Course != null)
                    .GroupBy(s => s.Section) // Group by Section Object
                    .Select(g => new 
                    {
                        SectionName = $"{g.Key!.Program} {g.Key.YearLevel}{g.Key.Name}",
                        Subjects = g.Select(s => $"{s.Course!.Code} ({s.Course.Units}u)").Distinct().ToList()
                    })
                    .OrderBy(x => x.SectionName)
                    .ToList();

                // Build the Display String
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Teaching Load: {instructor.Name}");
                sb.AppendLine("----------------------------------");
                
                foreach (var group in loadGroups)
                {
                    sb.AppendLine($"\n📌 {group.SectionName}");
                    foreach (var subject in group.Subjects)
                    {
                        sb.AppendLine($"   • {subject}");
                    }
                }

                // Create a simple popup window (Always on Top)
                var win = new Window
                {
                    Title = "Instructor Load",
                    Content = new TextBox 
                    { 
                        Text = sb.ToString(), 
                        IsReadOnly = true, 
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Padding = new Thickness(10),
                        FontSize = 14,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas") // Monospace for alignment
                    },
                    Width = 400,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Topmost = true, 
                    ResizeMode = ResizeMode.CanResize,
                    Background = System.Windows.Media.Brushes.White
                };
                win.Show();
            }
        }
        private void MissingSubjectsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSelector.SelectedItem == null) return;

            dynamic selectedItem = ClassSelector.SelectedItem;
            StudentSection section = selectedItem.OriginalObject;

            using (var db = new AppDbContext())
            {
                // 1. Get Scheduled Course IDs for this section & semester
                var scheduledCourseIds = db.Schedules
                    .Where(s => s.SectionId == section.Id && s.Semester == _currentSemester)
                    .Select(s => s.CourseId)
                    .Distinct()
                    .ToList();

                // 2. Get Required Course IDs from Curriculum
                // Matches Program, Year Level, and Semester
                var requiredCourses = db.Curriculums
                    .Include(c => c.Course)
                    .Where(c => c.Program == section.Program && 
                                c.YearLevel == section.YearLevel && 
                                c.Semester == _currentSemester)
                    .Select(c => c.Course)
                    .ToList();

                // 3. Find Missing
                var missing = requiredCourses
                    .Where(c => !scheduledCourseIds.Contains(c!.Id))
                    .OrderBy(c => c!.Code)
                    .ToList();

                if (missing.Count == 0)
                {
                    MessageBox.Show("✅ Great job! All subjects for this semester are scheduled.", "Complete");
                }
                else
                {
                    string msg = "⚠️ The following subjects are MISSING:\n\n";
                    foreach (var m in missing)
                    {
                        msg += $"• {m!.Code} ({m.Units} units)\n";
                    }
                    MessageBox.Show(msg, "Missing Subjects", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        private void SectionCoursesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSelector.SelectedItem == null) return;

            dynamic selectedItem = ClassSelector.SelectedItem;
            StudentSection section = selectedItem.OriginalObject;

            using (var db = new AppDbContext())
            {
                // 1. Get Required Curriculum
                var requiredCurriculum = db.Curriculums
                    .Include(c => c.Course)
                    .Where(c => c.Program == section.Program &&
                                c.YearLevel == section.YearLevel &&
                                c.Semester == _currentSemester)
                    .ToList();

                // 2. Get Current Schedules
                var currentSchedules = db.Schedules
                    .Include(s => s.Instructor)
                    .Where(s => s.SectionId == section.Id && s.Semester == _currentSemester)
                    .ToList();

                // 3. Build UI
                var rootStack = new StackPanel { Margin = new Thickness(15) };

                // --- HEADER: ASSIGNED ---
                rootStack.Children.Add(new TextBlock
                {
                    Text = "ASSIGNED SUBJECTS",
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                    Margin = new Thickness(0, 0, 0, 10),
                    TextDecorations = TextDecorations.Underline
                });

                // Table Header
                rootStack.Children.Add(CreateRow("Subject", "Description", "Units", "INSTRUCTOR", true));

                // Process Assigned
                bool hasAssigned = false;
                int totalAssignedUnits = 0; // Counter for Total

                foreach (var req in requiredCurriculum.OrderBy(c => c.Course!.Code))
                {
                    var match = currentSchedules.FirstOrDefault(s => s.CourseId == req.CourseId);

                    if (match != null)
                    {
                        string instrName = match.Instructor?.Name ?? "TBA";
                        CreateRowUI(rootStack, req.Course!.Code, req.Course.Name, req.Course.Units.ToString(), instrName);
                        
                        // Add to total
                        totalAssignedUnits += req.Course.Units; 
                        hasAssigned = true;
                    }
                }

                if (!hasAssigned)
                {
                    rootStack.Children.Add(new TextBlock { Text = "   (No subjects assigned yet)", FontStyle = FontStyles.Italic, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0,10,0,0) });
                }
                else
                {
                    var totalGrid = new Grid { Margin = new Thickness(0, 10, 0, 10) };
                    totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                    totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

                    // "Total" Label (Right aligned in Description column)
                    var totalLabel = new TextBlock 
                    { 
                        Text = "Total", 
                        FontWeight = FontWeights.Bold, 
                        HorizontalAlignment = HorizontalAlignment.Right, 
                        Margin = new Thickness(0,0,10,0) 
                    };
                    
                    // The Number (Centered in Units column)
                    var totalValue = new TextBlock 
                    { 
                        Text = totalAssignedUnits.ToString(), 
                        FontWeight = FontWeights.Bold, 
                        HorizontalAlignment = HorizontalAlignment.Center 
                    };

                    Grid.SetColumn(totalLabel, 1);
                    Grid.SetColumn(totalValue, 2);

                    totalGrid.Children.Add(totalLabel);
                    totalGrid.Children.Add(totalValue);
                    rootStack.Children.Add(totalGrid);
                    
                    // Add a separator line
                    rootStack.Children.Add(new Separator { Margin = new Thickness(0,0,0,10) });
                }

                // --- HEADER: UNASSIGNED ---
                rootStack.Children.Add(new TextBlock
                {
                    Text = "UNASSIGNED SUBJECTS",
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    Foreground = System.Windows.Media.Brushes.Red,
                    Margin = new Thickness(0, 10, 0, 10),
                    TextDecorations = TextDecorations.Underline
                });

                // Process Unassigned
                bool hasUnassigned = false;
                foreach (var req in requiredCurriculum.OrderBy(c => c.Course!.Code))
                {
                    bool isScheduled = currentSchedules.Any(s => s.CourseId == req.CourseId);

                    if (!isScheduled)
                    {
                        CreateRowUI(rootStack, req.Course!.Code, req.Course.Name, req.Course.Units.ToString(), "---");
                        hasUnassigned = true;
                    }
                }

                if (!hasUnassigned)
                {
                    rootStack.Children.Add(new TextBlock { Text = "   (All subjects scheduled!)", FontStyle = FontStyles.Italic, Foreground = System.Windows.Media.Brushes.Green, Margin = new Thickness(0, 10, 0, 0) });
                }
                
                // 4. Show Window
                var win = new Window
                {
                    Title = $"Subject Load: {section.Program} {section.YearLevel}-{section.Name}",
                    Content = new ScrollViewer { Content = rootStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                    Width = 650, 
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Topmost = true,
                    ResizeMode = ResizeMode.CanResize,
                    Background = System.Windows.Media.Brushes.White
                };
                win.Show();
            }
        }
        private void ClearScheduleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
            {
                if (selectedInstructor.IsScheduleLocked)
                {
                    MessageBox.Show("Locked schedule.", "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }
                if (MessageBox.Show($"Clear schedule for {selectedInstructor.Name}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var db = new AppDbContext())
                    {
                        var schedules = db.Schedules.Where(s => s.InstructorId == selectedInstructor.Id && s.Semester == _currentSemester).ToList();
                        foreach(var s in schedules) s.InstructorId = null; 
                        db.SaveChanges();
                    }
                    RefreshSchedule();
                }
            }
        }
        private void ClearClassBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSelector.SelectedItem == null)
            {
                MessageBox.Show("Please select a class first.");
                return;
            }

            dynamic selectedItem = ClassSelector.SelectedItem;
            StudentSection section = selectedItem.OriginalObject;

            var result = MessageBox.Show(
                $"Are you sure you want to remove ALL classes for {section.Program} {section.YearLevel}-{section.Name}?\n\nThis will delete the schedule entries.",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                using (var db = new AppDbContext())
                {
                    var schedules = db.Schedules
                        .Where(s => s.SectionId == section.Id && s.Semester == _currentSemester)
                        .ToList();

                    db.Schedules.RemoveRange(schedules);
                    db.SaveChanges();
                }

                // Refresh UI
                UpdateClassSchedule(section);
                RefreshSchedule(); // Also refresh instructor view since their loads changed
                MessageBox.Show("Class schedule cleared.");
            }
        }
        private void ClearRoomBtn_Click(object sender, RoutedEventArgs e)
        {
            if (RoomSelector.SelectedItem is not Room selectedRoom) return;

            if (MessageBox.Show($"Clear all classes in {selectedRoom.Name}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                using (var db = new AppDbContext())
                {
                    var schedules = db.Schedules.Where(s => s.RoomId == selectedRoom.Id && s.Semester == _currentSemester).ToList();
                    // We set RoomId to null (Unassign Room) rather than deleting the class entirely
                    foreach (var s in schedules) s.RoomId = null;
                    db.SaveChanges();
                }
                UpdateRoomSchedule(selectedRoom);
                RefreshSchedule();
                MessageBox.Show("Room cleared. Classes are now unassigned (Room: TBA).");
            }
        }
        private void CreateRowUI(StackPanel parent, string code, string desc, string units, string instructor)
        {
             parent.Children.Add(CreateRow(code, desc, units, instructor, false));
        }
        private Grid CreateRow(string c1, string c2, string c3, string c4, bool isHeader)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // Subject Code
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Description (Flexible)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });  // Units
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // Instructor

            var weight = isHeader ? FontWeights.Bold : FontWeights.Normal;
            var color = isHeader ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.DarkSlateGray;

            var t1 = new TextBlock { Text = c1, FontWeight = weight, Foreground = color, VerticalAlignment = VerticalAlignment.Center };
            var t2 = new TextBlock { Text = c2, FontWeight = weight, Foreground = color, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = c2 };
            var t3 = new TextBlock { Text = c3, FontWeight = weight, Foreground = color, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var t4 = new TextBlock { Text = c4.ToUpper(), FontWeight = weight, Foreground = color, VerticalAlignment = VerticalAlignment.Center }; // Instructor Name Uppercase

            Grid.SetColumn(t1, 0);
            Grid.SetColumn(t2, 1);
            Grid.SetColumn(t3, 2);
            Grid.SetColumn(t4, 3);

            grid.Children.Add(t1);
            grid.Children.Add(t2);
            grid.Children.Add(t3);
            grid.Children.Add(t4);

            return grid;
        }        

    #endregion   
                
    #region UI Navigation   

        private void OpenInstructorsWindow_Click(object sender, RoutedEventArgs e) 
        { 
            if (_instructorsWindow != null)
            {
                _instructorsWindow.Activate(); // Bring to front
                if (_instructorsWindow.WindowState == WindowState.Minimized) 
                    _instructorsWindow.WindowState = WindowState.Normal; // Un-minimize
                return;
            }
            _instructorsWindow = new Window 
            { 
                Title = "Instructors", 
                Content = new InstructorsView(_currentSemester), 
                Width = 1000, 
                Height = 600, 
                Topmost = GlobalSettings.InstructorsOnTop 
            };
            _instructorsWindow.Closed += (s, args) => _instructorsWindow = null;
            _instructorsWindow.Show();
        }
        private void OpenCoursesWindow_Click(object sender, RoutedEventArgs e) 
        { 
            if (_coursesWindow != null)
            {
                _coursesWindow.Activate();
                if (_coursesWindow.WindowState == WindowState.Minimized) 
                    _coursesWindow.WindowState = WindowState.Normal;
                return;
            }

            _coursesWindow = new Window 
            { 
                Title = "Courses", 
                Content = new CoursesView(), 
                Width = 900, 
                Height = 600, 
                Topmost = GlobalSettings.CoursesOnTop 
            };

            _coursesWindow.Closed += (s, args) => _coursesWindow = null;
            _coursesWindow.Show(); 
        }        
        private void OpenCurriculumWindow_Click(object sender, RoutedEventArgs e) 
        { 
            if (_curriculumWindow != null)
            {
                _curriculumWindow.Activate();
                if (_curriculumWindow.WindowState == WindowState.Minimized) 
                    _curriculumWindow.WindowState = WindowState.Normal;
                return;
            }

            _curriculumWindow = new Window 
            { 
                Title = "Curriculum Management", 
                Content = new Views.CurriculumManagerView(), 
                Width = 1000, 
                Height = 650, 
                Topmost = GlobalSettings.CoursesOnTop 
            };

            _curriculumWindow.Closed += (s, args) => _curriculumWindow = null;
            _curriculumWindow.Show(); 
        }
        private void OpenClassesWindow_Click(object sender, RoutedEventArgs e) 
        { 
            if (_classesWindow != null)
            {
                _classesWindow.Activate();
                if (_classesWindow.WindowState == WindowState.Minimized) 
                    _classesWindow.WindowState = WindowState.Normal;
                return;
            }

            _classesWindow = new Window 
            { 
                Title = "Classes", 
                Content = new SectionsView(_currentSemester), 
                Width = 800, 
                Height = 600, 
                Topmost = GlobalSettings.ClassesOnTop 
            };

            _classesWindow.Closed += (s, args) => _classesWindow = null;
            _classesWindow.Show(); 
        }
        private void OpenRoomsWindow_Click(object sender, RoutedEventArgs e) 
        { 
            if (_roomsWindow != null)
            {
                _roomsWindow.Activate();
                if (_roomsWindow.WindowState == WindowState.Minimized) 
                    _roomsWindow.WindowState = WindowState.Normal;
                return;
            }

            _roomsWindow = new Window 
            { 
                Title = "Rooms", 
                Content = new RoomsView(), 
                Width = 800, 
                Height = 600, 
                Topmost = GlobalSettings.RoomsOnTop 
            };

            _roomsWindow.Closed += (s, args) => _roomsWindow = null;
            _roomsWindow.Show(); 
        }     
        
        private void OpenStatsWindow_Click(object sender, RoutedEventArgs e) 
        { 
            if (_statsWindow != null)
            {
                _statsWindow.Activate();
                if (_statsWindow.WindowState == WindowState.Minimized) 
                    _statsWindow.WindowState = WindowState.Normal;
                return;
            }
            _statsWindow = new Window 
            { 
                Title = "Stats", 
                Content = new StatsView(), 
                Width = 500, 
                Height = 450, 
                Topmost = GlobalSettings.StatsOnTop 
            };

            _statsWindow.Closed += (s, args) => _statsWindow = null;
            _statsWindow.Show(); 
        }
        private void OpenGeneratorWindow_Click(object sender, RoutedEventArgs e) 
        { 
            if (_generatorWindow != null)
            {
                _generatorWindow.Activate();
                if (_generatorWindow.WindowState == WindowState.Minimized) 
                    _generatorWindow.WindowState = WindowState.Normal;
                return;
            }
            _generatorWindow = new Window 
            { 
                Title = "Generator", 
                Content = new MasterScheduleView(), 
                Width = 1000, Height = 700, 
                Topmost = GlobalSettings.GenerateOnTop 
            };
            _generatorWindow.Closed += (s, args) => _generatorWindow = null;
            _generatorWindow.Show(); 
        }
        private void OpenSettingsWindow_Click(object sender, RoutedEventArgs e) 
        { 
            var win = new Views.SettingsWindow(); 
            win.Owner = this; 
            win.Topmost = true;
            win.ShowDialog(); 
            
        }
        private void SemToggleButton_Click(object sender, RoutedEventArgs e) { 
            _currentSemester = (_currentSemester == 1) ? 2 : 1; 
            SemToggleButton.Content = $"Sem {_currentSemester}"; 

            // 2. Refresh Instructor Table
            RefreshSchedule(); 

            // 3. Refresh Class Table (Middle)
            if (ClassSelector.SelectedItem != null) 
            { 
                dynamic i = ClassSelector.SelectedItem; 
                UpdateClassSchedule(i.OriginalObject); 
            } 

            // 4. NEW: Refresh Room Table (Right)
            if (RoomSelector.SelectedItem is UniversityScheduler.Data.Room selectedRoom)
            {
                UpdateRoomSchedule(selectedRoom);
            } 
        }

    #endregion    
            
    }
    public class AlertItem
    {
        public string Icon { get; set; } = "⚠️";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int RelatedId { get; set; }      // ID of the object to jump to
        public string Type { get; set; } = "";  // "Instructor", "Room", "Section"
        public string Color { get; set; } = ""; 
    }
    public class VacancyItem
    {
        public string Header { get; set; } = "";      // "Dr. Ada Lovelace 0/9 Units"
        public string SubHeader { get; set; } = "";   // "BSCS"
        public int InstructorId { get; set; }         // 101
    }
    public class RoomVacancyItem
    {
        public string Header { get; set; } = "";    // "Lab 1"
        public string SubHeader { get; set; } = ""; // "Laboratory | Cap: 40"
        public int RoomId { get; set; }
    }
    public class VacancyCriteriaItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString(); // Unique ID for deletion
        public List<string> Days { get; set; } = new List<string>(); // ["Mon", "Wed"]
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        
        // Display string: "Mon, Wed 7:00 AM - 9:00 AM"
        public string DisplayText => $"{string.Join(", ", Days)} {DateTime.Today.Add(StartTime):h:mm tt} - {DateTime.Today.Add(EndTime):h:mm tt}";

        // The Logic Toggle: "AND" or "OR"
        private string _logic = "AND";
        public string Logic 
        { 
            get => _logic; 
            set { _logic = value; OnPropertyChanged(nameof(Logic)); } 
        }

        // Visibility: We hide the logic toggle for the very last item
        public Visibility LogicVisibility { get; set; } = Visibility.Visible; 

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

}