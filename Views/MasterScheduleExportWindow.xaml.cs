using System.Windows;

namespace UniversityScheduler.Views
{
   public partial class MasterScheduleExportWindow : Window
   {
      public MasterScheduleExportWindow()
      {
         InitializeComponent();
         LoadSettings();
      }

      private void LoadSettings()
      {
         SyTxt.Text = GlobalSettings.MasterSchoolYear;
         DateTxt.Text = GlobalSettings.MasterDateText;
         DeptNameTxt.Text = GlobalSettings.MasterDeptName;
         DeptAcronymTxt.Text = GlobalSettings.MasterDeptAcronym;
         SecNameTxt.Text = GlobalSettings.MasterSecName;
         SecPosTxt.Text = GlobalSettings.MasterSecPos;
         DeanNameTxt.Text = GlobalSettings.MasterDeanName;
         DeanPosTxt.Text = GlobalSettings.MasterDeanPos;
      }

      private void SaveSettings_Click(object sender, RoutedEventArgs e)
      {
         GlobalSettings.MasterSchoolYear = SyTxt.Text.Trim();
         GlobalSettings.MasterDateText = DateTxt.Text.Trim();
         GlobalSettings.MasterDeptName = DeptNameTxt.Text.Trim();
         GlobalSettings.MasterDeptAcronym = DeptAcronymTxt.Text.Trim();
         GlobalSettings.MasterSecName = SecNameTxt.Text.Trim();
         GlobalSettings.MasterSecPos = SecPosTxt.Text.Trim();
         GlobalSettings.MasterDeanName = DeanNameTxt.Text.Trim();
         GlobalSettings.MasterDeanPos = DeanPosTxt.Text.Trim();
         
         GlobalSettings.Save();
         
         MessageBox.Show("Export Settings Saved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
         this.Close();
      }
   }
}