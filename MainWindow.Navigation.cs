using System.Windows;
using UniversityScheduler.Views;

namespace UniversityScheduler
{
   public partial class MainWindow : Window
   {
      private void OpenInstructorsWindow_Click(object sender, RoutedEventArgs e) 
      { 
         if (_instructorsWindow != null)
         {
            _instructorsWindow.Activate(); // Bring to front
            if (_instructorsWindow.WindowState == WindowState.Minimized) 
               _instructorsWindow.WindowState = WindowState.Normal; // Un-minimize
            return;
         }
         _instructorsWindow = new Window 
         { 
            Title = "Instructors", 
            Content = new InstructorsView(_currentSemester), 
            Width = 1000, 
            Height = 600, 
            Topmost = GlobalSettings.InstructorsOnTop 
         };
         _instructorsWindow.Closed += (s, args) => _instructorsWindow = null;
         _instructorsWindow.Show();
      }
      private void OpenCoursesWindow_Click(object sender, RoutedEventArgs e) 
      { 
         if (_coursesWindow != null)
         {
            _coursesWindow.Activate();
            if (_coursesWindow.WindowState == WindowState.Minimized) 
               _coursesWindow.WindowState = WindowState.Normal;
            return;
         }

         _coursesWindow = new Window 
         { 
            Title = "Courses", 
            Content = new CoursesView(), 
            Width = 900, 
            Height = 600, 
            Topmost = GlobalSettings.CoursesOnTop 
         };

         _coursesWindow.Closed += (s, args) => _coursesWindow = null;
         _coursesWindow.Show(); 
      }        
      private void OpenCurriculumWindow_Click(object sender, RoutedEventArgs e) 
      { 
         if (_curriculumWindow != null)
         {
            _curriculumWindow.Activate();
            if (_curriculumWindow.WindowState == WindowState.Minimized) 
               _curriculumWindow.WindowState = WindowState.Normal;
            return;
         }

         _curriculumWindow = new Window 
         { 
            Title = "Curriculum Management", 
            Content = new Views.CurriculumManagerView(_currentSemester), 
            Width = 1000, 
            Height = 650, 
            Topmost = GlobalSettings.CoursesOnTop 
         };

         _curriculumWindow.Closed += (s, args) => _curriculumWindow = null;
         _curriculumWindow.Show(); 
      }
      private void OpenClassesWindow_Click(object sender, RoutedEventArgs e) 
      { 
         if (_classesWindow != null)
         {
            _classesWindow.Activate();
            if (_classesWindow.WindowState == WindowState.Minimized) 
               _classesWindow.WindowState = WindowState.Normal;
            return;
         }

         _classesWindow = new Window 
         { 
            Title = "Classes", 
            Content = new SectionsView(_currentSemester), 
            Width = 800, 
            Height = 600, 
            Topmost = GlobalSettings.ClassesOnTop 
         };

         _classesWindow.Closed += (s, args) => _classesWindow = null;
         _classesWindow.Show(); 
      }
      private void OpenRoomsWindow_Click(object sender, RoutedEventArgs e) 
      { 
         if (_roomsWindow != null)
         {
            _roomsWindow.Activate();
            if (_roomsWindow.WindowState == WindowState.Minimized) 
               _roomsWindow.WindowState = WindowState.Normal;
            return;
         }

         _roomsWindow = new Window 
         { 
            Title = "Rooms", 
            Content = new RoomsView(), 
            Width = 800, 
            Height = 600, 
            Topmost = GlobalSettings.RoomsOnTop 
         };

         _roomsWindow.Closed += (s, args) => _roomsWindow = null;
         _roomsWindow.Show(); 
      }     
      
      private void OpenStatsWindow_Click(object sender, RoutedEventArgs e) 
      { 
         if (_statsWindow != null)
         {
            _statsWindow.Activate();
            if (_statsWindow.WindowState == WindowState.Minimized) 
               _statsWindow.WindowState = WindowState.Normal;
            return;
         }
         _statsWindow = new Window 
         { 
            Title = "Stats", 
            Content = new StatsView(), 
            Width = 500, 
            Height = 450, 
            Topmost = GlobalSettings.StatsOnTop 
         };

         _statsWindow.Closed += (s, args) => _statsWindow = null;
         _statsWindow.Show(); 
      }
      private void OpenGeneratorWindow_Click(object sender, RoutedEventArgs e) 
      { 
         if (_generatorWindow != null)
         {
            _generatorWindow.Activate();
            if (_generatorWindow.WindowState == WindowState.Minimized) 
               _generatorWindow.WindowState = WindowState.Normal;
            return;
         }
         _generatorWindow = new Window 
         { 
            Title = "Generator", 
            Content = new MasterScheduleView(), 
            Width = 1000, Height = 700, 
            Topmost = GlobalSettings.GenerateOnTop 
         };
         _generatorWindow.Closed += (s, args) => _generatorWindow = null;
         _generatorWindow.Show(); 
      }
      private void OpenSettingsWindow_Click(object sender, RoutedEventArgs e) 
      { 
         var win = new Views.SettingsWindow(); 
         win.Owner = this; 
         win.Topmost = true;
         win.ShowDialog(); 
            
      }
      private void SemToggleButton_Click(object sender, RoutedEventArgs e) 
      { 
         _currentSemester = (_currentSemester == 1) ? 2 : 1; 
         SemToggleButton.Content = $"Sem {_currentSemester}"; 

         // 2. Refresh Instructor Table
         RefreshSchedule(); 

         // 3. Refresh Class Table (Middle)
         if (ClassSelector.SelectedItem != null) 
         { 
            dynamic i = ClassSelector.SelectedItem; 
            UpdateClassSchedule(i.OriginalObject); 
         } 

         // 4. NEW: Refresh Room Table (Right)
         if (RoomSelector.SelectedItem is UniversityScheduler.Data.Room selectedRoom)
         {
            UpdateRoomSchedule(selectedRoom);
         } 
      }
   }
}