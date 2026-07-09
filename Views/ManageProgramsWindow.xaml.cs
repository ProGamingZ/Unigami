using System.Linq;
using System.Windows;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
   public partial class ManageProgramsWindow : Window
   {
      public ManageProgramsWindow()
      {
         InitializeComponent();
         LoadPrograms();
      }

      private void LoadPrograms()
      {
         using var db = new AppDbContext();
         ProgramList.ItemsSource = db.Programs.OrderBy(p => p.Code).ToList();
      }

      private void ProgramList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
      {
         if (ProgramList.SelectedItem is DegreeProgram selected)
         {
            CodeTxt.Text = selected.Code;
            DescTxt.Text = selected.Description;
         }
         else
         {
            CodeTxt.Text = "";
            DescTxt.Text = "";
         }
      }

      private void Add_Click(object sender, RoutedEventArgs e)
      {
         string code = CodeTxt.Text.Trim().ToUpper();
         if (string.IsNullOrWhiteSpace(code)) return;

         using (var db = new AppDbContext())
         {
            if (db.Programs.Any(p => p.Code == code))
            {
               MessageBox.Show("This program code already exists.");
               return;
            }

            db.Programs.Add(new DegreeProgram { Code = code, Description = DescTxt.Text.Trim() });
            db.SaveChanges();
            MessageBox.Show($"{code} added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
         }
         
         LoadPrograms();
         MainWindow.TriggerDatabaseUpdated();
      }

      private void Update_Click(object sender, RoutedEventArgs e)
      {
         if (ProgramList.SelectedItem is not DegreeProgram selected) return;
         
         string oldCode = selected.Code;
         string newCode = CodeTxt.Text.Trim().ToUpper();
         if (string.IsNullOrWhiteSpace(newCode)) return;

         using (var db = new AppDbContext())
         {
            var progToUpdate = db.Programs.Find(selected.Id);
            if (progToUpdate == null) return;

            progToUpdate.Code = newCode;
            progToUpdate.Description = DescTxt.Text.Trim();

            // --- THE CASCADE UPDATE LOGIC ---
            // If they changed the name of the code, we must update all connected strings in the DB
            if (oldCode != newCode)
            {
               // 1. Update Courses (Comma-separated string)
               var courses = db.Courses.Where(c => c.RecommendedPrograms.Contains(oldCode)).ToList();
               foreach (var c in courses) c.RecommendedPrograms = c.RecommendedPrograms.Replace(oldCode, newCode);

               // 2. Update Instructors (Comma-separated string)
               var instructors = db.Instructors.Where(i => 
                  (i.ProgramSem1 != null && i.ProgramSem1.Contains(oldCode)) || 
                  (i.ProgramSem2 != null && i.ProgramSem2.Contains(oldCode))
               ).ToList();
               
               foreach (var i in instructors) 
               {
                   if (i.ProgramSem1 != null) i.ProgramSem1 = i.ProgramSem1.Replace(oldCode, newCode);
                   if (i.ProgramSem2 != null) i.ProgramSem2 = i.ProgramSem2.Replace(oldCode, newCode);
               }

               // 3. Update Sections (Direct string)
               var sections = db.Sections.Where(s => s.Program == oldCode).ToList();
               foreach (var s in sections) s.Program = newCode;

               // 4. Update Curriculums (Direct string)
               var curriculums = db.Curriculums.Where(c => c.Program == oldCode).ToList();
               foreach (var c in curriculums) c.Program = newCode;
            }

            db.SaveChanges();
         }

         MessageBox.Show("Program updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
         LoadPrograms();
         MainWindow.TriggerDatabaseUpdated();
      }

      private void Delete_Click(object sender, RoutedEventArgs e)
      {
         if (ProgramList.SelectedItem is not DegreeProgram selected) return;

         if (MessageBox.Show($"Are you sure you want to delete {selected.Code}? This will NOT delete courses, but it will remove this program tag from them.", 
               "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
         {
            using (var db = new AppDbContext())
            {
               var prog = db.Programs.Find(selected.Id);
               if (prog != null)
               {
                  db.Programs.Remove(prog);
                  db.SaveChanges();
                  MessageBox.Show($"{selected.Code} removed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
               }
            }
            LoadPrograms();
            MainWindow.TriggerDatabaseUpdated();
         }
      }
   }
}