using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Views
{
    // Helper class for the Dropdowns
    public class DropdownFilterItem
    {
        public string StringId { get; set; } = "";
        public int IntId { get; set; }
        public string Display { get; set; } = "";
    }

    public partial class InstructorsView : UserControl
    {
        private List<InstructorViewModel> _allInstructors = new List<InstructorViewModel>();
        private int _targetSemester;
        private bool _isInitializing = true; // Prevents filters from firing before data loads

        public InstructorsView(int semester)
        {
            InitializeComponent();
            _targetSemester = semester;
            SemToggleButton.Content = $"Sem {_targetSemester}";
            
            InitializeFilters();
            LoadInstructors();
            
            _isInitializing = false;
            ApplyFilters(); // Initial render
        }

        // ====================================================================
        // 1. DATA LOADING
        // ====================================================================
        private void LoadInstructors()
        {
            using (var db = new AppDbContext())
            {
                var instructors = db.Instructors
                            .Include(i => i.AssignedRoomSem1) 
                            .Include(i => i.AssignedRoomSem2) 
                            .ToList();
                
                var schedules = db.Schedules
                    .Include(s => s.Course)
                    .Where(s => s.InstructorId != null)
                    .ToList();

                var allSections = db.Sections.ToList();

                _allInstructors = instructors.Select(inst => 
                {
                    int currentLoad = schedules
                    .Where(s => s.InstructorId == inst.Id && 
                                s.Course != null && 
                                s.Semester == _targetSemester) 
                    .Select(s => new { s.CourseId, s.SectionId, s.Course!.Units })
                    .Distinct()
                    .Sum(x => x.Units);

                    int maxUnits = _targetSemester == 1 ? inst.MaxUnitsSem1 : inst.MaxUnitsSem2;

                    // --- TRANSLATE & GROUP SECTIONS (e.g. BSCS 1-ABCD) ---
                    string rawSections = _targetSemester == 1 ? inst.AssignedSectionsSem1 : inst.AssignedSectionsSem2;
                    var secIds = new List<int>();
                    string groupedSectionStr = "No Sections Assigned";

                    if (!string.IsNullOrWhiteSpace(rawSections))
                    {
                        var ids = rawSections.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var idStr in ids)
                        {
                            if (int.TryParse(idStr.Trim(), out int id)) secIds.Add(id);
                        }

                        if (secIds.Count > 0)
                        {
                            var assignedSecs = allSections.Where(s => secIds.Contains(s.Id)).ToList();
                            
                            // Group by Program and Year, then concatenate the section names (A, B, C -> ABC)
                            var grouped = assignedSecs
                                .GroupBy(s => new { s.Program, s.YearLevel })
                                .OrderBy(g => g.Key.Program).ThenBy(g => g.Key.YearLevel)
                                .Select(g => $"{g.Key.Program} {g.Key.YearLevel}-{string.Join("", g.Select(x => x.Name).OrderBy(n => n))}");
                            
                            groupedSectionStr = string.Join(", ", grouped);
                        }
                    }

                    return new InstructorViewModel
                    {
                        Source = inst,
                        Semester = _targetSemester, 
                        CurrentUnits = currentLoad,
                        UnitLoadDisplay = $"Units: {currentLoad} / {maxUnits}",
                        IsOverloaded = currentLoad > maxUnits,
                        SectionsStr = groupedSectionStr,
                        AssignedSectionIds = secIds
                    };
                }).OrderBy(vm => vm.Source.Surname).ThenBy(vm => vm.Source.FirstName).ToList();
            }

            if (!_isInitializing) ApplyFilters();
        }

        // ====================================================================
        // 2. CASCADING FILTER LOGIC
        // ====================================================================
        private void InitializeFilters()
        {
            using (var db = new AppDbContext())
            {
                // 1. Program Filter
                var programs = db.Programs.Select(p => p.Code).Distinct().ToList();
                var progItems = programs.Select(p => new DropdownFilterItem { StringId = p, Display = p }).ToList();
                progItems.Insert(0, new DropdownFilterItem { StringId = "All", Display = "All Programs" });
                
                FilterProgramCombo.ItemsSource = progItems;
                FilterProgramCombo.SelectedIndex = 0;

                // 2. Dynamic Status Filter (Fetches exactly what is currently in the DB)
                var statuses = _targetSemester == 1 
                    ? db.Instructors.Select(i => i.StatusSem1).Distinct().ToList()
                    : db.Instructors.Select(i => i.StatusSem2).Distinct().ToList();
                    
                var statusItems = statuses.Where(s => !string.IsNullOrWhiteSpace(s))
                                          .Select(s => new DropdownFilterItem { StringId = s, Display = s }).ToList();
                statusItems.Insert(0, new DropdownFilterItem { StringId = "All", Display = "All Statuses" });
                
                FilterStatusCombo.ItemsSource = statusItems;
                FilterStatusCombo.SelectedIndex = 0;

                // 3. Static Load Filter
                var loadItems = new List<DropdownFilterItem>
                {
                    new DropdownFilterItem { StringId = "All", Display = "All Loads" },
                    new DropdownFilterItem { StringId = "Regular", Display = "Regular Load" },
                    new DropdownFilterItem { StringId = "Underload", Display = "Underload" },
                    new DropdownFilterItem { StringId = "Overload", Display = "Overload" }
                };
                FilterLoadCombo.ItemsSource = loadItems;
                FilterLoadCombo.SelectedIndex = 0;
            }
        }

        private void FilterProgramCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedProg = FilterProgramCombo.SelectedItem as DropdownFilterItem;
            
            if (selectedProg != null && selectedProg.StringId != "All")
            {
                using (var db = new AppDbContext())
                {
                    var years = db.Sections.Where(s => s.Program == selectedProg.StringId)
                                           .Select(s => s.YearLevel).Distinct().OrderBy(y => y).ToList();
                    var yearItems = years.Select(y => new DropdownFilterItem { IntId = y, Display = $"{y} Year" }).ToList();
                    yearItems.Insert(0, new DropdownFilterItem { IntId = 0, Display = "All Years" });
                    
                    FilterYearCombo.ItemsSource = yearItems;
                    FilterYearCombo.SelectedIndex = 0;
                }
            }
            else
            {
                FilterYearCombo.ItemsSource = new List<DropdownFilterItem> { new DropdownFilterItem { IntId = 0, Display = "All Years" } };
                FilterYearCombo.SelectedIndex = 0;
            }
            
            if (!_isInitializing) ApplyFilters();
        }

        private void FilterYearCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedProg = FilterProgramCombo.SelectedItem as DropdownFilterItem;
            var selectedYear = FilterYearCombo.SelectedItem as DropdownFilterItem;

            if (selectedProg != null && selectedProg.StringId != "All" && selectedYear != null && selectedYear.IntId != 0)
            {
                using (var db = new AppDbContext())
                {
                    var sections = db.Sections.Where(s => s.Program == selectedProg.StringId && s.YearLevel == selectedYear.IntId).ToList();
                    var sectionItems = sections.Select(s => new DropdownFilterItem { IntId = s.Id, Display = s.FullDisplayName }).ToList();
                    sectionItems.Insert(0, new DropdownFilterItem { IntId = 0, Display = "All Sections" });
                    
                    FilterSectionCombo.ItemsSource = sectionItems;
                    FilterSectionCombo.SelectedIndex = 0;
                }
            }
            else
            {
                FilterSectionCombo.ItemsSource = new List<DropdownFilterItem> { new DropdownFilterItem { IntId = 0, Display = "All Sections" } };
                FilterSectionCombo.SelectedIndex = 0;
            }
            
            if (!_isInitializing) ApplyFilters();
        }

        private void FilterSectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing) ApplyFilters();
        }

        private void FilterStatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing) ApplyFilters();
        }
        private void FilterLoadCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing) ApplyFilters();
        }

        private void SemToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isInitializing = true; // Pause filters from auto-firing while loading
            
            // Toggle semester
            _targetSemester = _targetSemester == 1 ? 2 : 1;
            SemToggleButton.Content = $"Sem {_targetSemester}";
            
            // Re-fetch statuses for the new semester and reload the data
            InitializeFilters(); 
            LoadInstructors(); 
            
            _isInitializing = false;
            ApplyFilters();
        }

        private void SearchTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitializing) ApplyFilters();
        }

        // Central Filter Applier
        private void ApplyFilters()
        {
            if (_allInstructors == null) return;
            
            var filtered = _allInstructors.AsEnumerable();

            // 1. Search Query
            string query = SearchTxt.Text.ToLower();
            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(vm => 
                    vm.Name.ToLower().Contains(query) || 
                    vm.Program.ToLower().Contains(query) ||
                    vm.CoursesStr.ToLower().Contains(query) ||
                    vm.SectionsStr.ToLower().Contains(query)
                );
            }

            // 2. Program Filter
            var progItem = FilterProgramCombo.SelectedItem as DropdownFilterItem;
            if (progItem != null && progItem.StringId != "All")
            {
                filtered = filtered.Where(vm => vm.Program.Contains(progItem.StringId));
            }

            // 3. Year Filter
            var yearItem = FilterYearCombo.SelectedItem as DropdownFilterItem;
            if (yearItem != null && yearItem.IntId != 0)
            {
                filtered = filtered.Where(vm => vm.YearLevels.Contains(yearItem.IntId.ToString()));
            }

            // 4. Section Filter
            var sectionItem = FilterSectionCombo.SelectedItem as DropdownFilterItem;
            if (sectionItem != null && sectionItem.IntId != 0)
            {
                filtered = filtered.Where(vm => vm.AssignedSectionIds.Contains(sectionItem.IntId));
            }

            // 5. Status Filter
            var statusItem = FilterStatusCombo.SelectedItem as DropdownFilterItem;
            if (statusItem != null && statusItem.StringId != "All")
            {
                filtered = filtered.Where(vm => 
                    (_targetSemester == 1 ? vm.Source.StatusSem1 : vm.Source.StatusSem2) == statusItem.StringId
                );
            }

            // 6. Load Filter
            var loadItem = FilterLoadCombo.SelectedItem as DropdownFilterItem;
            if (loadItem != null && loadItem.StringId != "All")
            {
                if (loadItem.StringId == "Regular")
                    filtered = filtered.Where(vm => vm.CurrentUnits == vm.MaxUnits);
                else if (loadItem.StringId == "Underload")
                    filtered = filtered.Where(vm => vm.CurrentUnits < vm.MaxUnits);
                else if (loadItem.StringId == "Overload")
                    filtered = filtered.Where(vm => vm.CurrentUnits > vm.MaxUnits);
            }
            InstructorsGrid.ItemsSource = filtered.ToList();
        }


        // ====================================================================
        // 3. BUTTON HANDLERS (Add, Edit, Delete, Unassign, Settings)
        // ====================================================================
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
                        MessageBox.Show($"Cannot delete {selectedVM.Name} because they have assigned classes.\n\nPlease use the 'Unassign Schedules' button first.", "Deletion Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Cannot perform 'Reset All' because there are still instructors with assigned courses.\n\nPlease unassign all courses first using the 'Unassign Schedules' button.", "Reset Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
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
            // Fully supports MULTI-SELECTION automatically
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

    // ====================================================================
    // 4. HELPER CLASS FOR UI DISPLAY
    // ====================================================================
    public class InstructorViewModel
    {
        public required Instructor Source { get; set; }
        
        public int Semester { get; set; }
        public List<int> AssignedSectionIds { get; set; } = new List<int>();

        public string Name => Source.FullName;
        
        public string Room => (Semester == 1 ? Source.AssignedRoomSem1?.Name : Source.AssignedRoomSem2?.Name) ?? "No Room";
        public string Program => string.IsNullOrWhiteSpace(Semester == 1 ? Source.ProgramSem1 : Source.ProgramSem2) ? "No Program" : (Semester == 1 ? Source.ProgramSem1 : Source.ProgramSem2);
        
        public string YearLevels 
        {
            get
            {
                string raw = Semester == 1 ? Source.PreferredYearLevelsSem1 : Source.PreferredYearLevelsSem2;
                return string.IsNullOrWhiteSpace(raw) ? "No Years" : $"Years: {raw.Replace(",", ", ")}";
            }
        }

        public int MaxUnits => Semester == 1 ? Source.MaxUnitsSem1 : Source.MaxUnitsSem2;
        public int CurrentUnits { get; set; }
        
        public string UnitLoadDisplay { get; set; } = "Units: 0 / 0";
        public bool IsOverloaded { get; set; } = false;
        
        public string OverloadDisplay
        {
            get
            {
                int diff = CurrentUnits - MaxUnits;
                return diff > 0 ? $"Overload: +{diff}" : ""; 
            }
        }

        public string CoursesStr 
        {
            get
            {
                string raw = Semester == 1 ? Source.PreferredCourseCodesSem1 : Source.PreferredCourseCodesSem2;
                return string.IsNullOrWhiteSpace(raw) ? "No Courses Assigned" : raw.Replace(",", ", ");
            }
        }

        public string SectionsStr { get; set; } = "";

        public string FormattedTime
        {
            get
            {
                string raw = Semester == 1 ? Source.SchedulePreferencesSem1 : Source.SchedulePreferencesSem2;
                if (string.IsNullOrWhiteSpace(raw)) return "No Time Assigned";

                var formattedBlocks = new List<string>();
                var blocks = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var block in blocks)
                {
                    var parts = block.Split('|');
                    if (parts.Length == 2)
                    {
                        string days = parts[0].Replace("Mon", "Mo").Replace("Tue", "Tu").Replace("Wed", "We")
                                              .Replace("Thu", "Th").Replace("Fri", "Fr").Replace("Sat", "Sa").Replace("Sun", "Su");
                        
                        string time = parts[1].Replace(" AM", "AM").Replace(" PM", "PM");
                        formattedBlocks.Add($"{days} ({time})");
                    }
                }
                return string.Join("\n", formattedBlocks);
            }
        }
    }
}