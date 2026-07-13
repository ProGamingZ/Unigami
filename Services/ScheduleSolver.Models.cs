using System.Collections.Generic;
using Google.OrTools.Sat;
using UniversityScheduler.Data;

namespace UniversityScheduler.Services
{
   public partial class ScheduleSolver
   {
      private class ModelData
      {
         public List<AssignmentVar> Assignments { get; set; } = new List<AssignmentVar>();
         public Dictionary<(int SectionId, int Day, int Slot), List<BoolVar>> SectionsAtSlot { get; set; } = new Dictionary<(int, int, int), List<BoolVar>>();
         public Dictionary<(int RoomId, int Day, int Slot), List<BoolVar>> RoomsAtSlot { get; set; } = new Dictionary<(int, int, int), List<BoolVar>>();
         public Dictionary<(int InstructorId, int Day, int Slot), List<BoolVar>> InstructorsAtSlot { get; set; } = new Dictionary<(int, int, int), List<BoolVar>>();

         public List<BoolVar> Assumptions { get; set; } = new List<BoolVar>();
         public Dictionary<int, SchedulingTask> SiblingAssumptionMap { get; set; } = new Dictionary<int, SchedulingTask>();
         public Dictionary<int, string> OverlapAssumptionMap { get; set; } = new Dictionary<int, string>();
      }
      private class InstructorModelData
      {
         // Maps (Section, Course, Instructor) -> BoolVar
         public Dictionary<(int SectionId, int CourseId, int InstructorId), BoolVar> Assignments { get; set; } = new();
         
         // Maps InstructorId -> List of (Variable, Units)
         public Dictionary<int, List<(BoolVar Var, int Units)>> InstructorLoads { get; set; } = new();

      }
      private class AssignmentVar
      {
         public required SchedulingTask Task { get; set; }
         public required Room Room { get; set; }
         public int Day { get; set; }
         public int StartSlot { get; set; }
         public required BoolVar Variable { get; set; }
      }            

   }
}