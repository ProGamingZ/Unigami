using System;
using System.Linq;
using System.Windows;
using UniversityScheduler.Data;

namespace UniversityScheduler
{
   public partial class MainWindow : Window
   {
      private void ClearScheduleBtn_Click(object sender, RoutedEventArgs e)
      {
         if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
         {
            if (selectedInstructor.IsScheduleLocked)
            {
               MessageBox.Show("Locked schedule.", "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Stop);
               return;
            }
            if (MessageBox.Show($"Clear schedule for {selectedInstructor.FullName}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
               using (var db = new AppDbContext())
               {
                  var schedules = db.Schedules.Where(s => s.InstructorId == selectedInstructor.Id && s.Semester == _currentSemester).ToList();
                  foreach(var s in schedules) s.InstructorId = null; 
                  db.SaveChanges();
               }
               RefreshSchedule();
            }
         }
      }
      private void ClearClassBtn_Click(object sender, RoutedEventArgs e)
      {
         if (ClassSelector.SelectedItem == null)
         {
            MessageBox.Show("Please select a class first.");
            return;
         }

         dynamic selectedItem = ClassSelector.SelectedItem;
         StudentSection section = selectedItem.OriginalObject;

         var result = MessageBox.Show(
            $"Are you sure you want to remove ALL classes for {section.Program} {section.YearLevel}-{section.Name}?\n\nThis will delete the schedule entries.",
            "Confirm Clear",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

         if (result == MessageBoxResult.Yes)
         {
            using (var db = new AppDbContext())
            {
               var schedules = db.Schedules
                  .Where(s => s.SectionId == section.Id && s.Semester == _currentSemester)
                  .ToList();

               db.Schedules.RemoveRange(schedules);
               db.SaveChanges();
            }

            // Refresh UI
            UpdateClassSchedule(section);
            RefreshSchedule(); // Also refresh instructor view since their loads changed
            MessageBox.Show("Class schedule cleared.");
         }
      }
      private void ClearRoomBtn_Click(object sender, RoutedEventArgs e)
      {
         if (RoomSelector.SelectedItem is not Room selectedRoom) return;

         if (MessageBox.Show($"Clear all classes in {selectedRoom.Name}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
         {
            using (var db = new AppDbContext())
            {
               var schedules = db.Schedules.Where(s => s.RoomId == selectedRoom.Id && s.Semester == _currentSemester).ToList();
               // We set RoomId to null (Unassign Room) rather than deleting the class entirely
               foreach (var s in schedules) s.RoomId = null;
               db.SaveChanges();
            }
            UpdateRoomSchedule(selectedRoom);
            RefreshSchedule();
            MessageBox.Show("Room cleared. Classes are now unassigned (Room: TBA).");
         }
      }
   
   }
}