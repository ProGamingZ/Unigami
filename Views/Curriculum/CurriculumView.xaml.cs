using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UniversityScheduler.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Views
{
    public partial class CurriculumManagerView : UserControl
    {
        private int _targetSemester;
        
        // 1. Changed to ObservableCollection so the UI automatically tracks live changes
        private ObservableCollection<CurriculumGroup> _allGroupedCurriculum = new ObservableCollection<CurriculumGroup>();

        public CurriculumManagerView(int semester)
        {
            InitializeComponent();
            _targetSemester = semester;
            
            // 2. We bind the UI exactly ONCE in the constructor
            CurriculumCardsControl.ItemsSource = _allGroupedCurriculum;
            
            LoadData();
        }

        private void LoadData()
        {
            using var db = new AppDbContext();
            var rawData = db.Curriculums
                .Include(c => c.Course)
                .Where(c => c.Semester == _targetSemester)
                .ToList();

            // Structure the fresh data from the database
            var latestData = rawData
                .GroupBy(c => new { c.Program, c.YearLevel, c.Semester })
                .Select(g => new CurriculumGroup
                {
                    Program = g.Key.Program,
                    YearLevel = g.Key.YearLevel,
                    Semester = g.Key.Semester,
                    GroupTitle = $"{g.Key.Program} {g.Key.YearLevel}",
                    Items = new ObservableCollection<Curriculum>(g.OrderBy(c => c.Course?.Code))
                })
                .OrderBy(g => g.GroupTitle)
                .ToList();

            // --- 3. SMART SYNCHRONIZATION ---

            // A. Remove cards that were deleted in the database
            var toRemove = _allGroupedCurriculum.Where(old => !latestData.Any(n => n.GroupTitle == old.GroupTitle)).ToList();
            foreach (var item in toRemove)
            {
                _allGroupedCurriculum.Remove(item);
            }

            // B. Add new cards or update existing ones without destroying the UI
            for (int i = 0; i < latestData.Count; i++)
            {
                var newGroup = latestData[i];
                var existingGroup = _allGroupedCurriculum.FirstOrDefault(g => g.GroupTitle == newGroup.GroupTitle);

                if (existingGroup == null)
                {
                    // It's a new card, insert it at the correct alphabetical index
                    _allGroupedCurriculum.Insert(i, newGroup);
                }
                else
                {
                    // The card already exists on screen! Just clear and refill the inner table.
                    // This prevents the ToggleButton and ScrollViewer from resetting.
                    existingGroup.Items.Clear();
                    foreach (var course in newGroup.Items)
                    {
                        existingGroup.Items.Add(course);
                    }
                }
            }

            // C. If the user was actively searching while an edit happened, re-apply the filter
            ApplySearchFilter();
        }

        private void SearchTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        // 4. Updated Search Logic: Uses WPF's native visual filtering so it doesn't break our ObservableCollection
        private void ApplySearchFilter()
        {
            string query = SearchTxt.Text?.ToLower() ?? "";
            var view = CollectionViewSource.GetDefaultView(_allGroupedCurriculum);

            if (string.IsNullOrWhiteSpace(query))
            {
                view.Filter = null; // Show everything
            }
            else
            {
                view.Filter = obj =>
                {
                    if (obj is CurriculumGroup group)
                    {
                        // Show the card if the Title matches OR if any inner course matches
                        return group.GroupTitle.ToLower().Contains(query) ||
                               group.Items.Any(c => c.Course != null && 
                                                    (c.Course.Code.ToLower().Contains(query) || 
                                                     c.Course.Name.ToLower().Contains(query)));
                    }
                    return false;
                };
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddCurriculumWindow(_targetSemester);
            win.Topmost = true;
            win.ShowDialog();
            LoadData();
            MainWindow.TriggerDatabaseUpdated();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (CurriculumCardsControl.SelectedItem is CurriculumGroup selectedGroup)
            {
                var win = new AddCurriculumWindow(selectedGroup);
                win.Topmost = true;
                win.ShowDialog();
                LoadData();
                MainWindow.TriggerDatabaseUpdated();
            }
            else
            {
                MessageBox.Show("Please click on a card to edit it.");
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (CurriculumCardsControl.SelectedItem is CurriculumGroup selectedGroup)
            {
                if (MessageBox.Show($"Remove all courses for {selectedGroup.GroupTitle}?", 
                    "Confirm Remove Card", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var db = new AppDbContext())
                    {
                        var itemsToDelete = db.Curriculums.Where(c => 
                            c.Program == selectedGroup.Program && 
                            c.YearLevel == selectedGroup.YearLevel && 
                            c.Semester == selectedGroup.Semester).ToList();

                        db.Curriculums.RemoveRange(itemsToDelete);
                        db.SaveChanges();
                    }
                    LoadData();
                    MainWindow.TriggerDatabaseUpdated();
                }
            }
            else
            {
                MessageBox.Show("Please click on a card to remove it.");
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

    public class CurriculumGroup
    {
        public string Program { get; set; } = string.Empty;
        public int YearLevel { get; set; }
        public int Semester { get; set; }
        public string GroupTitle { get; set; } = string.Empty;
        public bool IsExpanded { get; set; } = true;
        public ObservableCollection<Curriculum> Items { get; set; } = [];
    }
}