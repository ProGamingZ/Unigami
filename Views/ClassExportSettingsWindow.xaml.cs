using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace UniversityScheduler.Views
{
    public class ClassExportConfig
    {
        public string UniversityName { get; set; } = "UNIVERSITY NAME";
        public string DepartmentName { get; set; } = "DEPARTMENT NAME";
    }

    public partial class ClassExportSettingsWindow : Window
    {
        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "class_export_settings.json");
        public ClassExportConfig CurrentConfig { get; set; } = new ClassExportConfig();

        public ClassExportSettingsWindow()
        {
            InitializeComponent();
            LoadConfig();
        }

        private void LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var saved = JsonSerializer.Deserialize<ClassExportConfig>(json);
                    if (saved != null) CurrentConfig = saved;
                }
                catch { }
            }
            UniNameTxt.Text = CurrentConfig.UniversityName;
            DeptNameTxt.Text = CurrentConfig.DepartmentName;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            CurrentConfig.UniversityName = UniNameTxt.Text;
            CurrentConfig.DepartmentName = DeptNameTxt.Text;

            try
            {
                string json = JsonSerializer.Serialize(CurrentConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
                MessageBox.Show("Class Export Settings Saved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static ClassExportConfig GetConfig()
        {
            if (File.Exists(ConfigPath))
            {
                try { return JsonSerializer.Deserialize<ClassExportConfig>(File.ReadAllText(ConfigPath)) ?? new ClassExportConfig(); }
                catch { }
            }
            return new ClassExportConfig();
        }
    }
}