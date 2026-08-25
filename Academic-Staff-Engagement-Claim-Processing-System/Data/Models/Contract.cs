using System;

namespace SystemModels
{
    public class Contract
    {
        public int Id { get; private set; }
        public string Content { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;

        public Contract(int id, string version)
        {
            Id = id;
            Version = version;
        }
    }
}