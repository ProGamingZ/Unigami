using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
    public partial class ClearSettingsWindow : Window
    {
        private readonly List<int> _instructorIds;

        public ClearSettingsWindow(List<int> instructorIds)
        {
            InitializeComponent();
            _instructorIds = instructorIds;
            Title = $"Clear Settings ({_instructorIds.Count} Instructors)";
        }

        private void CbAllSettings_Toggle(object sender, RoutedEventArgs e)
        {
            bool isChecked = CbAllSettings.IsChecked == true;
            foreach (var child in SettingsPanel.Children)
            {
                if (child is CheckBox cb) cb.IsChecked = isChecked;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (CbSem1.IsChecked == false && CbSem2.IsChecked == false)
            {
                MessageBox.Show("Please select at least one semester.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if any setting is selected
            bool anySettingSelected = false;
            foreach (var child in SettingsPanel.Children)
            {
                if (child is CheckBox cb && cb.IsChecked == true) anySettingSelected = true;
            }

            if (!anySettingSelected)
            {
                MessageBox.Show("Please select at least one setting to clear.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to clear the selected settings for {_instructorIds.Count} instructor(s)?\n\nThis cannot be undone.", 
                "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                using (var db = new AppDbContext())
                {
                    var instructors = db.Instructors.Where(i => _instructorIds.Contains(i.Id)).ToList();

                    foreach (var inst in instructors)
                    {
                        if (CbSem1.IsChecked == true)
                        {
                            if (CbSections.IsChecked == true) inst.AssignedSectionsSem1 = "";
                            if (CbCourses.IsChecked == true) inst.PreferredCourseCodesSem1 = "";
                            if (CbTime.IsChecked == true) inst.SchedulePreferencesSem1 = "";
                            if (CbRoom.IsChecked == true) inst.AssignedRoomIdSem1 = null;
                            if (CbPrograms.IsChecked == true) { inst.ProgramSem1 = ""; inst.PreferredYearLevelsSem1 = ""; }
                            if (CbStatus.IsChecked == true) { inst.StatusSem1 = "None"; inst.MaxUnitsSem1 = 0; }
                        }

                        if (CbSem2.IsChecked == true)
                        {
                            if (CbSections.IsChecked == true) inst.AssignedSectionsSem2 = "";
                            if (CbCourses.IsChecked == true) inst.PreferredCourseCodesSem2 = "";
                            if (CbTime.IsChecked == true) inst.SchedulePreferencesSem2 = "";
                            if (CbRoom.IsChecked == true) inst.AssignedRoomIdSem2 = null;
                            if (CbPrograms.IsChecked == true) { inst.ProgramSem2 = ""; inst.PreferredYearLevelsSem2 = ""; }
                            if (CbStatus.IsChecked == true) { inst.StatusSem2 = "None"; inst.MaxUnitsSem2 = 0; }
                        }
                    }

                    db.SaveChanges();
                }

                MessageBox.Show("Settings cleared successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
        }
    }
}