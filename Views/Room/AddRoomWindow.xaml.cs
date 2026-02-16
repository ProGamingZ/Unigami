using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
    public partial class AddRoomWindow : Window
    {
        private int _editingId = 0;

        public AddRoomWindow()
        {
            InitializeComponent();
        }

        public AddRoomWindow(Room roomToEdit)
        {
            InitializeComponent();
            _editingId = roomToEdit.Id;
            this.Title = "Edit Room";

            // Fill Fields
            NameTxt.Text = roomToEdit.Name;
            FloorTxt.Text = roomToEdit.FloorLevel.ToString();
            CapTxt.Text = roomToEdit.Capacity.ToString();
            
            // Equipment
            ChairsTxt.Text = roomToEdit.ChairCount.ToString();
            TablesTxt.Text = roomToEdit.TableCount.ToString();
            CompsTxt.Text = roomToEdit.ComputerCount.ToString();
            CeilFanTxt.Text = roomToEdit.CeilingFanCount.ToString();
            StandFanTxt.Text = roomToEdit.StandFanCount.ToString();
            AcTxt.Text = roomToEdit.AirConCount.ToString();
            WhiteboardTxt.Text = roomToEdit.WhiteboardCount.ToString();
            MonitorTxt.Text = roomToEdit.MonitorCount.ToString();
            ProjectorTxt.Text = roomToEdit.ProjectorCount.ToString();

            // Select Type
            foreach (ComboBoxItem item in TypeCombo.Items)
                if (item.Content.ToString() == roomToEdit.Type) 
                    TypeCombo.SelectedItem = item;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validate Name
            if (string.IsNullOrWhiteSpace(NameTxt.Text)) 
            { 
                MessageBox.Show("Room Name is required."); 
                return; 
            }

            // 2. Validate Numbers
            int Parse(string t, string fieldName) 
            {
                if (!int.TryParse(t, out int v) || v < 0) return -1;
                return v;
            }

            int floor = int.TryParse(FloorTxt.Text, out int f) ? f : 1; // Allow negative floors (basement)
            int cap = Parse(CapTxt.Text, "Capacity");
            
            if (cap < 0) { MessageBox.Show("Capacity must be positive."); return; }

            // Helper to parse equipment (defaults to 0 if invalid, that's fine for inventory)
            int ParseQty(string t) => int.TryParse(t, out int v) && v >= 0 ? v : 0;

            using (var db = new AppDbContext())
            {
                if (_editingId == 0)
                {
                    // --- CREATE ---
                    var newRoom = new Room
                    {
                        Name = NameTxt.Text,
                        Type = (TypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Classroom",
                        FloorLevel = floor,
                        Capacity = cap,
                        ChairCount = ParseQty(ChairsTxt.Text),
                        TableCount = ParseQty(TablesTxt.Text),
                        ComputerCount = ParseQty(CompsTxt.Text),
                        CeilingFanCount = ParseQty(CeilFanTxt.Text),
                        StandFanCount = ParseQty(StandFanTxt.Text),
                        AirConCount = ParseQty(AcTxt.Text),
                        WhiteboardCount = ParseQty(WhiteboardTxt.Text),
                        MonitorCount = ParseQty(MonitorTxt.Text),
                        ProjectorCount = ParseQty(ProjectorTxt.Text)
                    };
                    db.Rooms.Add(newRoom);
                }
                else
                {
                    // --- UPDATE ---
                    var existing = db.Rooms.Find(_editingId);
                    if (existing != null)
                    {
                        existing.Name = NameTxt.Text;
                        existing.Type = (TypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Classroom";
                        existing.FloorLevel = floor;
                        existing.Capacity = cap;
                        existing.ChairCount = ParseQty(ChairsTxt.Text);
                        existing.TableCount = ParseQty(TablesTxt.Text);
                        existing.ComputerCount = ParseQty(CompsTxt.Text);
                        existing.CeilingFanCount = ParseQty(CeilFanTxt.Text);
                        existing.StandFanCount = ParseQty(StandFanTxt.Text);
                        existing.AirConCount = ParseQty(AcTxt.Text);
                        existing.WhiteboardCount = ParseQty(WhiteboardTxt.Text);
                        existing.MonitorCount = ParseQty(MonitorTxt.Text);
                        existing.ProjectorCount = ParseQty(ProjectorTxt.Text);
                    }
                }
                db.SaveChanges();
            }

            MessageBox.Show("Room Saved!");
            this.Close();
        }
    }
}   