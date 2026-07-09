using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using UniversityScheduler.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace UniversityScheduler
{
   public partial class MainWindow : Window
   {
      public System.Collections.ObjectModel.ObservableCollection<AlertItem> TbaAlerts { get; set; } = [];
      public System.Collections.ObjectModel.ObservableCollection<AlertItem> IncompleteAlerts { get; set; } = [];
      public System.Collections.ObjectModel.ObservableCollection<AlertItem> CrowdedAlerts { get; set; } = [];
      public System.Collections.ObjectModel.ObservableCollection<AlertItem> OverloadAlerts { get; set; } = [];
      public System.Collections.ObjectModel.ObservableCollection<AlertItem> UnderloadAlerts { get; set; } = [];
      public System.Collections.ObjectModel.ObservableCollection<AlertItem> ContinuousAlerts { get; set; } = [];
      public System.Collections.ObjectModel.ObservableCollection<AlertItem> GapAlerts { get; set; } = [];

      private async Task RefreshSystemHealthAsync()
      {
         // 1. Run Analysis on Background Thread
         var result = await Task.Run(() =>
         {
            using var db = new AppDbContext();
            var tba = new List<AlertItem>();
            var incomplete = new List<AlertItem>();
            var crowded = new List<AlertItem>();
            var overload = new List<AlertItem>();
            var underload = new List<AlertItem>();
            var continuous = new List<AlertItem>();
            var gaps = new List<AlertItem>();

            int unassignedCount = 0;

            var allSchedules = db.Schedules
                              .Include(s => s.Course)
                              .Include(s => s.Section)
                              .Include(s => s.Room)
                              .Where(s => s.Semester == _currentSemester)
                              .ToList();

            // --- A. UNASSIGNED (TBA) ---
            var tbaGroups = allSchedules
               .Where(s => s.InstructorId == null)
               .GroupBy(s => s.SectionId);

            foreach (var group in tbaGroups)
            {
               var section = group.First().Section;
               if (section == null) continue;
               int count = group.Count();
               unassignedCount += count;

               tba.Add(new AlertItem
               {
                  Icon = "",
                  Title = section.FullDisplayName,
                  Description = $"{count} TBA Instructors",
                  RelatedId = section.Id,
                  Type = "Section"
               });
            }

            // ---  INCOMPLETE (Missing Units) --- 
            var allSections = db.Sections.ToList();
            var allCurriculums = db.Curriculums
                              .Include(c => c.Course)
                              .Where(c => c.Semester == _currentSemester)
                              .ToList();

            foreach (var section in allSections)
            {
               int maxUnits = allCurriculums
                  .Where(c => c.Program == section.Program && c.YearLevel == section.YearLevel && c.Course != null)
                  .Sum(c => c.Course!.Units);

               if (maxUnits == 0) continue;

               int assignedUnits = allSchedules
                  .Where(s => s.SectionId == section.Id && s.Course != null)
                  .Select(s => s.CourseId)
                  .Distinct()
                  .Sum(id => allSchedules.First(s => s.CourseId == id).Course!.Units);

               if (assignedUnits < maxUnits)
               {
                  incomplete.Add(new AlertItem
                  {
                  Icon = "⚠️",
                  Title = section.FullDisplayName,
                  Description = $"Missing: {maxUnits - assignedUnits} Units",
                  RelatedId = section.Id,
                  Type = "Section"
                  });
               }
            }

            // ---  CROWDED ---
            var crowdedList = allSchedules
               .Where(s => s.RoomId != null && s.Section != null && s.Section.StudentCount > s.Room!.Capacity)
               .ToList();

            foreach (var item in crowdedList)
            {
               crowded.Add(new AlertItem
               {
                  Icon = "⚠️",
                  Title = "Room Overcrowded",
                  Description = $"{item.Room!.Name} ({item.Room.Capacity}) vs {item.Section!.StudentCount}",
                  RelatedId = item.RoomId ?? 0,
                  Type = "Room"
               });
            }

            // --- D. INSTRUCTORS (Overload/Underload/Straight/Gaps) ---
            var instructors = db.Instructors.ToList();
            var instructorScheds = allSchedules.Where(s => s.InstructorId != null).ToList();

            foreach (var inst in instructors)
            {
               var myScheds = instructorScheds.Where(s => s.InstructorId == inst.Id).ToList();
               if (myScheds.Count == 0) continue;

               int currentUnits = myScheds.Where(s => s.Course != null)
                  .Select(s => new { s.CourseId, s.SectionId, s.Course!.Units })
                  .Distinct().Sum(x => x.Units);

               // DYNAMIC SEMESTER CHECK
               int maxInstUnits = _currentSemester == 1 ? inst.MaxUnitsSem1 : inst.MaxUnitsSem2;

               if (currentUnits > maxInstUnits)
                  overload.Add(new AlertItem { Icon = "", Title = inst.FullName, Description = $" {currentUnits}/{maxInstUnits} Units", RelatedId = inst.Id, Type = "Instructor" });

               if (currentUnits > 0 && currentUnits < (maxInstUnits * 0.75))
                  underload.Add(new AlertItem { Icon = "", Title = inst.FullName, Description = $"{currentUnits}/{maxInstUnits} Units", RelatedId = inst.Id, Type = "Instructor" });

               var dayGroups = myScheds.GroupBy(s => s.Day);
               foreach (var dayGroup in dayGroups)
               {
                  var sorted = dayGroup.OrderBy(s => DateTime.Parse(s.StartTime)).ToList();
                  double currentStreakHours = 0;
                  DateTime? lastEndTime = null;

                  foreach (var s in sorted)
                  {
                     DateTime start = DateTime.Parse(s.StartTime);
                     DateTime end = DateTime.Parse(s.EndTime);
                     double duration = (end - start).TotalHours;

                     if (lastEndTime != null)
                     {
                           double gapHours = (start - lastEndTime.Value).TotalHours;

                           if (gapHours > 3)
                           {
                              gaps.Add(new AlertItem
                              {
                                 Icon = "",
                                 Title = inst.FullName,
                                 Description = $"{s.Day}: {gapHours:0.#}hr Gap",
                                 RelatedId = inst.Id,
                                 Type = "Instructor"
                              });
                           }

                           if (gapHours < 0.33) currentStreakHours += duration;
                           else currentStreakHours = duration;
                     }
                     else
                     {
                           currentStreakHours = duration;
                     }

                     if (currentStreakHours >= 3)
                     {
                           if (!continuous.Any(x => x.Title == inst.FullName && x.Description.StartsWith(s.Day)))
                           {
                              continuous.Add(new AlertItem
                              {
                                 Icon = "",
                                 Title = inst.FullName,
                                 Description = $"{s.Day}: {currentStreakHours:0.#}hrs Straight",
                                 RelatedId = inst.Id,
                                 Type = "Instructor"
                              });
                           }
                     }
                     lastEndTime = end;
                  }
               }
            }
            return (tba, incomplete, crowded, overload, underload, continuous, gaps, unassignedCount);
         });

         // 2. Update UI
         TbaAlerts.Clear(); foreach (var i in result.tba) TbaAlerts.Add(i);
         IncompleteAlerts.Clear(); foreach (var i in result.incomplete) IncompleteAlerts.Add(i);
         if (TabIncomplete != null) TabIncomplete.Header = $"{IncompleteAlerts.Count} Incomplete";

         CrowdedAlerts.Clear(); foreach (var i in result.crowded) CrowdedAlerts.Add(i);
         OverloadAlerts.Clear(); foreach (var i in result.overload) OverloadAlerts.Add(i);
         UnderloadAlerts.Clear(); foreach (var i in result.underload) UnderloadAlerts.Add(i);
         ContinuousAlerts.Clear(); foreach (var i in result.continuous) ContinuousAlerts.Add(i);
         GapAlerts.Clear();        foreach (var i in result.gaps)       GapAlerts.Add(i);

         if (TabMissing != null) TabMissing.Header = $"{result.unassignedCount} Unassigned";
         if (TabCrowded != null) TabCrowded.Header = $"{CrowdedAlerts.Count} Crowded";
         if (TabOverload != null) TabOverload.Header = $"{OverloadAlerts.Count} Overload";
         if (TabUnderload != null) TabUnderload.Header = $"{UnderloadAlerts.Count} Underload";
         if (TabContinuous != null) TabContinuous.Header = $"{ContinuousAlerts.Count} Straight";
         if (TabGaps != null)       TabGaps.Header = $"{GapAlerts.Count} Gaps";
      }        

      private void JumpToAlert_Click(object sender, RoutedEventArgs e)
      {
         if (sender is Button btn && btn.Tag is AlertItem alert)
         {
            switch (alert.Type)
            {
               case "Section":
                  HandleSectionJump(this, alert.RelatedId); 
                  break;
               case "Room":
                  HandleRoomJump(this, alert.RelatedId);
                  break;
               case "Instructor":
                  HandleInstructorJump(this, alert.RelatedId);
                  break;
            }
         }
      }
   }
}