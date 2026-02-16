using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UniversityScheduler.Data;
using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Views
{
    public partial class RoomsView : UserControl
    {
        private List<Room> _allRooms = new List<Room>();

        public RoomsView()
        {
            InitializeComponent();
            LoadRooms();
        }

        private void LoadRooms()
        {
            using (var db = new AppDbContext())
            {
                // Cache data
                _allRooms = db.Rooms.OrderBy(r => r.FloorLevel).ThenBy(r => r.Name).ToList();
                RoomsGrid.ItemsSource = _allRooms;
            }
        }

        
        private void SearchTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchTxt.Text.ToLower();
            if (string.IsNullOrWhiteSpace(query))
            {
                RoomsGrid.ItemsSource = _allRooms;
            }
            else
            {
                var filtered = _allRooms.Where(r => 
                    r.Name.ToLower().Contains(query) || 
                    r.Type.ToLower().Contains(query)
                ).ToList();
                RoomsGrid.ItemsSource = filtered;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddRoomWindow();
            win.Topmost = true;
            win.ShowDialog();
            LoadRooms(); 
            MainWindow.TriggerDatabaseUpdated();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (RoomsGrid.SelectedItem is Room selectedRoom)
            {
                var win = new AddRoomWindow(selectedRoom);
                win.Topmost = true;
                win.ShowDialog();
                LoadRooms();
                MainWindow.TriggerDatabaseUpdated();
            }
            else
            {
                MessageBox.Show("Please select a room to edit.");
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (RoomsGrid.SelectedItem is Room selectedRoom)
            {
                // 1. OPEN DATABASE CONNECTION FIRST
                using (var db = new AppDbContext())
                {
                    // 2. NOW WE CAN USE 'db' FOR THE CHECK
                    if (db.Schedules.Any(s => s.RoomId == selectedRoom.Id))
                    {
                        MessageBox.Show($"Cannot delete {selectedRoom.Name} because there are classes scheduled in it.\nPlease clear the room schedule first.", "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 3. CONFIRM AND DELETE
                    if (MessageBox.Show($"Delete {selectedRoom.Name}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        db.Rooms.Remove(selectedRoom);
                        db.SaveChanges();
                        
                        LoadRooms();
                        MainWindow.TriggerDatabaseUpdated();
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a room to delete.");
            }
        }
    
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new AppDbContext())
            {
                //  SAFETY CHECK: Check if ANY room is being used
                if (db.Schedules.Any(s => s.RoomId != null))
                {
                    MessageBox.Show("Cannot reset Rooms because there are active Class Schedules.\n" +
                                    "Please clear the Schedules first via the Dashboard.", 
                                    "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (MessageBox.Show("Are you sure you want to delete ALL Rooms?", 
                    "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    if (MessageBox.Show("Confirming again: Delete ALL Rooms?", 
                        "Final Check", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                    {
                        db.Rooms.ExecuteDelete();
                        LoadRooms();
                        MainWindow.TriggerDatabaseUpdated();
                    }
                }
            }
        }

    }
}