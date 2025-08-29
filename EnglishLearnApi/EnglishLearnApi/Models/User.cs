using System.ComponentModel.DataAnnotations;

namespace EnglishLearnApi.Models
{
    public class User
    {
        [Key] public int Id { get; set; }

        [Required, MaxLength(50)] public string Username { get; set; }
        [Required, EmailAddress, MaxLength(200)] public string Email { get; set; }

        [Required] public string PasswordHash { get; set; }
        [Required] public string PasswordSalt { get; set; } // base64

        public string Role { get; set; } = "Learner";
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        public ICollection<RefreshToken> RefreshTokens { get; set; }
    }
}
