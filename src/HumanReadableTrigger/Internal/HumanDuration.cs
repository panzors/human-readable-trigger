using System.Globalization;
using System.Text;

namespace HumanReadableTrigger;

/// <summary>
/// Parses human-written durations such as "12min", "2 hours", "90m" or
/// "1 hour 30 minutes" into a <see cref="TimeSpan"/>.
/// </summary>
internal static class HumanDuration
{
    public static bool TryParse(string text, out TimeSpan value)
    {
        value = default;
        text = text.Trim().ToLowerInvariant();
        if (text.Length == 0)
        {
            return false;
        }

        var tokens = Tokenize(text);
        if (tokens.Count == 0 || tokens.Count % 2 != 0)
        {
            return false;
        }

        var total = TimeSpan.Zero;
        for (int i = 0; i < tokens.Count; i += 2)
        {
            if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
            {
                return false;
            }

            var span = UnitToTimeSpan(tokens[i + 1], amount);
            if (span is null)
            {
                return false;
            }

            total += span.Value;
        }

        value = total;
        return true;
    }

    // Splits on whitespace and on digit/letter boundaries so "12min" tokenizes to ["12", "min"].
    private static List<string> Tokenize(string text)
    {
        var sb = new StringBuilder(text.Length * 2);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (i > 0
                && ((char.IsDigit(text[i - 1]) && char.IsLetter(c))
                    || (char.IsLetter(text[i - 1]) && char.IsDigit(c))))
            {
                sb.Append(' ');
            }

            sb.Append(c);
        }

        return sb.ToString()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static TimeSpan? UnitToTimeSpan(string unit, double amount) => unit switch
    {
        "second" or "seconds" or "sec" or "secs" or "s" => TimeSpan.FromSeconds(amount),
        "minute" or "minutes" or "min" or "mins" or "m" => TimeSpan.FromMinutes(amount),
        "hour" or "hours" or "hr" or "hrs" or "h" => TimeSpan.FromHours(amount),
        "day" or "days" or "d" => TimeSpan.FromDays(amount),
        "week" or "weeks" or "wk" or "wks" or "w" => TimeSpan.FromDays(amount * 7),
        _ => null,
    };
}
