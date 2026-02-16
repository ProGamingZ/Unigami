using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Views
{
    // Helper class for the ComboBox
    public class CourseDisplayItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Programs { get; set; } = "";
        // This is what the user sees in the dropdown
        public string FullDisplay => $"{Code} - {Name}";
    }

    public partial class AddCurriculumWindow : Window
    {
        private int _editingId = 0;

        public AddCurriculumWindow()
        {
            InitializeComponent();
            LoadCourses();
            
            // Wire up event to update preview instantly
            ProgramCombo.SelectionChanged += UpdatePreview;

            ProgramCombo.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, 
            new TextChangedEventHandler((s, e) => UpdatePreview(s, null!)));

            YearCombo.SelectionChanged += UpdatePreview;
            SemesterCombo.SelectionChanged += UpdatePreview;
            CourseCombo.SelectionChanged += UpdatePreview;
        }

        public AddCurriculumWindow(Curriculum editItem) : this()
        {
            _editingId = editItem.Id;
            Title = "Edit Curriculum Entry";

            // Set Fields
            ProgramCombo.Text = editItem.Program;
            
            // Set Year
            foreach(ComboBoxItem item in YearCombo.Items)
                if (item.Tag.ToString() == editItem.YearLevel.ToString()) YearCombo.SelectedItem = item;

            // Set Semester
            foreach (ComboBoxItem item in SemesterCombo.Items)
                if (item.Tag.ToString() == editItem.Semester.ToString()) SemesterCombo.SelectedItem = item;

            // Set Course
            CourseCombo.SelectedValue = editItem.CourseId;
        }

        private List<CourseDisplayItem> _allCourseItems = new List<CourseDisplayItem>();
        private void LoadCourses()
        {
            using (var db = new AppDbContext())
            {
                _allCourseItems = db.Courses.OrderBy(c => c.Code).Select(c => new CourseDisplayItem 
                { 
                    Id = c.Id, 
                    Code = c.Code, 
                    Name = c.Name,
                    Programs = c.RecommendedPrograms ?? "" // Handle potential nulls
                }).ToList();

                CourseCombo.ItemsSource = _allCourseItems;
            }
        }

        private void FilterCourses()
        {
            string selectedProgram = ProgramCombo.Text.Trim(); 

            if (string.IsNullOrWhiteSpace(selectedProgram))
            {
                // If nothing is selected/typed, show all courses
                CourseCombo.ItemsSource = _allCourseItems;
            }
            else
            {
                // Filter: Only show courses where the Programs string contains the selected Program
                // (e.g., if Program is "BSCS", show courses that have "BSCS" in their list)
                var filtered = _allCourseItems
                    .Where(c => c.Programs.IndexOf(selectedProgram, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                    
                CourseCombo.ItemsSource = filtered;
            }
        }

        private void UpdatePreview(object sender, RoutedEventArgs e)
        {
            FilterCourses();
            // Simple preview logic
            string prog = ProgramCombo.Text;
            if (string.IsNullOrWhiteSpace(prog)) prog = "???";

            string course = "???";
            if (CourseCombo.SelectedItem is CourseDisplayItem c) course = c.Code;

            PreviewTxt.Text = $"{prog} {YearCombo.Text}, {SemesterCombo.Text}\nAdds Course: {course}";
        }
        
        // Handling the Editable ComboBox text change event manually requires a little casting
        private void UpdatePreview(object sender, TextChangedEventArgs e) => UpdatePreview(sender, new RoutedEventArgs());

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validation
            string program = ProgramCombo.Text.Trim();
            if (string.IsNullOrWhiteSpace(program))
            {
                MessageBox.Show("Program is required.");
                return;
            }

            if (CourseCombo.SelectedValue == null)
            {
                MessageBox.Show("Please select a Course.");
                return;
            }
            int courseId = (int)CourseCombo.SelectedValue;

            int year = int.Parse(((ComboBoxItem)YearCombo.SelectedItem).Tag.ToString() ?? "1");
            int sem = int.Parse(((ComboBoxItem)SemesterCombo.SelectedItem).Tag.ToString() ?? "1");

            using (var db = new AppDbContext())
            {
                // Check for duplicates
                bool exists = db.Curriculums.Any(c => 
                    c.Program == program && 
                    c.YearLevel == year && 
                    c.Semester == sem && 
                    c.CourseId == courseId &&
                    c.Id != _editingId); // Exclude self if editing

                if (exists)
                {
                    MessageBox.Show("This course is already in the curriculum for this Program/Year/Sem.");
                    return;
                }

                if (_editingId == 0)
                {
                    db.Curriculums.Add(new Curriculum
                    {
                        Program = program,
                        YearLevel = year,
                        Semester = sem,
                        CourseId = courseId
                    });
                }
                else
                {
                    var item = db.Curriculums.Find(_editingId);
                    if (item != null)
                    {
                        item.Program = program;
                        item.YearLevel = year;
                        item.Semester = sem;
                        item.CourseId = courseId;
                    }
                }
                db.SaveChanges();
            }

            MessageBox.Show("Curriculum Saved!");
            Close();
        }
    }
}