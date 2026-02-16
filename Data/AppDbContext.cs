using Microsoft.EntityFrameworkCore;

namespace UniversityScheduler.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Instructor> Instructors { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<StudentSection> Sections { get; set; }
        public DbSet<ClassSchedule> Schedules { get; set; }
        public DbSet<Curriculum> Curriculums { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite("Data Source=schedule.db");
        }
    }
}