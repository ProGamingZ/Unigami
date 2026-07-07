using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
    // Updated helper class to hold full course data for the mini-table
    public class CourseDisplayItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int Units { get; set; }
        public int LectureHours { get; set; }
        public int LabHours { get; set; }
        public string Programs { get; set; } = "";
        public string FullDisplay => $"{Code} - {Name}";
    }

    public partial class AddCurriculumWindow : Window
    {
        private List<CourseDisplayItem> _allCourseItems = new List<CourseDisplayItem>();
        
        // This collection binds to the DataGrid
        private ObservableCollection<CourseDisplayItem> _selectedCourses = new ObservableCollection<CourseDisplayItem>();
        
        private bool _isEditMode = false;

        // Constructor for ADDING
        public AddCurriculumWindow(int targetSemester)
        {
            InitializeComponent();
            LoadCourses();
            
            // Set Target Semester
            foreach (ComboBoxItem item in SemesterCombo.Items)
                if (item.Tag.ToString() == targetSemester.ToString()) SemesterCombo.SelectedItem = item;

            SelectedCoursesGrid.ItemsSource = _selectedCourses;
            ProgramCombo.SelectionChanged += (s, e) => FilterCourses();
            ProgramCombo.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, 
                new TextChangedEventHandler((s, e) => FilterCourses()));
            
            FilterCourses();
        }

        // Constructor for EDITING
        public AddCurriculumWindow(CurriculumGroup editGroup) : this(editGroup.Semester)
        {
            _isEditMode = true;
            Title = $"Edit {editGroup.GroupTitle}";

            // Lock the combo boxes so they don't accidentally move the entire group
            ProgramCombo.IsEnabled = false;
            YearCombo.IsEnabled = false;
            SemesterCombo.IsEnabled = false;

            ProgramCombo.Text = editGroup.Program;
            
            foreach(ComboBoxItem item in YearCombo.Items)
                if (item.Tag.ToString() == editGroup.YearLevel.ToString()) YearCombo.SelectedItem = item;

            // Load existing courses into the mini table
            foreach(var item in editGroup.Items)
            {
                if (item.Course != null)
                {
                    _selectedCourses.Add(new CourseDisplayItem 
                    { 
                        Id = item.Course.Id, 
                        Code = item.Course.Code, 
                        Name = item.Course.Name,
                        Units = item.Course.Units,
                        LectureHours = item.Course.LectureHours,
                        LabHours = item.Course.LabHours
                    });
                }
            }
            
            FilterCourses();
        }

        private void LoadCourses()
        {
            using (var db = new AppDbContext())
            {
                _allCourseItems = db.Courses.OrderBy(c => c.Code).Select(c => new CourseDisplayItem 
                { 
                    Id = c.Id, Code = c.Code, Name = c.Name,
                    Units = c.Units, LectureHours = c.LectureHours, LabHours = c.LabHours,
                    Programs = c.RecommendedPrograms ?? "" 
                }).ToList();
            }
        }

        private void FilterCourses()
        {
            string selectedProgram = ProgramCombo.Text.Trim(); 
            if (string.IsNullOrWhiteSpace(selectedProgram))
            {
                CourseCombo.ItemsSource = _allCourseItems;
            }
            else
            {
                var filtered = _allCourseItems
                    .Where(c => c.Programs.IndexOf(selectedProgram, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                CourseCombo.ItemsSource = filtered;
            }
        }

        // Add a course to the mini table
        private void AddCourse_Click(object sender, RoutedEventArgs e)
        {
            if (CourseCombo.SelectedItem is CourseDisplayItem selectedCourse)
            {
                // Prevent duplicate rows in the grid
                if (_selectedCourses.Any(c => c.Id == selectedCourse.Id))
                {
                    MessageBox.Show("This course is already added to the list.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _selectedCourses.Add(selectedCourse);
                CourseCombo.SelectedItem = null; // Reset combo box
                CourseCombo.Text = "";
            }
            else
            {
                MessageBox.Show("Please select a valid course from the dropdown.", "Invalid Selection");
            }
        }

        // Remove a course from the mini table
        private void RemoveCourse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CourseDisplayItem itemToRemove)
            {
                _selectedCourses.Remove(itemToRemove);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string program = ProgramCombo.Text.Trim();
            if (string.IsNullOrWhiteSpace(program)) { MessageBox.Show("Program is required."); return; }

            int year = int.Parse(((ComboBoxItem)YearCombo.SelectedItem).Tag.ToString() ?? "1");
            int sem = int.Parse(((ComboBoxItem)SemesterCombo.SelectedItem).Tag.ToString() ?? "1");

            using (var db = new AppDbContext())
            {
                // Step 1: Find all existing database entries for this specific Card
                var existingEntries = db.Curriculums.Where(c => 
                    c.Program == program && 
                    c.YearLevel == year && 
                    c.Semester == sem).ToList();

                // Step 1.5: SAFETY CHECK - If we are adding a new card, warn before overwriting
                if (!_isEditMode && existingEntries.Any())
                {
                    var result = MessageBox.Show(
                        $"A curriculum card for {program} {year}-{sem} already exists.\n\nDo you want to overwrite it with this new list of courses?", 
                        "Existing Curriculum Found", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Warning);

                    // If the user clicks No, cancel the save process and keep the window open
                    if (result == MessageBoxResult.No)
                    {
                        return; 
                    }
                }

                // Step 2: Delete the old entries (Proceeds if Editing, or if User said 'Yes' to overwrite)
                db.Curriculums.RemoveRange(existingEntries);

                // Step 3: Re-add whatever is currently in the _selectedCourses list
                foreach (var sc in _selectedCourses)
                {
                    db.Curriculums.Add(new Curriculum
                    {
                        Program = program,
                        YearLevel = year,
                        Semester = sem,
                        CourseId = sc.Id
                    });
                }
                
                db.SaveChanges();
            }

            MessageBox.Show("Curriculum Card Saved successfully!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
    
    }
}