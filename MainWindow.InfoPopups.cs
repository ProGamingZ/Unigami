using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using UniversityScheduler.Data;

namespace UniversityScheduler
{
   public partial class MainWindow : Window
   {

      private void ViewInstLoadBtn_Click(object sender, RoutedEventArgs e)
      {
         if (InstructorSelector.SelectedItem is not Instructor instructor) return;

         // 1. Get database assignments for the current semester
         string assignedSectionsStr = _currentSemester == 1 ? instructor.AssignedSectionsSem1 : instructor.AssignedSectionsSem2;
         string assignedCoursesStr = _currentSemester == 1 ? instructor.PreferredCourseCodesSem1 : instructor.PreferredCourseCodesSem2;

         if (string.IsNullOrWhiteSpace(assignedSectionsStr) || string.IsNullOrWhiteSpace(assignedCoursesStr))
         {
            MessageBox.Show("This instructor has no sections or courses assigned in their database profile for this semester.", "Teaching Load");
            return;
         }

         // 2. Parse the target IDs and Codes
         var sectionIds = assignedSectionsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                             .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                                             .Where(id => id > 0).ToList();
                                             
         var preferredCodes = assignedCoursesStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                .Select(s => s.Trim().ToUpper()).ToHashSet();

         // 3. Fetch exact Curriculum, Course, and Section data
         using var db = new AppDbContext();
         var sections = db.Sections
                          .Where(sec => sectionIds.Contains(sec.Id))
                          .OrderBy(sec => sec.Program).ThenBy(sec => sec.YearLevel).ThenBy(sec => sec.Name)
                          .ToList();
                          
         var curriculums = db.Curriculums
                             .Include(c => c.Course)
                             .Where(c => c.Semester == _currentSemester)
                             .ToList();

         // 4. Build the Display String
         StringBuilder sb = new StringBuilder();
         sb.AppendLine($"Database Official Load: {instructor.FullName}");
         sb.AppendLine($"Semester: {_currentSemester}");
         sb.AppendLine(new string('-', 60));

         int totalLecHours = 0;
         int totalLabHours = 0;

         foreach (var sec in sections)
         {
            // Find what subjects this section takes this semester
            var secCurriculum = curriculums.Where(c => c.Program == sec.Program && c.YearLevel == sec.YearLevel).ToList();
            
            bool hasClasses = false;
            StringBuilder secSb = new StringBuilder();
            secSb.AppendLine($"\n📌 {sec.Program} {sec.YearLevel}-{sec.Name}");

            foreach (var curr in secCurriculum.OrderBy(c => c.Course!.Code))
            {
               if (curr.Course == null) continue;
               string baseCode = curr.Course.Code.Trim().ToUpper();

               // Check if Instructor is assigned to the LECTURE component of this curriculum course
               if (preferredCodes.Contains($"{baseCode}-LEC") && curr.Course.LectureHours > 0)
               {
                  secSb.AppendLine($"   • {curr.Course.Code,-8} | LEC | {curr.Course.LectureHours} hrs | {curr.Course.Name}");
                  totalLecHours += curr.Course.LectureHours;
                  hasClasses = true;
               }
               
               // Check if Instructor is assigned to the LAB component of this curriculum course
               if (preferredCodes.Contains($"{baseCode}-LAB") && curr.Course.LabHours > 0)
               {
                  secSb.AppendLine($"   • {curr.Course.Code,-8} | LAB | {curr.Course.LabHours} hrs | {curr.Course.Name}");
                  totalLabHours += curr.Course.LabHours;
                  hasClasses = true;
               }
            }

            // Only print the section if the instructor actually teaches a curriculum-approved subject in it
            if (hasClasses)
            {
               sb.Append(secSb.ToString());
            }
         }

         sb.AppendLine(new string('-', 60));
         sb.AppendLine($"Total Lecture Hours:    {totalLecHours}");
         sb.AppendLine($"Total Laboratory Hours: {totalLabHours}");
         sb.AppendLine($"Total Contact Hours:    {totalLecHours + totalLabHours}");

         // 5. Create the Popup Window
         var win = new Window
         {
            Title = "Instructor Database Load",
            Content = new TextBox
            {
               Text = sb.ToString(),
               IsReadOnly = true,
               VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
               Padding = new Thickness(15),
               FontSize = 14,
               FontFamily = new System.Windows.Media.FontFamily("Consolas") 
            },
            Width = 600,
            Height = 650,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            ResizeMode = ResizeMode.CanResize,
            Background = System.Windows.Media.Brushes.WhiteSmoke
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
            string instrName = match.Instructor?.FullName ?? "TBA";
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