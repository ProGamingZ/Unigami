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
        public string Initials { get; set; } = string.Empty; 
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
        public string HomeAddress { get; set; } = string.Empty;
        public string BaccalaureateDegree { get; set; } = string.Empty;
        public string MastersDegree { get; set; } = string.Empty;
        public string DoctoralDegree { get; set; } = string.Empty;
        public string AdministrativeDesignation { get; set; } = string.Empty;
        public int ExperiencePublic { get; set; } = 0;
        public int ExperiencePrivate { get; set; } = 0;
        public bool IsScheduleLocked { get; set; } = false;

        // ================= SEMESTER 1 DATA =================
        public string StatusSem1 { get; set; } = "Full-time";
        public int MaxUnitsSem1 { get; set; } = 24;
        public string ProgramSem1 { get; set; } = string.Empty;
        public string SchedulePreferencesSem1 { get; set; } = string.Empty;
        public string PreferredYearLevelsSem1 { get; set; } = string.Empty;
        public string PreferredCourseCodesSem1 { get; set; } = string.Empty;
        public string AssignedSectionsSem1 { get; set; } = string.Empty;
        public int? AssignedRoomIdSem1 { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("AssignedRoomIdSem1")]
        public Room? AssignedRoomSem1 { get; set; }

        // ================= SEMESTER 2 DATA =================
        public string StatusSem2 { get; set; } = "Full-time";
        public int MaxUnitsSem2 { get; set; } = 24;
        public string ProgramSem2 { get; set; } = string.Empty;
        public string SchedulePreferencesSem2 { get; set; } = string.Empty;
        public string PreferredYearLevelsSem2 { get; set; } = string.Empty;
        public string PreferredCourseCodesSem2 { get; set; } = string.Empty;
        public string AssignedSectionsSem2 { get; set; } = string.Empty;
        public int? AssignedRoomIdSem2 { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("AssignedRoomIdSem2")]
        public Room? AssignedRoomSem2 { get; set; }
    }
}