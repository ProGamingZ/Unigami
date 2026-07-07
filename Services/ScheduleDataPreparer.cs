using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using UniversityScheduler.Data;

namespace UniversityScheduler.Services
{
    public class ScheduleDataPreparer
    {
        private readonly AppDbContext _db;

        public ScheduleDataPreparer(AppDbContext db)
        {
            _db = db;
        }

        public List<SchedulingTask> GenerateTasks(int semester, Action<string> log)
        {
            // 1. LOAD SETTINGS
            var settings = SchedulerSettings.Load();
            var excludedCodes = new HashSet<string>(settings.ExcludedCourses, StringComparer.OrdinalIgnoreCase);
            var splitExceptions = new HashSet<string>(settings.SplittingExceptions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            log($"Reading active sections... (Block Splitting: {settings.EnableBlockSplitting})");
            var tasks = new List<SchedulingTask>();
            var sections = _db.Sections.ToList();

            var lockedAssignments = _db.Schedules
                .Include(s => s.Instructor)
                .Where(s => s.Semester == semester && 
                            s.InstructorId != null && 
                            s.Instructor!.IsScheduleLocked)
                .Select(s => new { s.SectionId, s.CourseId })
                .Distinct()
                .ToList();

            // Create a fast lookup set
            var lockedSet = new HashSet<string>(lockedAssignments.Select(x => $"{x.SectionId}_{x.CourseId}"));

            foreach (var section in sections)
            {
                var neededCourses = _db.Curriculums
                    .Include(c => c.Course)
                    .Where(c => c.Program == section.Program 
                                && c.YearLevel == section.YearLevel 
                                && c.Semester == semester)
                    .Select(c => c.Course)
                    .ToList();

                foreach (var course in neededCourses)
                {
                    if (course == null) continue;

                    // --- EXCLUSION LOGIC ---
                    if (excludedCodes.Contains(course.Code) || 
                        excludedCodes.Any(ex => course.Code.StartsWith(ex, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    if (lockedSet.Contains($"{section.Id}_{course.Id}"))
                    {
                        continue; 
                    }

                    if (course.Name.Contains("Internship") || course.Code.Contains("OJT")) continue;

                    // --- SPLIT LOGIC CHECK ---
                    // A course splits ONLY if: Global setting is ON AND Course is NOT an exception.
                    bool splitLec = settings.EnableBlockSplitting;
                    bool splitLab = settings.EnableBlockSplitting;

                    // Check exact component exceptions (e.g., "CS101 - Lec")
                    if (splitExceptions.Contains($"{course.Code} - Lec")) splitLec = false;
                    if (splitExceptions.Contains($"{course.Code} - Lab")) splitLab = false;

                    // Backwards compatibility: If the old format "CS101" is used, exempt both
                    if (splitExceptions.Contains(course.Code)) 
                    {
                        splitLec = false;
                        splitLab = false;
                    }

                    // A. LECTURE COMPONENT
                    if (course.LectureHours > 0)
                    {
                        // If 3 hours AND splitting is allowed -> Create two 1.5hr tasks
                        if (course.LectureHours == 3 && splitLec) 
                        {
                            var t1 = CreateTask(section, course, "Lec", 3, 1);
                            var t2 = CreateTask(section, course, "Lec", 3, 2);
                            t1.RelatedTaskId = t2.TaskId;
                            t2.RelatedTaskId = t1.TaskId;
                            tasks.Add(t1);
                            tasks.Add(t2);
                        }
                        else
                        {
                            // Otherwise, keep as one big block (e.g. 6 blocks = 3 hours)
                            tasks.Add(CreateTask(section, course, "Lec", course.LectureHours * 2, 1));
                        }
                    }

                    // B. LAB COMPONENT
                    if (course.LabHours > 0)
                    {
                        // Same logic for Labs (if you want Labs to split too)
                        if (course.LabHours == 3 && splitLab) 
                        {
                            var t1 = CreateTask(section, course, "Lab", 3, 1);
                            var t2 = CreateTask(section, course, "Lab", 3, 2);
                            t1.RelatedTaskId = t2.TaskId;
                            t2.RelatedTaskId = t1.TaskId;
                            tasks.Add(t1);
                            tasks.Add(t2);
                        }
                        else
                        {
                            tasks.Add(CreateTask(section, course, "Lab", course.LabHours * 2, 1));
                        }
                    }
                }
            }
            log($"Data Prep Complete: Generated {tasks.Count} scheduling tasks.");
            return tasks;
        }
        
        
        private SchedulingTask CreateTask(StudentSection sec, Course course, string type, int blocks, int session)
        {
            string id = $"{sec.Id}_{course.Id}_{type[0]}_{session}";
            
            return new SchedulingTask
            {
                TaskId = id,
                SectionId = sec.Id,
                CourseId = course.Id,
                CourseCode = course.Code, 
                Component = type,
                Duration30MinBlocks = blocks,
                SessionNumber = session,
                StudentCount = sec.StudentCount,
                Program = sec.Program,
                YearLevel = sec.YearLevel   
            };
        }
    }
}