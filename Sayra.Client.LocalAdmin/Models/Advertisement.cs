using System;

namespace Sayra.Client.LocalAdmin.Models
{
    public class Advertisement
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty; // local file path or cache identifier
        public string ActionUrl { get; set; } = string.Empty;
        public string ButtonText { get; set; } = string.Empty;
        public int Priority { get; set; } = 1; // higher priority shown first
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
