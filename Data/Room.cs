using System.ComponentModel.DataAnnotations;

namespace UniversityScheduler.Data
{
    public class Room
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;       // e.g. "Room 301"
        public string Type { get; set; } = "Classroom";        // "Classroom" or "Computer Laboratory"
        public int Capacity { get; set; } = 40;
        public int FloorLevel { get; set; } = 1;               // 1, 2, 3, 4

        // --- Equipment Inventory (0 to 100) ---
        public int ChairCount { get; set; } = 0;
        public int TableCount { get; set; } = 0;
        public int CeilingFanCount { get; set; } = 0;
        public int StandFanCount { get; set; } = 0;
        public int AirConCount { get; set; } = 0;
        public int WhiteboardCount { get; set; } = 0;
        public int MonitorCount { get; set; } = 0;
        public int ComputerCount { get; set; } = 0;
        public int ProjectorCount { get; set; } = 0;
    }
}