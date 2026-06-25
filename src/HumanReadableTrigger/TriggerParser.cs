namespace HumanReadableTrigger;

/// <summary>
/// Parses simple human-readable trigger expressions such as
/// <c>"every 5 minutes"</c> or <c>"every 2 hours"</c> into a structured
/// <see cref="TriggerInterval"/>.
/// </summary>
public static class TriggerParser
{
    private static readonly Dictionary<string, TimeUnit> UnitLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["second"] = TimeUnit.Second,
        ["seconds"] = TimeUnit.Second,
        ["minute"] = TimeUnit.Minute,
        ["minutes"] = TimeUnit.Minute,
        ["hour"] = TimeUnit.Hour,
        ["hours"] = TimeUnit.Hour,
        ["day"] = TimeUnit.Day,
        ["days"] = TimeUnit.Day,
    };

    /// <summary>
    /// Parses a human-readable trigger expression.
    /// </summary>
    /// <param name="expression">
    /// An expression of the form <c>"every &lt;count&gt; &lt;unit&gt;"</c>,
    /// for example <c>"every 5 minutes"</c>. The leading <c>"every"</c> and an
    /// explicit count are both optional, so <c>"every minute"</c> and
    /// <c>"5 minutes"</c> are also valid.
    /// </param>
    /// <returns>The parsed <see cref="TriggerInterval"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="expression"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// <paramref name="expression"/> is empty or not a recognised trigger.
    /// </exception>
    public static TriggerInterval Parse(string expression)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        if (!TryParse(expression, out TriggerInterval interval))
        {
            throw new FormatException($"'{expression}' is not a valid trigger expression.");
        }

        return interval;
    }

    /// <summary>
    /// Attempts to parse a human-readable trigger expression without throwing.
    /// </summary>
    /// <param name="expression">The expression to parse.</param>
    /// <param name="interval">
    /// When this method returns <see langword="true"/>, the parsed interval;
    /// otherwise the default value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="expression"/> was parsed
    /// successfully; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryParse(string? expression, out TriggerInterval interval)
    {
        interval = default;

        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        string[] tokens = expression.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        int index = 0;

        // Optional leading "every".
        if (tokens[index].Equals("every", StringComparison.OrdinalIgnoreCase))
        {
            index++;
        }

        if (index >= tokens.Length)
        {
            return false;
        }

        // Optional count; defaults to 1 ("every minute" == "every 1 minute").
        int count = 1;
        if (int.TryParse(tokens[index], out int parsedCount))
        {
            if (parsedCount <= 0)
            {
                return false;
            }

            count = parsedCount;
            index++;
        }

        // Exactly one unit token must remain.
        if (index != tokens.Length - 1)
        {
            return false;
        }

        if (!UnitLookup.TryGetValue(tokens[index], out TimeUnit unit))
        {
            return false;
        }

        interval = new TriggerInterval(count, unit);
        return true;
    }
}
