using System;
using System.Collections.Generic;
using System.Linq;
using UniversityScheduler.Data;
using Google.OrTools.Sat;

namespace UniversityScheduler.Services
{
   public partial class ScheduleSolver
   {
      private List<Room> GetValidRooms(SchedulingTask task, List<Room> rooms, Dictionary<int, List<(string Program, int Year)>> restrictions, bool isLab, List<Instructor> assignedInstructors, int semester)
      {
         // 1. MAGNET LOGIC: If this is a LECTURE, force it into the assigned instructor's Homeroom
         if (assignedInstructors.Count > 0)
         {
            var homeroomInstr = assignedInstructors.FirstOrDefault(i => (semester == 1 ? i.AssignedRoomIdSem1 : i.AssignedRoomIdSem2) != null);
            
            if (homeroomInstr != null)
            {
               int homeroomId = (semester == 1 ? homeroomInstr.AssignedRoomIdSem1 : homeroomInstr.AssignedRoomIdSem2).Value;
               
               // Find the room, ensure it's big enough
               var homeroom = rooms.FirstOrDefault(r => r.Id == homeroomId && r.Capacity >= task.StudentCount);
               
               if (homeroom != null)
               {
                  bool roomIsLab = homeroom.Type.Contains("Lab") || homeroom.Type.Contains("Laboratory");
                  
                  // Match the class type to the room type!
                  // If it's a Lab class, the homeroom MUST be a Lab.
                  // If it's a Lec class, the homeroom MUST NOT be a Lab.
                  if ((isLab && roomIsLab) || (!isLab && !roomIsLab))
                  {
                     return new List<Room> { homeroom };
                  }
               }
            }
         }

         // 2. STANDARD LOGIC: (For Labs, TBA instructors, or if the homeroom is too small)
         var filtered = rooms.Where(r => 
         {
            if (r.Capacity < task.StudentCount) return false;
            if (restrictions.ContainsKey(r.Id))
            {
               var allowed = restrictions[r.Id];
               if (!allowed.Any(rule => rule.Program == task.Program && rule.Year == task.YearLevel)) return false;
            }
            return true;
         });

         if (isLab) return filtered.Where(r => r.Type.Contains("Laboratory") || r.Type.Contains("Lab")).ToList();
         return filtered.Where(r => !r.Type.Contains("Laboratory") && !r.Type.Contains("Lab")).ToList();
      }
      private List<int> GetAllowedDays(SchedulingTask task, SchedulerSettings settings)
      {
         var dayRule = settings.DayRules.FirstOrDefault(r => task.CourseCode.StartsWith(r.CoursePrefix, StringComparison.OrdinalIgnoreCase));
         if (dayRule != null && dayRule.AllowedDays.Count > 0)
         {
            return dayRule.AllowedDays.Select(d => GetDayIndex(d)).ToList();
         }
         return new List<int> { 0, 1, 2, 3, 4, 5 }; // Default Mon-Sat
      }
      private int GetMaxDailySlot(SchedulingTask task, SchedulerSettings settings, int defaultMax)
      {
         var timeRule = settings.TimeRules.FirstOrDefault(r => task.CourseCode.StartsWith(r.CoursePrefix, StringComparison.OrdinalIgnoreCase));
         if (timeRule != null && timeRule.LatestEndHour > settings.DayStartHour)
         {
            return (timeRule.LatestEndHour - settings.DayStartHour) * 2;
         }
         return defaultMax;
      }

      private List<ClassSchedule> GetGroupSchedules(Dictionary<string, List<ClassSchedule>> groupMap, int sectionId, int courseId)
      {
         string key = $"{sectionId}_{courseId}";
         return groupMap.ContainsKey(key) ? groupMap[key] : new List<ClassSchedule>();
      }
      // Helper to check overlap between two lists of schedules
      private bool DoGroupsOverlap(List<ClassSchedule> groupA, List<ClassSchedule> groupB)
      {
         foreach (var a in groupA)
         {
            foreach (var b in groupB)
            {
               if (a.Day == b.Day && IsTimeOverlapping(a, b)) return true;
            }
         }
         return false;
      }
      // helper for qualification check
      private bool IsInstructorQualified(Instructor instr, ClassSchedule schedule)
      {
         string assignedSections = (schedule.Semester == 1 ? instr.AssignedSectionsSem1 : instr.AssignedSectionsSem2) ?? "";
         string preferredCourses = (schedule.Semester == 1 ? instr.PreferredCourseCodesSem1 : instr.PreferredCourseCodesSem2) ?? "";

         if (string.IsNullOrWhiteSpace(assignedSections) || string.IsNullOrWhiteSpace(preferredCourses) || schedule.Section == null || schedule.Course == null) 
            return false;

         var sectionIds = assignedSections.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
         if (!sectionIds.Contains(schedule.Section.Id.ToString())) return false;

         var courseCodes = preferredCourses.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToUpper());
         
         // Format the schedule to match the saved "IT303-LEC" format
         string comp = (schedule.Component != null && schedule.Component.Contains("Lab")) ? "LAB" : "LEC";
         string targetCode = $"{schedule.Course.Code.Trim().ToUpper()}-{comp}";
         
         if (!courseCodes.Contains(targetCode)) return false;

