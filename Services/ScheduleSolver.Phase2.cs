using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Sat;
using UniversityScheduler.Data;

namespace UniversityScheduler.Services
{
   public partial class ScheduleSolver
   {
      public void AssignInstructors(List<ClassSchedule> schedules, List<Instructor> instructors, Action<string> log)
      {
         var settings = SchedulerSettings.Load();
         log($"Building Instructor Assignment Model (Max Time: {settings.MaxCalculationTimeSeconds}s)...");

         CpModel model = new CpModel();

         // STEP 1: Data Preparation
         // Group individual 30-min blocks into logical "Course Sections"
         var courseGroupsList = GroupSchedulesByCourse(schedules);
         // OPTIMIZATION: Create a Dictionary for fast O(1) lookups
         var courseGroupsMap = courseGroupsList.ToDictionary(g => g.Key, g => g.ToList()); 

         // Calculate how many units are already consumed by "Locked" schedules
         var lockedLoads = CalculateLockedLoads(schedules, instructors);

         // STEP 2: Create Variables (Who CAN teach What?)
         // Note: We iterate over the LIST here because we need to process every group once
         var data = CreateInstructorVariables(model, courseGroupsList, instructors, log);

         // STEP 3: Apply Constraints
         int semester = schedules.FirstOrDefault()?.Semester ?? 1;
         AddMaxLoadConstraints(model, data, instructors, lockedLoads, semester);
         
         // Note: We pass the MAP here for fast lookup inside the nested loops
         AddInstructorConflictConstraints(model, data, courseGroupsMap, instructors);

         // STEP 4: Objective & Solve
         model.Maximize(LinearExpr.Sum(data.Assignments.Values));
         RunInstructorSolver(model, data, courseGroupsMap, settings, log);
      }

      private List<IGrouping<string, ClassSchedule>> GroupSchedulesByCourse(List<ClassSchedule> schedules)
      {
         // Group by Section + Course to treat them as a single unit
         return schedules
            .GroupBy(s => $"{s.SectionId}_{s.CourseId}")
            .ToList();
      }
      private Dictionary<int, int> CalculateLockedLoads(List<ClassSchedule> schedules, List<Instructor> instructors)
      {
         var lockedLoads = new Dictionary<int, int>();
         
         // Find schedules that are ALREADY assigned to a locked instructor
         var lockedSchedules = schedules
            .Where(s => s.Instructor != null && s.Instructor.IsScheduleLocked)
            .ToList();

         foreach (var s in lockedSchedules)
         {
            int iId = s.Instructor!.Id;
            if (!lockedLoads.ContainsKey(iId)) lockedLoads[iId] = 0;
            
            // Only add units once per course (distinct check)
            // Note: Simple approximation; ideally done by grouping
            // For safety, we assume the input list has processed units correctly elsewhere or we count carefully
         }
         
         // Better approach: Group locked schedules to sum units correctly
         var lockedGroups = lockedSchedules
            .GroupBy(s => new { s.SectionId, s.CourseId, s.InstructorId })
            .Select(g => new { InstructorId = g.Key.InstructorId!.Value, Units = g.First().Course?.Units ?? 3 });

         foreach (var item in lockedGroups)
         {
            if (!lockedLoads.ContainsKey(item.InstructorId)) lockedLoads[item.InstructorId] = 0;
            lockedLoads[item.InstructorId] += item.Units;
         }

         return lockedLoads;
      }
      private InstructorModelData CreateInstructorVariables(CpModel model, List<IGrouping<string, ClassSchedule>> courseGroups, List<Instructor> instructors, Action<string> log)
      {
         var data = new InstructorModelData();
         int varCount = 0;

         foreach (var group in courseGroups)
         {
            var sectionSchedules = group.ToList();
            var first = sectionSchedules.First(); 
            
            // Skip if already locked
            if (first.Instructor != null && first.Instructor.IsScheduleLocked) continue;

            // Reset IDs for solver
            foreach(var s in sectionSchedules) s.InstructorId = null;

            // A. Filter Qualified Instructors
            var candidates = instructors.Where(i => 
               IsInstructorQualified(i, first) && 
               sectionSchedules.All(sched => FitsTimePreference(i, sched))
            ).ToList();

            if (!candidates.Any()) continue; // Leave as TBA

            // B. Create Variables
            var groupVars = new List<BoolVar>();

            foreach (var instr in candidates)
            {
               var isAssigned = model.NewBoolVar($"Sec{first.SectionId}_Crs{first.CourseId}_I{instr.Id}");
               
               // Map variable
               data.Assignments[(first.SectionId!.Value, first.CourseId, instr.Id)] = isAssigned;
               groupVars.Add(isAssigned);
               varCount++;

               // Track Load
               if (!data.InstructorLoads.ContainsKey(instr.Id))
                  data.InstructorLoads[instr.Id] = new List<(BoolVar, int)>();
               
               int units = first.Course?.Units ?? 3;
               data.InstructorLoads[instr.Id].Add((isAssigned, units));

            }

            // Constraint: Only 1 instructor per course
            model.AddAtMostOne(groupVars);
         }

         log($"Created {varCount} assignment variables.");
         return data;
      }

