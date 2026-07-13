using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Microsoft.EntityFrameworkCore;
using UniversityScheduler.Data;

namespace UniversityScheduler
{
   public partial class MainWindow : Window
   {

      private void ExportStandard_Click(object sender, RoutedEventArgs e)
      {
         if (InstructorSelector.SelectedItem is not Instructor selectedInstructor) { MessageBox.Show("Select instructor."); return; }
         SaveFileDialog saveDialog = new SaveFileDialog { Filter = "Excel CSV (*.csv)|*.csv", FileName = $"{selectedInstructor.FullName}_Standard_Schedule.csv" };
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

   }
}