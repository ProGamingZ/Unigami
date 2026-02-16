using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace UniversityScheduler
{
    public static class GlobalSettings
    {
        // 🟢 FIX: Ensure the path is valid and points to a 'Data' folder next to the .exe
        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "app_settings.json");

        // --- PUBLIC SETTINGS ---
        public static bool InstructorsOnTop { get; set; }
        public static bool CoursesOnTop { get; set; }
        public static bool ClassesOnTop { get; set; }
        public static bool RoomsOnTop { get; set; }
        public static bool StatsOnTop { get; set; }
        public static bool GenerateOnTop { get; set; }

        public static int StartTimeHour { get; set; } = 7; 
        public static int EndTimeHour { get; set; } = 21;  

        public static DateTime LastAlertDate { get; set; } = DateTime.MinValue;

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var data = JsonSerializer.Deserialize<GlobalSettingsData>(json);
                    if (data != null)
                    {
                        InstructorsOnTop = data.InstructorsOnTop;
                        CoursesOnTop = data.CoursesOnTop;
                        ClassesOnTop = data.ClassesOnTop;
                        RoomsOnTop = data.RoomsOnTop;
                        StatsOnTop = data.StatsOnTop;
                        GenerateOnTop = data.GenerateOnTop;

                        // Load Times (with defaults)
                        StartTimeHour = data.StartTimeHour == 0 ? 7 : data.StartTimeHour;
                        EndTimeHour = data.EndTimeHour == 0 ? 21 : data.EndTimeHour;

                        
                        LastAlertDate = data.LastAlertDate;
                    }
                }
            }
            catch { /* Ignore errors on first run */ }
        }

        public static void Save()
        {
            try
            {
                var data = new GlobalSettingsData
                {
                    InstructorsOnTop = InstructorsOnTop,
                    CoursesOnTop = CoursesOnTop,
                    ClassesOnTop = ClassesOnTop,
                    RoomsOnTop = RoomsOnTop,
                    StatsOnTop = StatsOnTop,
                    GenerateOnTop = GenerateOnTop,
                    StartTimeHour = StartTimeHour,
                    EndTimeHour = EndTimeHour,
                    
                    // 🟢 Save Alert Date
                    LastAlertDate = LastAlertDate
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                
                string? directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings!\nPath: {SettingsPath}\nError: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class GlobalSettingsData
        {
            public bool InstructorsOnTop { get; set; }
            public bool CoursesOnTop { get; set; }
            public bool ClassesOnTop { get; set; }
            public bool RoomsOnTop { get; set; }
            public bool StatsOnTop { get; set; }
            public bool GenerateOnTop { get; set; }
            public int StartTimeHour { get; set; }
            public int EndTimeHour { get; set; }
            public DateTime LastAlertDate { get; set; }
        }
    }
}