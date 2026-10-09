using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Media;

namespace NoMorePapers.Models
{
    public class Student : INotifyPropertyChanged
    {
        private bool _isSelected;

        [Key]
        public string Id { get; set; } = string.Empty;
        public int ProfileId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public string Grade { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Shift { get; set; } = string.Empty;

        public string? Guardian1Name { get; set; }
        public string? Guardian1Phone { get; set; }
        public string Guardian1Relation { get; set; } = string.Empty;
        public string? Guardian2Name { get; set; }
        public string? Guardian2Phone { get; set; }
        public string Guardian2Relation { get; set; } = string.Empty;

        public bool HasSiblings { get; set; }
        public string Status { get; set; } = "Activo";
        public string AvatarType { get; set; } = "DefaultMale";
        public byte[]? AvatarImage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public int? CurrentAge => BirthDate.HasValue ? CalculateAge(BirthDate.Value, DateTime.Today) : null;

        [NotMapped]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        [NotMapped]
        public Brush AvatarBrush { get; set; } = Brushes.Transparent;

        public event PropertyChangedEventHandler? PropertyChanged;

        public static int CalculateAge(DateTime birthDate, DateTime referenceDate)
        {
            var age = referenceDate.Year - birthDate.Year;
            if (birthDate.Date > referenceDate.AddYears(-age).Date) age--;
            return Math.Max(age, 0);
        }
    }
}
