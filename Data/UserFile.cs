using System.ComponentModel.DataAnnotations;

namespace GrokBlazorApp.Data
{
    public class UserFile
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;

        [Required]
        public string FileName { get; set; } = null!;

        [Required]
        public string Label { get; set; } = null!;

        public byte[] Content { get; set; } = null!;

        public DateTime UploadDate { get; set; }
    }
}
