using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniversityScheduler.Data
{
    public class Curriculum
    {
        [Key]
        public int Id { get; set; }

        public string Program { get; set; } = "BSCS"; 
        public int YearLevel { get; set; } = 1;       
        public int Semester { get; set; } = 1;        

        public int CourseId { get; set; }
        
        // Fix: Add '?' to allow this to be null temporarily during loading
        [ForeignKey("CourseId")] public Course? Course { get; set; }
    }
}