         return true;
      }

      private void AddToMap<K>(Dictionary<K, List<BoolVar>> map, K key, BoolVar var) where K : notnull
      {
         if (!map.ContainsKey(key)) map[key] = new List<BoolVar>();
         map[key].Add(var);
      }
      private string GetDayString(int d)
      {
         string[] days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
         return (d >= 0 && d < days.Length) ? days[d] : "Mon";
      }
      private int GetDayIndex(string day)
      {
         // Simple switch for standard abbreviations
         switch (day)
         {
            case "Mon": return 0;
            case "Tue": return 1;
            case "Wed": return 2;
            case "Thu": return 3;
            case "Fri": return 4;
            case "Sat": return 5;
            case "Sun": return 6;
            default: return 0;
         }
      }

      private bool FitsTimePreference(Instructor instr, ClassSchedule cls)
      {
         // 0. Parse Class Times (Strict 24h from Scheduler)
         if (!TimeSpan.TryParse(cls.StartTime, out var classStart) || 
            !TimeSpan.TryParse(cls.EndTime, out var classEnd)) return false;

         string prefs = cls.Semester == 1 ? instr.SchedulePreferencesSem1 : instr.SchedulePreferencesSem2;

         // 1. Handle "Any" or Empty
         if (string.IsNullOrWhiteSpace(prefs)) return true;

         var blocks = prefs.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

         foreach (var block in blocks)
         {
            var parts = block.Split('|');
            if (parts.Length != 2) continue;

            var days = parts[0].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
            if (!days.Contains("Any") && !days.Contains(cls.Day)) continue;

            var timeRange = parts[1].Split('-');
            if (timeRange.Length != 2) continue;

            // Uses the new global parser to prevent AM/PM bugs
            if (ParseTimeHelper(timeRange[0], out TimeSpan prefStart) && ParseTimeHelper(timeRange[1], out TimeSpan prefEnd))
            {
               if (classStart >= prefStart && classEnd <= prefEnd) return true;
            }
         }
         return false;
      }
      private bool IsTimeOverlapping(ClassSchedule a, ClassSchedule b)
      {
         if (!TimeSpan.TryParse(a.StartTime, out var s1) || !TimeSpan.TryParse(a.EndTime, out var e1)) return false;
         if (!TimeSpan.TryParse(b.StartTime, out var s2) || !TimeSpan.TryParse(b.EndTime, out var e2)) return false;
         return s1 < e2 && s2 < e1;
      }    
      private bool ParseTimeHelper(string raw, out TimeSpan result)
      {
         raw = raw.Trim();
         if (TimeSpan.TryParse(raw, out result)) return true;
         if (DateTime.TryParse(raw, out DateTime dt))
         {
            result = dt.TimeOfDay;
            return true;
         }
         return false;
      }

      private bool IsTaskAssignedToInstructor(Instructor instr, SchedulingTask task, int semester)
      {
         string assignedSections = (semester == 1 ? instr.AssignedSectionsSem1 : instr.AssignedSectionsSem2) ?? "";
         string preferredCourses = (semester == 1 ? instr.PreferredCourseCodesSem1 : instr.PreferredCourseCodesSem2) ?? "";

         if (string.IsNullOrWhiteSpace(assignedSections) || string.IsNullOrWhiteSpace(preferredCourses)) return false;

         var sectionIds = assignedSections.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
         if (!sectionIds.Contains(task.SectionId.ToString())) return false;

         var courseCodes = preferredCourses.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToUpper());
         
         // Format the task to match the saved "IT303-LEC" format
         string comp = task.Component.Contains("Lab") ? "LAB" : "LEC";
         string targetCode = $"{task.CourseCode.Trim().ToUpper()}-{comp}";
         
         if (!courseCodes.Contains(targetCode)) return false;

         return true;
      }

      private bool FitsInstructorTimeBlocks(Instructor instr, SchedulerSettings settings, int semester, int day, int startSlot, int duration)
      {
         string prefs = semester == 1 ? instr.SchedulePreferencesSem1 : instr.SchedulePreferencesSem2;
         if (string.IsNullOrWhiteSpace(prefs)) return true; // Empty means Any Time

         var blocks = prefs.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
         string dayStr = GetDayString(day);

         // Convert Slot Indexes to Actual TimeSpans
         TimeSpan slotStart = new TimeSpan(settings.DayStartHour, 0, 0).Add(TimeSpan.FromMinutes(startSlot * SlotDurationMinutes));
         TimeSpan slotEnd = slotStart.Add(TimeSpan.FromMinutes(duration * SlotDurationMinutes));

         foreach (var block in blocks)
         {
            var parts = block.Split('|');
            if (parts.Length != 2) continue;

            var prefDays = parts[0].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
            if (!prefDays.Contains("Any") && !prefDays.Contains(dayStr)) continue;

            var timeRange = parts[1].Split('-');
            if (timeRange.Length != 2) continue;

            if (ParseTimeHelper(timeRange[0], out TimeSpan prefStart) && ParseTimeHelper(timeRange[1], out TimeSpan prefEnd))
            {
               if (slotStart >= prefStart && slotEnd <= prefEnd) return true;
            }
         }
         return false;
      }


   }
}