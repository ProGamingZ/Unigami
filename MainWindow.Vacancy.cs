using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using UniversityScheduler.Data;
using System.Collections.Generic;
using System.Linq;
using System;

namespace UniversityScheduler
{
   public partial class MainWindow : Window
   {
      public List<string> VacancyTimeSlots { get; set; } = new List<string>();

      // Instructor Search
      public System.Collections.ObjectModel.ObservableCollection<VacancyItem> VacantInstructors { get; set; } = [];
      
      private void InstFilter_Changed(object sender, SelectionChangedEventArgs e)
      {
         CheckInstVacancyComplex();
      }

      private void JumpToInstVacancy_Click(object sender, RoutedEventArgs e)
      {
         if (sender is Button btn && btn.Tag is int instId)
         {
            if (instId == -1) return;
            HandleInstructorJump(this, instId);
         }
      }

      public System.Collections.ObjectModel.ObservableCollection<VacancyCriteriaItem> CriteriaList { get; set; } = [];
      
      private void AddInstCriteria_Click(object sender, RoutedEventArgs e)
      {
         var selectedDays = _dayCheckBoxes.Where(cb => cb.IsChecked == true).Select(cb => cb.Content.ToString()!).ToList();
         if (selectedDays.Count == 0) { MessageBox.Show("Select at least one day."); return; }

         string startStr = InstStartCombo?.SelectedItem as string ?? "7:00 AM";
         string endStr = InstEndCombo?.SelectedItem as string ?? "9:00 AM";
         if (!DateTime.TryParse(startStr, out DateTime dtStart) || !DateTime.TryParse(endStr, out DateTime dtEnd)) return;
         
         if (dtStart.TimeOfDay >= dtEnd.TimeOfDay) { MessageBox.Show("Invalid Time Range"); return; }

         var newItem = new VacancyCriteriaItem
         {
            Days = selectedDays,
            StartTime = dtStart.TimeOfDay,
            EndTime = dtEnd.TimeOfDay,
            Logic = "AND"
         };

         CriteriaList.Add(newItem);
         UpdateInstLogicVisibilities();
         CheckInstVacancyComplex(); 
      }

      private void RemoveInstCriteria_Click(object sender, RoutedEventArgs e)
      {
         if (sender is Button btn && btn.Tag is VacancyCriteriaItem item)
         {
            CriteriaList.Remove(item);
            UpdateInstLogicVisibilities();
            CheckInstVacancyComplex();
         }
      }

      private void ToggleInstLogic_Click(object sender, RoutedEventArgs e)
      {
         if (sender is System.Windows.Controls.Primitives.ToggleButton btn && btn.Tag is VacancyCriteriaItem item)
         {
            item.Logic = (btn.IsChecked == true) ? "OR" : "AND";
            CheckInstVacancyComplex();
         }
      }

      private void UpdateInstLogicVisibilities()
      {
         for (int i = 0; i < CriteriaList.Count; i++)
         {
            CriteriaList[i].LogicVisibility = (i == CriteriaList.Count - 1) ? Visibility.Collapsed : Visibility.Visible;
         }
         var temp = CriteriaList.ToList(); CriteriaList.Clear(); foreach(var i in temp) CriteriaList.Add(i);
      }

