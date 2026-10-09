using System;

namespace NoMorePapers.Models
{
    public sealed class Reminder
    {
        public int Id { get; set; }
        public int ProfileId { get; set; }
        public string DateKey { get; set; } = string.Empty;
        public DateTime ScheduledAt { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool NotificationsEnabled { get; set; } = true;
        public string Description { get; set; } = string.Empty;
        public string Recurrence { get; set; } = "No repetir";
        public DateTime? RecurrenceEndDate { get; set; }
        public int NotificationAdvanceMinutes { get; set; }
        public bool SoundEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
