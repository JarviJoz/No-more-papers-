using System;
using System.ComponentModel.DataAnnotations;

namespace NoMorePapers.Models
{
    public class CaseFollowUp
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string StudentCaseId { get; set; } = string.Empty;

        public int FollowUpNumber { get; set; }
        public string ContentRtf { get; set; } = string.Empty;
        public DateTime? FollowUpDate { get; set; }
        public byte[]? SignatureImage { get; set; }
        public DateTime SavedAt { get; set; } = DateTime.Now;
    }
}
