using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EnglishLearnApi.Models
{
    public class RefreshToken
    {
        [Key] public int Id { get; set; }

        [Required] public string Token { get; set; } // random token string
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; } = false;

        [ForeignKey("User")] public int UserId { get; set; }
        public User User { get; set; }
    }
}
