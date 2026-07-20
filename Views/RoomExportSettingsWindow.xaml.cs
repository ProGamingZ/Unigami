using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
    public class RoomTypeMapping
    {
        public string RoomType { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
    }

    public class RoomExportConfig
    {
        public string UniversityName { get; set; } = "UNIVERSITY NAME";
        public string DepartmentName { get; set; } = "DEPARTMENT NAME";
        public List<RoomTypeMapping> Mappings { get; set; } = new List<RoomTypeMapping>();
    }

    public partial class RoomExportSettingsWindow : Window
    {
        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "room_export_settings.json");
        public RoomExportConfig CurrentConfig { get; set; } = new RoomExportConfig();

        public RoomExportSettingsWindow()
        {
            InitializeComponent();
            LoadConfig();
        }

        private void LoadConfig()
        {
            // 1. Load Saved Settings
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var saved = JsonSerializer.Deserialize<RoomExportConfig>(json);
                    if (saved != null) CurrentConfig = saved;
                }
                catch { }
            }

            UniNameTxt.Text = CurrentConfig.UniversityName;
            DeptNameTxt.Text = CurrentConfig.DepartmentName;

            // 2. Scan Database for Active Room Types
            using (var db = new AppDbContext())
            {
                var dbTypes = db.Rooms.Select(r => r.Type).Distinct().ToList();
                var displayList = new List<RoomTypeMapping>();

                foreach (var type in dbTypes)
                {
                    if (string.IsNullOrWhiteSpace(type)) continue;
                    
                    var existing = CurrentConfig.Mappings.FirstOrDefault(m => m.RoomType == type);
                    if (existing != null)
                    {
                        displayList.Add(existing);
                    }
                    else
                    {
                        // Default fallback (e.g. "Classroom" defaults to "Room")
                        string defPrefix = type.Contains("Lab") ? "Lab" : "Room";
                        displayList.Add(new RoomTypeMapping { RoomType = type, Prefix = defPrefix });
                    }
                }
                TypePrefixesList.ItemsSource = displayList;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            CurrentConfig.UniversityName = UniNameTxt.Text;
            CurrentConfig.DepartmentName = DeptNameTxt.Text;
            CurrentConfig.Mappings = TypePrefixesList.ItemsSource.Cast<RoomTypeMapping>().ToList();

            try
            {
                string json = JsonSerializer.Serialize(CurrentConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
                MessageBox.Show("Room Export Settings Saved Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Helper Method used by MainWindow to fetch settings
        public static RoomExportConfig GetConfig()
        {
            if (File.Exists(ConfigPath))
            {
                try { return JsonSerializer.Deserialize<RoomExportConfig>(File.ReadAllText(ConfigPath)) ?? new RoomExportConfig(); }
                catch { }
            }
            return new RoomExportConfig();
        }
    }
}