using System.Collections.Generic;
using System.Collections.ObjectModel; // Required for ObservableCollection
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
    // NOTE: If 'SelectableProgram' is defined in AddInstructorWindow.cs, 
    // you DO NOT need to define it here again.

    public partial class AddCourseWindow : Window
    {
        private int _editingId = 0;
        private List<CheckBox> _prereqCheckboxes = new List<CheckBox>();

        public ObservableCollection<SelectableProgram> AvailablePrograms { get; set; } = new ObservableCollection<SelectableProgram>();

        public AddCourseWindow()
        {
            InitializeComponent();
            InitializeLists();
            LoadPrograms(null);
            LoadExistingCoursesAsPrereqs(null);
        }

        public AddCourseWindow(Course courseToEdit)
        {
            InitializeComponent();
            InitializeLists();
            _editingId = courseToEdit.Id;
            this.Title = "Edit Course";

            CodeTxt.Text = courseToEdit.Code;
            NameTxt.Text = courseToEdit.Name;
            UnitsTxt.Text = courseToEdit.Units.ToString();
            LecTxt.Text = courseToEdit.LectureHours.ToString();
            LabTxt.Text = courseToEdit.LabHours.ToString();
            
            // REMOVED ProgTxt line as it no longer exists in XAML
            
            LoadPrograms(courseToEdit.RecommendedPrograms);
            LoadExistingCoursesAsPrereqs(courseToEdit.PrerequisiteCodes);
        }

        private void InitializeLists()
        {
            ProgramCheckList.ItemsSource = AvailablePrograms;
        }

        private void LoadPrograms(string? existingPrograms)
        {
            AvailablePrograms.Clear();
            using (var db = new AppDbContext())
            {
                if (db.Database.CanConnect())
                {
                    var programs = db.Sections
                        .Where(s => s.Program != null)
                        .Select(s => s.Program)
                        .Distinct().OrderBy(p => p).ToList();

                    foreach (var p in programs)
                    {
                        AvailablePrograms.Add(new SelectableProgram { Name = p, IsSelected = false });
                    }
                }
            }

            if (!string.IsNullOrEmpty(existingPrograms))
            {
                var currentTags = existingPrograms.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in AvailablePrograms)
                {
                    if (currentTags.Contains(item.Name)) item.IsSelected = true;
                }
            }
        }

        private void LoadExistingCoursesAsPrereqs(string? currentPrereqs)
        {
            using (var db = new AppDbContext())
            {
                var courses = db.Courses.Where(c => c.Id != _editingId).ToList();
                var currentList = currentPrereqs?.Split(',').ToList() ?? new List<string>();

                foreach (var c in courses)
                {
                    var cb = new CheckBox
                    {
                        Content = $"{c.Code} - {c.Name}",
                        Tag = c.Code,
                        Margin = new Thickness(5),
                        IsChecked = currentList.Contains(c.Code)
                    };
                    
                    _prereqCheckboxes.Add(cb);
                    PrereqListPanel.Children.Add(cb);
                }

                if (courses.Count == 0)
                {
                    PrereqListPanel.Children.Add(new TextBlock 
                    { 
                        Text = "No other courses available yet.", 
                        Foreground = System.Windows.Media.Brushes.Gray, 
                        Margin = new Thickness(5) 
                    });
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. Basic Validation
            if (string.IsNullOrWhiteSpace(CodeTxt.Text) || string.IsNullOrWhiteSpace(NameTxt.Text))
            {
                MessageBox.Show("Code and Name are required.");
                return;
            }

            // 2. Numeric Validation (FIXED)
            if (!int.TryParse(UnitsTxt.Text, out int units) || units < 0)
            {
                MessageBox.Show("Units must be a positive number.");
                return;
            }
            if (!int.TryParse(LecTxt.Text, out int lec) || lec < 0)
            {
                MessageBox.Show("Lecture hours must be 0 or greater.");
                return;
            }
            if (!int.TryParse(LabTxt.Text, out int lab) || lab < 0)
            {
                MessageBox.Show("Lab hours must be 0 or greater.");
                return;
            }

            // 3. Gather Checkbox Data
            var selectedProgs = AvailablePrograms.Where(p => p.IsSelected).Select(p => p.Name).ToList();
            string finalProgramStr = string.Join(", ", selectedProgs);

            var selectedCodes = _prereqCheckboxes
                                .Where(cb => cb.IsChecked == true)
                                .Select(cb => cb.Tag.ToString())
                                .ToList();
            string prereqString = string.Join(",", selectedCodes);

            // 4. Save
            using (var db = new AppDbContext())
            {
                if (_editingId == 0)
                {
                    db.Courses.Add(new Course
                    {
                        Code = CodeTxt.Text,
                        Name = NameTxt.Text,
                        Units = units,
                        LectureHours = lec,
                        LabHours = lab,
                        RecommendedPrograms = finalProgramStr,
                        PrerequisiteCodes = prereqString
                    });
                }
                else
                {
                    var existing = db.Courses.Find(_editingId);
                    if (existing != null)
                    {
                        existing.Code = CodeTxt.Text;
                        existing.Name = NameTxt.Text;
                        existing.Units = units;
                        existing.LectureHours = lec;
                        existing.LabHours = lab;
                        existing.RecommendedPrograms = finalProgramStr;
                        existing.PrerequisiteCodes = prereqString;
                    }
                }
                db.SaveChanges();
            }

            MessageBox.Show("Course Saved!");
            this.Close();
        }
    }
}