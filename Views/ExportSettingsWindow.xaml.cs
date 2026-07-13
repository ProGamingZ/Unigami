using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ClosedXML.Excel;
using System.Linq;

namespace UniversityScheduler.Views
{
   public partial class ExportSettingsWindow : Window
   {
      // Arrays sized strictly for 7 dates (Index 0 to 6)
      private CheckBox[] _chkToday = new CheckBox[7];
      private TextBox[] _txtDates = new TextBox[7];

      public ExportSettingsWindow()
      {
         InitializeComponent();
         GenerateDateUI();
         LoadData();
      }

      private void GenerateDateUI()
      {
         string[] labels = { "Effectivity Date", "Date 1 (Dean 1)", "Date 2 (Dean 2)", "Date 3 (Dean 3)", "Date 4 (VP Acad)", "Date 5 (VP Res)", "Date 6 (VP Admin)" };
         
         for (int i = 0; i < 7; i++)
         {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

            grid.Children.Add(new TextBlock { Text = labels[i], VerticalAlignment = VerticalAlignment.Center });

            _txtDates[i] = new TextBox { Margin = new Thickness(5, 0, 5, 0) };
            Grid.SetColumn(_txtDates[i], 1);
            grid.Children.Add(_txtDates[i]);

            _chkToday[i] = new CheckBox { Content = "Use Today", VerticalAlignment = VerticalAlignment.Center };
            int index = i; // capture for closure
            _chkToday[i].Checked += (s, e) => { _txtDates[index].IsEnabled = false; _txtDates[index].Text = "<Auto: Today's Date>"; };
            _chkToday[i].Unchecked += (s, e) => { _txtDates[index].IsEnabled = true; _txtDates[index].Text = ""; };
            Grid.SetColumn(_chkToday[i], 2);
            grid.Children.Add(_chkToday[i]);

            DatesPanel.Children.Add(grid);
         }
      }

      private void LoadData()
      {
         TxtSchoolYear.Text = GlobalSettings.ExportSchoolYear;
         TxtSemester.Text = GlobalSettings.ExportSemesterText;
         TxtDPTLCode.Text = GlobalSettings.DPTLCode;
         TxtDepartmentName.Text = GlobalSettings.DepartmentName;
         ComboAcademicLevel.SelectedIndex = GlobalSettings.IsUndergraduate ? 0 : 1;
         
         TxtDean1.Text = GlobalSettings.Dean1Name;
         TxtDean2.Text = GlobalSettings.Dean2Name;
         TxtDean3.Text = GlobalSettings.Dean3Name;
         TxtVPAcad.Text = GlobalSettings.VPAcademicName;
         TxtVPRes.Text = GlobalSettings.VPResearchName;
         TxtVPAdmin.Text = GlobalSettings.VPAdminName;
         TxtPresident.Text = GlobalSettings.PresidentName;

         SetDateRow(0, GlobalSettings.Date0UseToday, GlobalSettings.Date0Text);
         SetDateRow(1, GlobalSettings.Date1UseToday, GlobalSettings.Date1Text);
         SetDateRow(2, GlobalSettings.Date2UseToday, GlobalSettings.Date2Text);
         SetDateRow(3, GlobalSettings.Date3UseToday, GlobalSettings.Date3Text);
         SetDateRow(4, GlobalSettings.Date4UseToday, GlobalSettings.Date4Text);
         SetDateRow(5, GlobalSettings.Date5UseToday, GlobalSettings.Date5Text);
         SetDateRow(6, GlobalSettings.Date6UseToday, GlobalSettings.Date6Text);

         TxtWeeks.Text = GlobalSettings.WeeksPerSemester.ToString();
         TxtFormLec.Text = GlobalSettings.FormulaLecHours;
         TxtFormLab.Text = GlobalSettings.FormulaLabHours;
         TxtFormContact.Text = GlobalSettings.FormulaContactHours;
      }

      private void SetDateRow(int i, bool useToday, string text)
      {
         _chkToday[i].IsChecked = useToday;
         if (!useToday) _txtDates[i].Text = text;
      }

