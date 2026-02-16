using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniversityScheduler.Data
{
    public class StudentSection
    {
        [Key]
        public int Id { get; set; }
        
        public string Program { get; set; } = "BSCS"; // BSCS, BSIT, BSIS, BSEMC
        public int YearLevel { get; set; } = 1;       // 1, 2, 3, 4
        public string Name { get; set; } = string.Empty; // e.g. "A", "B", "1A"
        public int StudentCount { get; set; } = 40;   // Default capacity
        
        // Helper to show full name in lists later (e.g. "BSCS 1-A")
        public string FullDisplayName => $"{Program} {YearLevel}-{Name}";
        
        [NotMapped]
        public string UnitsDisplay { get; set; } = "-";
    }
}