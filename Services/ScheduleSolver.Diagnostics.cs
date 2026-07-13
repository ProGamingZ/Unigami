using System;
using System.Collections.Generic;
using System.Linq;
using UniversityScheduler.Data;

namespace UniversityScheduler.Services
{
   public partial class ScheduleSolver
   {
      // 1. private void RunDiagnostics(...) { ... } (Lines 811 to the very end of the file)
      private void RunDiagnostics(List<SchedulingTask> tasks, List<Instructor> instructors, int semester, ModelData data, Action<string> log)
      {
         var settings = SchedulerSettings.Load();
         log("\n[--- RUNNING MATH DIAGNOSTICS ---]");
         int criticalErrors = 0;

         // 1. Check for mathematically trapped Instructors
         foreach (var instr in instructors)
         {
            var assignedTasks = tasks.Where(t => IsTaskAssignedToInstructor(instr, t, semester)).ToList();
            if (assignedTasks.Count == 0) continue;

            int requiredBlocks = assignedTasks.Sum(t => t.Duration30MinBlocks);
            var assignedTaskIds = assignedTasks.Select(t => t.TaskId).ToList();
            
            var availableSlots = data.Assignments
               .Where(a => assignedTaskIds.Contains(a.Task.TaskId))
               .SelectMany(a => Enumerable.Range(0, a.Task.Duration30MinBlocks).Select(offset => (a.Day, a.StartSlot + offset)))
               .Distinct()
               .Count();

            // TRAP A: Total Capacity (Infeasible)
            if (requiredBlocks > availableSlots)
            {
               log($"[CRITICAL] {instr.FullName}: Needs {requiredBlocks} blocks, but availability only allows {availableSlots}.");
               criticalErrors++;
            }
            // TRAP B: The "Tetris Trap" (Infeasible)
            // If they have less than 2 hours (4 blocks) of breathing room...
            else if ((availableSlots - requiredBlocks) <= 4)
            {
               bool hasLabs = assignedTasks.Any(t => t.Component.Contains("Lab") || t.Component.Contains("Practicum"));
               bool hasSplits = assignedTasks.Any(t => t.RelatedTaskId != null);
               
               if (hasLabs && hasSplits && settings.SiblingPattern == "Strict")
               {
                  decimal availHours = availableSlots / 2.0m;
                  decimal reqHours = requiredBlocks / 2.0m;
                  decimal bufferHours = availHours - reqHours;

                  log($"[TIME GAP] {instr.FullName}: Tight fit! ({availHours}h avail / {reqHours}h req = {bufferHours}h buffer). '{settings.SiblingPattern}' day spacing creates awkward time gaps that longer classes cannot fit into. Fix: Set Day Spacing to 'None' or expand availability.");
                  // We don't increment criticalErrors here, because the AI *might* miraculously find a fit, but we warn the user!
               }
            }

            // TRAP C: The Max Units Trap (Results in TBAs)
            int maxUnits = semester == 1 ? instr.MaxUnitsSem1 : instr.MaxUnitsSem2;
            
            if (maxUnits <= 0)
            {
               log($"[OVERLOAD] {instr.FullName}: Max Units is 0! All assigned classes will drop to TBA.");
            }
            else
            {
               // Rough estimate: 1 Unit is usually roughly 1 hour (2 blocks) of teaching time.
               // If they are assigned 40 blocks (20 hours), but their max units is only 15, warn the user.
               int estimatedAssignedUnits = requiredBlocks / 2; 
               if (estimatedAssignedUnits > maxUnits)
               {
                  log($"[OVERLOAD] {instr.FullName}: Assigned ~{estimatedAssignedUnits} units, but max is {maxUnits}. Excess classes will drop to TBA.");
               }
            }
         }

         // 2. Check for mathematically trapped Rooms (Global Capacity Check)
         int totalLabBlocksRequired = tasks.Where(t => t.Component == "Lab" || t.Component == "Practicum").Sum(t => t.Duration30MinBlocks);
         int totalLecBlocksRequired = tasks.Where(t => t.Component != "Lab" && t.Component != "Practicum").Sum(t => t.Duration30MinBlocks);

         int totalLabRoomCapacity = 0;
         int totalLecRoomCapacity = 0;

         var roomGroups = data.Assignments.GroupBy(a => a.Room);
         foreach (var rg in roomGroups)
         {
            int roomCapacity = rg.SelectMany(a => Enumerable.Range(0, a.Task.Duration30MinBlocks).Select(offset => (a.Day, a.StartSlot + offset))).Distinct().Count();

            if (rg.Key.Type.Contains("Laboratory") || rg.Key.Type == "Lab") 
               totalLabRoomCapacity += roomCapacity;
            else 
               totalLecRoomCapacity += roomCapacity;
         }

         if (totalLabBlocksRequired > totalLabRoomCapacity)
         {
            log($"[CRITICAL] Labs: Campus needs {totalLabBlocksRequired} blocks, but rooms only have {totalLabRoomCapacity} available.");
            criticalErrors++;
         }
         if (totalLecBlocksRequired > totalLecRoomCapacity)
         {
            log($"[CRITICAL] Lectures: Campus needs {totalLecBlocksRequired} blocks, but rooms only have {totalLecRoomCapacity} available.");
            criticalErrors++;
         }

         // 3. Check for "Day Spacing" (Sibling) Traps
         if (settings.EnableBlockSplitting && (settings.SiblingPattern == "Strict" || settings.SiblingPattern == "Relaxed"))
         {
            foreach (var instr in instructors)
            {
               var assignedTasks = tasks.Where(t => IsTaskAssignedToInstructor(instr, t, semester)).ToList();
               var splitTasks = assignedTasks.Where(t => t.RelatedTaskId != null).ToList();

               if (splitTasks.Count > 0)
               {
                  var assignedTaskIds = assignedTasks.Select(t => t.TaskId).ToList();
                  
                  // Look at the actual days the AI is allowed to schedule this instructor
                  var availableDays = data.Assignments
                     .Where(a => assignedTaskIds.Contains(a.Task.TaskId))
                     .Select(a => a.Day)
                     .Distinct()
                     .ToList();

                  bool hasValidPair = false;
                  if (settings.SiblingPattern == "Strict")
                  {
                     if ((availableDays.Contains(0) && availableDays.Contains(3)) || // Mon/Thu
                        (availableDays.Contains(1) && availableDays.Contains(4)) || // Tue/Fri
                        (availableDays.Contains(2) && availableDays.Contains(5)))   // Wed/Sat
                     {
                        hasValidPair = true;
                     }
                  }
                  else if (settings.SiblingPattern == "Relaxed")
                  {
                     foreach (int d1 in availableDays)
                     {
                        foreach (int d2 in availableDays)
                        {
                           if (Math.Abs(d1 - d2) >= 2) hasValidPair = true;
                        }
                     }
                  }

                  if (!hasValidPair)
                  {
                     string dayNames = availableDays.Count > 0 ? string.Join(", ", availableDays.Select(d => GetDayString(d))) : "None";
                     log($"[DAY SPACING TRAP] {instr.FullName} has a split class, but their available days ({dayNames}) cannot support '{settings.SiblingPattern}' spacing! Change to 'None' mode or give them matching days.");
                     criticalErrors++;
                  }
               }
            }
         }

         // 4. UNIFIED SIBLING GAP TRAP (Task-Level)
         if (settings.EnableBlockSplitting)
         {
            var taskGroups = tasks
               .GroupBy(t => new { t.SectionId, t.CourseId, t.Component })
               .Where(g => g.Count() == 2)
               .ToList();

            foreach (var group in taskGroups)
            {
               var sorted = group.OrderBy(t => t.SessionNumber).ToList();
               var t1 = sorted[0]; 
               var t2 = sorted[1];
               
               // Get all unique days where the AI successfully created variables for this specific split class
               var availableDays = data.Assignments
                  .Where(a => a.Task.TaskId == t1.TaskId || a.Task.TaskId == t2.TaskId)
                  .Select(a => a.Day)
                  .Distinct()
                  .OrderBy(d => d)
                  .ToList();

               bool hasValidPair = false;

               if (settings.SiblingPattern == "Strict")
               {
                  // Must have pairs with an exact 3-day gap: (0,3), (1,4), or (2,5)
                  if ((availableDays.Contains(0) && availableDays.Contains(3)) ||
                     (availableDays.Contains(1) && availableDays.Contains(4)) ||
                     (availableDays.Contains(2) && availableDays.Contains(5)))
                  {
                     hasValidPair = true;
                  }
               }
               else if (settings.SiblingPattern == "Relaxed")
               {
                  // Must have at least one pair where Day 2 is >= Day 1 + 2
                  for (int i = 0; i < availableDays.Count; i++)
                  {
                     for (int j = i + 1; j < availableDays.Count; j++)
                     {
                        if (availableDays[j] >= availableDays[i] + 2)
                        {
                           hasValidPair = true;
                           break;
                        }
                     }
                     if (hasValidPair) break;
                  }
               }
               else // SiblingPattern == "None"
               {
                  // Any available slots are technically valid in "None" mode.
                  // We just need to make sure the AI found at least *some* slots!
                  if (availableDays.Count > 0) hasValidPair = true;
               }

               if (!hasValidPair)
               {
                  string dayNames = availableDays.Count > 0 ? string.Join(", ", availableDays.Select(d => GetDayString(d))) : "None";
                  log($"[GAP TRAP CRITICAL] Task {t1.CourseCode} ({t1.Component}) for Section {t1.SectionId} cannot be scheduled! The solver only found valid slots on days: ({dayNames}). This fails your '{settings.SiblingPattern}' spacing rule. Check instructor availability, room availability, or lunch overlaps.");
                  criticalErrors++;
               }
            }
         }


         if (criticalErrors == 0) 
            log("Diagnostics Passed: No basic capacity traps found. (If it still fails, check Day Spacing).");
         else 
            log($"Diagnostics Failed: Found {criticalErrors} impossible rules. Fix the instructor/room limits in the UI.");
         log("[--------------------------------]\n");
      }


   }
}