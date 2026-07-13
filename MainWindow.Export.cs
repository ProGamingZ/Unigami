using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using UniversityScheduler.Data;
using ClosedXML.Excel;

namespace UniversityScheduler
{
   public partial class MainWindow : Window
   {
      private void ExporttoExcelBtn_Click(object sender, RoutedEventArgs e)
      {
         if (sender is Button btn && btn.ContextMenu != null)
         {
             btn.ContextMenu.PlacementTarget = btn;
             btn.ContextMenu.IsOpen = true;
         }
      }

      private void ExportStandard_Click(object sender, RoutedEventArgs e)
      {
         if (InstructorSelector.SelectedItem is not Instructor selectedInstructor) { MessageBox.Show("Select instructor."); return; }
         SaveFileDialog saveDialog = new SaveFileDialog { Filter = "Excel CSV (*.csv)|*.csv", FileName = $"{selectedInstructor.FullName}_Standard_Schedule.csv" };
         if (saveDialog.ShowDialog() == true) ExportScheduleToCsv(saveDialog.FileName, selectedInstructor.Id, null);
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
      
      // --- THE TENTATIVE RULE FILTER ---
      // Outside 8am-12pm and 1pm-5pm is overload. (Easy to edit later!)
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
                              string line3 = instructorId != null ? (activeClass.Section?.FullDisplayName ?? "Sec") : (activeClass.Instructor?.FullName ?? "TBA");
                              
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
      private void ClearScheduleBtn_Click(object sender, RoutedEventArgs e)
      {
         if (InstructorSelector.SelectedItem is Instructor selectedInstructor)
         {
            if (selectedInstructor.IsScheduleLocked)
            {
                  MessageBox.Show("Locked schedule.", "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Stop);
                  return;
            }
            if (MessageBox.Show($"Clear schedule for {selectedInstructor.FullName}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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