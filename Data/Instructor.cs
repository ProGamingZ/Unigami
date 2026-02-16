using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniversityScheduler.Data
{
    public class Instructor
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty; // e.g. "SJ"
        public string Program { get; set; } = string.Empty;
        public string Status { get; set; } = "Active"; // Active, OnLeave, etc.
        public int MaxUnits { get; set; } = 24; // Default to 24
        // This stores the days/times as a JSON string or comma-separated list
        // e.g., "Monday,Wednesday|8:00-17:00"
        public string SchedulePreferences { get; set; } = string.Empty;

        public bool IsScheduleLocked { get; set; } = false;


        // NEW: Preferred Year Levels (Stored as "1,2" or "3,4")
        public string PreferredYearLevels { get; set; } = string.Empty; 

        // NEW: Assigned "Home" Room
        public int? AssignedRoomId { get; set; }

        [ForeignKey("AssignedRoomId")]
        public Room? AssignedRoom { get; set; }
    }
}