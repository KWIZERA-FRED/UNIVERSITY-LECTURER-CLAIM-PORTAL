using System;

namespace SystemModels
{
    public class Course
    {
        public int Id { get; private set; }
        public string Names { get; set; } = string.Empty;

        public Course(int id, string names)
        {
            Id = id;
            Names = names;
        }
    }
}s