      private void AddMaxLoadConstraints(CpModel model, InstructorModelData data, List<Instructor> instructors, Dictionary<int, int> lockedLoads, int semester)
      {
         foreach (var instr in instructors)
         {
            if (data.InstructorLoads.ContainsKey(instr.Id))
            {
               var loadVars = data.InstructorLoads[instr.Id].Select(x => x.Var * x.Units).ToList();
               
               int used = lockedLoads.ContainsKey(instr.Id) ? lockedLoads[instr.Id] : 0;
               int maxUnits = semester == 1 ? instr.MaxUnitsSem1 : instr.MaxUnitsSem2;
               int remaining = Math.Max(0, maxUnits - used);

               model.Add(LinearExpr.Sum(loadVars) <= remaining);
            }
         }
      }
      private void AddInstructorConflictConstraints(CpModel model, InstructorModelData data, Dictionary<string, List<ClassSchedule>> courseGroupsMap, List<Instructor> instructors)
      {
         foreach (var instr in instructors)
         {
            var myPotentialAssignments = data.Assignments.Where(x => x.Key.InstructorId == instr.Id).ToList();

            for (int i = 0; i < myPotentialAssignments.Count; i++)
            {
               for (int j = i + 1; j < myPotentialAssignments.Count; j++)
               {
                     var assignA = myPotentialAssignments[i];
                     var assignB = myPotentialAssignments[j];

                     // USE THE NEW MAP LOOKUP HERE
                     var groupA = GetGroupSchedules(courseGroupsMap, assignA.Key.SectionId, assignA.Key.CourseId);
                     var groupB = GetGroupSchedules(courseGroupsMap, assignB.Key.SectionId, assignB.Key.CourseId);

                     if (DoGroupsOverlap(groupA, groupB))
                     {
                        model.Add(assignA.Value + assignB.Value <= 1);
                     }
               }
            }
         }
      }

      private void RunInstructorSolver(CpModel model, InstructorModelData data, Dictionary<string, List<ClassSchedule>> courseGroupsMap, SchedulerSettings settings, Action<string> log)
      {
         CpSolver solver = new CpSolver();
         solver.StringParameters = $"max_time_in_seconds:{settings.MaxCalculationTimeSeconds}.0;num_search_workers:{settings.MaxSearchWorkers};log_search_progress:true";

         CpSolverStatus status = solver.Solve(model);
         log($"Instructor Assignments: {status}");

         if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
         {
            int assignedCount = 0;
            foreach (var kvp in data.Assignments)
            {
               if (solver.Value(kvp.Value) == 1)
               {
                  var group = GetGroupSchedules(courseGroupsMap, kvp.Key.SectionId, kvp.Key.CourseId);
                  foreach (var schedule in group)
                  {
                     schedule.InstructorId = kvp.Key.InstructorId;
                     assignedCount++;
                  }
               }
            }
            log($"Successfully assigned {assignedCount} classes.");
         }
         else
         {
            log("No feasible instructor assignment found (or timed out).");
         }
      }  

   }
}