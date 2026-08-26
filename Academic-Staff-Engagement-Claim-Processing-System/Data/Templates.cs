using System;

namespace SystemModels
{
    public class Template
    {
        public int Id { get; private set; }
        public string Contract { get; set; } = string.Empty;
        public string Claim { get; set; } = string.Empty;
        public string Letter { get; set; } = string.Empty;

        public Template(int id)
        {
            Id = id;
        }
    }
}