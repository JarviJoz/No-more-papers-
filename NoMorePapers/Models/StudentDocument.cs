using System;
using System.ComponentModel.DataAnnotations;

namespace NoMorePapers.Models
{
    public class StudentDocument
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string StudentId { get; set; } = string.Empty;
        [Required]
        public string FileName { get; set; } = string.Empty;
        [Required]
        public string FilePath { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}
