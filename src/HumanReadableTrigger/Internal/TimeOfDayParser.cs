using System.Globalization;

namespace HumanReadableTrigger;

/// <summary>
/// Parses a human-written time of day such as "9am", "5pm", "1600", "16:00",
/// "noon" or "midnight" into a <see cref="TimeOnly"/>.
/// </summary>
internal static class TimeOfDayParser
{
    public static bool TryParse(string text, out TimeOnly time)
    {
        time = default;
        text = text.Trim().ToLowerInvariant();
        if (text.Length == 0)
        {
            return false;
        }

        switch (text)
        {
            case "midnight":
                time = new TimeOnly(0, 0);
                return true;
            case "noon" or "midday":
                time = new TimeOnly(12, 0);
                return true;
        }

        bool hasMeridiem = false;
        bool isPm = false;
        if (text.EndsWith("am", StringComparison.Ordinal))
        {
            hasMeridiem = true;
            text = text[..^2].Trim();
        }
        else if (text.EndsWith("pm", StringComparison.Ordinal))
        {
            hasMeridiem = true;
            isPm = true;
            text = text[..^2].Trim();
        }

        int hours;
        int minutes = 0;
        if (text.Contains(':'))
        {
            var parts = text.Split(':');
            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutes))
            {
                return false;
            }
        }
        else if (!hasMeridiem && (text.Length == 3 || text.Length == 4) && text.All(char.IsDigit))
        {
            // Compact 24-hour form: HMM or HHMM, e.g. "930" or "1600".
            int split = text.Length - 2;
            hours = int.Parse(text[..split], CultureInfo.InvariantCulture);
            minutes = int.Parse(text[split..], CultureInfo.InvariantCulture);
        }
        else if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out hours))
        {
            return false;
        }

        if (hasMeridiem)
        {
            if (hours is < 1 or > 12)
            {
                return false;
            }

            if (isPm && hours != 12)
            {
                hours += 12;
            }
            else if (!isPm && hours == 12)
            {
                hours = 0;
            }
        }

        if (hours is < 0 or > 23 || minutes is < 0 or > 59)
        {
            return false;
        }

        time = new TimeOnly(hours, minutes);
        return true;
    }
}