      private void Save_Click(object sender, RoutedEventArgs e)
      {
         GlobalSettings.ExportSchoolYear = TxtSchoolYear.Text;
         GlobalSettings.ExportSemesterText = TxtSemester.Text;
         GlobalSettings.DPTLCode = TxtDPTLCode.Text;
         GlobalSettings.DepartmentName = TxtDepartmentName.Text;
         GlobalSettings.IsUndergraduate = ComboAcademicLevel.SelectedIndex == 0;

         GlobalSettings.Dean1Name = TxtDean1.Text;
         GlobalSettings.Dean2Name = TxtDean2.Text;
         GlobalSettings.Dean3Name = TxtDean3.Text;
         GlobalSettings.VPAcademicName = TxtVPAcad.Text;
         GlobalSettings.VPResearchName = TxtVPRes.Text;
         GlobalSettings.VPAdminName = TxtVPAdmin.Text;
         GlobalSettings.PresidentName = TxtPresident.Text;

         GlobalSettings.Date0UseToday = _chkToday[0].IsChecked == true; GlobalSettings.Date0Text = _txtDates[0].Text;
         GlobalSettings.Date1UseToday = _chkToday[1].IsChecked == true; GlobalSettings.Date1Text = _txtDates[1].Text;
         GlobalSettings.Date2UseToday = _chkToday[2].IsChecked == true; GlobalSettings.Date2Text = _txtDates[2].Text;
         GlobalSettings.Date3UseToday = _chkToday[3].IsChecked == true; GlobalSettings.Date3Text = _txtDates[3].Text;
         GlobalSettings.Date4UseToday = _chkToday[4].IsChecked == true; GlobalSettings.Date4Text = _txtDates[4].Text;
         GlobalSettings.Date5UseToday = _chkToday[5].IsChecked == true; GlobalSettings.Date5Text = _txtDates[5].Text;
         GlobalSettings.Date6UseToday = _chkToday[6].IsChecked == true; GlobalSettings.Date6Text = _txtDates[6].Text;
         
         int.TryParse(TxtWeeks.Text, out int weeks);
         GlobalSettings.WeeksPerSemester = weeks > 0 ? weeks : 18;

         GlobalSettings.FormulaLecHours = TxtFormLec.Text;
         GlobalSettings.FormulaLabHours = TxtFormLab.Text;
         GlobalSettings.FormulaContactHours = TxtFormContact.Text;

         GlobalSettings.Save();
         MessageBox.Show("Export Settings Saved Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
         this.Close();
      }

      private void GenerateMock_Click(object sender, RoutedEventArgs e)
      {
         try
         {
            // Force a save to memory before generating mock
            Save_Click(null, null);

            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "TeachingLoad_Regular.xlsx");
            if (!File.Exists(templatePath))
            {
               MessageBox.Show("Could not find 'TeachingLoad_Regular.xlsx' in Templates folder.", "Error");
               return;
            }

            string mockPath = Path.Combine(Path.GetTempPath(), "MOCK_PREVIEW_SCHEDULE.xlsx");
            
            using (var workbook = new XLWorkbook(templatePath))
            {
               var ws = workbook.Worksheets.First();
               
               // Inject Mock Data
               ws.Cell("D8").Value = "DOE";
               ws.Cell("K8").Value = "JOHN";
               ws.Cell("R8").Value = "M.";
               ws.Cell("AE8").Value = "123 Mock Street";

               // Call the main window's helper to inject the signatures and formulas
               MainWindow.InjectMetadataAndFormulas(ws);

               // Inject 1 Mock Side-Table Class
               ws.Cell("AN21").Value = "Lec";
               ws.Cell("AN22").Value = "CS101";
               ws.Cell("AQ22").Value = 40; // Total Students
               ws.Cell("AT22").Value = 3; // Units
               ws.Cell("AW22").Value = 3 * GlobalSettings.WeeksPerSemester; // Total Hrs
               ws.Cell("BD22").Value = 3; // Lec Hrs
               ws.Cell("BE22").Value = 0; // Lab Hrs

               workbook.SaveAs(mockPath);
            }

            Process.Start(new ProcessStartInfo(mockPath) { UseShellExecute = true });
         }
         catch (Exception ex)
         {
            MessageBox.Show($"Error generating mock preview: {ex.Message}");
         }
      }
   }
}