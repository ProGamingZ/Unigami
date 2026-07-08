using System.Linq; // Required for LINQ
using System.Windows;

namespace UniversityScheduler.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Settings are already loaded by App.xaml.cs, just bind them
            CbInstructors.IsChecked = GlobalSettings.InstructorsOnTop;
            CbCourses.IsChecked = GlobalSettings.CoursesOnTop;
            CbClasses.IsChecked = GlobalSettings.ClassesOnTop;
            CbRooms.IsChecked = GlobalSettings.RoomsOnTop;
            CbStats.IsChecked = GlobalSettings.StatsOnTop;
            CbGenerate.IsChecked = GlobalSettings.GenerateOnTop;
            SetComboValue(ComboStartTime, GlobalSettings.StartTimeHour);
            SetComboValue(ComboEndTime, GlobalSettings.EndTimeHour);
        }

        private void SetComboValue(System.Windows.Controls.ComboBox cb, int value)
        {
            foreach (System.Windows.Controls.ComboBoxItem item in cb.Items)
            {
                if (item.Tag.ToString() == value.ToString())
                {
                    cb.SelectedItem = item;
                    break;
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. Update Static Variables
            GlobalSettings.InstructorsOnTop = CbInstructors.IsChecked == true;
            GlobalSettings.CoursesOnTop = CbCourses.IsChecked == true;
            GlobalSettings.ClassesOnTop = CbClasses.IsChecked == true;
            GlobalSettings.RoomsOnTop = CbRooms.IsChecked == true;
            GlobalSettings.StatsOnTop = CbStats.IsChecked == true;
            GlobalSettings.GenerateOnTop = CbGenerate.IsChecked == true;


            if (ComboStartTime.SelectedItem is System.Windows.Controls.ComboBoxItem startItem)
                GlobalSettings.StartTimeHour = int.Parse(startItem.Tag.ToString()!);

            if (ComboEndTime.SelectedItem is System.Windows.Controls.ComboBoxItem endItem)
                GlobalSettings.EndTimeHour = int.Parse(endItem.Tag.ToString()!);

            
            GlobalSettings.Save();
            ApplyToOpenWindows();

            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mainWin)
                {
                    mainWin.RefreshDashboard(); 
                }
            }

            this.Close();
        }

        private void ApplyToOpenWindows()
        {
            foreach (Window win in Application.Current.Windows)
            {
                string title = win.Title ?? "";

                if (title.Contains("Instructors")) win.Topmost = GlobalSettings.InstructorsOnTop;
                else if (title.Contains("Courses")) win.Topmost = GlobalSettings.CoursesOnTop;
                else if (title.Contains("Class Sections")) win.Topmost = GlobalSettings.ClassesOnTop; // Matches "Class Sections Management"
                else if (title.Contains("Rooms")) win.Topmost = GlobalSettings.RoomsOnTop;
                else if (title.Contains("Statistics")) win.Topmost = GlobalSettings.StatsOnTop;
                else if (title.Contains("Generator")) win.Topmost = GlobalSettings.GenerateOnTop; // Matches "Schedule Generator"
            }
        }
    
        private void ImportDb_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "SQLite Database (*.db)|*.db",
                Title = "Select Old schedule.db File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;
                
                // Open the new migration window
                var migrationWin = new DatabaseMigrationWindow(selectedPath);
                migrationWin.Topmost = true;
                migrationWin.ShowDialog();
                
                // Close settings window if migration triggered a restart
                this.Close();
            }
        }
        
    }
}