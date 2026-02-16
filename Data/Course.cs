using System.ComponentModel.DataAnnotations;

namespace UniversityScheduler.Data
{
    public class Course
    {
        [Key]
        public int Id { get; set; }
        
        public string Code { get; set; } = string.Empty; // e.g., "CS101"
        public string Name { get; set; } = string.Empty; // e.g., "Intro to Computing"
        
        public int Units { get; set; } = 3;
        public int LectureHours { get; set; } = 3;
        public int LabHours { get; set; } = 0;

        // Stores codes of required subjects: "CS101,Math101"
        public string PrerequisiteCodes { get; set; } = string.Empty; 

        // Helper to categorize (e.g., "BSCS, BSIT") - helps with your sorting requirement
        public string RecommendedPrograms { get; set; } = string.Empty; 
    }
}