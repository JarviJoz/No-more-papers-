namespace NoMorePapers.Models
{
    public sealed class AppSettings
    {
        public string FontSize { get; set; } = "Mediano";
        public string Appearance { get; set; } = "Claro";
        public string AccentColor { get; set; } = "#315BFA";
        public bool SoundsEnabled { get; set; } = true;
        public int NotificationVolume { get; set; } = 75;
        public string? CustomSoundPath { get; set; }
        public bool NotificationsEnabled { get; set; } = true;
        public DateTime? NotificationsDisabledUntil { get; set; }
    }
}