using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Google.OrTools.Sat;
using UniversityScheduler.Data;

namespace UniversityScheduler.Services
{
    public partial class ScheduleSolver
    {
        private const int SlotDurationMinutes = 30;

        public List<ClassSchedule> Solve(List<SchedulingTask> tasks, List<Room> rooms, List<Instructor> instructors, int semester, Action<string> log)
        {
            // STEP 1: Load Configuration & Validation
            var settings = SchedulerSettings.Load();
            log($"Configuration Loaded: {settings.DayStartHour}:00 - {settings.DayEndHour}:00 | Rules: {settings.DayRules.Count} Day, {settings.TimeRules.Count} Time");

            if (rooms.Count == 0 || tasks.Count == 0)
            {
                log("No rooms or tasks. Skipping.");
                return new List<ClassSchedule>();
            }

            // STEP 2: Prepare Constraints (Pre-calculation)
            // We calculate these ONCE before entering the heavy loops
            var roomRestrictions = BuildRoomRestrictions(instructors, semester);
            var blockedSlots = GetLockedSlots(semester, settings);

            // STEP 3: Build the Mathematical Model (Variables)
            CpModel model = new CpModel();
            var modelData = CreateModelVariables(model, tasks, rooms, instructors, semester, settings, roomRestrictions, blockedSlots, log);

            // STEP 4: Apply Constraints
            AddOverlapConstraints(model, modelData);
            AddSiblingConstraints(model, tasks, modelData, settings);
            AddLecBeforeLabConstraints(model, tasks, modelData);

            // STEP 5: Apply Symmetry Breaking (Speed Optimization)
            AddRoomSymmetryBreaking(model, modelData.Assignments, rooms, log);
            if (modelData.Assumptions.Count > 0)
            {
                model.AddAssumptions(modelData.Assumptions);
            }

            //Diagnostics before solving
            RunDiagnostics(tasks, instructors, semester, modelData, log);

            // STEP 6: Execute & Parse
            return RunSolver(model, modelData, semester, settings, log);
        }
        
