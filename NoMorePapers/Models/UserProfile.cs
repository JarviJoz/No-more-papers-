using System;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NoMorePapers.Models
{
    public partial class UserProfile : ObservableObject
    {
        [Key]
        public int Id { get; set; }
        public int SlotNumber { get; set; } // 1 a 5
        public string? Name { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? PasswordHash { get; set; }
        public string? ProfileImagePath { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsConfigured => !string.IsNullOrEmpty(Name);

        [ObservableProperty]
        private bool _isMenuOpen;
    }
}