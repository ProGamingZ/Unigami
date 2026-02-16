using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniversityScheduler.Data
{
    public class ClassSchedule
    {
        [Key]
        public int Id { get; set; }

        public int? RoomId { get; set; }
        public int? SectionId { get; set; }
        public int CourseId { get; set; }
        public int? InstructorId { get; set; }

        public string Day { get; set; } = "Mon"; 
        public string StartTime { get; set; } = "07:00"; 
        public string EndTime { get; set; } = "08:30";

        public int Semester { get; set; } = 1; // 1 or 2
        public string Component { get; set; } = "Lecture"; // "Lecture" or "Lab"

        // Navigation Properties
        [ForeignKey("RoomId")] public Room? Room { get; set; }
        [ForeignKey("SectionId")] public StudentSection? Section { get; set; }
        [ForeignKey("CourseId")] public Course? Course { get; set; }
        [ForeignKey("InstructorId")] public Instructor? Instructor { get; set; }
    }
}