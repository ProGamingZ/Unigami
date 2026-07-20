using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Views
{
    // Helper Class for the Dynamic Tabs
    public class CourseTabViewModel
    {
        public string TabName { get; set; } = string.Empty;
        public List<Course> Courses { get; set; } = new List<Course>();
    }

    public partial class CoursesView : UserControl
    {
        private List<Course> _allCourses = new List<Course>();
        private Course? _selectedCourse; // Tracks selection across all dynamic tables

        public CoursesView()
        {
            InitializeComponent();
            LoadCourses();
        }

        private void LoadCourses()
        {
            using (var db = new AppDbContext())
            {
                _allCourses = db.Courses.OrderBy(c => c.Code).ToList();
                CoursesTabControl.ItemsSource = BuildTabs(_allCourses);
            }
        }

        // --- THE CATEGORIZATION LOGIC ---
        private List<CourseTabViewModel> BuildTabs(List<Course> coursesToDistribute)
        {
            var tabs = new List<CourseTabViewModel>();
            
            using (var db = new AppDbContext())
            {
                // 1. Get available programs dynamically from the database
                var programs = db.Programs.Select(p => p.Code).OrderBy(p => p).ToList();
                
                // 2. Setup dictionary to act as our sorting bins
                var categorizedCourses = new Dictionary<string, List<Course>>();
                foreach (var p in programs) categorizedCourses[p] = new List<Course>();
                categorizedCourses["Shared Courses"] = new List<Course>();
                categorizedCourses["Uncategorized"] = new List<Course>();

                // 3. Sort courses into bins
                foreach (var c in coursesToDistribute)
                {
                    string progs = c.RecommendedPrograms ?? "";
                    // Split by comma to check quantity
                    var parts = progs.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 0)
                    {
                        categorizedCourses["Uncategorized"].Add(c);
                    }
                    else if (parts.Length > 1) // 2 or more programs = Shared
                    {
                        categorizedCourses["Shared Courses"].Add(c);
                    }
                    else // Exactly 1 program
                    {
                        string singleProg = parts[0].Trim();
                        if (categorizedCourses.ContainsKey(singleProg))
                        {
                            categorizedCourses[singleProg].Add(c);
                        }
                        else
                        {
                            categorizedCourses["Uncategorized"].Add(c); // Fallback if program was deleted
                        }
                    }
                }

                // 4. Build the final UI Tabs
                foreach (var p in programs)
                {
                    tabs.Add(new CourseTabViewModel { TabName = p, Courses = categorizedCourses[p] });
                }
                
                tabs.Add(new CourseTabViewModel { TabName = "Shared Courses", Courses = categorizedCourses["Shared Courses"] });
                
                // Only show uncategorized if it actually has forgotten courses
                if (categorizedCourses["Uncategorized"].Any())
                {
                    tabs.Add(new CourseTabViewModel { TabName = "Uncategorized", Courses = categorizedCourses["Uncategorized"] });
                }
            }
            return tabs;
        }

        private void SearchTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchTxt.Text.ToLower();
            List<Course> filtered;

            if (string.IsNullOrWhiteSpace(query))
            {
                filtered = _allCourses;
            }
            else
            {
                filtered = _allCourses.Where(c => 
                    (c.Code != null && c.Code.ToLower().Contains(query)) || 
                    (c.Name != null && c.Name.ToLower().Contains(query))
                ).ToList();
            }

            // Rebuild the tabs using only the filtered search results
            CoursesTabControl.ItemsSource = BuildTabs(filtered);
        }

        // --- SELECTION TRACKING HANDLERS ---
        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Whenever a user clicks a row in ANY of the tabs, save that course to our variable
            if (sender is DataGrid grid && grid.SelectedItem is Course course)
            {
                _selectedCourse = course;
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Prevent accidental edits: If the user changes tabs, clear their selection
            if (e.OriginalSource is TabControl)
            {
                _selectedCourse = null;
            }
        }

        // --- BUTTON HANDLERS ---
        private void ManagePrograms_Click(object sender, RoutedEventArgs e)
        {
            var win = new ManageProgramsWindow();
            win.Topmost = true;
            win.ShowDialog();
            
            // Reload just in case they added/deleted a program to refresh the Tabs
            LoadCourses(); 
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddCourseWindow();
            win.Topmost = true;
            win.ShowDialog();
            LoadCourses();
            MainWindow.TriggerDatabaseUpdated();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCourse != null)
            {
                var win = new AddCourseWindow(_selectedCourse);
                win.Topmost = true;
                win.ShowDialog();
                LoadCourses();
                MainWindow.TriggerDatabaseUpdated();
            }
            else
            {
                MessageBox.Show("Please select a course to edit from the active table.");
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCourse != null)
            {
                if (MessageBox.Show($"Delete {_selectedCourse.Code}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    using (var db = new AppDbContext())
                    {
                        db.Courses.Remove(_selectedCourse);
                        db.SaveChanges();
                    }
                    _selectedCourse = null;
                    LoadCourses();
                    MainWindow.TriggerDatabaseUpdated();
                }
            }
            else
            {
                MessageBox.Show("Please select a course to delete from the active table.");
            }
        }
    
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new AppDbContext())
            {
                if (db.Schedules.Any(s => s.CourseId != 0))
                {
                    MessageBox.Show("Cannot reset Courses because they are being used in the Class Schedule.\n" +
                                    "Please clear the Schedule first.", 
                                    "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (db.Curriculums.Any())
                {
                    MessageBox.Show("Cannot reset Courses because they are assigned to Curriculums.\n" +
                                    "Please clear the Curriculum assignments first.", 
                                    "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (MessageBox.Show("Are you sure you want to delete ALL Courses?", 
                    "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    if (MessageBox.Show("This will wipe the course list. Are you really sure?", 
                        "Final Check", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                    {
                        db.Courses.ExecuteDelete();
                        _selectedCourse = null;
                        LoadCourses();
                        MainWindow.TriggerDatabaseUpdated();
                    }
                }
            }
        }
    }
}