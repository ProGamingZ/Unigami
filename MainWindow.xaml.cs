using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using UniversityScheduler.Data;

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
                // and build all the tables/columns based on your C# models before seeding. ---
                using (var db = new AppDbContext())
                {
                    db.Database.EnsureCreated();
                }
                #if DEBUG
                    // 1. Independent Tables (Must be seeded first)
                    DataSeeder.SeedRooms();
                    DataSeeder.SeedCourses();
                    DataSeeder.SeedStudentSections();
                    // 2. Dependent Tables (Rely on the tables above)
                    DataSeeder.SeedInstructors(); 
                    DataSeeder.SeedCurriculum();  
                    DataSeeder.SeedClassSchedules(); 
                #endif
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Error during startup:\n{ex.Message}\n\n{ex.InnerException?.Message}", 
                                "Startup Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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