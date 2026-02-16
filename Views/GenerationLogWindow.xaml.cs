using System;
using System.Windows;

namespace UniversityScheduler.Views
{
    public partial class GenerationLogWindow : Window
    {
        public GenerationLogWindow()
        {
            InitializeComponent();
        }

        // This method allows external code to write to the box safely
        public void AddLog(string message)
        {
            // We use Dispatcher because this might be called from a background thread
            Dispatcher.Invoke(() => 
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogBox.AppendText($"[{timestamp}] {message}\n");
                LogBox.ScrollToEnd(); // Auto-scroll to bottom
            });
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}