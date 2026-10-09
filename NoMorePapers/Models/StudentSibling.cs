using System.ComponentModel.DataAnnotations;

namespace NoMorePapers.Models
{
    public class StudentSibling
    {
        [Required]
        public string StudentId { get; set; } = string.Empty;
        [Required]
        public string SiblingStudentId { get; set; } = string.Empty;
    }
}
