using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Runtime.InteropServices; 

namespace UniversityScheduler.Services
{
    public class SchedulerSettings
    {
        // --- 1. GENERAL CONSTRAINTS ---
        public bool AvoidLunchBreak { get; set; } = true;
        public int DayStartHour { get; set; } = 7; 
        public int DayEndHour { get; set; } = 21;  

        // --- 2. NEW FLEXIBLE RULE LISTS ---
        public List<DayConstraint> DayRules { get; set; } = new List<DayConstraint>();
        public List<TimeConstraint> TimeRules { get; set; } = new List<TimeConstraint>();
        public List<string> ExcludedCourses { get; set; } = new List<string>();
        public bool EnableBlockSplitting { get; set; } = true; 
        public List<string> SplittingExceptions { get; set; } = new List<string>(); 
        public string SiblingPattern { get; set; } = "Strict"; 

        // --- 3. PERFORMANCE SETTINGS ---
        public int MaxCalculationTimeSeconds { get; set; } = 600; 
        public int MaxSearchWorkers { get; set; } = 2; 

        // --- 4. HARDWARE INFO ---
        [System.Text.Json.Serialization.JsonIgnore]
        public string SystemInfoSummary { get; private set; } = "Detecting...";
        [System.Text.Json.Serialization.JsonIgnore]
        public int DetectedCores { get; private set; } = 1;

        // Path: .../Data/scheduler_settings.json
        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "scheduler_settings.json");

        public SchedulerSettings()
        {
            ScanHardware();
        }

        public void ScanHardware()
        {
            DetectedCores = Environment.ProcessorCount;

            var memStatus = new MEMORYSTATUSEX();
            long freeRamMb = 0;
            long totalRamMb = 0;

            if (GlobalMemoryStatusEx(memStatus))
            {
                freeRamMb = (long)(memStatus.ullAvailPhys / 1024 / 1024);
                totalRamMb = (long)(memStatus.ullTotalPhys / 1024 / 1024);
            }
            else
            {
                // Fallback if API fails (rare)
                var gcInfo = GC.GetGCMemoryInfo();
                totalRamMb = gcInfo.TotalAvailableMemoryBytes / 1024 / 1024;
                freeRamMb = totalRamMb; // Just assume full if we can't read OS
            }

            SystemInfoSummary = $"Detected: {DetectedCores} CPU Cores | {freeRamMb / 1024.0:F1} GB Free / {totalRamMb / 1024.0:F1} GB Total";

            // AUTO-TUNING LOGIC
            bool isDangerous = MaxSearchWorkers > DetectedCores;
            
            if (MaxSearchWorkers == 2 || isDangerous)
            {
                // If less than 4GB of *ACTUAL FREE* RAM, force low mode
                if (freeRamMb < 4000) 
                    MaxSearchWorkers = 1; // Low End
                else if (isDangerous)
                    MaxSearchWorkers = Math.Max(1, DetectedCores - 1); 
                else if (DetectedCores >= 8 && freeRamMb > 8000) 
                    MaxSearchWorkers = 4; // High End
                else 
                    MaxSearchWorkers = Math.Max(1, DetectedCores / 2); // Standard
            }
        }

        public static SchedulerSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<SchedulerSettings>(json);
                    
                    if (settings != null)
                    {
                        settings.ScanHardware();
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading scheduler settings: {ex.Message}");
            }

            var defaults = new SchedulerSettings();
            defaults.Save(); 
            return defaults;
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);

                string? directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save Scheduler Settings!\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
    }

    // --- HELPER CLASSES ---
    public class DayConstraint
    {
        public string CoursePrefix { get; set; } = ""; 
        public List<string> AllowedDays { get; set; } = new List<string>(); 
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplayString => $"{CoursePrefix} → {string.Join(", ", AllowedDays)}";
    }

    public class TimeConstraint
    {
        public string CoursePrefix { get; set; } = ""; 
        public int LatestEndHour { get; set; } 

        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplayString 
        {
            get 
            {
                string ampm = LatestEndHour >= 12 ? "PM" : "AM";
                int h = LatestEndHour > 12 ? LatestEndHour - 12 : LatestEndHour;
                return $"{CoursePrefix} → End by {h}:00 {ampm}";
            }
        }
    }
}