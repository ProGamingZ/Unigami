using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using UniversityScheduler.Data;

namespace UniversityScheduler.Services
{
    public class ScheduleDataPreparer(AppDbContext db)
    {
        private readonly AppDbContext _db = db;

        public List<SchedulingTask> GenerateTasks(int semester, Action<string> log)
        {
            var settings = SchedulerSettings.Load();
            var splitExceptions = new HashSet<string>(settings.SplittingExceptions ?? new List<string>(), 
            StringComparer.OrdinalIgnoreCase);
            var excludedCodes = new HashSet<string>(settings.ExcludedCourses ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            var tasks = new List<SchedulingTask>();
            var allInstructors = _db.Instructors.ToList();
            var allCourses = _db.Courses.ToList();
            
            // 1. Build Whitelist of (SectionId, CourseId)
            var whitelist = new HashSet<(int SectionId, int CourseId)>();

            foreach (var inst in allInstructors)
            {
                // Get Sem-Specific Assignments
                string sectionIdsStr = semester == 1 ? inst.AssignedSectionsSem1 : inst.AssignedSectionsSem2;
                string courseCodesStr = semester == 1 ? inst.PreferredCourseCodesSem1 : inst.PreferredCourseCodesSem2;

                // Parse Section IDs (Added Trim)
                var sectionIds = sectionIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                              .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                                              .Where(id => id > 0);
                
                // Parse Course Codes safely (Added Trim and ToUpper to ignore formatting mistakes)
                var courseCodes = courseCodesStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                .Select(s => s.Trim().ToUpper());
                
                var courseIds = allCourses.Where(c => c.Code != null && courseCodes.Contains(c.Code.Trim().ToUpper()))
                                          .Select(c => c.Id);

                foreach (var secId in sectionIds)
                {
                    foreach (var cId in courseIds)
                    {
                        whitelist.Add((secId, cId));
                    }
                }
            }

            log($"Filter Created: Scheduling only {whitelist.Count} assigned combinations.");

            // 2. Generate Tasks ONLY for whitelisted items
            var sections = _db.Sections.ToList();
            foreach (var section in sections)
            {
                var currs = _db.Curriculums.Include(c => c.Course)
                               .Where(c => c.Program == section.Program && 
                                           c.YearLevel == section.YearLevel && 
                                           c.Semester == semester).ToList();

                foreach (var curr in currs)
                {
                    if (curr.Course == null) continue;
                    if (excludedCodes.Contains(curr.Course.Code)) continue;

                    // ONLY generate if this Section/Course combo is assigned to someone
                    if (whitelist.Contains((section.Id, curr.Course.Id)))
                    {
                        // --- LECTURE HANDLING ---
                        int lecBlocks = curr.Course.LectureHours * 2;
                        if (lecBlocks > 0)
                        {
                            bool isLecExempt = splitExceptions.Contains(curr.Course.Code) || 
                                               splitExceptions.Contains($"{curr.Course.Code} Lec")||
                                               splitExceptions.Contains($"{curr.Course.Code} - Lec");

                            if (settings.EnableBlockSplitting && lecBlocks >= 6 && !isLecExempt)
                            {
                                int half = lecBlocks / 2;
                                var t1 = CreateTask(section, curr.Course, "Lecture", half, 1);
                                var t2 = CreateTask(section, curr.Course, "Lecture", half, 2);
                                t1.RelatedTaskId = t2.TaskId;
                                t2.RelatedTaskId = t1.TaskId;
                                tasks.Add(t1);
                                tasks.Add(t2);
                            }
                            else
                            {
                                tasks.Add(CreateTask(section, curr.Course, "Lecture", lecBlocks, 1));
                            }
                        }

                        // --- LAB HANDLING ---
                        int labBlocks = curr.Course.LabHours * 2;
                        if (labBlocks > 0)
                        {
                            // FIX: Check for base code OR the " Lab" suffix from the UI
                            bool isLabExempt = splitExceptions.Contains(curr.Course.Code) || 
                                               splitExceptions.Contains($"{curr.Course.Code} Lab")||
                                               splitExceptions.Contains($"{curr.Course.Code} - Lab");

                            if (settings.EnableBlockSplitting && labBlocks >= 6 && !isLabExempt)
                            {
                                int half = labBlocks / 2;
                                var t1 = CreateTask(section, curr.Course, "Lab", half, 1);
                                var t2 = CreateTask(section, curr.Course, "Lab", half, 2);
                                t1.RelatedTaskId = t2.TaskId;
                                t2.RelatedTaskId = t1.TaskId;
                                tasks.Add(t1);
                                tasks.Add(t2);
                            }
                            else
                            {
                                tasks.Add(CreateTask(section, curr.Course, "Lab", labBlocks, 1));
                            }
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