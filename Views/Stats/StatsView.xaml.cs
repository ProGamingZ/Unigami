using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
    public partial class StatsView : UserControl
    {
        public StatsView()
        {
            InitializeComponent();
            LoadStats();
        }

        private void LoadStats()
        {
            using (var db = new AppDbContext())
            {
                // --- 1. INSTRUCTORS ---
                var instructors = db.Instructors.ToList();
                var allScheds = db.Schedules.Include(s => s.Course).Where(s => s.InstructorId != null).ToList();
                int activeSemester = allScheds.FirstOrDefault()?.Semester ?? 1;

                var instructorData = instructors.Select(inst => {
                    var distinctClasses = allScheds
                        .Where(s => s.InstructorId == inst.Id && s.Course != null)
                        .Select(s => new { s.CourseId, s.SectionId, s.Course!.Units })
                        .Distinct();
                    
                    int totalUnits = distinctClasses.Sum(x => x.Units);
                    string status = (activeSemester == 1 ? inst.StatusSem1 : inst.StatusSem2);
                    if (string.IsNullOrWhiteSpace(status)) status = "Unspecified Status";
                    
                    // No fallback! If it's empty, it will be stripped entirely.
                    string programRaw = (activeSemester == 1 ? inst.ProgramSem1 : inst.ProgramSem2) ?? "";

                    return new { inst, totalUnits, status, programRaw };
                }).ToList();

                InstructorCount.Text = instructors.Count.ToString();

                var statusStats = new List<InstructorStatusStat>();
                foreach(var sg in instructorData.GroupBy(x => x.status))
                {
                    var stat = new InstructorStatusStat 
                    {
                        Status = sg.Key,
                        TotalCount = sg.Count(),
                        ZeroUnitsCount = sg.Count(x => x.totalUnits == 0),
                        ProgramStats = new List<StatItem>()
                    };

                    var programCounts = new Dictionary<string, int>();
                    foreach(var item in sg)
                    {
                        // Split and strictly ignore empty strings to remove the "General Education" fallback
                        var progs = item.programRaw.Split(new[] {',', ' '}, StringSplitOptions.RemoveEmptyEntries);
                        foreach(var p in progs)
                        {
                            if (!programCounts.ContainsKey(p)) programCounts[p] = 0;
                            programCounts[p]++;
                        }
                    }

                    stat.ProgramStats = programCounts
                        .Select(kv => new StatItem { Name = kv.Key, Count = kv.Value })
                        .OrderByDescending(x => x.Count).ToList();
                        
                    statusStats.Add(stat);
                }
                InstructorStatsList.ItemsSource = statusStats.OrderByDescending(s => s.TotalCount).ToList();

                // --- 2. ROOMS & UTILIZATION ---
                var rooms = db.Rooms.ToList();
                RoomCount.Text = rooms.Count.ToString();

                // Group rooms by Floor
                var roomFloorStats = rooms.GroupBy(r => r.FloorLevel)
                    .Select(g => new StatItem { Name = $"Floor {g.Key}", Count = g.Count() })
                    .OrderBy(x => x.Name).ToList();
                RoomStatsList.ItemsSource = roomFloorStats;

                // Room Utilization Math
                double maxRoomHours = rooms.Count * 84; 
                var roomSchedules = db.Schedules.Where(s => s.RoomId != null).ToList();
                double usedHours = roomSchedules.Sum(s => {
                    if (DateTime.TryParse(s.StartTime, out var st) && DateTime.TryParse(s.EndTime, out var et)) return (et - st).TotalHours;
                    return 0;
                });

                double roomUtilPercent = maxRoomHours == 0 ? 0 : Math.Round((usedHours / maxRoomHours) * 100, 1);
                RoomUtilPercentTxt.Text = $"{roomUtilPercent}%";
                RoomUtilSubTxt.Text = $"{Math.Round(usedHours, 1)} / {maxRoomHours} Hours Booked";

                // --- 3. COURSES ---
                var courses = db.Courses.ToList();
                CourseCount.Text = courses.Count.ToString();

                var courseProgramCounts = new Dictionary<string, int>();
                foreach(var c in courses)
                {
                    var progs = (c.RecommendedPrograms ?? "").Split(new[] {',', ' '}, StringSplitOptions.RemoveEmptyEntries);
                    foreach(var p in progs)
                    {
                        if (!courseProgramCounts.ContainsKey(p)) courseProgramCounts[p] = 0;
                        courseProgramCounts[p]++;
                    }
                }
                CourseStatsList.ItemsSource = courseProgramCounts
                    .Select(kv => new StatItem { Name = kv.Key, Count = kv.Value })
                    .OrderByDescending(x => x.Count).ToList();

                // --- 4. CLASSES (SECTIONS) ---
                var sections = db.Sections.ToList();
                SectionCount.Text = sections.Count.ToString();

                var sectionStats = sections.GroupBy(s => s.Program)
                    .Select(g => new SectionProgramStat 
                    {
                        Program = string.IsNullOrWhiteSpace(g.Key) ? "Unspecified" : g.Key,
                        TotalCount = g.Count(),
                        YearStats = g.GroupBy(x => x.YearLevel)
                                     .Select(yg => new StatItem { Name = $"Year {yg.Key}", Count = yg.Count() })
                                     .OrderBy(x => x.Name).ToList()
                    })
                    .OrderByDescending(x => x.TotalCount).ToList();
                
                SectionStatsList.ItemsSource = sectionStats;
            }
        }
    }

    // --- Helper Classes for Nested Data Binding ---
    public class InstructorStatusStat
    {
        public string Status { get; set; } = "";
        public int TotalCount { get; set; }
        public int ZeroUnitsCount { get; set; }
        public Visibility ZeroUnitsVisibility => ZeroUnitsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        public List<StatItem> ProgramStats { get; set; } = new List<StatItem>();
    }

    public class SectionProgramStat
    {
        public string Program { get; set; } = "";
        public int TotalCount { get; set; }
        public List<StatItem> YearStats { get; set; } = new List<StatItem>();
    }

    public class StatItem
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }
}