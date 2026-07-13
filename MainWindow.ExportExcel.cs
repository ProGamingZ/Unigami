using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using UniversityScheduler.Data;

namespace UniversityScheduler
{
   public partial class MainWindow : Window
   {
      // ✂️ CUT & PASTE THESE FROM THE ORIGINAL FILE:
      // 1. ExporttoExcelBtn_Click
      // 2. ExportRegularTeachingLoad_Click
      // 3. ExportTeachingOverload_Click
      // 4. InjectMetadataAndFormulas
      // 5. ExecuteSplitTeachingLoadExport
      // 6. FillTeachingLoadSheet
      // 7. GetDayColumnExcel
      // 8. GetTimeRowExcel
      // 9. IsOverload

      private void ExporttoExcelBtn_Click(object sender, RoutedEventArgs e)
      {
         if (sender is Button btn && btn.ContextMenu != null)
         {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
         }
      }

      private void ExportRegularTeachingLoad_Click(object sender, RoutedEventArgs e)
      {
         ExecuteSplitTeachingLoadExport(
            isOverloadTarget: false, 
            templateName: "TeachingLoad_Regular.xlsx", 
            defaultFileNameSuffix: "_Regular_Teaching_Load.xlsx"
         );
      }

      private void ExportTeachingOverload_Click(object sender, RoutedEventArgs e)
      {
         ExecuteSplitTeachingLoadExport(
            isOverloadTarget: true, 
            templateName: "TeachingLoad_Overload.xlsx", 
            defaultFileNameSuffix: "_Teaching_Overload.xlsx"
         );
      }

      public static void InjectMetadataAndFormulas(IXLWorksheet ws)
      {
         string today = DateTime.Now.ToString("MMMM dd, yyyy");

         // 1. School Year & Sem
         ws.Cell("F5").Value = GlobalSettings.ExportSchoolYear;
         ws.Cell("F6").Value = GlobalSettings.ExportSemesterText;
         ws.Cell("E54").Value = GlobalSettings.DPTLCode;
         ws.Cell("AN19").Value = GlobalSettings.DepartmentName;

         // 2. Names
         ws.Cell("F60").Value = GlobalSettings.Dean1Name;
         ws.Cell("AA60").Value = GlobalSettings.Dean2Name;
         ws.Cell("AP60").Value = GlobalSettings.Dean3Name;
         ws.Cell("A63").Value = GlobalSettings.VPAcademicName;
         ws.Cell("P63").Value = GlobalSettings.VPResearchName;
         ws.Cell("AM63").Value = GlobalSettings.VPAdminName;
         ws.Cell("P69").Value = GlobalSettings.PresidentName;

         // 3. Dates
         ws.Cell("F55").Value = GlobalSettings.Date0UseToday ? today : GlobalSettings.Date0Text;
         ws.Cell("F61").Value = GlobalSettings.Date1UseToday ? today : GlobalSettings.Date1Text;
         ws.Cell("AA61").Value = GlobalSettings.Date2UseToday ? today : GlobalSettings.Date2Text;
         ws.Cell("AP61").Value = GlobalSettings.Date3UseToday ? today : GlobalSettings.Date3Text;
         ws.Cell("D65").Value = GlobalSettings.Date4UseToday ? today : GlobalSettings.Date4Text;
         ws.Cell("X65").Value = GlobalSettings.Date5UseToday ? today : GlobalSettings.Date5Text;
         ws.Cell("AQ65").Value = GlobalSettings.Date6UseToday ? today : GlobalSettings.Date6Text;

         // 4. Math Formulas (These cells will evaluate as native Excel Math)
         // Weekly totals (V49, V50) always receive the Lec/Lab sums regardless of level
         ws.Cell("V49").FormulaA1 = GlobalSettings.FormulaLecHours.Replace("=", "");
         ws.Cell("V50").FormulaA1 = GlobalSettings.FormulaLabHours.Replace("=", "");

         // The specific Sub-Totals shift cells dynamically based on Academic Level
         if (GlobalSettings.IsUndergraduate)
         {
            ws.Cell("AX48").FormulaA1 = GlobalSettings.FormulaContactHours.Replace("=", "");
            ws.Cell("AW50").FormulaA1 = GlobalSettings.FormulaLecHours.Replace("=", "");
            ws.Cell("AW51").FormulaA1 = GlobalSettings.FormulaLabHours.Replace("=", "");
         }
         else
         {
            ws.Cell("AX52").FormulaA1 = GlobalSettings.FormulaContactHours.Replace("=", "");
            ws.Cell("AW54").FormulaA1 = GlobalSettings.FormulaLecHours.Replace("=", "");
            ws.Cell("AW55").FormulaA1 = GlobalSettings.FormulaLabHours.Replace("=", "");
         }
      }