      private void CheckInstVacancyComplex()
      {
         VacantInstructors.Clear();
         
         if (CriteriaList.Count == 0) return; 

         string programFilter = (VacancyInstProgramCombo?.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "All";

         using var db = new UniversityScheduler.Data.AppDbContext();
         
         // 1. Get Base Instructors (DYNAMIC SEMESTER FILTER)
         var query = db.Instructors.AsEnumerable()
            .Where(i => (_currentSemester == 1 ? i.StatusSem1 : i.StatusSem2) != "Inactive");
            
         if (programFilter != "All") 
            query = query.Where(i => ((_currentSemester == 1 ? i.ProgramSem1 : i.ProgramSem2) ?? "").Contains(programFilter));
            
         var candidates = query.ToList();

         var schedules = db.Schedules.Where(s => s.Semester == _currentSemester && s.InstructorId != null).ToList();
         var finalMatches = new List<Instructor>();

         foreach (var inst in candidates)
         {
               List<bool> criteriaResults = new List<bool>();
               foreach (var crit in CriteriaList)
               {
                  bool isFree = true;
                  foreach (var day in crit.Days)
                  {
                     bool hasConflict = schedules.Any(s =>
                        s.InstructorId == inst.Id &&
                        s.Day == day &&
                        ParseTime(s.StartTime) < crit.EndTime &&
                        ParseTime(s.EndTime) > crit.StartTime
                     );
                     if (hasConflict) { isFree = false; break; }
                  }
                  criteriaResults.Add(isFree);
               }

               bool combinedResult = criteriaResults[0]; 

               for (int i = 0; i < criteriaResults.Count - 1; i++)
               {
                  string op = CriteriaList[i].Logic; 
                  bool nextVal = criteriaResults[i + 1];

                  if (op == "AND") combinedResult = combinedResult && nextVal;
                  else if (op == "OR") combinedResult = combinedResult || nextVal;
               }

               if (combinedResult) finalMatches.Add(inst);
         }

         if (finalMatches.Count == 0)
               VacantInstructors.Add(new VacancyItem { Header = "No matches found", InstructorId = -1 });
         else
         {
               var allScheds = db.Schedules.Include(s => s.Course).Where(s => s.Semester == _currentSemester).ToList();
               foreach (var inst in finalMatches)
               {
                  int units = allScheds.Where(s => s.InstructorId == inst.Id && s.Course != null)
                                    .Select(s => new { s.CourseId, s.SectionId, s.Course!.Units }).Distinct().Sum(x => x.Units);

                  string prog = _currentSemester == 1 ? inst.ProgramSem1 : inst.ProgramSem2;
                  int maxUnits = _currentSemester == 1 ? inst.MaxUnitsSem1 : inst.MaxUnitsSem2;

                  VacantInstructors.Add(new VacancyItem
                  {
                     Header = inst.FullName,
                     SubHeader = $"{prog ?? "GenEd"}   {units}/{maxUnits}",
                     InstructorId = inst.Id
                  });
               }
         }
      }

      private void InstDayCb_Loaded(object sender, RoutedEventArgs e)
      {
         if (sender is CheckBox cb)
         {
            if (!_dayCheckBoxes.Contains(cb)) _dayCheckBoxes.Add(cb);
         }
      }

      // Room Search
      public System.Collections.ObjectModel.ObservableCollection<RoomVacancyItem> VacantRooms { get; set; } = [];
      
      private void RoomFilter_Changed(object sender, SelectionChangedEventArgs e)
      {
         if (this.IsLoaded) CheckRoomVacancyComplex();
      }

      private void JumpToRoomVacancy_Click(object sender, RoutedEventArgs e)
      {
         if (sender is Button btn && btn.Tag is int roomId)
         {
            if (roomId == -1) return;
            HandleRoomJump(this, roomId);
         }
      }

      public System.Collections.ObjectModel.ObservableCollection<VacancyCriteriaItem> RoomCriteriaList { get; set; } = [];
      
      private void AddRoomCriteria_Click(object sender, RoutedEventArgs e)
      {
         var selectedDays = _roomDayCheckBoxes.Where(cb => cb.IsChecked == true).Select(cb => cb.Content.ToString()!).ToList();
         if (selectedDays.Count == 0) { MessageBox.Show("Select at least one day."); return; }

         string startStr = RoomStartCombo?.SelectedItem as string ?? "7:00 AM";
         string endStr = RoomEndCombo?.SelectedItem as string ?? "9:00 AM";
         if (!DateTime.TryParse(startStr, out DateTime dtStart) || !DateTime.TryParse(endStr, out DateTime dtEnd)) return;
         
         if (dtStart.TimeOfDay >= dtEnd.TimeOfDay) { MessageBox.Show("Invalid Time Range"); return; }

         var newItem = new VacancyCriteriaItem
         {
            Days = selectedDays,
            StartTime = dtStart.TimeOfDay,
            EndTime = dtEnd.TimeOfDay,
            Logic = "AND"
         };

         RoomCriteriaList.Add(newItem);
         UpdateRoomLogicVisibilities();
         CheckRoomVacancyComplex();
      }

      private void RemoveRoomCriteria_Click(object sender, RoutedEventArgs e)
      {
         if (sender is Button btn && btn.Tag is VacancyCriteriaItem item)
         {
            RoomCriteriaList.Remove(item);
            UpdateRoomLogicVisibilities();
            CheckRoomVacancyComplex();
         }
      }

      private void ToggleRoomLogic_Click(object sender, RoutedEventArgs e)
      {
         if (sender is System.Windows.Controls.Primitives.ToggleButton btn && btn.Tag is VacancyCriteriaItem item)
         {
            item.Logic = (btn.IsChecked == true) ? "OR" : "AND";
            CheckRoomVacancyComplex();
         }
      }

      private void UpdateRoomLogicVisibilities()
      {
         for (int i = 0; i < RoomCriteriaList.Count; i++)
         {
            RoomCriteriaList[i].LogicVisibility = (i == RoomCriteriaList.Count - 1) ? Visibility.Collapsed : Visibility.Visible;
         }
         var temp = RoomCriteriaList.ToList(); RoomCriteriaList.Clear(); foreach(var i in temp) RoomCriteriaList.Add(i);
      }

      private void CheckRoomVacancyComplex()
      {
         VacantRooms.Clear();
         
         if (RoomCriteriaList.Count == 0) return;

         string floorFilter = (VacancyRoomFloorCombo?.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "All";

         using var db = new UniversityScheduler.Data.AppDbContext();
         var query = db.Rooms.AsQueryable();
         if (floorFilter != "All")
         {
               if (int.TryParse(floorFilter, out int floor)) query = query.Where(r => r.FloorLevel == floor);
         }
         var candidates = query.OrderBy(r => r.Name).ToList();

         var schedules = db.Schedules.Where(s => s.Semester == _currentSemester && s.RoomId != null).ToList();
         var finalMatches = new List<Room>();

         foreach (var room in candidates)
         {
               List<bool> criteriaResults = new List<bool>();
               foreach (var crit in RoomCriteriaList)
               {
                  bool isFree = true;
                  foreach (var day in crit.Days)
                  {
                     bool hasConflict = schedules.Any(s =>
                        s.RoomId == room.Id &&
                        s.Day == day &&
                        ParseTime(s.StartTime) < crit.EndTime &&
                        ParseTime(s.EndTime) > crit.StartTime
                     );
                     if (hasConflict) { isFree = false; break; }
                  }
                  criteriaResults.Add(isFree);
               }

               bool combinedResult = criteriaResults[0];
               for (int i = 0; i < criteriaResults.Count - 1; i++)
               {
                  string op = RoomCriteriaList[i].Logic;
                  bool nextVal = criteriaResults[i + 1];
                  if (op == "AND") combinedResult = combinedResult && nextVal;
                  else if (op == "OR") combinedResult = combinedResult || nextVal;
               }

               if (combinedResult) finalMatches.Add(room);
         }

         if (finalMatches.Count == 0)
               VacantRooms.Add(new RoomVacancyItem { Header = "No rooms free", RoomId = -1 });
         else
         {
               foreach (var r in finalMatches)
               {
                  VacantRooms.Add(new RoomVacancyItem
                  {
                     Header = r.Name,
                     SubHeader = $"{r.Type} | Cap: {r.Capacity}",
                     RoomId = r.Id
                  });
               }
         }
      }

      private void RoomDayCb_Loaded(object sender, RoutedEventArgs e)
      {
         if (sender is CheckBox cb)
         {
            if (!_roomDayCheckBoxes.Contains(cb)) _roomDayCheckBoxes.Add(cb);
         }
      }
   }
}