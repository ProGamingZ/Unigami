using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls; // Required for Button
using System.Windows.Input;    // Required for Mouse events

namespace UniversityScheduler
{
    public partial class App : Application
    {
        //  FLIGHT RECORDER: Stores the last thing the user did
        private static string _lastUserAction = "Application Started";
        private static string _lastActiveWindow = "None";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. GLOBAL LISTENER: Watch for ANY button click in the ENTIRE app
            // This is a "Global Hook" that runs before the actual button code runs.
            EventManager.RegisterClassHandler(typeof(Button), System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(LogUserClick));

            // 2. GLOBAL LISTENER: Watch for Window Focus changes
            EventManager.RegisterClassHandler(typeof(Window), Window.GotFocusEvent, new RoutedEventHandler(LogWindowFocus));
        }

        // This runs silently every time a user clicks ANY button
        private void LogUserClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement element)
            {
                // Try to get a meaningful name (Name, Content, or Tooltip)
                string name = element.Name;
                if (string.IsNullOrEmpty(name) && element is ContentControl cc) 
                    name = cc.Content?.ToString() ?? "Unknown Button";
                
                _lastUserAction = $"Clicked Button: '{name}'";
            }
        }

        // This runs silently when a user switches windows
        private void LogWindowFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Window win)
            {
                _lastActiveWindow = win.Title;
            }
        }

        //  THE GLOBAL CATCH-ALL
      private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
      {
         // 1. Prevent the crash immediately
         e.Handled = true;

         // 2. Build the message (Flight Recorder logic)
         if (_lastActiveWindow == "None" || _lastActiveWindow == "")
         {
            var activeWin = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);
            _lastActiveWindow = activeWin?.Title ?? "Unknown Context";
         }

         string errorMsg = $"An unexpected error occurred in {_lastActiveWindow}.\n" +
                           $"Last Action: {_lastUserAction}\n\n" +
                           $"Error: {e.Exception.Message}\n\n" +
                           "Do you want to close the application?\n" +
                           "(Click 'Yes' to Restart, 'No' to try and continue)";

         // 3. ASK THE USER
         var result = MessageBox.Show(errorMsg, "Unexpected Error", MessageBoxButton.YesNo, MessageBoxImage.Error);

         // 4. Act on their choice
         if (result == MessageBoxResult.Yes)
         {
            Environment.Exit(1); // User chose to die
         }
         // If 'No', we do nothing. e.Handled = true keeps the app alive.
      }
    }
}