        private Dictionary<int, List<(string Program, int Year)>> BuildRoomRestrictions(List<Instructor> instructors, int semester)
        {
            var restrictions = new Dictionary<int, List<(string, int)>>();

            foreach (var instr in instructors)
            {
                string status = semester == 1 ? instr.StatusSem1 : instr.StatusSem2;
                int? assignedRoomId = semester == 1 ? instr.AssignedRoomIdSem1 : instr.AssignedRoomIdSem2;

                if (assignedRoomId == null) 
                    continue;

                int roomId = assignedRoomId.Value;
                if (!restrictions.ContainsKey(roomId)) restrictions[roomId] = new List<(string, int)>();

                string program = semester == 1 ? instr.ProgramSem1 : instr.ProgramSem2;
                string yearLevels = semester == 1 ? instr.PreferredYearLevelsSem1 : instr.PreferredYearLevelsSem2;

                var progParts = (program ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var yearParts = (yearLevels ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var p in progParts)
                {
                    foreach (var y in yearParts)
                    {
                        if (int.TryParse(y, out int yInt)) restrictions[roomId].Add((p, yInt));
                    }
                }
            }
            return restrictions;
        }
        
        private void AddRoomSymmetryBreaking(CpModel model, List<AssignmentVar> assignments, List<Room> rooms, Action<string> log)
        {
            // Group rooms by identical functional properties (Type and Capacity)
            var identicalRoomGroups = rooms
                .GroupBy(r => new { r.Type, r.Capacity })
                .Where(g => g.Count() > 1)
                .Select(g => g.OrderBy(r => r.Id).ToList())
                .ToList();

            int symmetryConstraintsAdded = 0;

            foreach (var group in identicalRoomGroups)
            {
                for (int i = 0; i < group.Count - 1; i++)
                {
                    var room1 = group[i];
                    var room2 = group[i + 1];

                    // Get all assignment variables for Room1 and Room2
                    var r1Vars = assignments.Where(a => a.Room.Id == room1.Id).Select(a => a.Variable).ToList();
                    var r2Vars = assignments.Where(a => a.Room.Id == room2.Id).Select(a => a.Variable).ToList();

                    if (r1Vars.Count > 0 && r2Vars.Count > 0)
                    {
                        // Force Room 1 to always take >= the number of classes as Room 2.
                        // This stops the AI from testing mirrored schedules (e.g. swapping Room 101 and 102)
                        model.Add(LinearExpr.Sum(r1Vars) >= LinearExpr.Sum(r2Vars));
                        symmetryConstraintsAdded++;
                    }
                }
            }
            log($"Added {symmetryConstraintsAdded} room symmetry breaking constraints.");
        }

        private HashSet<string> GetLockedSlots(int semester, SchedulerSettings settings)
        {
            var blocked = new HashSet<string>();
            using (var db = new AppDbContext())
            {
                var lockedSchedules = db.Schedules
                    .Include(s => s.Instructor)
                    .Where(s => s.Semester == semester && s.Instructor != null && s.Instructor.IsScheduleLocked)
                    .ToList();

                foreach (var s in lockedSchedules)
                {
                    if (TimeSpan.TryParse(s.StartTime, out var start) && TimeSpan.TryParse(s.EndTime, out var end))
                    {
                        int startSlot = (int)((start.TotalMinutes - (settings.DayStartHour * 60)) / SlotDurationMinutes);
                        int duration = (int)((end.TotalMinutes - start.TotalMinutes) / SlotDurationMinutes);
                        int dayIdx = GetDayIndex(s.Day);

                        for (int i = 0; i < duration; i++)
                        {
                            // Block the physical Room
                            if (s.RoomId.HasValue) 
                                blocked.Add($"R_{s.RoomId.Value}_{dayIdx}_{startSlot + i}");
                            
                            // Block the Student Section
                            if (s.SectionId.HasValue) 
                                blocked.Add($"S_{s.SectionId.Value}_{dayIdx}_{startSlot + i}");
                        }
                    }
                }
            }
            return blocked;
        }
        private ModelData CreateModelVariables(CpModel model, List<SchedulingTask> tasks, List<Room> rooms, List<Instructor> instructors, int semester, SchedulerSettings settings, Dictionary<int, List<(string Program, int Year)>> roomRestrictions, HashSet<string> blockedSlots, Action<string> log)
        {
            var data = new ModelData();
            int variableCount = 0;

            // Calculate Constants
            int totalSlotsPerDay = (settings.DayEndHour - settings.DayStartHour) * 2;
            int lunchSlotIndex = (12 - settings.DayStartHour) * 2;
            var lunchSlots = new[] { lunchSlotIndex, lunchSlotIndex + 1 };

            foreach (var task in tasks)
            {
                int duration = task.Duration30MinBlocks;
                bool isLab = task.Component == "Lab" || task.Component == "Practicum";

                // Find assigned instructors for this exact task 
                var assignedInstructors = instructors.Where(i => IsTaskAssignedToInstructor(i, task, semester)).ToList();

                // A. Find Valid Rooms
                var validRooms = GetValidRooms(task, rooms, roomRestrictions, isLab, assignedInstructors, semester);
                if (!validRooms.Any())
                {
                    log($"WARNING: Task {task.TaskId} ({task.CourseCode}) has no valid rooms.");
                    continue;
                }

                // B. Get Allowed Time/Day
                var allowedDays = GetAllowedDays(task, settings);
                int maxDailySlot = GetMaxDailySlot(task, settings, totalSlotsPerDay);

                // C. Generate Variables
                foreach (var room in validRooms)
                {
                    foreach (int day in allowedDays)
                    {
                        for (int slot = 0; slot <= maxDailySlot - duration; slot++)
                        {
                            // Check Lunch
                            if (settings.AvoidLunchBreak && !isLab && lunchSlots.Any(l => l >= slot && l < slot + duration))
                                continue;

                            // Check Blocked Slots (Rooms AND Sections)
                            bool isBlocked = false;
                            for (int d = 0; d < duration; d++)
                            {
                                // If the Room OR the Section is locked by another schedule, skip this time slot!
                                if (blockedSlots.Contains($"R_{room.Id}_{day}_{slot + d}") || 
                                    blockedSlots.Contains($"S_{task.SectionId}_{day}_{slot + d}")) 
                                { 
                                    isBlocked = true; 
                                    break; 
                                }
                            }
                            if (isBlocked) continue;

                            // The AI physically cannot generate a class at a time the assigned instructor is not available.
                            if (assignedInstructors.Count > 0)
                            {
                                bool fitsAnyInstructor = false;
                                foreach (var instr in assignedInstructors)
                                {
                                    if (FitsInstructorTimeBlocks(instr, settings, semester, day, slot, duration))
                                    {
                                        fitsAnyInstructor = true;
                                        break;
                                    }
                                }
                                if (!fitsAnyInstructor) continue; // Skip generating this illegal time slot!
                            }
                            // ---------------------------------------------

                            // Create Variable
                            var boolVar = model.NewBoolVar($"T{task.TaskId}_R{room.Id}_D{day}_S{slot}");
                            variableCount++;

                            var assignment = new AssignmentVar { Task = task, Room = room, Day = day, StartSlot = slot, Variable = boolVar };
                            data.Assignments.Add(assignment);

                            // Map for constraints
                            for (int d = 0; d < duration; d++)
                            {
                                AddToMap(data.RoomsAtSlot, (room.Id, day, slot + d), boolVar);
                                AddToMap(data.SectionsAtSlot, (task.SectionId, day, slot + d), boolVar);
                                foreach (var instr in assignedInstructors)
                                {
                                    AddToMap(data.InstructorsAtSlot, (instr.Id, day, slot + d), boolVar);
                                }
                            }
                        }
                    }
                }
                
                // Ensure task is assigned exactly once
                var taskVars = data.Assignments.Where(a => a.Task == task).Select(a => a.Variable).ToList();
                if (taskVars.Count > 0) model.AddExactlyOne(taskVars);
                else log($"CRITICAL: No valid slots for {task.CourseCode} - Could be a time conflict with the instructor.");
            }

            log($"Created {variableCount} variables.");
            return data;
        }
        private void AddOverlapConstraints(CpModel model, ModelData data)
        {
            foreach (var kvp in data.RoomsAtSlot) model.AddAtMostOne(kvp.Value);
            foreach (var kvp in data.SectionsAtSlot) model.AddAtMostOne(kvp.Value);
            foreach (var kvp in data.InstructorsAtSlot) model.AddAtMostOne(kvp.Value);
        }
        private void AddSiblingConstraints(CpModel model, List<SchedulingTask> tasks, ModelData data, SchedulerSettings settings)
        {
            var assignments = data.Assignments; // Extract assignments here
            
            var taskGroups = tasks
                .GroupBy(t => new { t.SectionId, t.CourseId, t.Component })
                .Where(g => g.Count() == 2)
                .ToList();

            foreach (var group in taskGroups)
            {
                var sorted = group.OrderBy(t => t.SessionNumber).ToList();
                var t1 = sorted[0]; 
                var t2 = sorted[1];
                
                if (t1.CourseCode.Contains("NSTP")) continue;
                if (t1.Component == "Lab" && t1.Duration30MinBlocks > 4) continue; 

                var t1Vars = assignments.Where(a => a.Task == t1).ToList();
                var t2Vars = assignments.Where(a => a.Task == t2).ToList();

                if (t1Vars.Count == 0 || t2Vars.Count == 0) continue;

                var t1Day = LinearExpr.Sum(t1Vars.Select(a => a.Variable * a.Day));
                var t2Day = LinearExpr.Sum(t2Vars.Select(a => a.Variable * a.Day));

                if (settings.SiblingPattern == "Strict")
                {
                    // 1. Create a "Name Tag" assumption for this specific rule
                    var enforceStrict = model.NewBoolVar($"Strict_{t1.CourseCode}_{t1.SectionId}");
                    
                    // 2. Only enforce the 3-day gap IF the assumption is active
                    model.Add(t2Day == t1Day + 3).OnlyEnforceIf(enforceStrict);
                    
                    // 3. Save the Name Tag so we can print it if it crashes
                    data.Assumptions.Add(enforceStrict);
                    data.SiblingAssumptionMap[enforceStrict.Index] = t1;
                }
                else if (settings.SiblingPattern == "Relaxed")
                {
                    model.Add(t2Day >= t1Day + 2); 
                }
            }
        }
        
        private void AddLecBeforeLabConstraints(CpModel model, List<SchedulingTask> tasks, ModelData data)
        {
            var assignments = data.Assignments;
            
            // Group the tasks by Section and Course
            var courseGroups = tasks.GroupBy(t => new { t.SectionId, t.CourseId });

            foreach (var group in courseGroups)
            {
                // Separate into Lec and Lab, and sort them chronologically by Session Number
                var lecTasks = group.Where(t => !t.Component.Contains("Lab") && !t.Component.Contains("Practicum"))
                                    .OrderBy(t => t.SessionNumber).ToList();
                                    
                var labTasks = group.Where(t => t.Component.Contains("Lab") || t.Component.Contains("Practicum"))
                                    .OrderBy(t => t.SessionNumber).ToList();

                // Match them up (e.g. Lec 1 precedes Lab 1. Lec 2 precedes Lab 2).
                int pairsToConstrain = Math.Min(lecTasks.Count, labTasks.Count);

                for (int i = 0; i < pairsToConstrain; i++)
                {
                var lec = lecTasks[i];
                var lab = labTasks[i];

                var lecVars = assignments.Where(a => a.Task == lec).ToList();
                var labVars = assignments.Where(a => a.Task == lab).ToList();

                if (lecVars.Count > 0 && labVars.Count > 0)
                {
                    // 1. Calculate Absolute Time Formula: (Day * 100) + StartSlot
                    // Example: Monday (0) Slot 10 = 10. 
                    // Example: Tuesday (1) Slot 5 = 105. 
                    // Because slots never exceed 30 per day, days will never overlap!
                    var lecAbsTime = LinearExpr.Sum(lecVars.Select(a => a.Variable * ((a.Day * 100) + a.StartSlot)));
                    var labAbsTime = LinearExpr.Sum(labVars.Select(a => a.Variable * ((a.Day * 100) + a.StartSlot)));

                    // 2. Enforce Chronology: Lab absolute time MUST be >= (Lec absolute time + Lec duration)
                    model.Add(labAbsTime >= lecAbsTime + lec.Duration30MinBlocks);
                }
                }
            }
        }

        private List<ClassSchedule> RunSolver(CpModel model, ModelData modelData, int semester, SchedulerSettings settings, Action<string> log)
        {
            log("Starting Solver...");
            CpSolver solver = new CpSolver();
            solver.StringParameters = $"max_time_in_seconds:{settings.MaxCalculationTimeSeconds}.0;num_search_workers:{settings.MaxSearchWorkers};log_search_progress:true";

            CpSolverStatus status = solver.Solve(model);
            log($"Solver Status: {status}");

            log($"\n--- INTERNAL SOLVER STATS ---\n{solver.ResponseStats()}\n-----------------------------");
            var results = new List<ClassSchedule>();
            if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
            {
                TimeSpan dayStart = new TimeSpan(settings.DayStartHour, 0, 0);

                foreach (var assign in modelData.Assignments)
                {
                    if (solver.Value(assign.Variable) == 1)
                    {
                        TimeSpan startT = dayStart.Add(new TimeSpan(0, assign.StartSlot * SlotDurationMinutes, 0));
                        TimeSpan endT = startT.Add(new TimeSpan(0, assign.Task.Duration30MinBlocks * SlotDurationMinutes, 0));

                        results.Add(new ClassSchedule
                        {
                            SectionId = assign.Task.SectionId,
                            CourseId = assign.Task.CourseId,
                            RoomId = assign.Room.Id,
                            InstructorId = null, // Set in Phase 2
                            Day = GetDayString(assign.Day),
                            StartTime = startT.ToString(@"hh\:mm"),
                            EndTime = endT.ToString(@"hh\:mm"),
                            Semester = semester,
                            Component = assign.Task.Component
                        });
                    }
                }
                log($"Scheduled {results.Count} classes.");
            }
            else if (status == CpSolverStatus.Infeasible)
            {
                log("No solution found.");
                
                if (solver.Response.SufficientAssumptionsForInfeasibility.Count > 0)
                {
                    log("\n[CRITICAL FAILURE] The solver identified these specific rules as the exact cause of the mathematical gridlock:");
                    
                    using (var db = new AppDbContext())
                    {
                        var allInstructors = db.Instructors.Include(i => i.AssignedRoomSem1).Include(i => i.AssignedRoomSem2).ToList();

                        // 1. Separate the errors
                        var failingSiblingTasks = new List<SchedulingTask>();
                        var failingOverlaps = new List<string>();

                        foreach (var idx in solver.Response.SufficientAssumptionsForInfeasibility)
                        {
                            if (modelData.SiblingAssumptionMap.ContainsKey(idx)) failingSiblingTasks.Add(modelData.SiblingAssumptionMap[idx]);
                            if (modelData.OverlapAssumptionMap.ContainsKey(idx)) failingOverlaps.Add(modelData.OverlapAssumptionMap[idx]);
                        }

                        // 2. Process Sibling/Spacing Errors (Grouped by Instructor)
                        if (failingSiblingTasks.Count > 0)
                        {
                            var taskGroups = failingSiblingTasks.GroupBy(t => {
                                var instrs = allInstructors.Where(i => IsTaskAssignedToInstructor(i, t, semester)).ToList();
                                return instrs.Count > 0 ? instrs.First().Id : -1;
                            });

                            foreach (var group in taskGroups)
                            {
                                if (group.Key == -1) continue;
                                
                                var instr = allInstructors.First(i => i.Id == group.Key);
                                string prefs = semester == 1 ? instr.SchedulePreferencesSem1 : instr.SchedulePreferencesSem2;
                                string course = group.First().CourseCode;
                                int count = group.Count();

                                log($" -> [Conflict] {instr.FullName} | {count} sections of {course}");
                                log($"    └─ Availability: {(string.IsNullOrWhiteSpace(prefs) ? "Anytime" : prefs.Replace(";", " "))}");
                                log($"    └─ Reason: 'Strict Mode' spacing (M/Th, T/F, W/S) requires {count} compatible day-pairs, but Instructor availability provides fewer valid slots than sections required.");
                            }
                        }

                        // 3. Process Overlap Errors (Double Booking)
                        foreach (var overlap in failingOverlaps)
                        {
                            var parts = overlap.Split('|');
                            int id = int.Parse(parts[1]);

                            if (parts[0] == "Instructor") {
                                var i = db.Instructors.Find(id);
                                log($" -> Double Booking Error: Instructor {i?.FullName} does not have enough hours to teach their assigned load without overlapping.");
                            }
                            else if (parts[0] == "Room") {
                                var r = db.Rooms.Find(id);
                                log($" -> Room Traffic Jam: Room {r?.Name} does not have enough physical slots to host all the classes assigned to it.");
                            }
                            else if (parts[0] == "Section") {
                                var s = db.Sections.Find(id);
                                log($" -> Section Overlap: Section {s?.Program} {s?.YearLevel}-{s?.Name} has too many subjects trying to schedule at the exact same time.");
                            }
                        }
                    }
                }
            }
            else
            {
                log("No solution found.");
            }
            return results;
        }

    }  
}