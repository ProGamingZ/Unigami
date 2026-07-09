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

         UpdateStatus(30, "Analyzing database structures...");

         using (var db = new AppDbContext())
         {
            // 1. Wipe current DB and recreate perfect new schema from your C# models
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            UpdateStatus(50, "Calculating schema differences...");

            var connection = db.Database.GetDbConnection();
            connection.Open();

            try
            {
               using (var command = connection.CreateCommand())
               {
                  // 2. Attach Old Database
                  command.CommandText = $"ATTACH DATABASE '{_tempDbPath}' AS OldDb";
                  command.ExecuteNonQuery();

                  // 3. Turn off foreign keys to prevent order-of-insertion crashes
                  command.CommandText = "PRAGMA foreign_keys = OFF;";
                  command.ExecuteNonQuery();

                  // 4. Get all tables that exist in the NEW perfectly generated database
                  var tables = new List<string>();
                  command.CommandText = "SELECT name FROM main.sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                  using (var reader = command.ExecuteReader())
                  {
                        while (reader.Read()) tables.Add(reader.GetString(0));
                  }

                  UpdateStatus(70, "Auto-mapping and transferring data...");

                  // 5. Loop through every table dynamically
                  foreach (var table in tables)
                  {
                        var newColumns = GetTableColumns(connection, "main", table);
                        var oldColumns = GetTableColumns(connection, "OldDb", table);

                        // If the table didn't exist in the old DB at all, skip it
                        if (oldColumns.Count == 0) continue;

                        var insertCols = new List<string>();
                        var selectCols = new List<string>();

                        // 6. Compare columns to build the perfect SQL string automatically
                        foreach (var col in newColumns)
                        {
                           insertCols.Add(col.Name);

                           if (oldColumns.Any(o => o.Name == col.Name))
                           {
                              // Column exists in both! Copy it normally.
                              selectCols.Add(col.Name);
                           }
                           else
                           {
                              // Column is MISSING in the old database! Safely inject a default value.
                              if (col.Type.Contains("INT") || col.Type.Contains("NUM") || col.Type.Contains("REAL") || col.Type.Contains("BOOL"))
                                    selectCols.Add("0"); 
                              else
                                    selectCols.Add("''"); // Blank string for text
                           }
                        }

                        // 7. Execute the dynamic transfer for this specific table
                        string insertStr = string.Join(", ", insertCols);
                        string selectStr = string.Join(", ", selectCols);
                        
                        command.CommandText = $"INSERT INTO main.{table} ({insertStr}) SELECT {selectStr} FROM OldDb.{table}";
                        command.ExecuteNonQuery();
                  }

                  // 8. Restore safety constraints and detach
                  command.CommandText = "PRAGMA foreign_keys = ON;";
                  command.ExecuteNonQuery();
                  
                  command.CommandText = "DETACH DATABASE OldDb";
                  command.ExecuteNonQuery();
               }
            }
            finally
            {
               connection.Close();
            }
         }

         UpdateStatus(90, "Finalizing settings...");
         System.Threading.Thread.Sleep(500);
      }

      private List<(string Name, string Type)> GetTableColumns(System.Data.Common.DbConnection conn, string dbName, string tableName)
      {
         var columns = new List<(string Name, string Type)>();
         using (var cmd = conn.CreateCommand())
         {
            cmd.CommandText = $"PRAGMA {dbName}.table_info('{tableName}')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
               // Column 1 is Name, Column 2 is Data Type
               columns.Add((reader.GetString(1), reader.GetString(2).ToUpper()));
            }
         }
         return columns;
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