using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Google.OrTools.Sat;
using UniversityScheduler.Data;

namespace UniversityScheduler.Services
{
    public class ScheduleSolver
    {
        private const int SlotDurationMinutes = 30;
        
        #region Create Schedule

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
                var modelData = CreateModelVariables(model, tasks, rooms, settings, roomRestrictions, blockedSlots, log);

                // STEP 4: Apply Constraints
                AddOverlapConstraints(model, modelData);
                AddSiblingConstraints(model, tasks, modelData.Assignments, settings);

                // STEP 5: Set Objective (Minimize Bad Schedules)
                AddObjectiveFunction(model, modelData.Assignments, settings);

                // STEP 6: Execute & Parse
                return RunSolver(model, modelData.Assignments, semester, settings, log);
            }
            
            private Dictionary<int, List<(string Program, int Year)>> BuildRoomRestrictions(List<Instructor> instructors, int semester)
            {
                var restrictions = new Dictionary<int, List<(string, int)>>();

                foreach (var instr in instructors)
                {
                    string status = semester == 1 ? instr.StatusSem1 : instr.StatusSem2;
                    int? assignedRoomId = semester == 1 ? instr.AssignedRoomIdSem1 : instr.AssignedRoomIdSem2;

                    if (!string.Equals(status, "Full-Time", StringComparison.OrdinalIgnoreCase) || assignedRoomId == null) 
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
                                blocked.Add($"{s.RoomId}_{dayIdx}_{startSlot + i}");
                            }
                        }
                    }
                }
                return blocked;
            }
            private ModelData CreateModelVariables(CpModel model, List<SchedulingTask> tasks, List<Room> rooms, SchedulerSettings settings, Dictionary<int, List<(string Program, int Year)>> roomRestrictions, HashSet<string> blockedSlots, Action<string> log)
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

                    // A. Find Valid Rooms
                    var validRooms = GetValidRooms(task, rooms, roomRestrictions, isLab);
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

                                // Check Blocked Slots
                                bool isBlocked = false;
                                for (int d = 0; d < duration; d++)
                                {
                                    if (blockedSlots.Contains($"{room.Id}_{day}_{slot + d}")) { isBlocked = true; break; }
                                }
                                if (isBlocked) continue;

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
                                }
                            }
                        }
                    }
                    
                    // Ensure task is assigned exactly once
                    var taskVars = data.Assignments.Where(a => a.Task == task).Select(a => a.Variable).ToList();
                    if (taskVars.Count > 0) model.AddExactlyOne(taskVars);
                    else log($"CRITICAL: No valid slots for {task.CourseCode}");
                }

                log($"Created {variableCount} variables.");
                return data;
            }
            private void AddOverlapConstraints(CpModel model, ModelData data)
            {
                foreach (var kvp in data.RoomsAtSlot) model.AddAtMostOne(kvp.Value);
                foreach (var kvp in data.SectionsAtSlot) model.AddAtMostOne(kvp.Value);
            }
            private void AddSiblingConstraints(CpModel model, List<SchedulingTask> tasks, List<AssignmentVar> assignments, SchedulerSettings settings)
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
                    
                    // Skip special cases
                    if (t1.CourseCode.Contains("NSTP")) continue;
                    if (t1.Component == "Lab" && t1.Duration30MinBlocks > 4) continue; // Skip heavy labs

                    var t1Vars = assignments.Where(a => a.Task == t1).ToList();
                    var t2Vars = assignments.Where(a => a.Task == t2).ToList();

                    if (t1Vars.Count == 0 || t2Vars.Count == 0) continue;

                    var t1Day = LinearExpr.Sum(t1Vars.Select(a => a.Variable * a.Day));
                    var t2Day = LinearExpr.Sum(t2Vars.Select(a => a.Variable * a.Day));

                    if (settings.SiblingPattern == "Strict")
                        model.Add(t2Day == t1Day + 3); // e.g., Mon(0) & Thu(3)
                    else if (settings.SiblingPattern == "Relaxed")
                        model.Add(t2Day >= t1Day + 2); // At least 1 day gap
                }
            }
            private void AddObjectiveFunction(CpModel model, List<AssignmentVar> assignments, SchedulerSettings settings)
            {
                var objectiveTerms = new List<LinearExpr>();
                int saturdayPenalty = 10000;
                int eveningPenalty = 5000;
                int eveningCutoffSlot = (18 - settings.DayStartHour) * 2; // 6:00 PM

                foreach (var assign in assignments)
                {
                    int cost = assign.StartSlot; // Base cost: prefer earlier slots
                    if (assign.Day == 5) cost += saturdayPenalty;
                    if (assign.StartSlot >= eveningCutoffSlot) cost += eveningPenalty;
                    
                    objectiveTerms.Add(assign.Variable * cost);
                }
                model.Minimize(LinearExpr.Sum(objectiveTerms));
            }
            private List<ClassSchedule> RunSolver(CpModel model, List<AssignmentVar> assignments, int semester, SchedulerSettings settings, Action<string> log)
            {
                log("Starting Solver...");
                CpSolver solver = new CpSolver();
                solver.StringParameters = $"max_time_in_seconds:{settings.MaxCalculationTimeSeconds}.0;num_search_workers:{settings.MaxSearchWorkers};log_search_progress:true";

                CpSolverStatus status = solver.Solve(model);
                log($"Solver Status: {status}");

                var results = new List<ClassSchedule>();
                if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
                {
                    TimeSpan dayStart = new TimeSpan(settings.DayStartHour, 0, 0);

                    foreach (var assign in assignments)
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
                else
                {
                    log("No solution found.");
                }
                return results;
            }

            private class ModelData
            {
                public List<AssignmentVar> Assignments { get; set; } = new List<AssignmentVar>();
                public Dictionary<(int SectionId, int Day, int Slot), List<BoolVar>> SectionsAtSlot { get; set; } = new Dictionary<(int, int, int), List<BoolVar>>();
                public Dictionary<(int RoomId, int Day, int Slot), List<BoolVar>> RoomsAtSlot { get; set; } = new Dictionary<(int, int, int), List<BoolVar>>();
            }
            // These mini-helpers are used in CreateModelVariables
            private List<Room> GetValidRooms(SchedulingTask task, List<Room> rooms, Dictionary<int, List<(string Program, int Year)>> restrictions, bool isLab)
            {
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

        #endregion

        #region Assign Instructors

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
                SetInstructorObjective(model, data);
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

                        // Calculate Score (Priority)
                        int score = CalculatePreferenceScore(instr, first);
                        data.ObjectiveTerms.Add(isAssigned * score);
                    }

                    // Constraint: Only 1 instructor per course
                    model.AddAtMostOne(groupVars);
                }

                log($"Created {varCount} assignment variables.");
                return data;
            }
            private int CalculatePreferenceScore(Instructor instr, ClassSchedule sched)
            {
                int score = 10; // Base
                string prefYears = sched.Semester == 1 ? instr.PreferredYearLevelsSem1 : instr.PreferredYearLevelsSem2;
                string prefCourses = sched.Semester == 1 ? instr.PreferredCourseCodesSem1 : instr.PreferredCourseCodesSem2;
                int? assignedRoom = sched.Semester == 1 ? instr.AssignedRoomIdSem1 : instr.AssignedRoomIdSem2;

                // Bonus: Preferred Year Level
                if (!string.IsNullOrEmpty(prefYears))
                {
                    var years = prefYears.Split(',');
                    if (sched.Section != null && years.Contains(sched.Section.YearLevel.ToString())) score += 5;
                }

                // Bonus: Preferred Specific Course (+30 priority points)
                if (!string.IsNullOrEmpty(prefCourses) && sched.Course != null)
                {
                    var preferredCourses = prefCourses.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (preferredCourses.Contains(sched.Course.Code)) score += 30;
                }

                // Bonus: Owns the Room
                if (sched.RoomId != null && assignedRoom == sched.RoomId) score += 50;

                return score;
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
            private void SetInstructorObjective(CpModel model, InstructorModelData data)
            {
                model.Maximize(LinearExpr.Sum(data.ObjectiveTerms));
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
                            // USE THE NEW MAP LOOKUP HERE
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

            private class InstructorModelData
            {
                // Maps (Section, Course, Instructor) -> BoolVar
                public Dictionary<(int SectionId, int CourseId, int InstructorId), BoolVar> Assignments { get; set; } = new();
                
                // Maps InstructorId -> List of (Variable, Units)
                public Dictionary<int, List<(BoolVar Var, int Units)>> InstructorLoads { get; set; } = new();
                
                // List of scores for the objective function
                public List<LinearExpr> ObjectiveTerms { get; set; } = new();
            }
            // Helper to retrieve the List<Schedule> from the grouped collection
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
            // Improved helper for qualification check
            private bool IsInstructorQualified(Instructor instr, ClassSchedule schedule)
            {
                // Note: You can pass settings.UniversalSubjects here if needed
                string program = schedule.Semester == 1 ? instr.ProgramSem1 : instr.ProgramSem2;
                if (string.IsNullOrEmpty(program) || schedule.Section == null) return false;

                var instructorPrograms = program.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return instructorPrograms.Contains(schedule.Section.Program);
            }

        #endregion


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
        private class AssignmentVar
        {
            public required SchedulingTask Task { get; set; }
            public required Room Room { get; set; }
            public int Day { get; set; }
            public int StartSlot { get; set; }
            public required BoolVar Variable { get; set; }
        }            
        private bool FitsTimePreference(Instructor instr, ClassSchedule cls)
        {
            // 0. Parse Class Times (Strict 24h from Scheduler)
            if (!TimeSpan.TryParse(cls.StartTime, out var classStart) || 
                !TimeSpan.TryParse(cls.EndTime, out var classEnd)) return false;

            string prefs = cls.Semester == 1 ? instr.SchedulePreferencesSem1 : instr.SchedulePreferencesSem2;

            // 1. Handle "Any" or Empty (Assuming explicit availability if empty, optional)
            if (string.IsNullOrWhiteSpace(prefs)) return true;

            var blocks = prefs.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in blocks)
            {
                var parts = block.Split('|');
                if (parts.Length != 2) continue;

                var days = parts[0].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                
                // --- 1. Day Check ---
                // Using "Any" logic or strict list
                if (!days.Contains("Any") && !days.Contains(cls.Day)) continue;

                var timeRange = parts[1].Split('-');
                if (timeRange.Length != 2) continue;

                // --- 2. Robust Time Parsing (Fixes the AM/PM bug) ---
                TimeSpan prefStart, prefEnd;

                // Helper function to parse flexible formats
                bool ParseTime(string raw, out TimeSpan result)
                {
                    raw = raw.Trim();
                    // Try standard TimeSpan (e.g. "13:00")
                    if (TimeSpan.TryParse(raw, out result)) return true;
                    
                    // Try DateTime for AM/PM (e.g. "1:00 pm")
                    if (DateTime.TryParse(raw, out DateTime dt))
                    {
                        result = dt.TimeOfDay;
                        return true;
                    }
                    return false;
                }

                if (ParseTime(timeRange[0], out prefStart) && ParseTime(timeRange[1], out prefEnd))
                {
                    // Strict Fit Check: Class must start AFTER pref start AND end BEFORE pref end
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
    
    }  
}