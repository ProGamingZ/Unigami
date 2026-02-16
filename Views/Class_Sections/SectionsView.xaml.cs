using System.Collections.Generic; // Required for List<>
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Views
{
    public partial class SectionsView : UserControl
    {
        // Cache for search
        private List<StudentSection> _allSections = new List<StudentSection>();
        private int _currentSemester;

        public SectionsView(int semester)
        {
            InitializeComponent();
            _currentSemester = semester;
            LoadSections();
        }

        private void LoadSections()
        {
            using (var db = new AppDbContext())
            {
                // Store in cache
                _allSections = db.Sections
                             .OrderBy(s => s.Program)
                             .ThenBy(s => s.YearLevel)
                             .ThenBy(s => s.Name)
                             .ToList();

                int targetSemester = _currentSemester;

                var allSchedules = db.Schedules
                                    .Include(s => s.Course)
                                    .Where(s => s.Semester == targetSemester)
                                    .ToList();

                // 3. Fetch ALL Curriculum info (for Semester 1) to calculate "Max"
                var allCurriculums = db.Curriculums
                                    .Include(c => c.Course)
                                    .Where(c => c.Semester == targetSemester)
                                    .ToList();

                // 4. Calculate Units for each Section in memory
                foreach (var section in _allSections)
                {
                    // A. Calculate Assigned Units
                    // Logic: Filter schedules for this section -> Get Unique Courses -> Sum Units
                    int assigned = allSchedules
                        .Where(s => s.SectionId == section.Id && s.Course != null)
                        .Select(s => s.CourseId) 
                        .Distinct() // distinct so we don't double-count split schedules (Mon/Wed)
                        .Sum(id => allSchedules.First(s => s.CourseId == id).Course!.Units);

                    // B. Calculate Max Units
                    // Logic: Filter curriculum for this Program/Year -> Sum Units
                    int max = allCurriculums
                        .Where(c => c.Program == section.Program && 
                                    c.YearLevel == section.YearLevel && 
                                    c.Course != null)
                        .Sum(c => c.Course!.Units);

                    // C. Set the Display Property
                    section.UnitsDisplay = $"{assigned} / {max}";
                }

                SectionsGrid.ItemsSource = _allSections;
            }
        }

        // NEW: Search Logic
        private void SearchTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchTxt.Text.ToLower();
            if (string.IsNullOrWhiteSpace(query))
            {
                SectionsGrid.ItemsSource = _allSections;
            }
            else
            {
                var filtered = _allSections.Where(s => 
                    s.FullDisplayName.ToLower().Contains(query) ||
                    s.Program.ToLower().Contains(query)
                ).ToList();
                SectionsGrid.ItemsSource = filtered;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddSectionWindow();
            win.Topmost = true;
            win.ShowDialog();
            LoadSections();
            MainWindow.TriggerDatabaseUpdated();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (SectionsGrid.SelectedItem is StudentSection selectedSection)
            {
                var win = new AddSectionWindow(selectedSection);
                win.Topmost = true;
                win.ShowDialog();
                LoadSections();
                MainWindow.TriggerDatabaseUpdated();
            }
            else
            {
                MessageBox.Show("Please select a section to edit.");
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (SectionsGrid.SelectedItem is StudentSection selectedSection)
            {
                // 1. OPEN DATABASE CONNECTION FIRST
                using (var db = new AppDbContext())
                {
                    // 2. NOW WE CAN USE 'db' FOR THE CHECK
                    if (db.Schedules.Any(s => s.SectionId == selectedSection.Id))
                    {
                        MessageBox.Show($"Cannot delete {selectedSection.Name} because it has scheduled classes.\nPlease clear its schedule first.", "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 3. CONFIRM AND DELETE
                    if (MessageBox.Show($"Delete {selectedSection.FullDisplayName}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        db.Sections.Remove(selectedSection);
                        db.SaveChanges();
                        
                        // Refresh list (This calls LoadSections which makes its own db connection, so it's safe)
                        LoadSections();
                        MainWindow.TriggerDatabaseUpdated();
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a section to delete.");
            }
        }
    
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new AppDbContext())
            {
                
                if (db.Schedules.Any(s => s.SectionId != null))
                {
                    MessageBox.Show("Cannot reset Sections because there are active Class Schedules.\n" +
                                    "Please clear the Class Schedules first via the Dashboard.", 
                                    "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (MessageBox.Show("Are you sure you want to delete ALL Student Sections?", 
                    "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    if (MessageBox.Show("Confirm delete ALL Sections? This is permanent.", 
                        "Final Check", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                    {
                        db.Sections.ExecuteDelete();
                        LoadSections(); // Refresh list
                        MainWindow.TriggerDatabaseUpdated();
                    }
                }
            }
        }
    }
}