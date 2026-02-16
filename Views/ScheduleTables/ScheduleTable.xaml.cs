using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; 
using System.Windows.Media;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
    public class ScheduleSlotArgs : EventArgs
    {
        public string Day { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty; 
        public ClassSchedule? ExistingSchedule { get; set; } 
    }

    public partial class ScheduleTable : UserControl
    {
        public bool ShowInstructor { get; set; } = true;
        public bool ShowRoom { get; set; } = true;
        public bool ShowSection { get; set; } = true;

        public event EventHandler<ScheduleSlotArgs>? SlotClicked;
        public event EventHandler<int>? InstructorJumpRequested;
        public event EventHandler<int>? RoomJumpRequested;     
        public event EventHandler<int>? SectionJumpRequested;


        public string Title
        {
            get { return TableTitle.Text; }
            set { TableTitle.Text = value; }
        }

        public ScheduleTable()
        {
            InitializeComponent();
            var defaultColumns = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            RefreshTable(defaultColumns, new List<ClassSchedule>());
        }

        private void MainScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (HeaderScroll != null) HeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
            if (TimeScroll != null) TimeScroll.ScrollToVerticalOffset(e.VerticalOffset);
        }

        public void RefreshTable(List<string> columnHeaders, List<ClassSchedule> data)
        {
            TimeSpan StartTime = new TimeSpan(GlobalSettings.StartTimeHour, 0, 0);
            TimeSpan EndTime = new TimeSpan(GlobalSettings.EndTimeHour, 0, 0);
            int IntervalMinutes = 30;

            HeaderGrid.Children.Clear(); HeaderGrid.ColumnDefinitions.Clear();
            TimeGrid.Children.Clear(); TimeGrid.RowDefinitions.Clear();
            ContentGrid.Children.Clear(); ContentGrid.RowDefinitions.Clear(); ContentGrid.ColumnDefinitions.Clear();

            // 1. PRE-CALCULATE SMART NAMES FOR THIS BATCH OF DATA
            // We extract all unique instructors referenced in this schedule list
            var uniqueInstructors = data
                .Where(s => s.Instructor != null)
                .Select(s => s.Instructor!)
                .DistinctBy(i => i.Id)
                .ToList();
            
            var nameMap = GetSmartInstructorNames(uniqueInstructors);

            var totalMinutes = (EndTime - StartTime).TotalMinutes;
            int totalRows = (int)(totalMinutes / IntervalMinutes);

            // --- HEADERS ---
            foreach (var headerText in columnHeaders)
            {
                HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                ContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

                var border = CreateHeaderBorder(headerText);
                Grid.SetColumn(border, HeaderGrid.ColumnDefinitions.Count - 1);
                HeaderGrid.Children.Add(border);
            }
            
            HeaderGrid.Margin = new Thickness(0, 0, SystemParameters.VerticalScrollBarWidth, 0);
            TimeGrid.Margin = new Thickness(0, 0, 0, SystemParameters.HorizontalScrollBarHeight);

            // --- ROWS & CELLS ---
            TimeSpan currentTime = StartTime;
            DateTime baseDate = DateTime.Today;

            for (int i = 0; i < totalRows; i++)
            {
                TimeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                ContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

                // Time Label
                var timeBorder = new Border
                {
                    Background = (Brush)Application.Current.Resources["BackgroundBrush1"],
                    BorderBrush = (Brush)Application.Current.Resources["TableLineBrush"],
                    BorderThickness = new Thickness(0, 0, 1, 1)
                };
                DateTime startDt = baseDate.Add(currentTime);
                DateTime endDt = startDt.AddMinutes(IntervalMinutes);
                string timeString = $"{startDt:h:mm}-{endDt:h:mm}{startDt:tt}".ToLower();

                timeBorder.Child = new TextBlock
                {
                    Text = timeString,
                    FontSize = 11, // Reduced font size for time labels
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                Grid.SetRow(timeBorder, i);
                TimeGrid.Children.Add(timeBorder);

                // Clickable Empty Cells
                for (int col = 0; col < columnHeaders.Count; col++)
                {
                    var cell = new Border
                    {
                        BorderBrush = (Brush)Application.Current.Resources["TableLineBrush"],
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Background = Brushes.Transparent,
                        Cursor = Cursors.Hand
                    };

                    string currentDay = columnHeaders[col];
                    string currentTimeStr = startDt.ToString("HH:mm");

                    cell.MouseLeftButtonDown += (s, e) => 
                    {
                        SlotClicked?.Invoke(this, new ScheduleSlotArgs 
                        { 
                            Day = currentDay, 
                            Time = currentTimeStr, 
                            ExistingSchedule = null 
                        });
                    };

                    Grid.SetRow(cell, i);
                    Grid.SetColumn(cell, col);
                    ContentGrid.Children.Add(cell);
                }
                currentTime = currentTime.Add(TimeSpan.FromMinutes(IntervalMinutes));
            }

            // --- DATA CARDS ---
            foreach (var cls in data)
            {
                if (string.IsNullOrEmpty(cls.StartTime) || string.IsNullOrEmpty(cls.EndTime)) continue;

                if (TimeSpan.TryParse(cls.StartTime, out TimeSpan start) &&
                    TimeSpan.TryParse(cls.EndTime, out TimeSpan end))
                {
                    int colIndex = columnHeaders.IndexOf(cls.Day);
                    if (colIndex >= 0)
                    {
                        double minutesFromStart = (start - StartTime).TotalMinutes;
                        int startRow = (int)Math.Floor(minutesFromStart / IntervalMinutes);
                        double durationMinutes = (end - start).TotalMinutes;
                        int rowSpan = (int)Math.Ceiling(durationMinutes / IntervalMinutes);

                        if (startRow >= 0 && rowSpan > 0)
                        {
                            var card = CreateScheduleCard(cls, nameMap);
                            
                            card.Cursor = Cursors.Hand;
                            card.MouseLeftButtonDown += (s, e) =>
                            {
                                e.Handled = true; 
                                SlotClicked?.Invoke(this, new ScheduleSlotArgs 
                                { 
                                    Day = cls.Day, 
                                    Time = cls.StartTime, 
                                    ExistingSchedule = cls 
                                });
                            };

                            Grid.SetColumn(card, colIndex);
                            Grid.SetRow(card, startRow);
                            Grid.SetRowSpan(card, rowSpan);
                            ContentGrid.Children.Add(card);
                        }
                    }
                }
            }
        }

        
        private Dictionary<int, string> GetSmartInstructorNames(List<Instructor> instructors)
        {
            var result = new Dictionary<int, string>();
            var parsedList = new List<(int Id, string LastName, string FirstInitial)>();

            foreach (var instr in instructors)
            {
                // Clean titles
                string cleanName = instr.Name
                    .Replace("Dr.", "").Replace("Mr.", "").Replace("Ms.", "").Replace("Mrs.", "")
                    .Replace("Prof.", "").Replace("Engr.", "").Trim();

                var parts = cleanName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string lastName = parts.Length > 0 ? parts.Last() : cleanName;
                string initial = parts.Length > 0 ? parts[0].Substring(0, 1) : "";

                parsedList.Add((instr.Id, lastName, initial));
            }

            var groups = parsedList.GroupBy(p => p.LastName);

            foreach (var group in groups)
            {
                if (group.Count() == 1)
                {
                    // Unique Last Name -> "Turing"
                    var item = group.First();
                    result[item.Id] = item.LastName;
                }
                else
                {
                    // Duplicate Last Name -> "A. Turing"
                    foreach (var item in group)
                    {
                        result[item.Id] = $"{item.FirstInitial}. {item.LastName}";
                    }
                }
            }
            return result;
        }

        private Border CreateHeaderBorder(string text)
        {
            var border = new Border
            {
                Background = (Brush)Application.Current.Resources["SidebarBrush"],
                BorderBrush = (Brush)Application.Current.Resources["TextDarkBrush"],
                BorderThickness = new Thickness(0, 0, 1, 1)
            };
            border.Child = new TextBlock
            {
                Text = text,
                Foreground = (Brush)Application.Current.Resources["BackgroundBrush"],
                FontWeight = FontWeights.SemiBold,
                FontSize = 14, 
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return border;
        }

        private Border CreateScheduleCard(ClassSchedule cls, Dictionary<int, string> nameMap)
        {
            var cardBackground = (Brush)Application.Current.Resources["PrimaryBrush"];
            string toolTipText = "Click to Edit";

            
            if (cls.Room != null && cls.Section != null)
            {
                if (cls.Section.StudentCount > cls.Room.Capacity)
                {
                    // Set to Warning Red
                    cardBackground = (Brush)Application.Current.Resources["DangerBrush"]; 
                    
                    // Add details to tooltip
                    toolTipText = $"⚠️ OVERCROWDED!\n" +
                                  $"Students: {cls.Section.StudentCount}\n" +
                                  $"Room Cap: {cls.Room.Capacity}\n\n" +
                                  "Click to Edit";
                }
            }

            if (cls.InstructorId == null)
            {
                cardBackground = (Brush)Application.Current.Resources["DangerBrush"];
                toolTipText = "⚠️ INSTRUCTOR TBA\nClick to Assign";
            }

            var card = new Border
            {
                Background = cardBackground,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(3, 1, 3, 1),
                Padding = new Thickness(2),
                ToolTip = toolTipText
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // --- 1. PREPARE INFO STRING (Moved to Top) ---
            string info = "";

            // Room (Hide if ShowRoom=False or Room is missing)
            if (ShowRoom && cls.Room != null)
                info += cls.Room.Name + Environment.NewLine;

            // Section (Hide if ShowSection=False or Section is missing)
            if (ShowSection && cls.Section != null)
                info += $"{cls.Section.Program} {cls.Section.YearLevel}{cls.Section.Name}" + Environment.NewLine;

            // Instructor (Hide if ShowInstructor=False)
            if (ShowInstructor)
            {
                string instrName = "TBA";
                if (cls.InstructorId != null && nameMap.ContainsKey(cls.InstructorId.Value))
                {
                    instrName = nameMap[cls.InstructorId.Value];
                }
                else if (cls.Instructor != null)
                {
                    instrName = cls.Instructor.Name;
                }
                info += instrName;
            }

            // --- 2. BUILD UI ---
            
            // Course Code (Always Show)
            stack.Children.Add(new TextBlock
            {
                Text = cls.Course?.Code ?? "Subject",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = (Brush)Application.Current.Resources["BackgroundBrush"],
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });

            // Info Block (Uses the string we built above)
            stack.Children.Add(new TextBlock
            {
                Text = info.Trim(),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["BackgroundBrush"],
                TextAlignment = TextAlignment.Center,
            });

            // --- 3. CONTEXT MENU ---
            var contextMenu = new ContextMenu();

            // A. Jump to Instructor (Check for null)
            if (ShowInstructor && cls.InstructorId != null)
            {
                var item = new MenuItem { Header = "Jump to Instructor" };
                item.Click += (s, e) => InstructorJumpRequested?.Invoke(this, cls.InstructorId.Value);
                contextMenu.Items.Add(item);
            }

            // B. Jump to Room (Check for null)
            if (ShowRoom && cls.RoomId != null)
            {
                var item = new MenuItem { Header = "Jump to Room" };
                item.Click += (s, e) => RoomJumpRequested?.Invoke(this, cls.RoomId.Value);
                contextMenu.Items.Add(item);
            }

            // C. Jump to Section (FIXED: SectionId is likely not nullable)
            // Removed 'cls.SectionId != null' check and '.Value' property
            if (ShowSection) 
            {
                var item = new MenuItem { Header = "Jump to Section" };
                item.Click += (s, e) => SectionJumpRequested?.Invoke(this, cls.SectionId!.Value);
                contextMenu.Items.Add(item);
            }

            if (contextMenu.Items.Count > 0)
            {
                card.ContextMenu = contextMenu;
            }

            card.Child = stack;
            return card;
        }
    }
}