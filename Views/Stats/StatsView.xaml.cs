using System.Linq;
using System.Windows.Controls;
using UniversityScheduler.Data;

namespace UniversityScheduler.Views
{
    public partial class StatsView : UserControl
    {
        public StatsView()
        {
            InitializeComponent();
            LoadStats();
        }

        private void LoadStats()
        {
            using (var db = new AppDbContext())
            {
                // Simple counts
                InstructorCount.Text = db.Instructors.Count().ToString();
                CourseCount.Text = db.Courses.Count().ToString();
                SectionCount.Text = db.Sections.Count().ToString();
                RoomCount.Text = db.Rooms.Count().ToString();
            }
        }
    }
}