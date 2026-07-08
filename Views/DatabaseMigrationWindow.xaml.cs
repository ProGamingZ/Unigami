using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
   public partial class DatabaseMigrationWindow : Window
   {
      private string _oldDbPath;
      private string _tempDbPath;

      public DatabaseMigrationWindow(string oldDbPath)
      {
         InitializeComponent();
         _oldDbPath = oldDbPath;
         
         // We copy the old DB to a temp file so we don't accidentally corrupt their original backup
         _tempDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "temp_migration.db");
      }

      private async void StartBtn_Click(object sender, RoutedEventArgs e)
      {
         StartBtn.IsEnabled = false;
         
         try
         {
               await Task.Run(() => PerformDataPump());

               // Success State
               StatusText.Text = "Migration Complete! Restart required.";
               StatusText.Foreground = System.Windows.Media.Brushes.Green;
               MigrationProgress.Value = 100;
               
               StartBtn.Visibility = Visibility.Collapsed;
               RestartBtn.Visibility = Visibility.Visible;
         }
         catch (Exception ex)
         {
               MessageBox.Show($"Migration failed: {ex.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
               StatusText.Text = "Migration Failed.";
               StatusText.Foreground = System.Windows.Media.Brushes.Red;
               StartBtn.IsEnabled = true;
         }
         finally
         {
            // Force Entity Framework and SQLite to drop all background file locks
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            System.Threading.Thread.Sleep(200); // Give Windows a split second to release

            // Cleanup temp file safely
            try
            {
               if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
            }
            catch 
            { 
               // If Windows is being extremely stubborn, ignore it. 
               // It's just a temp file that will get overwritten next time anyway.
            }
         }
      }

      private void PerformDataPump()
      {
         UpdateStatus(10, "Creating safe temporary backup...");
         File.Copy(_oldDbPath, _tempDbPath, true);

         UpdateStatus(30, "Analyzing old database structure...");
         using (var conn = new SqliteConnection($"Data Source={_tempDbPath};Pooling=False;"))
         {
            conn.Open();
            using (var cmd = new SqliteCommand("SELECT count(*) FROM sqlite_master WHERE type='table' AND name='Rooms'", conn))
            {
               long tableExists = (long)cmd.ExecuteScalar();
               if (tableExists == 0)
               {
                  throw new Exception("Invalid File. This is not a valid Unigami schedule.db backup.");
               }
            }
         }
         
         bool hasPreferredCourseCol = false;

         // 1. Analyze the old DB using raw SQLite to see if it's missing the new column
         using (var conn = new SqliteConnection($"Data Source={_tempDbPath};Pooling=False;"))
         {
            conn.Open();
            using (var cmd = new SqliteCommand("PRAGMA table_info(Instructors)", conn))
            using (var reader = cmd.ExecuteReader())
            {
               while (reader.Read())
               {
                     if (reader.GetString(1) == "PreferredCourseCodes") hasPreferredCourseCol = true;
               }
            }
         }

         UpdateStatus(50, "Preparing new database schema...");
         
         using (var db = new AppDbContext())
         {
            // 2. Wipe the current DB and recreate the PERFECT new schema
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            UpdateStatus(70, "Pumping data into new schema...");

            // 3. Attach the old DB directly to the SQL engine for ultra-fast transferring
            db.Database.ExecuteSql($"ATTACH DATABASE '{_tempDbPath}' AS OldDb");

            // Turn off foreign keys temporarily just in case order gets weird
            db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

            // Pump standard tables
            db.Database.ExecuteSqlRaw(
               "INSERT INTO main.Rooms (Id, Name, Type, Capacity, FloorLevel, ChairCount, TableCount, CeilingFanCount, StandFanCount, AirConCount, WhiteboardCount, MonitorCount, ComputerCount, ProjectorCount) " +
               "SELECT Id, Name, Type, Capacity, FloorLevel, ChairCount, TableCount, CeilingFanCount, StandFanCount, AirConCount, WhiteboardCount, MonitorCount, ComputerCount, ProjectorCount FROM OldDb.Rooms");

            db.Database.ExecuteSqlRaw(
               "INSERT INTO main.Courses (Id, Code, Name, Units, LectureHours, LabHours, PrerequisiteCodes, RecommendedPrograms) " +
               "SELECT Id, Code, Name, Units, LectureHours, LabHours, PrerequisiteCodes, RecommendedPrograms FROM OldDb.Courses");

            db.Database.ExecuteSqlRaw(
               "INSERT INTO main.Sections (Id, Program, YearLevel, Name, StudentCount) " +
               "SELECT Id, Program, YearLevel, Name, StudentCount FROM OldDb.Sections");

            db.Database.ExecuteSqlRaw(
               "INSERT INTO main.Curriculums (Id, Program, YearLevel, Semester, CourseId) " +
               "SELECT Id, Program, YearLevel, Semester, CourseId FROM OldDb.Curriculums");

            db.Database.ExecuteSqlRaw(
               "INSERT INTO main.Schedules (Id, RoomId, SectionId, CourseId, InstructorId, Day, StartTime, EndTime, Semester, Component) " +
               "SELECT Id, RoomId, SectionId, CourseId, InstructorId, Day, StartTime, EndTime, Semester, Component FROM OldDb.Schedules");

            // 4. Pump Instructors (Handling the dynamically added PreferredCourseCodes)
            if (hasPreferredCourseCol)
            {
               db.Database.ExecuteSqlRaw(
                  "INSERT INTO main.Instructors (Id, Name, Initials, Program, Status, MaxUnits, SchedulePreferences, IsScheduleLocked, PreferredYearLevels, AssignedRoomId, PreferredCourseCodes) " +
                  "SELECT Id, Name, Initials, Program, Status, MaxUnits, SchedulePreferences, IsScheduleLocked, PreferredYearLevels, AssignedRoomId, PreferredCourseCodes FROM OldDb.Instructors");
            }
            else
            {
               db.Database.ExecuteSqlRaw(
                  "INSERT INTO main.Instructors (Id, Name, Initials, Program, Status, MaxUnits, SchedulePreferences, IsScheduleLocked, PreferredYearLevels, AssignedRoomId, PreferredCourseCodes) " +
                  "SELECT Id, Name, Initials, Program, Status, MaxUnits, SchedulePreferences, IsScheduleLocked, PreferredYearLevels, AssignedRoomId, '' FROM OldDb.Instructors");
            }

            // 4. Pump Instructors (Handling the missing column dynamically)
            if (hasPreferredCourseCol)
            {
               db.Database.ExecuteSqlRaw("INSERT INTO main.Instructors SELECT * FROM OldDb.Instructors");
            }
            else
            {
               // If it's an old DB, we explicitly list the columns and inject an empty string ('') into PreferredCourseCodes
               string pumpCommand = 
                  @"INSERT INTO main.Instructors 
                  (Id, Name, Initials, Program, Status, MaxUnits, SchedulePreferences, IsScheduleLocked, PreferredYearLevels, AssignedRoomId, PreferredCourseCodes) 
                  SELECT Id, Name, Initials, Program, Status, MaxUnits, SchedulePreferences, IsScheduleLocked, PreferredYearLevels, AssignedRoomId, '' 
                  FROM OldDb.Instructors";
               
               db.Database.ExecuteSqlRaw(pumpCommand);
            }

            // Restore constraints and detach
            db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
            db.Database.ExecuteSqlRaw("DETACH DATABASE OldDb");
         }

         UpdateStatus(90, "Finalizing settings...");
         System.Threading.Thread.Sleep(500); // Tiny pause so the UI feels smooth
      }

      private void UpdateStatus(int progress, string message)
      {
         Dispatcher.Invoke(() => 
         {
               MigrationProgress.Value = progress;
               StatusText.Text = message;
         });
      }

      private void RestartBtn_Click(object sender, RoutedEventArgs e)
      {
         // Safely restarts the WPF application
         var exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
         if (exeName != null)
         {
               System.Diagnostics.Process.Start(exeName);
         }
         Application.Current.Shutdown();
      }
   }
}