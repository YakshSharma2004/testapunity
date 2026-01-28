using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using testapi1.Application;

namespace testapi1.Services
{
    public class TextNormalizationService : ITextNormalizer
    {
        private readonly ILogger<TextNormalizationService> _logger;

        private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex RepeatPunctExceptPeriod =
            new(@"([\p{P}\p{S}-[\.]])\1{1,}", RegexOptions.Compiled);
        private static readonly Regex TooManyPeriods =
            new(@"\.{4,}", RegexOptions.Compiled);

        public TextNormalizationService(ILogger<TextNormalizationService> logger)
        {
            _logger = logger;
        }

        public string NormalizeForMatch(string input)
        {
            var raw = input ?? "";

            var text = raw.Normalize(NormalizationForm.FormKC);
            text = MultiWhitespace.Replace(text, " ").Trim();
            text = text.ToLowerInvariant();

            text = RepeatPunctExceptPeriod.Replace(text, "$1");
            text = TooManyPeriods.Replace(text, "...");

            _logger.LogDebug("Normalized text. Raw='{Raw}' Normalized='{Normalized}'", raw, text);

            return text;
        }
    }
}
