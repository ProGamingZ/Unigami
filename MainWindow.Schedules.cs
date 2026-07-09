using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using UniversityScheduler.Data;
using UniversityScheduler.Views;
using System.Collections.Generic;
using System.Linq;
using System;

namespace UniversityScheduler
{
   public partial class MainWindow : Window
   {
      private void RefreshSchedule()
      {
         if (!IsLoaded) return;

         if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
               UpdateInstructorSchedule(selectedInstructor);
         else
               ClearInstructorSchedule();

         if (ClassSelector.SelectedItem != null)
         {
               dynamic item = ClassSelector.SelectedItem;
               UpdateClassSchedule(item.OriginalObject);
         }

         if (RoomSelector.SelectedItem is Room selectedRoom)
               UpdateRoomSchedule(selectedRoom);

         _ = RefreshSystemHealthAsync();

         if (CriteriaList.Count > 0) CheckInstVacancyComplex();
         if (RoomCriteriaList.Count > 0) CheckRoomVacancyComplex();
      }

      private void InstructorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
         {
            LockScheduleChk.IsChecked = selectedInstructor.IsScheduleLocked;
            UpdateInstructorSchedule(selectedInstructor);
         }        
      }

      private void UpdateInstructorSchedule(Instructor instructor)
      {
         using var db = new AppDbContext();
         int currentSemester = _currentSemester;

         var schedules = db.Schedules
            .Include(s => s.Course)
            .Include(s => s.Room)
            .Include(s => s.Section)
            .Include(s => s.Instructor)
            .Where(s => s.InstructorId == instructor.Id && s.Semester == currentSemester)
            .ToList();

         var days = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
         InstructorScheduleTable.RefreshTable(days, schedules);

         int totalUnits = schedules.Where(s => s.Course != null)
            .Select(s => new { s.CourseId, s.SectionId, s.Course!.Units })
            .Distinct().Sum(x => x.Units);

         // DYNAMIC SEMESTER CHECK
         int maxUnits = currentSemester == 1 ? instructor.MaxUnitsSem1 : instructor.MaxUnitsSem2;
         int overload = Math.Max(0, totalUnits - maxUnits);

         InstTotalUnitsTxt.Text = $"Units: {totalUnits} / {maxUnits}";
         InstOverloadTxt.Text = $"Overload: {overload}";

         if (totalUnits > maxUnits)
         {
            InstTotalUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            InstOverloadTxt.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            InstOverloadTxt.FontWeight = FontWeights.SemiBold;
         }
         else if (totalUnits == maxUnits)
         {
            InstTotalUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("SidebarBrush");
            InstOverloadTxt.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
            InstOverloadTxt.FontWeight = FontWeights.Normal;
         }
         else
         {
            InstTotalUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("InfoBrush");
            InstOverloadTxt.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
            InstOverloadTxt.FontWeight = FontWeights.Normal;
         }

         InstructorScheduleTable.Title = instructor.FullName;
      }

      private void ClearInstructorSchedule()
      {
         InstructorScheduleTable.RefreshTable(new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, new List<ClassSchedule>());
         InstructorScheduleTable.Title = "Instructor Schedules";
      }

      private void ScheduleTable_SlotClicked(object? sender, ScheduleSlotArgs e)
      {
         if (e.ExistingSchedule != null)
         {
            var editWin = new EditScheduleWindow(e.ExistingSchedule.Id);
            if (editWin.ShowDialog() == true) RefreshSchedule(); 
         }
         else
         {
            if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
            {
               var addWin = new EditScheduleWindow(
                  selectedInstructor.Id, 
                  e.Day, 
                  e.Time, 
                  _currentSemester
               );
               if (addWin.ShowDialog() == true) RefreshSchedule();
            }
            else
            {
               MessageBox.Show("Please select an instructor first.");
            }
         }
      }

      private void LockScheduleChk_Click(object sender, RoutedEventArgs e)
      {
         if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
         {
            using var db = new AppDbContext();
            var inst = db.Instructors.Find(selectedInstructor.Id);
            if (inst != null) { inst.IsScheduleLocked = LockScheduleChk.IsChecked == true; db.SaveChanges(); selectedInstructor.IsScheduleLocked = inst.IsScheduleLocked; }
         }
      }

