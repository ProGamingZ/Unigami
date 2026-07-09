using System;
using System.Windows.Media;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
   public partial class DatabaseMigrationWindow : Window
   {
      private string _oldDbPath;
      private string _tempDbPath;
      private int _currentStep = 0;
      
      // Caches the old data so the UI can interact with it safely
      private Dictionary<string, DataTable> _oldDataCache = new Dictionary<string, DataTable>();

      public DatabaseMigrationWindow(string oldDbPath)
      {
         InitializeComponent();
         _oldDbPath = oldDbPath;
         _tempDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "temp_migration.db");

         // Attach event to format the DataGrid columns dynamically
         RowSelectionGrid.AutoGeneratingColumn += RowSelectionGrid_AutoGeneratingColumn;

         Loaded += DatabaseMigrationWindow_Loaded;
      }

      private void DatabaseMigrationWindow_Loaded(object sender, RoutedEventArgs e)
      {
         // Create the temp safe copy immediately
         File.Copy(_oldDbPath, _tempDbPath, true);
         WizardTabs.SelectedIndex = 0;
         RunSchemaAnalysis();
      }

      #region Navigation Logic
      private void NextBtn_Click(object sender, RoutedEventArgs e)
      {
         if (_currentStep == 0)
         {
               _currentStep = 1;
               WizardTabs.SelectedIndex = 1;
               BackBtn.Visibility = Visibility.Visible;
               if (MasterTableList.SelectedIndex == -1) MasterTableList.SelectedIndex = 0;
         }
         else if (_currentStep == 1)
         {
               // Move to Final Step & Extract Selected IDs on the UI thread
               if (MessageBox.Show("Are you sure you are ready to import? This will wipe your CURRENT database and replace it with the selected data.", "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
               {
                  _currentStep = 2;
                  WizardTabs.SelectedIndex = 2;
                  BackBtn.Visibility = Visibility.Collapsed;
                  NextBtn.Visibility = Visibility.Collapsed;
                  CancelBtn.Visibility = Visibility.Collapsed;
                  
                  // Extract data settings for the background pump
                  var selectedIds = ExtractSelectedIds();
                  bool importSchedules = ImportSchedulesChk.IsChecked == true;
                  bool importCurriculums = ImportCurriculumsChk.IsChecked == true;

                  // Launch Pump
                  _ = Task.Run(() => PerformDataPump(selectedIds, importSchedules, importCurriculums));
               }
         }
      }

      private void BackBtn_Click(object sender, RoutedEventArgs e)
      {
         if (_currentStep == 1)
         {
               _currentStep = 0;
               WizardTabs.SelectedIndex = 0;
               BackBtn.Visibility = Visibility.Collapsed;
         }
      }

      private void CancelBtn_Click(object sender, RoutedEventArgs e) => Close();
      
      private void FinishBtn_Click(object sender, RoutedEventArgs e)
      {
         var exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
         if (exeName != null) System.Diagnostics.Process.Start(exeName);
         Application.Current.Shutdown();
      }
      #endregion

      #region Step 1 & 2: Schema Analysis & Data Binding
      private void RunSchemaAnalysis()
      {
         var schemaStats = new List<SchemaStatusItem>();

         using (var conn = new SqliteConnection($"Data Source={_tempDbPath};Pooling=False;"))
         {
               conn.Open();
               var tables = new[] { "Instructors", "Rooms", "Courses", "Sections", "Schedules", "Curriculums" };
               
               foreach (var table in tables)
               {
                  try
                  {
                     using var cmd = new SqliteCommand($"SELECT COUNT(*) FROM {table}", conn);
                     int count = Convert.ToInt32(cmd.ExecuteScalar());
                     schemaStats.Add(new SchemaStatusItem { TableName = table, RowCount = count, Status = count > 0 ? "✅ Ready to Migrate" : "⚠️ Empty" });
                  }
                  catch
                  {
                     schemaStats.Add(new SchemaStatusItem { TableName = table, RowCount = 0, Status = "❌ Missing from Backup" });
                  }
               }
         }
         SchemaGrid.ItemsSource = schemaStats;
      }

      private void MasterTableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         if (MasterTableList.SelectedItem is ListBoxItem item && item.Tag != null)
         {
               string tableName = item.Tag.ToString()!;
               RowSelectorGroup.Header = $"Rows for {tableName}";

               if (!_oldDataCache.ContainsKey(tableName))
               {
                  LoadRawTableData(tableName);
               }
               RowSelectionGrid.ItemsSource = _oldDataCache[tableName].DefaultView;
         }
      }

      private void LoadRawTableData(string tableName)
      {
         var dt = new DataTable();
         // Add our custom selection checkbox column first!
         dt.Columns.Add("Import", typeof(bool)).DefaultValue = true; 

         using (var conn = new SqliteConnection($"Data Source={_tempDbPath};Pooling=False;"))
         {
               conn.Open();
               using var cmd = new SqliteCommand($"SELECT * FROM {tableName}", conn);
               try 
               { 
                  using var reader = cmd.ExecuteReader();
                  dt.Load(reader);
               } 
               catch { /* Table missing, leave empty */ }
         }
         
         // Set all to true by default
         foreach (DataRow row in dt.Rows) row["Import"] = true;
         
         _oldDataCache[tableName] = dt;
      }

      // Formats the WPF grid so ONLY the Checkbox is editable
      private void RowSelectionGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
      {
         if (e.PropertyName == "Import")
         {
               e.Column.DisplayIndex = 0; // Force checkbox to the far left
               e.Column.IsReadOnly = false;
         }
         else
         {
               e.Column.IsReadOnly = true;
         }
      }
      #endregion

      #region Step 3: The Relational Data Pump
      private Dictionary<string, List<int>> ExtractSelectedIds()
      {
         var selected = new Dictionary<string, List<int>>();
         foreach (var kvp in _oldDataCache)
         {
               selected[kvp.Key] = new List<int>();
               foreach (DataRow row in kvp.Value.Rows)
               {
                  if ((bool)row["Import"] == true)
                  {
                     selected[kvp.Key].Add(Convert.ToInt32(row["Id"]));
                  }
               }
         }
         return selected;
      }

      private void PerformDataPump(Dictionary<string, List<int>> selectedIds, bool importSchedules, bool importCurriculums)
      {
         UpdateStatus(10, "Wiping current database schema...");
         
         using (var db = new AppDbContext())
         {
               db.Database.EnsureDeleted();
               db.Database.EnsureCreated();

               var connection = db.Database.GetDbConnection();
               connection.Open();

               try
               {
                  using (var cmd = connection.CreateCommand())
                  {
                     cmd.CommandText = $"ATTACH DATABASE '{_tempDbPath}' AS OldDb";
                     cmd.ExecuteNonQuery();

                     cmd.CommandText = "PRAGMA foreign_keys = OFF;";
                     cmd.ExecuteNonQuery();

                     // 1. PUMP MASTER TABLES (Filtered by User Checkboxes)
                     var masterTables = new[] { "Instructors", "Rooms", "Courses", "Sections" };
                     foreach (var table in masterTables)
                     {
                           UpdateStatus(40, $"Transferring {table}...");
                           LogMessage($"Transferring {table}...");

                           // If they didn't even click the tab, assume they want all of them. If they did, use their filtered list.
                           string idFilter = "";
                           if (selectedIds.ContainsKey(table))
                           {
                              if (selectedIds[table].Count == 0) continue; // Skip table if they unchecked everything
                              idFilter = $"WHERE Id IN ({string.Join(",", selectedIds[table])})";
                           }

                           PumpTableDynamically(connection, cmd, table, idFilter);
                     }

                     // 2. PUMP DEPENDENT TABLES (Filtered safely against Master Data)
                     if (importSchedules)
                     {
                           UpdateStatus(70, "Safely filtering and transferring Schedules...");
                           LogMessage("Transferring Schedules (Skipping orphans)...");
                           
                           // MAGIC: This SQL forces schedules to ONLY import if their Room, Section, and Course were also imported!
                           string safeScheduleFilter = "WHERE CourseId IN (SELECT Id FROM main.Courses) " +
                                                      "AND RoomId IN (SELECT Id FROM main.Rooms) " +
                                                      "AND SectionId IN (SELECT Id FROM main.Sections) " +
                                                      "AND (InstructorId IS NULL OR InstructorId IN (SELECT Id FROM main.Instructors))";
                           
                           PumpTableDynamically(connection, cmd, "Schedules", safeScheduleFilter);
                     }

                     if (importCurriculums)
                     {
                           UpdateStatus(85, "Safely filtering and transferring Curriculums...");
                           LogMessage("Transferring Curriculums (Skipping orphans)...");
                           
                           string safeCurriculumFilter = "WHERE CourseId IN (SELECT Id FROM main.Courses)";
                           PumpTableDynamically(connection, cmd, "Curriculums", safeCurriculumFilter);
                     }

                     // Restore Constraints
                     cmd.CommandText = "PRAGMA foreign_keys = ON;";
                     cmd.ExecuteNonQuery();
                     
                     cmd.CommandText = "DETACH DATABASE OldDb";
                     cmd.ExecuteNonQuery();
                  }

                  Dispatcher.Invoke(() => 
                  {
                     StatusText.Text = "Migration Complete! Restart required.";
                     StatusText.Foreground = (Brush)Application.Current.Resources["PrimaryBrush"];
                     MigrationProgress.Value = 100;
                     FinishBtn.Visibility = Visibility.Visible;
                     LogMessage("SUCCESS: Database perfectly rebuilt.");
                  });
               }
               catch (Exception ex)
               {
                  Dispatcher.Invoke(() => 
                  {
                     StatusText.Text = "CRITICAL ERROR";
                     StatusText.Foreground = System.Windows.Media.Brushes.Red;
                     LogMessage($"ERROR: {ex.Message}");
                     CancelBtn.Visibility = Visibility.Visible;
                  });
               }
               finally
               {
                  connection.Close();
               }
         }
      }

      private void PumpTableDynamically(System.Data.Common.DbConnection conn, System.Data.Common.DbCommand cmd, string table, string whereClause)
      {
         var newColumns = GetTableColumns(conn, "main", table);
         var oldColumns = GetTableColumns(conn, "OldDb", table);

         if (oldColumns.Count == 0) return; // Table doesn't exist in old DB

         var insertCols = new List<string>();
         var selectCols = new List<string>();

         foreach (var col in newColumns)
         {
               insertCols.Add(col.Name);

               if (oldColumns.Any(o => o.Name == col.Name))
               {
                  selectCols.Add(col.Name);
               }
               else
               {
                  // Map missing 'Name' to 'FirstName' for ultra-old backups
                  if (col.Name == "FirstName" && oldColumns.Any(o => o.Name == "Name"))
                     selectCols.Add("Name");
                  else if (col.Type.Contains("INT") || col.Type.Contains("NUM") || col.Type.Contains("REAL") || col.Type.Contains("BOOL"))
                     selectCols.Add("0");
                  else
                     selectCols.Add("''");
               }
         }

         string insertStr = string.Join(", ", insertCols);
         string selectStr = string.Join(", ", selectCols);
         
         cmd.CommandText = $"INSERT INTO main.{table} ({insertStr}) SELECT {selectStr} FROM OldDb.{table} {whereClause}";
         
         int rowsAffected = cmd.ExecuteNonQuery();
         LogMessage($" -> {table}: {rowsAffected} rows imported.");
      }

      private List<(string Name, string Type)> GetTableColumns(System.Data.Common.DbConnection conn, string dbName, string tableName)
      {
         var columns = new List<(string Name, string Type)>();
         using (var cmd = conn.CreateCommand())
         {
               cmd.CommandText = $"PRAGMA {dbName}.table_info('{tableName}')";
               using var reader = cmd.ExecuteReader();
               while (reader.Read()) columns.Add((reader.GetString(1), reader.GetString(2).ToUpper()));
         }
         return columns;
      }

      private void UpdateStatus(int progress, string message)
      {
         Dispatcher.Invoke(() => { MigrationProgress.Value = progress; StatusText.Text = message; });
      }

      private void LogMessage(string msg)
      {
         Dispatcher.Invoke(() => { LogList.Items.Add($"> {msg}"); });
      }
      #endregion

      public class SchemaStatusItem
      {
         public string TableName { get; set; } = string.Empty;
         public int RowCount { get; set; }
         public string Status { get; set; } = string.Empty;
      }
   }
}