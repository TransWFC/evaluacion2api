using System.Text.RegularExpressions;

namespace LibraryApp.Services
{
    public static class EmailValidator
    {
        // Patrón simple para validar emails (puedes ajustarlo según tus necesidades)
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