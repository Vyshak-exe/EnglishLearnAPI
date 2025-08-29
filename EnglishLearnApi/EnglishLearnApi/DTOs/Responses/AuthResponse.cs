namespace EnglishLearnApi.DTOs.Responses
{
    public class AuthResponse
    {

        public string AccessToken { get; set; }
        public DateTime AccessTokenExpiresAt { get; set; }

        public string RefreshToken { get; set; }
        public DateTime RefreshTokenExpiresAt { get; set; }

        // optional user info
        public int UserId { get; set; }
        public string Username { get; set; }
        public string? Email { get; set; }

    }
}
