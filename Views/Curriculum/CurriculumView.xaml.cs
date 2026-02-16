using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Views
{
    public partial class CurriculumManagerView : UserControl
    {
        private List<Curriculum> _allCurriculum = new List<Curriculum>();

        public CurriculumManagerView()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            using (var db = new AppDbContext())
            {
                // We need to Include(c => c.Course) to display Code/Name
                _allCurriculum = db.Curriculums
                    .Include(c => c.Course)
                    .OrderBy(c => c.Program)
                    .ThenBy(c => c.YearLevel)
                    .ThenBy(c => c.Semester)
                    .ToList();

                CurriculumGrid.ItemsSource = _allCurriculum;
            }
        }

        private void SearchTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchTxt.Text.ToLower();
            if (string.IsNullOrWhiteSpace(query))
            {
                CurriculumGrid.ItemsSource = _allCurriculum;
            }
            else
            {
                var filtered = _allCurriculum.Where(c => 
                    c.Program.ToLower().Contains(query) || 
                    (c.Course != null && c.Course.Code.ToLower().Contains(query)) ||
                    (c.Course != null && c.Course.Name.ToLower().Contains(query))
                ).ToList();
                CurriculumGrid.ItemsSource = filtered;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddCurriculumWindow();
            win.Topmost = true;
            win.ShowDialog();
            LoadData();
            MainWindow.TriggerDatabaseUpdated();
            
        }


        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (CurriculumGrid.SelectedItem is Curriculum selected)
            {
                var win = new AddCurriculumWindow(selected);
                win.Topmost = true;
                win.ShowDialog();
                LoadData();
                MainWindow.TriggerDatabaseUpdated();
            }
            else
            {
                MessageBox.Show("Please select an entry to edit.");
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (CurriculumGrid.SelectedItem is Curriculum selected)
            {
                if (MessageBox.Show($"Remove {selected.Course?.Code} from {selected.Program} {selected.YearLevel}-{selected.Semester}?", 
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var db = new AppDbContext())
                    {
                        var item = db.Curriculums.Find(selected.Id);
                        if (item != null)
                        {
                            db.Curriculums.Remove(item);
                            db.SaveChanges();
                        }
                    }
                    LoadData();
                    MainWindow.TriggerDatabaseUpdated();
                }
            }
            else
            {
                MessageBox.Show("Please select an entry to remove.");
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to delete ALL Curriculum entries?", 
                "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                using (var db = new AppDbContext())
                {
                    db.Curriculums.ExecuteDelete();
                }
                LoadData();
                MainWindow.TriggerDatabaseUpdated();
            }
        }
    }
}