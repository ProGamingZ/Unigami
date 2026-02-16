using System.Collections.Generic; // Required for List<>
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Views
{
    public partial class CoursesView : UserControl
    {
        // FIX: Define the cache variable here
        private List<Course> _allCourses = new List<Course>();

        public CoursesView()
        {
            InitializeComponent();
            LoadCourses();
        }

        private void LoadCourses()
        {
            using (var db = new AppDbContext())
            {
                // FIX: Save data to _allCourses so search works
                _allCourses = db.Courses.OrderBy(c => c.Code).ToList();
                CoursesGrid.ItemsSource = _allCourses;
            }
        }

        private void SearchTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchTxt.Text.ToLower();
            if (string.IsNullOrWhiteSpace(query))
            {
                CoursesGrid.ItemsSource = _allCourses;
            }
            else
            {
                var filtered = _allCourses.Where(c => 
                    c.Code.ToLower().Contains(query) || 
                    c.Name.ToLower().Contains(query)
                ).ToList();
                CoursesGrid.ItemsSource = filtered;
            }
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
            if (CoursesGrid.SelectedItem is Course selectedCourse)
            {
                var win = new AddCourseWindow(selectedCourse);
                win.Topmost = true;
                win.ShowDialog();
                LoadCourses();
                MainWindow.TriggerDatabaseUpdated();
            }
            else
            {
                MessageBox.Show("Please select a course to edit.");
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (CoursesGrid.SelectedItem is Course selectedCourse)
            {
                if (MessageBox.Show($"Delete {selectedCourse.Code}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    using (var db = new AppDbContext())
                    {
                        db.Courses.Remove(selectedCourse);
                        db.SaveChanges();
                    }
                    LoadCourses();
                    MainWindow.TriggerDatabaseUpdated();
                }
            }
            else
            {
                MessageBox.Show("Please select a course to delete.");
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
                        LoadCourses();
                        MainWindow.TriggerDatabaseUpdated();
                    }
                }
            }
        }


    }
}