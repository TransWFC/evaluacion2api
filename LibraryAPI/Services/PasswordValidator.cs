using System.Text.RegularExpressions;

namespace LibraryApp.Services
{
    public static class PasswordValidator
    {
        // Al menos 5 caracteres, 1 may�scula, 1 min�scula, 1 n�mero
        private static readonly Regex PasswordRegex = new Regex(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{5,}$",
            RegexOptions.Compiled);

        public static bool IsValid(string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            return PasswordRegex.IsMatch(password);
        }
    }
}