      private void ExecuteSplitTeachingLoadExport(bool isOverloadTarget, string templateName, string defaultFileNameSuffix)
      {
         if (InstructorSelector.SelectedItem is not Instructor selectedInstructor) 
         { 
            MessageBox.Show("Please select an instructor first.", "Notification", MessageBoxButton.OK, MessageBoxImage.Warning); 
            return; 
         }
         
         SaveFileDialog saveDialog = new SaveFileDialog 
         { 
            Filter = "Excel Workbook (*.xlsx)|*.xlsx", 
            FileName = $"{selectedInstructor.FullName}{defaultFileNameSuffix}" 
         } ;
         if (saveDialog.ShowDialog() != true) return;

         try
         {
            // 1. Locate Target Workbook Template
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", templateName);
            if (!File.Exists(templatePath))
            {
                  MessageBox.Show($"Template file '{templateName}' not found in the Templates folder!", "Missing File", MessageBoxButton.OK, MessageBoxImage.Error);
                  return;
            }

            using (var db = new AppDbContext())
            {
                  var instructor = db.Instructors.Find(selectedInstructor.Id);
                  if (instructor == null) return;

                  // Fetch current context schedules
                  var allSchedules = db.Schedules
                                    .Include(s => s.Course)
                                    .Include(s => s.Room)
                                    .Include(s => s.Section)
                                    .Where(s => s.Semester == _currentSemester && s.InstructorId == instructor.Id)
                                    .ToList();

                  // 2. Filter data precisely based on target selection
                  var filteredSchedules = allSchedules
                     .Where(s => IsOverload(s.StartTime, s.EndTime) == isOverloadTarget)
                     .ToList();

                  // 3. Open and Process Template Workbook
                  using (var workbook = new XLWorkbook(templatePath))
                  {
                     // Both separate templates use a primary worksheet (e.g., "SHEET1" or "LOAD")
                     // We grab the first available sheet dynamically to ensure compatibility
                     var targetSheet = workbook.Worksheets.First();

                     // 4. Fill Metadata and Schedule Grid
                     FillTeachingLoadSheet(targetSheet, instructor, filteredSchedules, isOverloadTarget);

                     // 5. Save Completed Workbook
                     workbook.SaveAs(saveDialog.FileName);
                  }

                  string typeLabel = isOverloadTarget ? "Overload" : "Regular";
                  MessageBox.Show($"{typeLabel} Teaching Load exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
         }
         catch (Exception ex) 
         { 
            MessageBox.Show($"Error during Excel generation: {ex.Message}", "Export Failure", MessageBoxButton.OK, MessageBoxImage.Error); 
         }
      }

      private void FillTeachingLoadSheet(IXLWorksheet ws, Instructor instructor, List<ClassSchedule> schedules, bool isOverload)
      {
         // --- 1. Inject Profile Data ---
         ws.Cell("D8").Value = instructor.Surname;
         ws.Cell("K8").Value = instructor.FirstName;
         ws.Cell("R8").Value = instructor.MiddleName;
         ws.Cell("AE8").Value = instructor.HomeAddress;
         
         ws.Cell("R11").Value = instructor.BaccalaureateDegree;
         ws.Cell("R12").Value = instructor.MastersDegree;
         ws.Cell("R13").Value = instructor.DoctoralDegree;
         ws.Cell("R14").Value = instructor.AdministrativeDesignation; 
         
         ws.Cell("R15").Value = instructor.ExperiencePublic;
         ws.Cell("AG16").Value = instructor.ExperiencePrivate; 
         ws.Cell("AS15").Value = instructor.ExperiencePublic + instructor.ExperiencePrivate;
         ws.Cell("AC55").Value = instructor.FullName.ToUpper();

         // --- 2. Inject Checkboxes (Filled with Solid Color) ---
         string[] allCheckboxes = { "Q4", "AC4", "L6", "T6", "AA6", "AI6" };
         foreach (string cellRef in allCheckboxes)
         {
            ws.Cell(cellRef).Value = "";
            ws.Cell(cellRef).Style.Fill.BackgroundColor = XLColor.NoColor; 
         }

         // ACADEMIC LEVEL CHECKBOXES
         if (GlobalSettings.IsUndergraduate) 
             ws.Cell("Q4").Style.Fill.BackgroundColor = XLColor.Black;
         else 
             ws.Cell("AC4").Style.Fill.BackgroundColor = XLColor.Black;

         // EMPLOYMENT STATUS CHECKBOXES
         string currentStatus = _currentSemester == 1 ? instructor.StatusSem1 : instructor.StatusSem2;
         if (isOverload) 
             ws.Cell("T6").Style.Fill.BackgroundColor = XLColor.Black;
         else if (currentStatus.Equals("Part-time", StringComparison.OrdinalIgnoreCase)) 
             ws.Cell("AA6").Style.Fill.BackgroundColor = XLColor.Black;
         else 
             ws.Cell("L6").Style.Fill.BackgroundColor = XLColor.Black;

         // --- 3. Inject Schedule Blocks ---
         foreach (var schedule in schedules)
         {
            try
            {
               int col = GetDayColumnExcel(schedule.Day);
               
               // 1. BULLETPROOF TIME PARSER: Mathematically calculate rows instead of relying on string switches
               int startRow = 0;
               int endRow = 0;
               if (DateTime.TryParse(schedule.StartTime, out DateTime st)) 
                  startRow = 19 + (int)Math.Round((st.TimeOfDay.TotalHours - 7.0) * 2);
               if (DateTime.TryParse(schedule.EndTime, out DateTime et)) 
                  endRow = 19 + (int)Math.Round((et.TimeOfDay.TotalHours - 7.0) * 2);

               if (col > 0 && startRow > 0 && endRow > startRow)
               {
                  int endCol = (schedule.Day == "Sat") ? col + 4 : col + 5;
                  
                  // Format exactly as requested: Course + Comp / Section / Room
                  string compLabel = (schedule.Component != null && schedule.Component.Contains("Lab")) ? "Lab" : "Lec";
                  string classInfo = $"{schedule.Course?.Code} {compLabel}\n{schedule.Section?.FullDisplayName}\n{schedule.Room?.Name}";

                  // 2. Identify safe rows (Strictly avoid touching Lunch Break at rows 29 and 30)
                  List<int> validRows = new List<int>();
                  for (int r = startRow; r < endRow; r++)
                  {
                     if (r == 29 || r == 30) continue; // Completely skip the lunch banner
                     validRows.Add(r);
                  }

                  // 3. Inject and Merge contiguous blocks safely around the lunch break
                  if (validRows.Count > 0)
                  {
                     int segStart = validRows[0];
                     int segEnd = validRows[0];

                     for (int i = 1; i <= validRows.Count; i++)
                     {
                        // Check if the next row is contiguous (no gap)
                        if (i < validRows.Count && validRows[i] == segEnd + 1)
                        {
                              segEnd = validRows[i]; // Extend the block
                        }
                        else
                        {
                              // Block ended. Create the visual block for this segment
                              var classRange = ws.Range(segStart, col, segEnd, endCol);
                              
                              // SAFETY NET: Force unmerge first to prevent ClosedXML from silently crashing
                              // if the user's template had random accidental merged cells left over!
                              classRange.Unmerge();
                              classRange.Merge();
                              
                              // Display the data beautifully centered
                              classRange.Value = classInfo;
                              classRange.Style.Alignment.WrapText = true;
                              classRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                              classRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                              classRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                              
                              // Start a new block (Handles Overlappers that jump across the lunch break)
                              if (i < validRows.Count)
                              {
                                 segStart = validRows[i];
                                 segEnd = validRows[i];
                              }
                        }
                     }
                  }
               }
            }
            catch 
            { 
                // Ignore silent rendering errors for a single class so the rest of the board finishes rendering
            }
         }

         // --- 4. Inject Dynamic Settings, Metadata, & Formulas ---
         // (This handles School Year, Signatures, Dates, AND AN19 Department Name!)
         InjectMetadataAndFormulas(ws);

         // --- 5. Inject Side Table (Subjects, Students, Hours) ---
         var courseGroups = schedules
             .Where(s => s.Course != null)
             .GroupBy(s => s.Course!.Id)
             .OrderBy(g => g.First().Course!.Code)
             .ToList();

         int sideRow = 21; 
         
         double totalWeeklyLecHours = 0;
         double totalWeeklyLabHours = 0;
         
         foreach (var courseGroup in courseGroups)
         {
            if (sideRow > 45) break; 

            var course = courseGroup.First().Course!;

            // 2. Count Unique Sections across BOTH Lec and Lab for this course
            var uniqueSectionsList = courseGroup
               .Where(s => s.Section != null)
               .Select(s => s.Section)
               .GroupBy(s => s!.Id)
               .Select(g => g.First())
               .ToList();
               
            int numSections = uniqueSectionsList.Count > 0 ? uniqueSectionsList.Count : 1; 
            
            int totalStudents = uniqueSectionsList.Sum(s => s!.StudentCount);
            if (totalStudents == 0) totalStudents = numSections * 40;

            // 3. Now split the course into its scheduled Components (Lec / Lab)
            var componentGroups = courseGroup
               .GroupBy(s => s.Component)
               .OrderByDescending(g => g.Key) // Sorts "Lecture" before "Lab"
               .ToList();

            foreach (var compGroup in componentGroups)
            {
               if (sideRow > 45) break;

               string component = compGroup.Key;
               string compText = component.Contains("Lab") ? "Lab" : "Lec";

               // ACCURATE AT CALCULATION: Course Units x Global Number of Sections
               int atValue = 0;
               if (compText == "Lec") 
               {
                  atValue = course.LectureHours * numSections;
                  totalWeeklyLecHours += atValue;
               }
               else if (compText == "Lab") 
               {
                  atValue = course.LabHours * numSections;
                  totalWeeklyLabHours += atValue;
               }

               // ACCURATE AW CALCULATION: AT * Weeks
               double awValue = atValue * GlobalSettings.WeeksPerSemester;

               // Inject Side Table Data
               ws.Cell(sideRow, "AN").Value = compText;                
               ws.Cell(sideRow + 1, "AN").Value = course.Code;         
               ws.Cell(sideRow + 1, "AQ").Value = totalStudents;       
               ws.Cell(sideRow + 1, "AT").Value = atValue;             
               ws.Cell(sideRow + 1, "AW").Value = awValue;             
               
               sideRow += 2; 
            }
         }

         // --- 6. Inject Hardcoded Weekly Totals (V49, V50, AW Summaries) ---
         // V49 and V50 get the strict Weekly (1 week) Hours
         ws.Cell("V49").Value = totalWeeklyLecHours;
         ws.Cell("V50").Value = totalWeeklyLabHours;

         // Calculate Total Semester Hours (Weekly x Weeks)
         double totalSemesterLecHours = totalWeeklyLecHours * GlobalSettings.WeeksPerSemester;
         double totalSemesterLabHours = totalWeeklyLabHours * GlobalSettings.WeeksPerSemester;

         // Route the Total Semester Hours to the correct AW cells based on Level
         if (GlobalSettings.IsUndergraduate)
         {
            ws.Cell("AW50").Value = totalSemesterLecHours;
            ws.Cell("AW51").Value = totalSemesterLabHours;
         }
         else
         {
            ws.Cell("AW54").Value = totalSemesterLecHours;
            ws.Cell("AW55").Value = totalSemesterLabHours;
         }
      }

      private int GetDayColumnExcel(string day)
      {
         return day switch
         {
            "Mon" => 4,   // Column D
            "Tue" => 10,  // Column J
            "Wed" => 16,  // Column P
            "Thu" => 22,  // Column V
            "Fri" => 28,  // Column AB
            "Sat" => 34,  // Column AH
            _ => -1
         };
      }

      private int GetTimeRowExcel(string startTimeStr)
      {
         TimeSpan time = ParseTime(startTimeStr);
         
         // Grid starts exactly at 7:00 AM on Row 19.
         // Every 30 minutes advances down by 1 row index.
         int baseRow = 19;
         TimeSpan baseTime = new TimeSpan(7, 0, 0);

         if (time >= baseTime)
         {
            double currentMinutes = (time - baseTime).TotalMinutes;
            return baseRow + (int)(currentMinutes / 30);
         }
         return -1;
      }

      private bool IsOverload(string startTimeStr, string endTimeStr)
      {
         TimeSpan start = ParseTime(startTimeStr);
         TimeSpan end = ParseTime(endTimeStr);

         TimeSpan morningStart = new TimeSpan(8, 0, 0);   // 8:00 AM
         TimeSpan morningEnd = new TimeSpan(12, 0, 0);    // 12:00 PM
         TimeSpan afternoonStart = new TimeSpan(12, 0, 0); // 12:00 PM
         TimeSpan afternoonEnd = new TimeSpan(17, 0, 0);   // 5:00 PM

         if (start < morningStart) return true; // Early morning
         if (start >= morningEnd && start < afternoonStart) return true; // Lunch break
         if (start >= afternoonEnd) return true; // Evening
         if (end > afternoonEnd) return true; // Ends late

         return false; // Fits perfectly inside regular hours
      }




   }
}