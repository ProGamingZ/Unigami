using System.ComponentModel.DataAnnotations;

namespace UniversityScheduler.Data
{
   public class DegreeProgram
   {
      [Key]
      public int Id { get; set; }
      
      public string Code { get; set; } = string.Empty; // e.g., "BSCS"
      public string Description { get; set; } = string.Empty; // e.g., "Computer Science"
      
      public string FullDisplay => $"{Code} - {Description}";
   }
}