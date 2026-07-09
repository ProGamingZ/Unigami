using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Views
{
    public partial class InstructorsView : UserControl
    {
        // Change list type to our new ViewModel
        private List<InstructorViewModel> _allInstructors = new List<InstructorViewModel>();
        private int _targetSemester;

        public InstructorsView(int semester)
        {
            InitializeComponent();
            _targetSemester = semester;
            LoadInstructors();
        }

        private void LoadInstructors()
        {
            using (var db = new AppDbContext())
            {
                var instructors = db.Instructors
                            .Include(i => i.AssignedRoom) 
                            .ToList();
                
                // Fetch ALL schedules
                var schedules = db.Schedules
                    .Include(s => s.Course)
                    .Where(s => s.InstructorId != null)
                    .ToList();

                _allInstructors = instructors.Select(inst => 
                {
                    // Calculate Semester Load
                    int currentLoad = schedules
                    .Where(s => s.InstructorId == inst.Id && 
                                s.Course != null && 
                                s.Semester == _targetSemester) 
                    .Select(s => new { s.CourseId, s.SectionId, s.Course!.Units })
                    .Distinct()
                    .Sum(x => x.Units);

                    return new InstructorViewModel
                    {
                        Source = inst,
                        CurrentUnits = currentLoad,
                        // Helpful format for secretaries
                        UnitLoadDisplay = $"{currentLoad} / {inst.MaxUnits}" 
                    };
                }).OrderBy(vm => vm.Source.Surname).ThenBy(vm => vm.Source.FirstName).ToList();

                InstructorsGrid.ItemsSource = _allInstructors;
            }
        }

        // --- Search Logic Updated for ViewModel ---
        private void SearchTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchTxt.Text.ToLower();

            if (string.IsNullOrWhiteSpace(query))
            {
                InstructorsGrid.ItemsSource = _allInstructors;
            }
            else
            {
                var filtered = _allInstructors.Where(vm => 
                    vm.Source.FullName.ToLower().Contains(query) || 
                    vm.Source.Program.ToLower().Contains(query)
                ).ToList();

                InstructorsGrid.ItemsSource = filtered;
            }
        }

        // --- Event Handlers Updated for ViewModel Casting ---

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddInstructorWindow();
            addWindow.Topmost = true;
            addWindow.ShowDialog();
            LoadInstructors();
            MainWindow.TriggerDatabaseUpdated();
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            // CAST to InstructorViewModel, then access .Source
            if (InstructorsGrid.SelectedItem is InstructorViewModel selectedVM)
            {
                var editWindow = new AddInstructorWindow(selectedVM.Source);
                editWindow.Topmost = true;
                editWindow.ShowDialog();
                LoadInstructors();
                MainWindow.TriggerDatabaseUpdated();
            }
            else
            {
                MessageBox.Show("Please select an instructor to edit.");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (InstructorsGrid.SelectedItem is InstructorViewModel selectedVM)
            {
                using (var db = new AppDbContext())
                {
                    // 1. SAFETY CHECK: Is this instructor assigned to any classes?
                    bool hasAssignments = db.Schedules.Any(s => s.InstructorId == selectedVM.Source.Id);

                    if (hasAssignments)
                    {
                        MessageBox.Show(
                            $"Cannot delete {selectedVM.Name} because they have assigned classes.\n\n" +
                            "Please use the 'Unassign Selected' button first.", // Updated message
                            "Deletion Blocked",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return; // Stop here. Do not attempt delete.
                    }

                    // 2. If safe, proceed with confirmation and delete
                    if (MessageBox.Show($"Delete {selectedVM.Name}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        // Re-attach the object to the new context to delete it
                        db.Instructors.Remove(selectedVM.Source);
                        db.SaveChanges();
                        LoadInstructors();
                        MainWindow.TriggerDatabaseUpdated();
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select an instructor to delete.");
            }
        }    
       
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new AppDbContext())
            {
                // 1. SAFETY CHECK: Are there ANY assigned courses?
                // We check if any schedule has a non-null InstructorId
                bool anyAssignments = db.Schedules.Any(s => s.InstructorId != null);

                if (anyAssignments)
                {
                    MessageBox.Show(
                        "Cannot perform 'Reset All' because there are still instructors with assigned courses.\n\n" +
                        "Please unassign all courses first using the 'Unassign Selected' button on the relevant instructors.",
                        "Reset Blocked",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return; // STOP execution here prevents the crash
                }

                // 2. If safe, proceed
                if (MessageBox.Show("Are you sure you want to delete ALL Instructors?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    db.Instructors.ExecuteDelete(); 
                    LoadInstructors();
                    MainWindow.TriggerDatabaseUpdated();
                }
            }
        }
    
        private void UnassignButton_Click(object sender, RoutedEventArgs e)
        {
            // Get selected items (Support Multiple Selection)
            var selectedItems = InstructorsGrid.SelectedItems.Cast<InstructorViewModel>().ToList();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one instructor to unassign.");
                return;
            }

            if (MessageBox.Show($"Unassign all courses for {selectedItems.Count} selected instructor(s)?", 
                "Confirm Unassign", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using (var db = new AppDbContext())
                {
                    // Get the IDs of the selected instructors
                    var instructorIds = selectedItems.Select(vm => vm.Source.Id).ToList();

                    // Find all schedules assigned to these IDs
                    var schedulesToUpdate = db.Schedules
                        .Where(s => s.InstructorId != null && instructorIds.Contains(s.InstructorId.Value))
                        .ToList();

                    // Unassign them (Set to NULL)
                    foreach (var schedule in schedulesToUpdate)
                    {
                        schedule.InstructorId = null;
                    }

                    db.SaveChanges();
                }
                
                MessageBox.Show("Courses unassigned successfully.");
                LoadInstructors(); // Refresh the grid
                MainWindow.TriggerDatabaseUpdated();
            }
        }
    
    }

    // --- Helper Class for Display ---
    public class InstructorViewModel
    {
        public required Instructor Source { get; set; }

        // Expose properties for DataGrid Binding
        public string Name => Source.FullName;
        public string Initials => Source.Initials;
        public string Room => Source.AssignedRoom?.Name ?? "-";
        public string Program => Source.Program;
        public string Status => Source.Status;
        
        public int CurrentUnits { get; set; }
        public int MaxUnits => Source.MaxUnits;


        // The new Calculated Property
        public string UnitLoadDisplay { get; set; } = "0 / 0";
        
        // Calculate the specific Overload amount
        public string OverloadDisplay
        {
            get
            {
                int diff = CurrentUnits - MaxUnits;
                if (diff > 0)
                {
                    return $"+{diff}"; // e.g., "+3"
                }
                return "-"; // Empty or dash if not overloaded
            }
        }

        // Color coding logic (Optional: Makes text Red if overloaded)
        public string LoadColor 
        {
            get 
            {
                try 
                {
                    var parts = UnitLoadDisplay.Split('/');
                    if (int.Parse(parts[0].Trim()) > int.Parse(parts[1].Trim())) return "#C0392B"; // Red
                } 
                catch {}
                return "#333333"; // Default Black/Gray
            }
        }
    }
}