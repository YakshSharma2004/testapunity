using System.Text.RegularExpressions;

namespace testapi1.Domain.Progression
{
    public static partial class ProgressionSessionId
    {
        private static readonly Regex SessionIdRegex = SessionIdPattern();

        public static string NewId()
        {
            return $"ps_{Guid.NewGuid():N}";
        }

        public static bool IsValid(string? sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return false;
            }

            return SessionIdRegex.IsMatch(sessionId.Trim());
        }

        [GeneratedRegex("^ps_[a-f0-9]{32}$", RegexOptions.Compiled)]
        private static partial Regex SessionIdPattern();
    }
}
