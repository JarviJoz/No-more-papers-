using BCrypt.Net;

namespace NoMorePapers.Helpers
{
    public static class SecurityHelper
    {
        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password)) return string.Empty;
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public static bool VerifyPassword(string inputPassword, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(inputPassword) || string.IsNullOrWhiteSpace(storedHash))
                return false;
            return BCrypt.Net.BCrypt.Verify(inputPassword, storedHash);
        }
    }
}