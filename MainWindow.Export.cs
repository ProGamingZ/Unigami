using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using UniversityScheduler.Data;

namespace UniversityScheduler
{
   public partial class MainWindow : Window
   {
      private void ExporttoExcelBtn_Click(object sender, RoutedEventArgs e)
      {
         if (InstructorSelector.SelectedItem is not Instructor selectedInstructor) { MessageBox.Show("Select instructor."); return; }
         SaveFileDialog saveDialog = new SaveFileDialog { Filter = "Excel CSV (*.csv)|*.csv", FileName = $"{selectedInstructor.Name}_Schedule.csv" };
         if (saveDialog.ShowDialog() == true) ExportScheduleToCsv(saveDialog.FileName, selectedInstructor.Id, null);
      }
      private void ExportClassBtn_Click(object sender, RoutedEventArgs e)
      {
         if (ClassSelector.SelectedItem == null) { MessageBox.Show("Select class."); return; }
         dynamic selectedItem = ClassSelector.SelectedItem;
         StudentSection section = selectedItem.OriginalObject;
         SaveFileDialog saveDialog = new SaveFileDialog { Filter = "Excel CSV (*.csv)|*.csv", FileName = $"{section.Program}_{section.YearLevel}{section.Name}_Schedule.csv" };
         if (saveDialog.ShowDialog() == true) ExportScheduleToCsv(saveDialog.FileName, null, section.Id);
      }
      private void ExportRoomBtn_Click(object sender, RoutedEventArgs e)
      {
         if (RoomSelector.SelectedItem is not Room selectedRoom) return;

         SaveFileDialog saveDialog = new SaveFileDialog
         {
            Filter = "Excel CSV (*.csv)|*.csv",
            FileName = $"{selectedRoom.Name}_Schedule.csv"
         };

         if (saveDialog.ShowDialog() == true)
         {
            // Pass null for Instructor/Section, and the Room ID
            // Note: You need to update ExportScheduleToCsv to accept a RoomId parameter!
            ExportScheduleToCsv(saveDialog.FileName, null, null, selectedRoom.Id);
         }
      }
      private void ExportScheduleToCsv(string fileName, int? instructorId, int? sectionId, int? roomId = null)
      {
         try
         {
            if (File.Exists(fileName))
            {
                  try 
                  {
                     using FileStream stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None);
                     stream.Close();
                  }
                  catch (IOException)
                  {
                     MessageBox.Show("The file is currently open in Excel.\nPlease close it and try again.", "File Locked", MessageBoxButton.OK, MessageBoxImage.Warning);
                     return;
                  }
            }
            using (var db = new AppDbContext())
            {
                  var query = db.Schedules.Include(s => s.Course).Include(s => s.Room).Include(s => s.Section).Include(s => s.Instructor).Where(s => s.Semester == _currentSemester);
                  if (instructorId != null) query = query.Where(s => s.InstructorId == instructorId);
                  if (sectionId != null) query = query.Where(s => s.SectionId == sectionId);
                  if (roomId != null) query = query.Where(s => s.RoomId == roomId);
                  var schedules = query.ToList();

                  StringBuilder csv = new StringBuilder();
                  csv.AppendLine("Time,Mon,Tue,Wed,Thu,Fri,Sat,Sun");

                  TimeSpan currentTime = new TimeSpan(GlobalSettings.StartTimeHour, 0, 0);
                  TimeSpan endTime = new TimeSpan(GlobalSettings.EndTimeHour, 0, 0);

                  while (currentTime <= endTime)
                  {
                     List<string> rowData = new List<string> { DateTime.Today.Add(currentTime).ToString("h:mm tt") };
                     var days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
                     foreach (var day in days)
                     {
                        var activeClass = schedules.FirstOrDefault(s => s.Day == day && ParseTime(s.StartTime) <= currentTime && ParseTime(s.EndTime) > currentTime);
                        if (activeClass != null)
                        {
                              int blockIndex = (int)((currentTime - ParseTime(activeClass.StartTime)).TotalMinutes / 30);
                              string line1 = activeClass.Course?.Code ?? "Subject";
                              string line2 = activeClass.Room?.Name ?? "TBA";
                              string line3 = instructorId != null ? (activeClass.Section?.FullDisplayName ?? "Sec") : (activeClass.Instructor?.Name ?? "TBA");
                              
                              switch (blockIndex) {
                                 case 0: rowData.Add($"\"{line1}\""); break;
                                 case 1: rowData.Add($"\"{line2}\""); break;
                                 case 2: rowData.Add($"\"{line3}\""); break;
                                 default: rowData.Add("\"-DO-\""); break;
                              }
                        }
                        else rowData.Add("");
                     }
                     csv.AppendLine(string.Join(",", rowData));
                     currentTime = currentTime.Add(new TimeSpan(0, 30, 0));
                  }
                  File.WriteAllText(fileName, csv.ToString());
                  MessageBox.Show("Export Successful!");
            }
         }
         catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
      }
      private void ViewInstLoadBtn_Click(object sender, RoutedEventArgs e)
      {
         if (InstructorSelector.SelectedItem is not Instructor instructor) return;

         using var db = new AppDbContext();
         // Fetch data again to ensure freshness
         var schedules = db.Schedules
            .Include(s => s.Course)
            .Include(s => s.Section)
            .Where(s => s.InstructorId == instructor.Id && s.Semester == _currentSemester)
            .ToList();

         if (schedules.Count == 0)
         {
            MessageBox.Show("No classes assigned to this instructor.", "Teaching Load");
            return;
         }

         // Group by Section
         var loadGroups = schedules
            .Where(s => s.Section != null && s.Course != null)
            .GroupBy(s => s.Section) // Group by Section Object
            .Select(g => new
            {
                  SectionName = $"{g.Key!.Program} {g.Key.YearLevel}{g.Key.Name}",
                  Subjects = g.Select(s => $"{s.Course!.Code} ({s.Course.Units}u)").Distinct().ToList()
            })
            .OrderBy(x => x.SectionName)
            .ToList();

         // Build the Display String
         StringBuilder sb = new StringBuilder();
         sb.AppendLine($"Teaching Load: {instructor.Name}");
         sb.AppendLine("----------------------------------");

         foreach (var group in loadGroups)
         {
            sb.AppendLine($"\n📌 {group.SectionName}");
            foreach (var subject in group.Subjects)
            {
            sb.AppendLine($"   • {subject}");
            }
         }

         // Create a simple popup window (Always on Top)
         var win = new Window
         {
            Title = "Instructor Load",
            Content = new TextBox
            {
            Text = sb.ToString(),
            IsReadOnly = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(10),
            FontSize = 14,
            FontFamily = new System.Windows.Media.FontFamily("Consolas") // Monospace for alignment
            },
            Width = 400,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            ResizeMode = ResizeMode.CanResize,
            Background = System.Windows.Media.Brushes.White
         };
         win.Show();
      }
      private void MissingSubjectsBtn_Click(object sender, RoutedEventArgs e)
      {
         if (ClassSelector.SelectedItem == null) return;

         dynamic selectedItem = ClassSelector.SelectedItem;
         StudentSection section = selectedItem.OriginalObject;

         using var db = new AppDbContext();
         // 1. Get Scheduled Course IDs for this section & semester
         var scheduledCourseIds = db.Schedules
            .Where(s => s.SectionId == section.Id && s.Semester == _currentSemester)
            .Select(s => s.CourseId)
            .Distinct()
            .ToList();

         // 2. Get Required Course IDs from Curriculum
         // Matches Program, Year Level, and Semester
         var requiredCourses = db.Curriculums
            .Include(c => c.Course)
            .Where(c => c.Program == section.Program &&
                        c.YearLevel == section.YearLevel &&
                        c.Semester == _currentSemester)
            .Select(c => c.Course)
            .ToList();

         // 3. Find Missing
         var missing = requiredCourses
            .Where(c => !scheduledCourseIds.Contains(c!.Id))
            .OrderBy(c => c!.Code)
            .ToList();

         if (missing.Count == 0)
         {
            MessageBox.Show("✅ Great job! All subjects for this semester are scheduled.", "Complete");
         }
         else
         {
            string msg = "⚠️ The following subjects are MISSING:\n\n";
            foreach (var m in missing)
            {
            msg += $"• {m!.Code} ({m.Units} units)\n";
            }
            MessageBox.Show(msg, "Missing Subjects", MessageBoxButton.OK, MessageBoxImage.Information);
         }
      }
      private void SectionCoursesBtn_Click(object sender, RoutedEventArgs e)
      {
         if (ClassSelector.SelectedItem == null) return;

         dynamic selectedItem = ClassSelector.SelectedItem;
         StudentSection section = selectedItem.OriginalObject;

         using var db = new AppDbContext();
         // 1. Get Required Curriculum
         var requiredCurriculum = db.Curriculums
            .Include(c => c.Course)
            .Where(c => c.Program == section.Program &&
                        c.YearLevel == section.YearLevel &&
                        c.Semester == _currentSemester)
            .ToList();

         // 2. Get Current Schedules
         var currentSchedules = db.Schedules
            .Include(s => s.Instructor)
            .Where(s => s.SectionId == section.Id && s.Semester == _currentSemester)
            .ToList();

         // 3. Build UI
         var rootStack = new StackPanel { Margin = new Thickness(15) };

         // --- HEADER: ASSIGNED ---
         rootStack.Children.Add(new TextBlock
         {
            Text = "ASSIGNED SUBJECTS",
            FontWeight = FontWeights.Bold,
            FontSize = 16,
            Foreground = System.Windows.Media.Brushes.DarkSlateGray,
            Margin = new Thickness(0, 0, 0, 10),
            TextDecorations = TextDecorations.Underline
         });

         // Table Header
         rootStack.Children.Add(CreateRow("Subject", "Description", "Units", "INSTRUCTOR", true));

         // Process Assigned
         bool hasAssigned = false;
         int totalAssignedUnits = 0; // Counter for Total

         foreach (var req in requiredCurriculum.OrderBy(c => c.Course!.Code))
         {
            var match = currentSchedules.FirstOrDefault(s => s.CourseId == req.CourseId);

            if (match != null)
            {
            string instrName = match.Instructor?.Name ?? "TBA";
               CreateRowUI(rootStack, req.Course!.Code, req.Course.Name, req.Course.Units.ToString(), instrName);

            // Add to total
            totalAssignedUnits += req.Course.Units;
            hasAssigned = true;
            }
         }

         if (!hasAssigned)
         {
            rootStack.Children.Add(new TextBlock { Text = "   (No subjects assigned yet)", FontStyle = FontStyles.Italic, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 10, 0, 0) });
         }
         else
         {
            var totalGrid = new Grid { Margin = new Thickness(0, 10, 0, 10) };
            totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

            // "Total" Label (Right aligned in Description column)
            var totalLabel = new TextBlock
            {
            Text = "Total",
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 10, 0)
            };