      private void ClassSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         if (ClassSelector.SelectedItem != null)
         {
            dynamic selectedItem = ClassSelector.SelectedItem;
            UpdateClassSchedule(selectedItem.OriginalObject);
         }
      }

      private void UpdateClassSchedule(StudentSection section)
      {
         using var db = new AppDbContext();
         var schedules = db.Schedules
            .Include(s => s.Course)
            .Include(s => s.Room)
            .Include(s => s.Section)
            .Include(s => s.Instructor)
            .Where(s => s.SectionId == section.Id && s.Semester == _currentSemester)
            .ToList();

         var days = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
         ClassScheduleTable.RefreshTable(days, schedules);
         ClassScheduleTable.Title = $"{section.Program} {section.YearLevel}-{section.Name}";

         int assignedUnits = schedules
            .Where(s => s.Course != null)
            .Select(s => s.CourseId)
            .Distinct()
            .Select(id => schedules.First(s => s.CourseId == id).Course!.Units)
            .Sum();

         int maxUnits = db.Curriculums
            .Include(c => c.Course)
            .Where(c => c.Program == section.Program &&
                        c.YearLevel == section.YearLevel &&
                        c.Semester == _currentSemester)
            .Select(c => c.Course != null ? c.Course.Units : 0) 
            .Sum();

         ClassUnitsTxt.Text = $"Units: {assignedUnits} / {maxUnits}";

         if (assignedUnits > maxUnits)
         {
            ClassUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            ClassUnitsTxt.ToolTip = "Warning: Exceeds curriculum requirements";
         }
         else if (assignedUnits == maxUnits && maxUnits > 0)
         {
            ClassUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("SidebarBrush");
            ClassUnitsTxt.ToolTip = "Schedule Complete";
         }
         else
         {
            ClassUnitsTxt.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
            ClassUnitsTxt.ToolTip = "Missing subjects";
         }

         ClassStudentCountTxt.Text = $"Students: {section.StudentCount}";
      }

      private void ClearClassSchedule()
      {
         ClassScheduleTable.RefreshTable(new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, new List<ClassSchedule>());
         ClassScheduleTable.Title = "Class Schedules";
      }

      private void ClassScheduleTable_SlotClicked(object? sender, ScheduleSlotArgs e)
      {
         if (e.ExistingSchedule != null)
         {
            var editWin = new EditScheduleWindow(e.ExistingSchedule.Id);
            if (editWin.ShowDialog() == true) 
            {
               if (ClassSelector.SelectedItem != null)
               {
                  dynamic selectedItem = ClassSelector.SelectedItem;
                  UpdateClassSchedule(selectedItem.OriginalObject);
               }
               RefreshSchedule(); 
            }
         }
         else
         {
            if (ClassSelector.SelectedItem != null)
            {
               dynamic selectedItem = ClassSelector.SelectedItem;
               StudentSection section = selectedItem.OriginalObject;

               var addWin = new EditScheduleWindow(
                  section.Id,
                  e.Day,
                  e.Time,
                  _currentSemester,
                  true 
               );

               if (addWin.ShowDialog() == true)
               {
                  UpdateClassSchedule(section);
                  RefreshSchedule(); 
               }
            }
            else
            {
               MessageBox.Show("Please select a class (Section) first.");
            }
         }
      }

      private void ClassScheduleTable_InstructorJumpRequested(object sender, int instructorId)
      {
         var targetInstructor = _allInstructors.FirstOrDefault(i => i.Id == instructorId);
         if (targetInstructor != null) InstructorSelector.SelectedItem = targetInstructor;
         else MessageBox.Show("Could not find this instructor in the active list.");
      } 

      private void RoomSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         if (RoomSelector.SelectedItem is Room selectedRoom) UpdateRoomSchedule(selectedRoom);
      }

      private void UpdateRoomSchedule(Room room)
      {
         using var db = new AppDbContext();
         var schedules = db.Schedules
            .Include(s => s.Course)
            .Include(s => s.Room)
            .Include(s => s.Section)
            .Include(s => s.Instructor)
            .Where(s => s.RoomId == room.Id && s.Semester == _currentSemester)
            .ToList();

         var days = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
         RoomScheduleTable.RefreshTable(days, schedules);
         RoomScheduleTable.Title = room.Name;
         RoomDetailsTxt.Text = $"Type: {room.Type}  |  Cap: {room.Capacity}";

         double totalHours = schedules.Sum(s => (ParseTime(s.EndTime) - ParseTime(s.StartTime)).TotalHours);
         double maxHours = 98.0;
         int usagePercent = (int)((totalHours / maxHours) * 100);

         RoomStatusTxt.Text = $"Usage: {usagePercent}% ({schedules.Count} classes)";

         if (usagePercent > 80) RoomStatusTxt.Foreground = (System.Windows.Media.Brush)FindResource("SidebarBrush"); 
         else RoomStatusTxt.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
      }

      private void RoomScheduleTable_SlotClicked(object? sender, ScheduleSlotArgs e)
      {
         if (e.ExistingSchedule != null)
         {
            var editWin = new EditScheduleWindow(e.ExistingSchedule.Id);
            if (editWin.ShowDialog() == true) 
            {
               if (RoomSelector.SelectedItem is Room selectedRoom)
                  UpdateRoomSchedule(selectedRoom);
               RefreshSchedule(); 
            }
         }
         else
         {
            if (RoomSelector.SelectedItem is Room selectedRoom)
            {
               var addWin = new EditScheduleWindow(
                  e.Day, 
                  e.Time, 
                  _currentSemester, 
                  selectedRoom.Id 
               );

               if (addWin.ShowDialog() == true)
               {
                  UpdateRoomSchedule(selectedRoom);
                  RefreshSchedule();
               }
            }
            else MessageBox.Show("Please select a room first.");
         }
      }

      private void HandleInstructorJump(object sender, int instructorId)
      {
         if (InstProgramCombo.SelectedIndex != 0) InstProgramCombo.SelectedIndex = 0;
         var target = _allInstructors.FirstOrDefault(i => i.Id == instructorId);
         if (target != null) InstructorSelector.SelectedItem = target;
      }

      private void HandleRoomJump(object sender, int roomId)
      {
         if (RoomFloorCombo.SelectedIndex != 0) RoomFloorCombo.SelectedIndex = 0;
         var target = _allRooms.FirstOrDefault(r => r.Id == roomId);
         if (target != null) RoomSelector.SelectedItem = target;
      }

      private void HandleSectionJump(object sender, int sectionId)
      {
         if (ClassProgramCombo.SelectedIndex != 0) ClassProgramCombo.SelectedIndex = 0;
         dynamic? targetItem = null;
         
         foreach (dynamic item in ClassSelector.Items)
         {
            if (item.Id == sectionId)
            {
               targetItem = item;
               break;
            }
         }

         if (targetItem != null) ClassSelector.SelectedItem = targetItem;
         else MessageBox.Show("Could not find the target section in the current list.");
      }

      private static TimeSpan ParseTime(string t)
      {
         return DateTime.TryParse(t, out var dt) ? dt.TimeOfDay : TimeSpan.Zero;
      }
   }
}