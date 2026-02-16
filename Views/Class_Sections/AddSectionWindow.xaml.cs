using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
    public partial class AddSectionWindow : Window
    {
        private int _editingId = 0;

        public AddSectionWindow()
        {
            InitializeComponent();
        }

        public AddSectionWindow(StudentSection sectionToEdit)
        {
            InitializeComponent();
            _editingId = sectionToEdit.Id;
            this.Title = "Edit Section";

            NameTxt.Text = sectionToEdit.Name;
            CountTxt.Text = sectionToEdit.StudentCount.ToString();

            // Set Program (Handle custom text)
            ProgramCombo.Text = sectionToEdit.Program;

            // Select Year
            foreach (ComboBoxItem item in YearCombo.Items)
            {
                if (item.Tag != null && item.Tag.ToString() == sectionToEdit.YearLevel.ToString())
                    YearCombo.SelectedItem = item;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validation
            if (string.IsNullOrWhiteSpace(NameTxt.Text)) 
            { 
                MessageBox.Show("Section Name is required."); 
                return; 
            }

            // Get Program Text (Since it is editable, we use .Text property)
            string program = ProgramCombo.Text.Trim();
            if (string.IsNullOrWhiteSpace(program))
            {
                MessageBox.Show("Please select or type a Program.");
                return;
            }

            // Validate Count
            if (!int.TryParse(CountTxt.Text, out int count) || count <= 0) 
            { 
                MessageBox.Show("Student Count must be a positive number."); 
                return; 
            }

            // Get Year
            var selectedYearItem = YearCombo.SelectedItem as ComboBoxItem;
            if (selectedYearItem == null || selectedYearItem.Tag == null)
            {
                MessageBox.Show("Please select a Year Level.");
                return;
            }
            int year = int.Parse(selectedYearItem.Tag.ToString() ?? "1");

            // 2. Save to DB
            using (var db = new AppDbContext())
            {
                if (_editingId == 0)
                {
                    db.Sections.Add(new StudentSection
                    {
                        Program = program,
                        YearLevel = year,
                        Name = NameTxt.Text,
                        StudentCount = count
                    });
                }
                else
                {
                    var existing = db.Sections.Find(_editingId);
                    if (existing != null)
                    {
                        existing.Program = program;
                        existing.YearLevel = year;
                        existing.Name = NameTxt.Text;
                        existing.StudentCount = count;
                    }
                }
                db.SaveChanges();
            }

            MessageBox.Show("Section Saved!");
            this.Close();
        }
    }
}