            // The Number (Centered in Units column)
            var totalValue = new TextBlock
            {
            Text = totalAssignedUnits.ToString(),
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
            };

            Grid.SetColumn(totalLabel, 1);
            Grid.SetColumn(totalValue, 2);

            totalGrid.Children.Add(totalLabel);
            totalGrid.Children.Add(totalValue);
            rootStack.Children.Add(totalGrid);

            // Add a separator line
            rootStack.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 10) });
         }

         // --- HEADER: UNASSIGNED ---
         rootStack.Children.Add(new TextBlock
         {
            Text = "UNASSIGNED SUBJECTS",
            FontWeight = FontWeights.Bold,
            FontSize = 16,
            Foreground = System.Windows.Media.Brushes.Red,
            Margin = new Thickness(0, 10, 0, 10),
            TextDecorations = TextDecorations.Underline
         });

         // Process Unassigned
         bool hasUnassigned = false;
         foreach (var req in requiredCurriculum.OrderBy(c => c.Course!.Code))
         {
            bool isScheduled = currentSchedules.Any(s => s.CourseId == req.CourseId);

            if (!isScheduled)
            {
               CreateRowUI(rootStack, req.Course!.Code, req.Course.Name, req.Course.Units.ToString(), "---");
            hasUnassigned = true;
            }
         }

         if (!hasUnassigned)
         {
            rootStack.Children.Add(new TextBlock { Text = "   (All subjects scheduled!)", FontStyle = FontStyles.Italic, Foreground = System.Windows.Media.Brushes.Green, Margin = new Thickness(0, 10, 0, 0) });
         }

         // 4. Show Window
         var win = new Window
         {
            Title = $"Subject Load: {section.Program} {section.YearLevel}-{section.Name}",
            Content = new ScrollViewer { Content = rootStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            Width = 650,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            ResizeMode = ResizeMode.CanResize,
            Background = System.Windows.Media.Brushes.White
         };
         win.Show();
      }
      private void ClearScheduleBtn_Click(object sender, RoutedEventArgs e)
      {
         if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
         {
            if (selectedInstructor.IsScheduleLocked)
            {
                  MessageBox.Show("Locked schedule.", "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Stop);
                  return;
            }
            if (MessageBox.Show($"Clear schedule for {selectedInstructor.Name}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                  using (var db = new AppDbContext())
                  {
                     var schedules = db.Schedules.Where(s => s.InstructorId == selectedInstructor.Id && s.Semester == _currentSemester).ToList();
                     foreach(var s in schedules) s.InstructorId = null; 
                     db.SaveChanges();
                  }
                  RefreshSchedule();
            }
         }
      }
      private void ClearClassBtn_Click(object sender, RoutedEventArgs e)
      {
         if (ClassSelector.SelectedItem == null)
         {
            MessageBox.Show("Please select a class first.");
            return;
         }

         dynamic selectedItem = ClassSelector.SelectedItem;
         StudentSection section = selectedItem.OriginalObject;

         var result = MessageBox.Show(
            $"Are you sure you want to remove ALL classes for {section.Program} {section.YearLevel}-{section.Name}?\n\nThis will delete the schedule entries.",
            "Confirm Clear",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

         if (result == MessageBoxResult.Yes)
         {
            using (var db = new AppDbContext())
            {
                  var schedules = db.Schedules
                     .Where(s => s.SectionId == section.Id && s.Semester == _currentSemester)
                     .ToList();

                  db.Schedules.RemoveRange(schedules);
                  db.SaveChanges();
            }

            // Refresh UI
            UpdateClassSchedule(section);
            RefreshSchedule(); // Also refresh instructor view since their loads changed
            MessageBox.Show("Class schedule cleared.");
         }
      }
      private void ClearRoomBtn_Click(object sender, RoutedEventArgs e)
      {
         if (RoomSelector.SelectedItem is not Room selectedRoom) return;

         if (MessageBox.Show($"Clear all classes in {selectedRoom.Name}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
         {
            using (var db = new AppDbContext())
            {
                  var schedules = db.Schedules.Where(s => s.RoomId == selectedRoom.Id && s.Semester == _currentSemester).ToList();
                  // We set RoomId to null (Unassign Room) rather than deleting the class entirely
                  foreach (var s in schedules) s.RoomId = null;
                  db.SaveChanges();
            }
            UpdateRoomSchedule(selectedRoom);
            RefreshSchedule();
            MessageBox.Show("Room cleared. Classes are now unassigned (Room: TBA).");
         }
      }
      private static void CreateRowUI(StackPanel parent, string code, string desc, string units, string instructor)
      {
         parent.Children.Add(CreateRow(code, desc, units, instructor, false));
      }
      private static Grid CreateRow(string c1, string c2, string c3, string c4, bool isHeader)
      {
         var grid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
         grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // Subject Code
         grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Description (Flexible)
         grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });  // Units
         grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // Instructor

         var weight = isHeader ? FontWeights.Bold : FontWeights.Normal;
         var color = isHeader ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.DarkSlateGray;

         var t1 = new TextBlock { Text = c1, FontWeight = weight, Foreground = color, VerticalAlignment = VerticalAlignment.Center };
         var t2 = new TextBlock { Text = c2, FontWeight = weight, Foreground = color, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = c2 };
         var t3 = new TextBlock { Text = c3, FontWeight = weight, Foreground = color, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
         var t4 = new TextBlock { Text = c4.ToUpper(), FontWeight = weight, Foreground = color, VerticalAlignment = VerticalAlignment.Center }; // Instructor Name Uppercase

         Grid.SetColumn(t1, 0);
         Grid.SetColumn(t2, 1);
         Grid.SetColumn(t3, 2);
         Grid.SetColumn(t4, 3);

         grid.Children.Add(t1);
         grid.Children.Add(t2);
         grid.Children.Add(t3);
         grid.Children.Add(t4);

         return grid;
      }        

   }
}