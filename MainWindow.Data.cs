using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;

namespace UniversityScheduler
{
   public partial class MainWindow : Window
   {
      public void RefreshDashboard()
      {
         LoadInstructorData();
         LoadClassData();
         LoadRoomData();
         InitializeVacancyTools(); 
      }

      private void LoadInstructorData()
      {
         using var db = new AppDbContext();
         _allInstructors = [.. db.Instructors.OrderBy(i => i.Surname).ThenBy(i => i.FirstName)];
         UpdateInstructorList("All");
      }
      private void UpdateInstructorList(string programFilter)
      {
         if (InstructorSelector == null) return;
         InstructorSelector.SelectionChanged -= InstructorSelector_SelectionChanged;
         try 
         {
            if (programFilter == "All") InstructorSelector.ItemsSource = _allInstructors;
            else
            {
               var filtered = _allInstructors
                  .Where(i => (i.Program ?? "").Contains(programFilter) || (i.Program ?? "").Contains("General Education"))
                  .ToList();
               InstructorSelector.ItemsSource = filtered;
            }

            if (InstructorSelector.Items.Count > 0) InstructorSelector.SelectedIndex = 0;
            else
            {
               InstructorSelector.SelectedIndex = -1;
               ClearInstructorSchedule();
            }
         }
         finally { InstructorSelector.SelectionChanged += InstructorSelector_SelectionChanged; }
         
         if (InstructorSelector.SelectedItem is Instructor i) UpdateInstructorSchedule(i);
      }
      private void InstProgramCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         if (InstructorSelector == null || !IsLoaded) return;
         if (InstProgramCombo.SelectedItem is ComboBoxItem item)
         {
            string selectedProgram = item.Tag?.ToString() ?? "All";
            UpdateInstructorList(selectedProgram);
         }
      }

      private void LoadClassData()
      {
         using var db = new AppDbContext();
         _allSections = db.Sections.OrderBy(s => s.Program).ThenBy(s => s.YearLevel).ThenBy(s => s.Name).ToList();
         UpdateClassList("All");
      }
      private void UpdateClassList(string programFilter)
      {
         if (ClassSelector == null) return;
         ClassSelector.SelectionChanged -= ClassSelector_SelectionChanged;

         try
         {
            IEnumerable<StudentSection> filtered;
            if (programFilter == "All") filtered = _allSections;
            else filtered = _allSections.Where(s => s.Program == programFilter);

            var displayList = filtered.Select(s => new 
            { 
               Id = s.Id, 
               DisplayName = $"{s.Program} {s.YearLevel}-{s.Name}", 
               OriginalObject = s 
            }).ToList();

            ClassSelector.ItemsSource = displayList;
            ClassSelector.SelectedValuePath = "Id";

            if (displayList.Count > 0) ClassSelector.SelectedIndex = 0;
            else
            {
               ClassSelector.SelectedIndex = -1;
               ClearClassSchedule();
            }
         }
         finally { ClassSelector.SelectionChanged += ClassSelector_SelectionChanged; }

         if (ClassSelector.SelectedItem != null) 
         {
            dynamic item = ClassSelector.SelectedItem;
            UpdateClassSchedule(item.OriginalObject);
         }
      }
      private void ClassProgramCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         if (ClassSelector == null || !IsLoaded) return;
         if (ClassProgramCombo.SelectedItem is ComboBoxItem item)
         {
            string selectedProgram = item.Tag?.ToString() ?? "All";
            UpdateClassList(selectedProgram);
         }
      }

      private void LoadRoomData()
      {
         using (var db = new AppDbContext())
         {
            _allRooms = db.Rooms.OrderBy(r => r.Name).ToList();
            var floors = _allRooms.Select(r => r.FloorLevel.ToString())
                                    .Distinct()
                                    .OrderBy(f => f)
                                    .ToList();

            RoomFloorCombo.Items.Clear();
            
            // Add "All" option
            var allItem = new ComboBoxItem { Content = "All", Tag = "All", IsSelected = true };
            RoomFloorCombo.Items.Add(allItem);

            // Add detected floors
            foreach (var f in floors)
            {
               RoomFloorCombo.Items.Add(new ComboBoxItem { Content = $"{f}F", Tag = f });
            }

            // 3. Show All Rooms initially
            UpdateRoomList("All");
         }
      }
      private void UpdateRoomList(string floorFilter)
      {
         // Detach event to prevent triggering "SelectionChanged" while swapping sources
         if (RoomSelector == null) return;
         RoomSelector.SelectionChanged -= RoomSelector_SelectionChanged;

         try
         {
            List<Room> filtered;
            
            if (floorFilter == "All") 
            {
               filtered = _allRooms;
            }
            else
            {
               // Filter: Convert FloorLevel to string to match the tag
               filtered = _allRooms
               .Where(r => r.FloorLevel.ToString() == floorFilter)
               .ToList();
            }

            RoomSelector.ItemsSource = filtered;

            // Auto-select first item if available
            if (filtered.Count > 0) 
               RoomSelector.SelectedIndex = 0;
            else
            {
               RoomSelector.SelectedIndex = -1;
               // Optional: Clear the schedule view if no rooms match
               RoomScheduleTable.RefreshTable(new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, new List<ClassSchedule>());
               RoomScheduleTable.Title = "Room Schedules";
               RoomStatusTxt.Text = "Classes: 0";
            }
         }
         finally 
         { 
            // Re-attach event
            RoomSelector.SelectionChanged += RoomSelector_SelectionChanged; 
         }

         // Trigger update for the newly selected room (if any)
         if (RoomSelector.SelectedItem is Room r) 
            UpdateRoomSchedule(r);
      }
      private void RoomFloorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         if (RoomSelector == null || !IsLoaded) return;

         if (RoomFloorCombo.SelectedItem is ComboBoxItem item)
         {
            string selectedFloor = item.Tag?.ToString() ?? "All";
            UpdateRoomList(selectedFloor);
         }
      }

   }
}