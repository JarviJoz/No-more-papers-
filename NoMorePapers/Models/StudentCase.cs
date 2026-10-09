using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Media;

namespace NoMorePapers.Models
{
    public class StudentCase : INotifyPropertyChanged
    {
        private bool _isSelected;

        public event PropertyChangedEventHandler? PropertyChanged;

        [Key]
        public string Id { get; set; } = string.Empty; // Ej: CASE-2026-000001

        public int ProfileId { get; set; }
        public string? StudentId { get; set; }
        public Student? Student { get; set; }

        [Required]
        public string StudentName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Grade { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Shift { get; set; } = string.Empty; // Jornada
        public string AvatarType { get; set; } = "DefaultMale";
        public byte[]? AvatarImage { get; set; }

        // Acudiente
        public string? GuardianName { get; set; }
        public string? GuardianPhone { get; set; }
        public string GuardianRelation { get; set; } = string.Empty; // Padre, Madre, Otro: [Texto]

        // Información del caso
        public string Reason { get; set; } = string.Empty; // Motivo
        public string Observations { get; set; } = string.Empty;
        public string Categories { get; set; } = string.Empty; // Almacenado como texto separado por comas
        public string Importance { get; set; } = "Normal";

        // Fechas y Estados
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? FinishedAt { get; set; }

        public string Status { get; set; } = "SIN EMPEZAR";
        public string PreviousStatus { get; set; } = "En Proceso";
        public bool NotificationsEnabled { get; set; } = true;
        public int NotificationIntervalDays { get; set; } = 15;

        public bool IsFavorite { get; set; }
        public bool IsArchived { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        [NotMapped]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        [NotMapped]
        public Brush AgingBrush { get; set; } = Brushes.Transparent;

        [NotMapped]
        public string AgingTooltip { get; set; } = string.Empty;
    }
}