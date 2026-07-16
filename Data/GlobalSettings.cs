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

        static GlobalSettings()
        {
            Load();
        }

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

        // --- EXPORT METADATA ---
        public static string ExportSchoolYear { get; set; } = "2026-2027";
        public static string ExportSemesterText { get; set; } = "First Semester";
        public static string DepartmentName { get; set; } = "CCIS";
        public static bool IsUndergraduate { get; set; } = true;

        public static string Dean1Name { get; set; } = "JUAN DELA CRUZ, Ph.D.";
        public static string Dean2Name { get; set; } = "MARIA CLARA, MIT";
        public static string Dean3Name { get; set; } = "";
        public static string VPAcademicName { get; set; } = "DR. ACADEMIC VP";
        public static string VPResearchName { get; set; } = "DR. RESEARCH VP";
        public static string VPAdminName { get; set; } = "DR. ADMIN VP";
        public static string PresidentName { get; set; } = "DR. UNIVERSITY PRESIDENT";

        // --- EXPORT DATES ---
        public static string DPTLCode { get; set; } = "DPTL";
        public static bool Date0UseToday { get; set; } = false;
        public static string Date0Text { get; set; } = "August 1, 2025";
        public static bool Date1UseToday { get; set; } = true;
        public static string Date1Text { get; set; } = "";
        public static bool Date2UseToday { get; set; } = true;
        public static string Date2Text { get; set; } = "";
        public static bool Date3UseToday { get; set; } = true;
        public static string Date3Text { get; set; } = "";
        public static bool Date4UseToday { get; set; } = true;
        public static string Date4Text { get; set; } = "";
        public static bool Date5UseToday { get; set; } = true;
        public static string Date5Text { get; set; } = "";
        public static bool Date6UseToday { get; set; } = true;
        public static string Date6Text { get; set; } = "";

        // --- EXPORT MATH & FORMULAS ---
        public static int WeeksPerSemester { get; set; } = 18;
        public static string FormulaLecHours { get; set; } = "=SUM(BD19:BD46)";
        public static string FormulaLabHours { get; set; } = "=SUM(BE19:BE46)";
        public static string FormulaContactHours { get; set; } = "=SUM(AW19:AW46)";

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    
                    // Added options to ensure it reads the data regardless of casing issues
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var data = JsonSerializer.Deserialize<GlobalSettingsData>(json, options);
                    
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

                        ExportSchoolYear = data.ExportSchoolYear ?? "2026-2027";
                        ExportSemesterText = data.ExportSemesterText ?? "First Semester";
                        DepartmentName = data.DepartmentName ?? "CCIS";
                        IsUndergraduate = data.IsUndergraduate;
                        Dean1Name = data.Dean1Name ?? "JUAN DELA CRUZ, Ph.D.";
                        Dean2Name = data.Dean2Name ?? "MARIA CLARA, MIT";
                        Dean3Name = data.Dean3Name ?? "";
                        VPAcademicName = data.VPAcademicName ?? "DR. ACADEMIC VP";
                        VPResearchName = data.VPResearchName ?? "DR. RESEARCH VP";
                        VPAdminName = data.VPAdminName ?? "DR. ADMIN VP";
                        PresidentName = data.PresidentName ?? "DR. UNIVERSITY PRESIDENT";

                        DPTLCode = data.DPTLCode ?? "DPTL";
                        Date0UseToday = data.Date0UseToday;
                        Date0Text = data.Date0Text ?? "August 1, 2025";
                        Date1UseToday = data.Date1UseToday;
                        Date1Text = data.Date1Text ?? "";
                        Date2UseToday = data.Date2UseToday;
                        Date2Text = data.Date2Text ?? "";
                        Date3UseToday = data.Date3UseToday;
                        Date3Text = data.Date3Text ?? "";
                        Date4UseToday = data.Date4UseToday;
                        Date4Text = data.Date4Text ?? "";
                        Date5UseToday = data.Date5UseToday;
                        Date5Text = data.Date5Text ?? "";
                        Date6UseToday = data.Date6UseToday;
                        Date6Text = data.Date6Text ?? "";

                        WeeksPerSemester = data.WeeksPerSemester == 0 ? 18 : data.WeeksPerSemester;
                        FormulaLecHours = data.FormulaLecHours ?? "=SUM(BD19:BD46)";
                        FormulaLabHours = data.FormulaLabHours ?? "=SUM(BE19:BE46)";
                        FormulaContactHours = data.FormulaContactHours ?? "=SUM(AW19:AW46)";                        
                        
                        LastAlertDate = data.LastAlertDate;

                        MasterSchoolYear = data.MasterSchoolYear ?? "SY: 2025-2026";
                        MasterDateText = data.MasterDateText ?? "";
                        MasterDeptName = data.MasterDeptName ?? "COLLEGE OF COMPUTING AND INFORMATION SCIENCES";
                        MasterDeptAcronym = data.MasterDeptAcronym ?? "CCIS";
                        MasterSecName = data.MasterSecName ?? "";
                        MasterSecPos = data.MasterSecPos ?? "SECRETARY";
                        MasterDeanName = data.MasterDeanName ?? "";
                        MasterDeanPos = data.MasterDeanPos ?? "DEAN";
                    }
                }
            }
            catch (Exception ex) 
            { 
                // Un-silenced the catch block so you know if the JSON crashes
                MessageBox.Show($"Failed to load settings file!\n\nThe app will use default settings.\nError: {ex.Message}", "Settings Load Error", MessageBoxButton.OK, MessageBoxImage.Warning); 
            }
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
                    
                    ExportSchoolYear = ExportSchoolYear,
                    ExportSemesterText = ExportSemesterText,
                    DepartmentName = DepartmentName,
                    IsUndergraduate = IsUndergraduate,
                    Dean1Name = Dean1Name,
                    Dean2Name = Dean2Name,
                    Dean3Name = Dean3Name,
                    VPAcademicName = VPAcademicName,
                    VPResearchName = VPResearchName,
                    VPAdminName = VPAdminName,
                    PresidentName = PresidentName,
                    DPTLCode = DPTLCode,

                    Date0UseToday = Date0UseToday,
                    Date0Text = Date0Text,
                    Date1UseToday = Date1UseToday,
                    Date1Text = Date1Text,
                    Date2UseToday = Date2UseToday,
                    Date2Text = Date2Text,
                    Date3UseToday = Date3UseToday,
                    Date3Text = Date3Text,
                    Date4UseToday = Date4UseToday,
                    Date4Text = Date4Text,
                    Date5UseToday = Date5UseToday,
                    Date5Text = Date5Text,
                    Date6UseToday = Date6UseToday,
                    Date6Text = Date6Text,

                    WeeksPerSemester = WeeksPerSemester,
                    FormulaLecHours = FormulaLecHours,
                    FormulaLabHours = FormulaLabHours,
                    FormulaContactHours = FormulaContactHours,

                    LastAlertDate = LastAlertDate,

                    MasterSchoolYear = MasterSchoolYear,
                    MasterDateText = MasterDateText,
                    MasterDeptName = MasterDeptName,
                    MasterDeptAcronym = MasterDeptAcronym,
                    MasterSecName = MasterSecName,
                    MasterSecPos = MasterSecPos,
                    MasterDeanName = MasterDeanName,
                    MasterDeanPos = MasterDeanPos,
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


        public static string MasterSchoolYear { get; set; } = "SY: 2025-2026";
        public static string MasterDateText { get; set; } = "";
        public static string MasterDeptName { get; set; } = "COLLEGE OF COMPUTING AND INFORMATION SCIENCES";
        public static string MasterDeptAcronym { get; set; } = "CCIS";
        public static string MasterSecName { get; set; } = "";
        public static string MasterSecPos { get; set; } = "SECRETARY";
        public static string MasterDeanName { get; set; } = "";
        public static string MasterDeanPos { get; set; } = "DEAN";

        public class GlobalSettingsData
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

            public string? ExportSchoolYear { get; set; }
            public string? ExportSemesterText { get; set; }
            public string? DepartmentName { get; set; }
            public bool IsUndergraduate { get; set; }
            public string? Dean1Name { get; set; }
            public string? Dean2Name { get; set; }
            public string? Dean3Name { get; set; }
            public string? VPAcademicName { get; set; }
            public string? VPResearchName { get; set; }
            public string? VPAdminName { get; set; }
            public string? PresidentName { get; set; }

            public string? DPTLCode { get; set; }
            public bool Date0UseToday { get; set; }
            public string? Date0Text { get; set; }
            public bool Date1UseToday { get; set; }
            public string? Date1Text { get; set; }
            public bool Date2UseToday { get; set; }
            public string? Date2Text { get; set; }
            public bool Date3UseToday { get; set; }
            public string? Date3Text { get; set; }
            public bool Date4UseToday { get; set; }
            public string? Date4Text { get; set; }
            public bool Date5UseToday { get; set; }
            public string? Date5Text { get; set; }
            public bool Date6UseToday { get; set; }
            public string? Date6Text { get; set; }

            public int WeeksPerSemester { get; set; }
            public string? FormulaLecHours { get; set; }
            public string? FormulaLabHours { get; set; }
            public string? FormulaContactHours { get; set; }

            public string? MasterSchoolYear { get; set; }
            public string? MasterDateText { get; set; }
            public string? MasterDeptName { get; set; }
            public string? MasterDeptAcronym { get; set; }
            public string? MasterSecName { get; set; }
            public string? MasterSecPos { get; set; }
            public string? MasterDeanName { get; set; }
            public string? MasterDeanPos { get; set; }
        }
    }
}