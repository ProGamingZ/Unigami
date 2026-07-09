using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniversityScheduler.Data
{
    public class Instructor
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public string FullName 
        {
            get
            {
                string t = string.IsNullOrWhiteSpace(Title) ? "" : $"{Title} ";
                string m = string.IsNullOrWhiteSpace(MiddleName) ? "" : $" {MiddleName}";
                string s = string.IsNullOrWhiteSpace(Suffix) ? "" : $" {Suffix}";
                return $"{t}{FirstName}{m} {Surname}{s}".Trim();
            }
        }

        public string Initials { get; set; } = string.Empty; // e.g. "SJ"
        public string Program { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string HomeAddress { get; set; } = string.Empty;
        public string BaccalaureateDegree { get; set; } = string.Empty;
        public string MastersDegree { get; set; } = string.Empty;
        public string DoctoralDegree { get; set; } = string.Empty;
        public int ExperiencePublic { get; set; } = 0;
        public int ExperiencePrivate { get; set; } = 0;
        public int MaxUnits { get; set; } = 24; // Default to 24
        // This stores the days/times as a JSON string or comma-separated list
        // e.g., "Monday,Wednesday|8:00-17:00"
        public string SchedulePreferences { get; set; } = string.Empty;

        public bool IsScheduleLocked { get; set; } = false;


        //Preferred Year Levels (Stored as "1,2" or "3,4")
        public string PreferredYearLevels { get; set; } = string.Empty; 
        //Preferred Course Codes (Stored as "CS101,IT102")
        public string PreferredCourseCodes { get; set; } = string.Empty;

        // NEW: Assigned "Home" Room
        public int? AssignedRoomId { get; set; }

        [ForeignKey("AssignedRoomId")]
        public Room? AssignedRoom { get; set; }
    }
}