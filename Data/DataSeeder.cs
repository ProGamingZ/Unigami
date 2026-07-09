using System.Collections.Generic;
using System.Linq;
using UniversityScheduler.Data;

namespace UniversityScheduler.Data
{
    public static class DataSeeder
    {
        public static void SeedCourses()
        {
            using (var db = new AppDbContext())
            {
                // 1. Check if courses already exist to prevent duplicates
                if (db.Courses.Any()) return; 

                var courses = new List<Course>
                {
                    // --- General Education (Shared by all) ---
                    new Course { Code = "GE1", Name = "Understanding the Self", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "GE2", Name = "Readings in Philippine History", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "GE3", Name = "Environmental Science", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "GE4", Name = "Mathematics in the Modern World", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "GE5", Name = "Purposive Communication", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "GE6", Name = "Science, Technology, and Society", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "GE7", Name = "Art Appreciation", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "GE8", Name = "Ethics (Values Formation/Professionalism)", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "GE9", Name = "Rizals Life and Works", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    
                    // --- NSTP & PE ---
                    new Course { Code = "NSTP1", Name = "National Service Training Program 1", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "NSTP2", Name = "National Service Training Program 2", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "PATHFIT1", Name = "Movement Competency Training", Units = 2, LectureHours = 2, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "PATHFIT2", Name = "Exercise-Based Fitness Activities", Units = 2, LectureHours = 2, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "PATHFIT3", Name = "Dance, Sports, Martial Arts, Group Exercise", Units = 2, LectureHours = 2, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "PATHFIT4", Name = "Outdoor and Adventure Activities", Units = 2, LectureHours = 2, LabHours = 0, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },

                    // --- ITE Common Core ---
                    new Course { Code = "ITE1", Name = "Introduction to Computing", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC" },
                    new Course { Code = "ITE2", Name = "Computer Programming 1", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC", PrerequisiteCodes = "ITE1" },
                    new Course { Code = "ITE3", Name = "Computer Programming 2", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC", PrerequisiteCodes = "ITE2" },
                    new Course { Code = "ITE4", Name = "Data Structures and Algorithms", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS, BSIT, BSIS, BSEMC", PrerequisiteCodes = "ITE2" },

                    // --- BSIT Major ---
                    new Course { Code = "IT101", Name = "Introduction to Human Computer Interaction", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT102", Name = "Fundamentals of Database", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT", PrerequisiteCodes = "ITE1" },
                    new Course { Code = "IT103", Name = "Discrete Mathematics", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT104", Name = "Networking 1", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT", PrerequisiteCodes = "IT103" },
                    new Course { Code = "IT105", Name = "Object Oriented Programming", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT106", Name = "Information Management", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT107", Name = "Platform Technologies", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT108", Name = "Application Development", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT", PrerequisiteCodes = "IT104" },
                    new Course { Code = "IT109", Name = "Networking 2", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT201", Name = "Web Systems and Technologies", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT202", Name = "Mobile Programming 1", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT203", Name = "Mobile Programming 2", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT", PrerequisiteCodes = "IT202" },
                    new Course { Code = "IT204", Name = "Advanced Networking 1 (CCNA)", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT205", Name = "Advanced Networking 2 (CCNA)", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT", PrerequisiteCodes = "IT204" },
                    // BSIT Missing Courses Added:
                    new Course { Code = "IT206", Name = "Platform Technologies 2", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT", PrerequisiteCodes = "IT107" },
                    new Course { Code = "IT207", Name = "Capstone Project 1", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIT", PrerequisiteCodes = "IT205" },
                    new Course { Code = "IT208", Name = "Mobile Programming 2 (alt)", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT", PrerequisiteCodes = "IT202" },
                    new Course { Code = "IT209", Name = "Project Management", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT210", Name = "Information Assurance and Security 1", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT", PrerequisiteCodes = "IT104" },
                    new Course { Code = "IT211", Name = "Information Assurance and Security 2", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT212", Name = "System Administration and Maintenance", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIT", PrerequisiteCodes = "IT207" },
                    new Course { Code = "IT213", Name = "Quantitative Methods", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIT" },
                    new Course { Code = "IT214", Name = "Practicum (600 hours)", Units = 6, LectureHours = 0, LabHours = 6, RecommendedPrograms = "BSIT", PrerequisiteCodes = "IT207" },

                    // --- BSCS Major ---
                    new Course { Code = "CS101", Name = "Computer Programming 1", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS" },
                    new Course { Code = "CS102", Name = "Computer Programming 2", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS", PrerequisiteCodes = "CS101" },
                    new Course { Code = "CS201", Name = "Algorithms and Complexity", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS", PrerequisiteCodes = "CS102" },
                    new Course { Code = "CS202", Name = "Math for Computer Science / Discrete Structures", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS" },
                    new Course { Code = "CS203", Name = "Architecture and Organization", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS" },
                    new Course { Code = "CS204", Name = "Object Oriented Programming (CS)", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS", PrerequisiteCodes = "CS102" },
                    new Course { Code = "CS205", Name = "Operating Systems", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS" },
                    new Course { Code = "CS206", Name = "Automata Theory and Formal Languages", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS" },
                    new Course { Code = "CS207", Name = "Software Engineering 1", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS" },
                    new Course { Code = "CS208", Name = "Software Engineering 2", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS", PrerequisiteCodes = "CS204" },
                    new Course { Code = "CS209", Name = "Programming Languages", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS", PrerequisiteCodes = "CS208" },
                    // BSCS Missing Courses Added:
                    new Course { Code = "CS210", Name = "Computer Networks", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS", PrerequisiteCodes = "CS203" },
                    new Course { Code = "CS211", Name = "Multimedia", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS" },
                    new Course { Code = "CS212", Name = "CS Thesis Writing 1", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS" },
                    new Course { Code = "CS213", Name = "CS Thesis Writing 2", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS" },
                    new Course { Code = "CS214", Name = "Integrative Programming 1", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS", PrerequisiteCodes = "CS102" },
                    new Course { Code = "CS215", Name = "Integrative Programming 2", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS", PrerequisiteCodes = "CS214" },
                    new Course { Code = "CS216", Name = "Advanced Algorithms", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSCS", PrerequisiteCodes = "CS201" },
                    new Course { Code = "CS217", Name = "Data Mining 1", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS", PrerequisiteCodes = "CS201" },
                    new Course { Code = "CS218", Name = "Data Mining 2", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSCS", PrerequisiteCodes = "CS217" },
                    new Course { Code = "CS219", Name = "Practicum (240 hours)", Units = 3, LectureHours = 0, LabHours = 3, RecommendedPrograms = "BSCS" },

                    // --- BSIS Major ---
                    new Course { Code = "IS101", Name = "Fundamentals of Information Systems", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIS" },
                    new Course { Code = "IS102", Name = "Systems Analysis and Design 1", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIS", PrerequisiteCodes = "IS101" },
                    new Course { Code = "IS103", Name = "Systems Analysis and Design 2", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIS", PrerequisiteCodes = "IS102" },
                    new Course { Code = "IS104", Name = "Financial Management", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIS" },
                    // BSIS Missing Courses Added:
                    new Course { Code = "IS105", Name = "Quantitative Methods", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIS" },
                    new Course { Code = "IS106", Name = "Enterprise Architecture", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIS", PrerequisiteCodes = "IS101" },
                    new Course { Code = "IS107", Name = "IS Project Management 1", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIS" },
                    new Course { Code = "IS108", Name = "IS Project Management 2", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIS", PrerequisiteCodes = "IS107" },
                    new Course { Code = "IS109", Name = "IS Security Management", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIS" },
                    new Course { Code = "IS110", Name = "Evaluation of Business Performance", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIS" },
                    new Course { Code = "IS111", Name = "Business Intelligence", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIS", PrerequisiteCodes = "IS102" },
                    new Course { Code = "IS112", Name = "IS Capstone Project 1", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIS" },
                    new Course { Code = "IS113", Name = "IS Capstone Project 2", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSIS" },
                    new Course { Code = "IS114", Name = "Human Computer Interaction (IS)", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIS" },
                    new Course { Code = "IS115", Name = "Innovation and New Technologies", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSIS" },
                    new Course { Code = "IS116", Name = "Practicum (600 hours)", Units = 6, LectureHours = 0, LabHours = 6, RecommendedPrograms = "BSIS" },

                    // --- BSEMC Major ---
                    new Course { Code = "EMC100", Name = "Freehand and Digital Drawing", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "EMC201", Name = "Introduction to Game Design and Development", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "EMC202", Name = "Computer Graphics Programming", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    // BSEMC Missing Courses Added:
                    new Course { Code = "EMC203", Name = "Usability, HCI, and User Interaction", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "EMC204", Name = "Design and Production Process", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "EMC205", Name = "Applications Development and Emerging Technologies", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "EMC206", Name = "Principles of 3D Animation", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "EMC207", Name = "Capstone Project 1", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSEMC", PrerequisiteCodes = "EMC204" },
                    new Course { Code = "EMC208", Name = "Capstone Project 2", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "DA201", Name = "Digital Photography", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "DA202", Name = "Image and Video Processing", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "DA203", Name = "Principles of 2D Animation", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "DA204", Name = "Advanced 2D Animation", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC", PrerequisiteCodes = "DA203" },
                    new Course { Code = "DA205", Name = "Advanced Sound Production", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "DA206", Name = "Principles of 3D Animation", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "DA207", Name = "Advanced 3D Animation and Scripting", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC", PrerequisiteCodes = "DA206" },
                    new Course { Code = "DA208", Name = "Modeling and Rigging", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC", PrerequisiteCodes = "DA206" },
                    new Course { Code = "DA209", Name = "Digital Image Manipulation and Typography", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "DA210", Name = "Multimedia 1", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "DA211", Name = "Multimedia 2", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC", PrerequisiteCodes = "DA210" },
                    new Course { Code = "DA212", Name = "Texture and Mapping", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC", PrerequisiteCodes = "DA208" },
                    new Course { Code = "DA213", Name = "Compositing and Rendering", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC", PrerequisiteCodes = "DA208" },
                    new Course { Code = "DA214", Name = "Animation Design and Production", Units = 3, LectureHours = 2, LabHours = 3, RecommendedPrograms = "BSEMC", PrerequisiteCodes = "DA204" },
                    new Course { Code = "DA215", Name = "Entrepreneurship", Units = 3, LectureHours = 3, LabHours = 0, RecommendedPrograms = "BSEMC" },
                    new Course { Code = "DA216", Name = "Internship (486 hours)", Units = 9, LectureHours = 0, LabHours = 9, RecommendedPrograms = "BSEMC", PrerequisiteCodes = "DA207" }
                };

                db.Courses.AddRange(courses);
                db.SaveChanges();
            }
        }
        public static void SeedPrograms()
        {
            using (var db = new AppDbContext())
            {
                if (db.Programs.Any()) return;

                db.Programs.AddRange(
                    new DegreeProgram { Code = "BSCS", Description = "Computer Science" },
                    new DegreeProgram { Code = "BSIT", Description = "Information Technology" },
                    new DegreeProgram { Code = "BSIS", Description = "Information Systems" },
                    new DegreeProgram { Code = "BSEMC", Description = "Entertainment & Multimedia Computing" }
                );
                db.SaveChanges();
            }
        }
        public static void SeedInstructors()
        {
            using (var db = new AppDbContext())
            {
                // Prevent duplicate seeding
                if (db.Instructors.Any()) return;

                // 1. Fetch Rooms to assign to Full-time instructors
                // We use a dictionary for easy lookup by Name
                var rooms = db.Rooms.ToDictionary(r => r.Name, r => r.Id);

                // Helper to safely get a Room ID (returns null if room doesn't exist)
                int? GetRoom(string name) => rooms.ContainsKey(name) ? rooms[name] : null;

                var instructors = new List<Instructor>
                {
                    // --- BSCS Faculty ---
                    
                    // Dean: Assigned to a Lecture Room. Prefers Seniors/Thesis (Year 4).
                    new Instructor { 
                        FirstName = "Alan", Surname = "Turing", Initials = "AT",
                        ProgramSem1 = "BSCS", StatusSem1 = "Full-time", MaxUnitsSem1 = 24, SchedulePreferencesSem1 = "Mon,Wed|08:00-11:00;Tue,Thu|13:00-16:00", PreferredYearLevelsSem1 = "4", AssignedRoomIdSem1 = GetRoom("301"),
                        ProgramSem2 = "BSCS", StatusSem2 = "Full-time", MaxUnitsSem2 = 24, SchedulePreferencesSem2 = "Mon,Wed|08:00-11:00;Tue,Thu|13:00-16:00", PreferredYearLevelsSem2 = "4", AssignedRoomIdSem2 = GetRoom("301")
                    },

                    // Full-time Prof: Teaches core programming. Prefers Freshmen (Year 1, 2). Assigned to a Lab.
                    new Instructor { 
                        FirstName = "Grace", Surname = "Hopper", Initials = "GH", 
                        ProgramSem1 = "BSCS", StatusSem1 = "Full-time", MaxUnitsSem1 = 21, SchedulePreferencesSem1 = "Mon,Wed,Fri|07:30-11:30;Mon,Wed,Fri|13:00-16:00", PreferredYearLevelsSem1 = "1,2", AssignedRoomIdSem1 = GetRoom("Lab 1"),
                        ProgramSem2 = "BSCS", StatusSem2 = "Full-time", MaxUnitsSem2 = 21, SchedulePreferencesSem2 = "Mon,Wed,Fri|07:30-11:30;Mon,Wed,Fri|13:00-16:00", PreferredYearLevelsSem2 = "1,2", AssignedRoomIdSem2 = GetRoom("Lab 1") 
                    },

                    // Part-time Industry: No room assigned. Prefers specialized Major subjects (Year 3).
                    new Instructor { 
                        FirstName = "Linus", Surname = "Torvalds", Initials = "LT", 
                        ProgramSem1 = "BSCS", StatusSem1 = "Part-time", MaxUnitsSem1 = 12, SchedulePreferencesSem1 = "Mon,Wed,Fri|17:30-20:30;Sat|08:00-12:00", PreferredYearLevelsSem1 = "3", AssignedRoomIdSem1 = null,
                        ProgramSem2 = "BSCS", StatusSem2 = "Part-time", MaxUnitsSem2 = 12, SchedulePreferencesSem2 = "Mon,Wed,Fri|17:30-20:30;Sat|08:00-12:00", PreferredYearLevelsSem2 = "3", AssignedRoomIdSem2 = null 
                    },

                    // Visiting Lecturer: No room assigned.
                    new Instructor { 
                        FirstName = "Ada", Surname = "Lovelace", Initials = "AL", 
                        ProgramSem1 = "BSCS", StatusSem1 = "Visiting", MaxUnitsSem1 = 9, SchedulePreferencesSem1 = "Tue|13:00-17:00;Thu|09:00-12:00", PreferredYearLevelsSem1 = "2,3", AssignedRoomIdSem1 = null,
                        ProgramSem2 = "BSCS", StatusSem2 = "Visiting", MaxUnitsSem2 = 9, SchedulePreferencesSem2 = "Tue|13:00-17:00;Thu|09:00-12:00", PreferredYearLevelsSem2 = "2,3", AssignedRoomIdSem2 = null
                    },

                    // Junior Instructor: Assigned to a Lab. Takes lower years.
                    new Instructor { 
                        FirstName = "Dennis", Surname = "Ritchie", Initials = "DR", 
                        ProgramSem1 = "BSCS", StatusSem1 = "Full-time", MaxUnitsSem1 = 24, SchedulePreferencesSem1 = "Mon,Tue,Thu|09:00-12:00;Mon,Tue,Thu|13:00-18:00;Fri|09:00-12:00", PreferredYearLevelsSem1 = "1,2", AssignedRoomIdSem1 = GetRoom("Lab 2"),
                        ProgramSem2 = "BSCS", StatusSem2 = "Full-time", MaxUnitsSem2 = 24, SchedulePreferencesSem2 = "Mon,Tue,Thu|09:00-12:00;Mon,Tue,Thu|13:00-18:00;Fri|09:00-12:00", PreferredYearLevelsSem2 = "1,2", AssignedRoomIdSem2 = GetRoom("Lab 2")
                    },


                    // --- BSIT Faculty ---

                    // Senior Prof: Assigned Room 401. Prefers Seniors.
                    new Instructor {  
                        FirstName = "Tim", Surname = "Berners-Lee", Initials = "TBL", 
                        ProgramSem1 = "BSIT", StatusSem1 = "Full-time", MaxUnitsSem1 = 24, SchedulePreferencesSem1 = "Mon,Tue,Wed,Thu|08:00-12:00;Mon,Tue,Wed,Thu|13:00-16:00", PreferredYearLevelsSem1 = "3,4", AssignedRoomIdSem1 = GetRoom("401"),
                        ProgramSem2 = "BSIT", StatusSem2 = "Full-time", MaxUnitsSem2 = 24, SchedulePreferencesSem2 = "Mon,Tue,Wed,Thu|08:00-12:00;Mon,Tue,Wed,Thu|13:00-16:00", PreferredYearLevelsSem2 = "3,4", AssignedRoomIdSem2 = GetRoom("401")
                    },

                    // Networking Specialist: Assigned a Lab.
                    new Instructor { 
                        FirstName = "Vint", Surname = "Cerf", Initials = "VC", 
                        ProgramSem1 = "BSIT", StatusSem1 = "Full-time", MaxUnitsSem1 = 9, SchedulePreferencesSem1 = "Sat|08:00-12:00;Sat|13:00-17:00", PreferredYearLevelsSem1 = "3,4", AssignedRoomIdSem1 = GetRoom("Lab 3"),
                        ProgramSem2 = "BSIT", StatusSem2 = "Full-time", MaxUnitsSem2 = 9, SchedulePreferencesSem2 = "Sat|08:00-12:00;Sat|13:00-17:00", PreferredYearLevelsSem2 = "3,4", AssignedRoomIdSem2 = GetRoom("Lab 3")
                    },

                    // Full-time: Assigned Room 302.
                    new Instructor {  
                        FirstName = "Radia", Surname = "Perlman", Initials = "RP", 
                        ProgramSem1 = "BSIT", StatusSem1 = "Full-time", MaxUnitsSem1 = 21, SchedulePreferencesSem1 = "Tue,Thu,Fri|07:00-12:00;Tue,Thu|13:00-15:00", PreferredYearLevelsSem1 = "1,2", AssignedRoomIdSem1 = GetRoom("302"),
                        ProgramSem2 = "BSIT", StatusSem2 = "Full-time", MaxUnitsSem2 = 21, SchedulePreferencesSem2 = "Tue,Thu,Fri|07:00-12:00;Tue,Thu|13:00-15:00", PreferredYearLevelsSem2 = "1,2", AssignedRoomIdSem2 = GetRoom("302")
                    },

                    // Part-time: No room.
                    new Instructor { 
                        FirstName = "Margaret", Surname = "Hamilton", Initials = "MH", 
                        ProgramSem1 = "BSIT", StatusSem1 = "Part-time", MaxUnitsSem1 = 12, SchedulePreferencesSem1 = "Mon,Wed,Fri|07:30-11:30", PreferredYearLevelsSem1 = "1,2", AssignedRoomIdSem1 = null,
                        ProgramSem2 = "BSIT", StatusSem2 = "Part-time", MaxUnitsSem2 = 12, SchedulePreferencesSem2 = "Mon,Wed,Fri|07:30-11:30", PreferredYearLevelsSem2 = "1,2", AssignedRoomIdSem2 = null
                    },

                    // Probationary Full-time: Assigned Room 404.
                    new Instructor { 
                        FirstName = "Ken", Surname = "Thompson", Initials = "KT", 
                        ProgramSem1 = "BSIT", StatusSem1 = "Full-time", MaxUnitsSem1 = 18, SchedulePreferencesSem1 = "Mon,Wed|10:00-14:00;Tue,Thu|16:00-20:00", PreferredYearLevelsSem1 = "1,2", AssignedRoomIdSem1 = GetRoom("404"),
                        ProgramSem2 = "BSIT", StatusSem2 = "Full-time", MaxUnitsSem2 = 18, SchedulePreferencesSem2 = "Mon,Wed|10:00-14:00;Tue,Thu|16:00-20:00", PreferredYearLevelsSem2 = "1,2", AssignedRoomIdSem2 = GetRoom("404")
                    },


                    // --- BSIS Faculty ---

                    // Dept Chair: Assigned Room 303.
                    new Instructor { 
                        FirstName = "Edgar", Surname = "Codd", Initials = "EC", 
                        ProgramSem1 = "BSIS", StatusSem1 = "Full-time", MaxUnitsSem1 = 24, SchedulePreferencesSem1 = "Mon,Tue,Wed,Thu|09:00-12:00;Mon,Tue,Wed,Thu|13:30-16:30", PreferredYearLevelsSem1 = "3,4", AssignedRoomIdSem1 = GetRoom("303"),
                        ProgramSem2 = "BSIS", StatusSem2 = "Full-time", MaxUnitsSem2 = 24, SchedulePreferencesSem2 = "Mon,Tue,Wed,Thu|09:00-12:00;Mon,Tue,Wed,Thu|13:30-16:30", PreferredYearLevelsSem2 = "3,4", AssignedRoomIdSem2 = GetRoom("303")
                    },

                    // Visiting
                    new Instructor { 
                        FirstName = "Peter", Surname = "Chen", Initials = "PC", 
                        ProgramSem1 = "BSIS", StatusSem1 = "Visiting", MaxUnitsSem1 = 6, SchedulePreferencesSem1 = "Wed|13:00-17:00", PreferredYearLevelsSem1 = "3", AssignedRoomIdSem1 = null,
                        ProgramSem2 = "BSIS", StatusSem2 = "Visiting", MaxUnitsSem2 = 6, SchedulePreferencesSem2 = "Wed|13:00-17:00", PreferredYearLevelsSem2 = "3", AssignedRoomIdSem2 = null
                    },

                    // Full-time: Assigned Room 304.
                    new Instructor { 
                        FirstName = "Barbara", Surname = "Liskov", Initials = "BL", 
                        ProgramSem1 = "BSIS", StatusSem1 = "Full-time", MaxUnitsSem1 = 24, SchedulePreferencesSem1 = "Mon,Tue,Thu,Fri|08:00-12:00;Mon,Tue,Thu,Fri|13:00-17:00", PreferredYearLevelsSem1 = "1,2", AssignedRoomIdSem1 = GetRoom("304"),
                        ProgramSem2 = "BSIS", StatusSem2 = "Full-time", MaxUnitsSem2 = 24, SchedulePreferencesSem2 = "Mon,Tue,Thu,Fri|08:00-12:00;Mon,Tue,Thu,Fri|13:00-17:00", PreferredYearLevelsSem2 = "1,2", AssignedRoomIdSem2 = GetRoom("304")
                    },

                    // Part-time
                    new Instructor {  
                        FirstName = "Larry", Surname = "Ellison", Initials = "LE", 
                        ProgramSem1 = "BSIS", StatusSem1 = "Part-time", MaxUnitsSem1 = 9, SchedulePreferencesSem1 = "Tue,Thu|17:30-20:30", PreferredYearLevelsSem1 = "4", AssignedRoomIdSem1 = null,
                        ProgramSem2 = "BSIS", StatusSem2 = "Part-time", MaxUnitsSem2 = 9, SchedulePreferencesSem2 = "Tue,Thu|17:30-20:30", PreferredYearLevelsSem2 = "4", AssignedRoomIdSem2 = null
                    },

                    // Full-time: Assigned Lab 4.
                    new Instructor { 
                        FirstName = "Michael", Surname = "Stonebraker", Initials = "MS", 
                        ProgramSem1 = "BSIS", StatusSem1 = "Full-time", MaxUnitsSem1 = 21, SchedulePreferencesSem1 = "Mon,Wed,Fri|10:00-13:00;Mon,Wed,Fri|15:00-19:00", PreferredYearLevelsSem1 = "2,3", AssignedRoomIdSem1 = GetRoom("Lab 4"),
                        ProgramSem2 = "BSIS", StatusSem2 = "Full-time", MaxUnitsSem2 = 21, SchedulePreferencesSem2 = "Mon,Wed,Fri|10:00-13:00;Mon,Wed,Fri|15:00-19:00", PreferredYearLevelsSem2 = "2,3", AssignedRoomIdSem2 = GetRoom("Lab 4")
                    },


                    // --- BSEMC Faculty ---

                    // Game Dev Lead: Assigned Room 201.
                    new Instructor { 
                        FirstName = "Shigeru", Surname = "Miyamoto", Initials = "SM", 
                        ProgramSem1 = "BSEMC", StatusSem1 = "Full-time", MaxUnitsSem1 = 24, SchedulePreferencesSem1 = "Mon,Tue,Wed,Thu,Fri|10:00-13:00;Mon,Tue,Wed,Thu,Fri|14:00-19:00", PreferredYearLevelsSem1 = "1,2", AssignedRoomIdSem1 = GetRoom("201"),
                        ProgramSem2 = "BSEMC", StatusSem2 = "Full-time", MaxUnitsSem2 = 24, SchedulePreferencesSem2 = "Mon,Tue,Wed,Thu,Fri|10:00-13:00;Mon,Tue,Wed,Thu,Fri|14:00-19:00", PreferredYearLevelsSem2 = "1,2", AssignedRoomIdSem2 = GetRoom("201")
                    },

                    // Part-time 3D Artist
                    new Instructor { 
                        FirstName = "Hideo", Surname = "Kojima", Initials = "HK", 
                        ProgramSem1 = "BSEMC", StatusSem1 = "Part-time", MaxUnitsSem1 = 12, SchedulePreferencesSem1 = "Mon,Wed,Fri|18:00-21:00", PreferredYearLevelsSem1 = "3,4", AssignedRoomIdSem1 = null,
                        ProgramSem2 = "BSEMC", StatusSem2 = "Part-time", MaxUnitsSem2 = 12, SchedulePreferencesSem2 = "Mon,Wed,Fri|18:00-21:00", PreferredYearLevelsSem2 = "3,4", AssignedRoomIdSem2 = null
                    },

                    // Animation: Assigned Room 202.
                    new Instructor { 
                        FirstName = "Hayao", Surname = "Miyazaki", Initials = "HM", 
                        ProgramSem1 = "BSEMC", StatusSem1 = "Full-time", MaxUnitsSem1 = 21, SchedulePreferencesSem1 = "Tue,Thu|08:00-12:00;Tue,Thu|13:00-18:00", PreferredYearLevelsSem1 = "1,2", AssignedRoomIdSem1 = GetRoom("202"),
                        ProgramSem2 = "BSEMC", StatusSem2 = "Full-time", MaxUnitsSem2 = 21, SchedulePreferencesSem2 = "Tue,Thu|08:00-12:00;Tue,Thu|13:00-18:00", PreferredYearLevelsSem2 = "1,2", AssignedRoomIdSem2 = GetRoom("202")
                    },

                    // Sound Engineer: Assigned Room 305.
                    new Instructor { 
                        FirstName = "Nobuo", Surname = "Uematsu", Initials = "NU", 
                        ProgramSem1 = "BSEMC", StatusSem1 = "Full-time", MaxUnitsSem1 = 6, SchedulePreferencesSem1 = "Sat|09:00-12:00", PreferredYearLevelsSem1 = "1,2,3,4", AssignedRoomIdSem1 = GetRoom("305"),
                        ProgramSem2 = "BSEMC", StatusSem2 = "Full-time", MaxUnitsSem2 = 6, SchedulePreferencesSem2 = "Sat|09:00-12:00", PreferredYearLevelsSem2 = "1,2,3,4", AssignedRoomIdSem2 = GetRoom("305")
                    },

                    // Programmer: Assigned Room 402.
                    new Instructor { 
                        FirstName = "John", Surname = "Carmack", Initials = "JC", 
                        ProgramSem1 = "BSEMC", StatusSem1 = "Full-time", MaxUnitsSem1 = 24, SchedulePreferencesSem1 = "Mon,Tue,Wed,Thu,Fri|08:00-12:00;Mon,Tue,Wed,Thu,Fri|13:00-16:00", PreferredYearLevelsSem1 = "3,4", AssignedRoomIdSem1 = GetRoom("402"),
                        ProgramSem2 = "BSEMC", StatusSem2 = "Full-time", MaxUnitsSem2 = 24, SchedulePreferencesSem2 = "Mon,Tue,Wed,Thu,Fri|08:00-12:00;Mon,Tue,Wed,Thu,Fri|13:00-16:00", PreferredYearLevelsSem2 = "3,4", AssignedRoomIdSem2 = GetRoom("402")
                    }
                };

                db.Instructors.AddRange(instructors);
                db.SaveChanges();
            }
        }        
        public static void SeedRooms()
        {
            using (var db = new AppDbContext())
            {
                if (db.Rooms.Any()) return;

                var rooms = new List<Room>
                {
                    // Labs (2nd Floor)
                    new Room { Name = "Lab1", Type = "Laboratory", Capacity = 40, FloorLevel = 2, ComputerCount = 35, ChairCount = 35, TableCount = 11, AirConCount = 2, WhiteboardCount = 2, MonitorCount = 1 },
                    new Room { Name = "Lab2", Type = "Laboratory", Capacity = 40, FloorLevel = 2, ComputerCount = 35, ChairCount = 35, TableCount = 11, AirConCount = 2, WhiteboardCount = 2, MonitorCount = 1 },
                    new Room { Name = "Lab3", Type = "Laboratory", Capacity = 40, FloorLevel = 2, ComputerCount = 35, ChairCount = 35, TableCount = 11, AirConCount = 2, WhiteboardCount = 2, MonitorCount = 1 },
                    new Room { Name = "Lab4", Type = "Laboratory", Capacity = 40, FloorLevel = 2, ComputerCount = 35, ChairCount = 35, TableCount = 11, AirConCount = 2, WhiteboardCount = 2, MonitorCount = 1 },

                    // Lecture Rooms (2nd Floor)
                    new Room { Name = "201", Type = "Classroom", Capacity = 40, FloorLevel = 2, ChairCount = 40, TableCount = 1, CeilingFanCount = 4, StandFanCount = 2, AirConCount = 2, WhiteboardCount = 2 },
                    new Room { Name = "202", Type = "Classroom", Capacity = 40, FloorLevel = 2, ChairCount = 38, TableCount = 1, CeilingFanCount = 2, WhiteboardCount = 2 },

                    // Special Rooms (2nd Floor)
                    new Room { Name = "Multimedia", Type = "ClassRoom", Capacity = 50, FloorLevel = 2, ChairCount = 40, TableCount = 1, CeilingFanCount = 4, StandFanCount = 2, AirConCount = 2, WhiteboardCount = 2, MonitorCount = 2 },
                    
                    // Lecture Rooms (3rd Floor)
                    new Room { Name = "301", Type = "Classroom", Capacity = 40, FloorLevel = 3, ChairCount = 35, TableCount = 1, CeilingFanCount = 4, StandFanCount = 2, WhiteboardCount = 2 },
                    new Room { Name = "302", Type = "Classroom", Capacity = 40, FloorLevel = 3, ChairCount = 35, TableCount = 1, CeilingFanCount = 4, StandFanCount = 2, WhiteboardCount = 2 },
                    new Room { Name = "303", Type = "Classroom", Capacity = 40, FloorLevel = 3, ChairCount = 35, TableCount = 1, CeilingFanCount = 3, StandFanCount = 2, WhiteboardCount = 2 },
                    new Room { Name = "304", Type = "Classroom", Capacity = 40, FloorLevel = 3, ChairCount = 35, TableCount = 1, CeilingFanCount = 4, StandFanCount = 2, WhiteboardCount = 2 },
                    new Room { Name = "305", Type = "Classroom", Capacity = 40, FloorLevel = 3, ChairCount = 35, TableCount = 1, CeilingFanCount = 4, StandFanCount = 2, WhiteboardCount = 2 },
                    new Room { Name = "306", Type = "Classroom", Capacity = 40, FloorLevel = 3, ChairCount = 35, TableCount = 1, CeilingFanCount = 4, StandFanCount = 2, WhiteboardCount = 2 },
                    new Room { Name = "307", Type = "Classroom", Capacity = 40, FloorLevel = 3, ChairCount = 35, TableCount = 3, CeilingFanCount = 1, StandFanCount = 3, WhiteboardCount = 2 },
                    new Room { Name = "308", Type = "Classroom", Capacity = 40, FloorLevel = 3, ChairCount = 40, TableCount = 2, CeilingFanCount = 3, WhiteboardCount = 2 },
                    new Room { Name = "309", Type = "Classroom", Capacity = 40, FloorLevel = 3, ChairCount = 28, TableCount = 4, CeilingFanCount = 1, StandFanCount = 2, WhiteboardCount = 2 },

                    // Lecture Rooms (4th Floor)
                    new Room { Name = "401", Type = "Classroom", Capacity = 40, FloorLevel = 4, ChairCount = 50, TableCount = 2, CeilingFanCount = 4, StandFanCount = 3, WhiteboardCount = 2 },
                    new Room { Name = "402", Type = "Classroom", Capacity = 40, FloorLevel = 4, ChairCount = 40, TableCount = 3, CeilingFanCount = 2, StandFanCount = 2, WhiteboardCount = 2 },
                    new Room { Name = "403", Type = "Classroom", Capacity = 40, FloorLevel = 4, ChairCount = 33, TableCount = 2, CeilingFanCount = 4, StandFanCount = 1, WhiteboardCount = 2 },
                    new Room { Name = "404", Type = "Classroom", Capacity = 40, FloorLevel = 4, ChairCount = 41, TableCount = 1, CeilingFanCount = 4, WhiteboardCount = 2 }
                };

                db.Rooms.AddRange(rooms);
                db.SaveChanges();
            }
        }
        public static void SeedStudentSections()
        {
            using (var db = new AppDbContext())
            {
                if (db.Sections.Any()) return;

                var sections = new List<StudentSection>
                {
                    // BSCS Sections
                    new StudentSection { Program = "BSCS", YearLevel = 1, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSCS", YearLevel = 1, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSCS", YearLevel = 1, Name = "C", StudentCount = 40 },
                    new StudentSection { Program = "BSCS", YearLevel = 1, Name = "D", StudentCount = 40 },
                    new StudentSection { Program = "BSCS", YearLevel = 2, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSCS", YearLevel = 2, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSCS", YearLevel = 2, Name = "C", StudentCount = 40 },
                    new StudentSection { Program = "BSCS", YearLevel = 3, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSCS", YearLevel = 3, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSCS", YearLevel = 4, Name = "A", StudentCount = 40 },

                    // BSIT Sections
                    new StudentSection { Program = "BSIT", YearLevel = 1, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSIT", YearLevel = 1, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSIT", YearLevel = 1, Name = "C", StudentCount = 40 },
                    new StudentSection { Program = "BSIT", YearLevel = 1, Name = "D", StudentCount = 40 },
                    new StudentSection { Program = "BSIT", YearLevel = 2, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSIT", YearLevel = 2, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSIT", YearLevel = 2, Name = "C", StudentCount = 40 },
                    new StudentSection { Program = "BSIT", YearLevel = 3, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSIT", YearLevel = 3, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSIT", YearLevel = 4, Name = "A", StudentCount = 40 },

                    // BSIS Sections
                    new StudentSection { Program = "BSIS", YearLevel = 1, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSIS", YearLevel = 1, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSIS", YearLevel = 1, Name = "C", StudentCount = 40 },
                    new StudentSection { Program = "BSIS", YearLevel = 1, Name = "D", StudentCount = 40 },
                    new StudentSection { Program = "BSIS", YearLevel = 2, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSIS", YearLevel = 2, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSIS", YearLevel = 2, Name = "C", StudentCount = 40 },
                    new StudentSection { Program = "BSIS", YearLevel = 3, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSIS", YearLevel = 3, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSIS", YearLevel = 4, Name = "A", StudentCount = 40 },

                    // BSEMC Sections
                    new StudentSection { Program = "BSEMC", YearLevel = 1, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSEMC", YearLevel = 1, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSEMC", YearLevel = 1, Name = "C", StudentCount = 40 },
                    new StudentSection { Program = "BSEMC", YearLevel = 1, Name = "D", StudentCount = 40 },
                    new StudentSection { Program = "BSEMC", YearLevel = 2, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSEMC", YearLevel = 2, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSEMC", YearLevel = 2, Name = "C", StudentCount = 40 },
                    new StudentSection { Program = "BSEMC", YearLevel = 3, Name = "A", StudentCount = 40 },
                    new StudentSection { Program = "BSEMC", YearLevel = 3, Name = "B", StudentCount = 40 },
                    new StudentSection { Program = "BSEMC", YearLevel = 4, Name = "A", StudentCount = 40 },
                };

                db.Sections.AddRange(sections);
                db.SaveChanges();
            }
        }
        public static void SeedClassSchedules()
        {
            using (var db = new AppDbContext())
            {
                if (db.Schedules.Any()) return;

                var schedules = new List<ClassSchedule>
                {
                    // ================= SEMESTER 1 =================

                    // --- FRIDAY ---
                    new ClassSchedule { SectionId = 5, CourseId = 2, RoomId = 14, InstructorId = 1, Day = "Fri", StartTime = "07:00", EndTime = "08:30", Semester = 1, Component = "Lec" },
                    new ClassSchedule { SectionId = 5, CourseId = 43, RoomId = 2, InstructorId = 2, Day = "Fri", StartTime = "11:30", EndTime = "13:00", Semester = 1, Component = "Lab" },
                    new ClassSchedule { SectionId = 5, CourseId = 12, RoomId = 15, InstructorId = 3, Day = "Fri", StartTime = "13:00", EndTime = "15:00", Semester = 1, Component = "Lec" },
                    new ClassSchedule { SectionId = 23, CourseId = 43, RoomId = 5, InstructorId = 4, Day = "Fri", StartTime = "07:00", EndTime = "08:30", Semester = 1, Component = "Lab" },
                    
                    // --- MONDAY ---
                    new ClassSchedule { SectionId = 5, CourseId = 43, RoomId = 6, InstructorId = 3, Day = "Mon", StartTime = "10:00", EndTime = "11:30", Semester = 1, Component = "Lab" },
                    new ClassSchedule { SectionId = 23, CourseId = 2, RoomId = 11, InstructorId = 4, Day = "Mon", StartTime = "08:30", EndTime = "10:00", Semester = 1, Component = "Lec" },
                    new ClassSchedule { SectionId = 24, CourseId = 43, RoomId = 2, InstructorId = 4, Day = "Mon", StartTime = "13:00", EndTime = "14:30", Semester = 1, Component = "Lab" },
                    
                    // --- SATURDAY ---
                    new ClassSchedule { SectionId = 5, CourseId = 1, RoomId = 20, InstructorId = 3, Day = "Sat", StartTime = "07:00", EndTime = "08:30", Semester = 1, Component = "Lec" },
                    new ClassSchedule { SectionId = 5, CourseId = 10, RoomId = 17, InstructorId = 4, Day = "Sat", StartTime = "08:30", EndTime = "10:00", Semester = 1, Component = "Lec" },
                    
                    // --- TUESDAY ---
                    new ClassSchedule { SectionId = 5, CourseId = 43, RoomId = 7, InstructorId = 5, Day = "Tue", StartTime = "10:00", EndTime = "12:00", Semester = 1, Component = "Lec" },
                    new ClassSchedule { SectionId = 7, CourseId = 58, RoomId = 5, InstructorId = 4, Day = "Tue", StartTime = "07:00", EndTime = "08:30", Semester = 1, Component = "Lab" },

                    // --- WEDNESDAY ---
                    new ClassSchedule { SectionId = 5, CourseId = 3, RoomId = 8, InstructorId = 1, Day = "Wed", StartTime = "07:00", EndTime = "08:30", Semester = 1, Component = "Lec" },
                    new ClassSchedule { SectionId = 25, CourseId = 43, RoomId = 2, InstructorId = 5, Day = "Wed", StartTime = "11:30", EndTime = "13:00", Semester = 1, Component = "Lab" },

                    // --- THURSDAY ---
                    new ClassSchedule { SectionId = 23, CourseId = 3, RoomId = 18, InstructorId = 3, Day = "Thu", StartTime = "07:00", EndTime = "08:30", Semester = 1, Component = "Lec" },
                    new ClassSchedule { SectionId = 23, CourseId = 43, RoomId = 5, InstructorId = 5, Day = "Thu", StartTime = "13:00", EndTime = "14:30", Semester = 1, Component = "Lab" },


                    // ================= SEMESTER 2 (For Testing Filters) =================
                    
                    // (These IDs correspond to 2nd Sem curriculum items in your SQL)
                    new ClassSchedule { SectionId = 5, CourseId = 48, RoomId = 1, InstructorId = 1, Day = "Fri", StartTime = "08:30", EndTime = "10:00", Semester = 2, Component = "Lab" },
                    new ClassSchedule { SectionId = 5, CourseId = 46, RoomId = 18, InstructorId = 2, Day = "Fri", StartTime = "10:30", EndTime = "12:00", Semester = 2, Component = "Lec" },
                    new ClassSchedule { SectionId = 5, CourseId = 4, RoomId = 14, InstructorId = 3, Day = "Mon", StartTime = "08:30", EndTime = "10:00", Semester = 2, Component = "Lec" },
                    new ClassSchedule { SectionId = 5, CourseId = 44, RoomId = 6, InstructorId = 4, Day = "Wed", StartTime = "10:00", EndTime = "11:30", Semester = 2, Component = "Lab" }
                };

                db.Schedules.AddRange(schedules);
                db.SaveChanges();
            }
        }
        public static void SeedCurriculum()
        {
            using (var db = new AppDbContext())
            {
                // 1. Stop if data exists
                if (db.Curriculums.Any()) return;

                // 2. Get a Lookup Map (CourseCode -> CourseId) for speed
                var courseMap = db.Courses.ToDictionary(c => c.Code, c => c.Id);
                var list = new List<Curriculum>();

                // Helper function to keep code clean
                void Add(string program, int year, int sem, params string[] codes)
                {
                    foreach (var code in codes)
                    {
                        if (courseMap.TryGetValue(code, out int cId))
                        {
                            list.Add(new Curriculum { Program = program, YearLevel = year, Semester = sem, CourseId = cId });
                        }
                    }
                }

                // ==================== BSCS (Computer Science) ====================
                // Year 1
                Add("BSCS", 1, 1, "GE1", "GE2", "GE3", "CS101", "ITE1", "PATHFIT1", "NSTP1");
                Add("BSCS", 1, 2, "GE4", "CS102", "CS202", "CS204", "PATHFIT2", "NSTP2");
                // Year 2
                Add("BSCS", 2, 1, "GE5", "GE6", "CS201", "CS204", "CS203", "CS205", "PATHFIT3");
                Add("BSCS", 2, 2, "GE7", "GE8", "CS209", "CS210", "CS206", "CS205", "PATHFIT4");
                // Year 3
                Add("BSCS", 3, 1, "CS214", "CS202", "CS206", "GE9");
                Add("BSCS", 3, 2, "CS209", "CS216", "CS211", "CS215"); // Added CS211, CS215
                // Year 4
                Add("BSCS", 4, 1, "CS212", "CS214", "CS217", "GE9"); // Added CS217
                Add("BSCS", 4, 2, "CS213", "CS218", "CS219"); // Added CS218

                // ==================== BSIT (Information Technology) ====================
                // Year 1
                Add("BSIT", 1, 1, "GE1", "GE2", "GE3", "ITE1", "ITE2", "PATHFIT1", "NSTP1");
                Add("BSIT", 1, 2, "ITE3", "ITE4", "IT102", "GE4", "PATHFIT2", "NSTP2");
                // Year 2
                Add("BSIT", 2, 1, "GE5", "GE6", "IT103", "IT104", "IT105", "IT106", "PATHFIT3");
                Add("BSIT", 2, 2, "GE7", "GE8", "IT201", "IT109", "IT108", "PATHFIT4");
                // Year 3
                Add("BSIT", 3, 1, "IT210", "IT204", "IT205", "IT206", "IT207", "GE9"); // Added IT206
                Add("BSIT", 3, 2, "IT201", "IT203", "IT205", "IT206", "IT207", "GE9");
                // Year 4
                Add("BSIT", 4, 1, "IT211", "IT212", "IT213", "IT207", "IT208", "IT209", "IT210"); // Added IT208, IT209, IT210
                Add("BSIT", 4, 2, "IT214"); 

                // ==================== BSIS (Information Systems) ====================
                // Year 1
                Add("BSIS", 1, 1, "GE1", "GE2", "GE3", "IS101", "IS114", "PATHFIT1", "NSTP1");
                Add("BSIS", 1, 2, "ITE4", "IS102", "GE4", "PATHFIT2", "NSTP2");
                // Year 2
                Add("BSIS", 2, 1, "GE5", "GE6", "IS102", "IS104", "IS105", "IS106", "PATHFIT3"); // Added IS105, IS106
                Add("BSIS", 2, 2, "GE7", "GE8", "IS103", "IS110", "IS111", "PATHFIT4");
                // Year 3
                Add("BSIS", 3, 1, "IS107", "IS109", "IS105", "GE9"); // Added IS107, IS109
                Add("BSIS", 3, 2, "IS108", "IS112", "IS111", "GE9"); // Added IS108
                // Year 4
                Add("BSIS", 4, 1, "IS113", "IS114", "IS115"); // Added IS115
                Add("BSIS", 4, 2, "IS116");

                // ==================== BSEMC (Entertainment & Multimedia) ====================
                // Year 1
                Add("BSEMC", 1, 1, "GE1", "GE2", "GE3", "EMC100", "ITE1", "ITE2", "PATHFIT1", "NSTP1");
                Add("BSEMC", 1, 2, "GE4", "EMC201", "EMC100", "PATHFIT2", "NSTP2");
                // Year 2
                Add("BSEMC", 2, 1, "GE5", "GE6", "DA201", "EMC202", "EMC203", "EMC204", "PATHFIT3"); // Added EMC203, EMC204
                Add("BSEMC", 2, 2, "GE7", "GE8", "EMC205", "EMC206", "DA202", "DA203", "PATHFIT4"); // Added DA202
                // Year 3
                Add("BSEMC", 3, 1, "GE9", "EMC204", "DA204", "DA205", "DA210", "DA207"); // Added DA205, DA210, DA207
                Add("BSEMC", 3, 2, "EMC205", "EMC206", "EMC207", "EMC208", "DA208", "DA209", "DA211", "DA212", "DA213"); // Added DA211, DA212, DA213
                // Year 4
                Add("BSEMC", 4, 1, "DA212", "DA213", "DA214", "DA215", "DA209"); // Added DA209
                Add("BSEMC", 4, 2, "DA216");

                db.Curriculums.AddRange(list);
                db.SaveChanges();
            }
        }

    }
}