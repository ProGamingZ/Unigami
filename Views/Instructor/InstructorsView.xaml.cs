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
                // 1. UPDATED: Include both semester rooms!
                var instructors = db.Instructors
                            .Include(i => i.AssignedRoomSem1) 
                            .Include(i => i.AssignedRoomSem2) 
                            .ToList();
                
                var schedules = db.Schedules
                    .Include(s => s.Course)
                    .Where(s => s.InstructorId != null)
                    .ToList();

                _allInstructors = instructors.Select(inst => 
                {
                    int currentLoad = schedules
                    .Where(s => s.InstructorId == inst.Id && 
                                s.Course != null && 
                                s.Semester == _targetSemester) 
                    .Select(s => new { s.CourseId, s.SectionId, s.Course!.Units })
                    .Distinct()
                    .Sum(x => x.Units);

                    // 2. UPDATED: Grab the correct max units dynamically
                    int maxUnits = _targetSemester == 1 ? inst.MaxUnitsSem1 : inst.MaxUnitsSem2;

                    return new InstructorViewModel
                    {
                        Source = inst,
                        Semester = _targetSemester, // Pass the semester to the ViewModel!
                        CurrentUnits = currentLoad,
                        UnitLoadDisplay = $"{currentLoad} / {maxUnits}" 
                    };
                }).OrderBy(vm => vm.Source.Surname).ThenBy(vm => vm.Source.FirstName).ToList();

                InstructorsGrid.ItemsSource = _allInstructors;
            }
        }

        private void SearchTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchTxt.Text.ToLower();

            if (string.IsNullOrWhiteSpace(query))
            {
                InstructorsGrid.ItemsSource = _allInstructors;
            }
            else
            {
                // 3. UPDATED: Use the ViewModel's dynamically calculated Program property
                var filtered = _allInstructors.Where(vm => 
                    vm.Source.FullName.ToLower().Contains(query) || 
                    (vm.Program ?? "").ToLower().Contains(query)
                ).ToList();

                InstructorsGrid.ItemsSource = filtered;
            }
        }

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
                    bool hasAssignments = db.Schedules.Any(s => s.InstructorId == selectedVM.Source.Id);

                    if (hasAssignments)
                    {
                        MessageBox.Show(
                            $"Cannot delete {selectedVM.Name} because they have assigned classes.\n\n" +
                            "Please use the 'Unassign Selected' button first.", 
                            "Deletion Blocked",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return; 
                    }

                    if (MessageBox.Show($"Delete {selectedVM.Name}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
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
                bool anyAssignments = db.Schedules.Any(s => s.InstructorId != null);

                if (anyAssignments)
                {
                    MessageBox.Show(
                        "Cannot perform 'Reset All' because there are still instructors with assigned courses.\n\n" +
                        "Please unassign all courses first using the 'Unassign Selected' button on the relevant instructors.",
                        "Reset Blocked",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return; 
                }

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
                    var instructorIds = selectedItems.Select(vm => vm.Source.Id).ToList();

                    var schedulesToUpdate = db.Schedules
                        .Where(s => s.InstructorId != null && instructorIds.Contains(s.InstructorId.Value))
                        .ToList();

                    foreach (var schedule in schedulesToUpdate)
                    {
                        schedule.InstructorId = null;
                    }

                    db.SaveChanges();
                }
                
                MessageBox.Show("Courses unassigned successfully.");
                LoadInstructors();
                MainWindow.TriggerDatabaseUpdated();
            }
        }
    
        private void ClearSettings_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = InstructorsGrid.SelectedItems.Cast<InstructorViewModel>().ToList();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one instructor to clear settings for.");
                return;
            }

            var instructorIds = selectedItems.Select(vm => vm.Source.Id).ToList();
            
            var clearWindow = new ClearSettingsWindow(instructorIds);
            clearWindow.Topmost = true;
            clearWindow.Owner = Window.GetWindow(this);
            clearWindow.ShowDialog();
            
            LoadInstructors();
            MainWindow.TriggerDatabaseUpdated();
        }
    
    }

    // --- Helper Class for Display ---
    public class InstructorViewModel
    {
        public required Instructor Source { get; set; }
        
        // 4. UPDATED: We added Semester so the ViewModel knows which data to show!
        public int Semester { get; set; }

        public string Name => Source.FullName;
        public string Initials => Source.Initials;
        
        // 5. UPDATED: Dynamic properties based on the Semester
        public string Room => (Semester == 1 ? Source.AssignedRoomSem1?.Name : Source.AssignedRoomSem2?.Name) ?? "-";
        public string Program => (Semester == 1 ? Source.ProgramSem1 : Source.ProgramSem2) ?? "";
        public string Status => (Semester == 1 ? Source.StatusSem1 : Source.StatusSem2) ?? "Unknown";
        public int MaxUnits => Semester == 1 ? Source.MaxUnitsSem1 : Source.MaxUnitsSem2;
        
        public int CurrentUnits { get; set; }
        public string UnitLoadDisplay { get; set; } = "0 / 0";
        
        public string OverloadDisplay
        {
            get
            {
                int diff = CurrentUnits - MaxUnits;
                if (diff > 0) return $"+{diff}"; 
                return "-"; 
            }
        }

        public string LoadColor 
        {
            get 
            {
                try 
                {
                    var parts = UnitLoadDisplay.Split('/');
                    if (int.Parse(parts[0].Trim()) > int.Parse(parts[1].Trim())) return "#C0392B"; 
                } 
                catch {}
                return "#333333"; 
            }
        }
    }
}