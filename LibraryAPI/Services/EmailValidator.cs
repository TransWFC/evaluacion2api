using System.Text.RegularExpressions;

namespace LibraryApp.Services
{
    public static class EmailValidator
    {
        // Patr�n simple para validar emails (puedes ajustarlo seg�n tus necesidades)
        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsValid(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            return EmailRegex.IsMatch(email);
        }
    }
}