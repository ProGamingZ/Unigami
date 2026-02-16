namespace UniversityScheduler.Services
{
    public class SchedulingTask
    {
        // initialized to prevent CS8618
        public string TaskId { get; set; } = string.Empty; 
        
        public int SectionId { get; set; }
        public int CourseId { get; set; }
        
        // initialized to prevent CS8618
        public string CourseCode { get; set; } = string.Empty; 
        
        // initialized to prevent CS8618
        public string Component { get; set; } = "Lecture"; 
        
        public int Duration30MinBlocks { get; set; } 
        public int SessionNumber { get; set; }   

        // Make this Nullable '?' because not all tasks have a "Sibling"
        public string? RelatedTaskId { get; set; } 
        public int StudentCount { get; set; } = 0;

        public string Program { get; set; } = ""; 
        public int YearLevel { get; set; } = 1;
    }
}