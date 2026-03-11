using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public static class IcsCalendarCodec
{
    public static string CreateSingleEventCalendar(CalendarEventRecord item, string? forcedUid = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        var uid = string.IsNullOrWhiteSpace(forcedUid)
            ? string.IsNullOrWhiteSpace(item.ExternalUid) ? item.Id : item.ExternalUid
            : forcedUid;

        var builder = new StringBuilder();
        builder.AppendLine("BEGIN:VCALENDAR");
        builder.AppendLine("VERSION:2.0");
        builder.AppendLine("PRODID:-//Label CRM Demo//Calendar Sync//EN");
        builder.AppendLine("CALSCALE:GREGORIAN");
        builder.AppendLine("BEGIN:VEVENT");
        builder.AppendLine($"UID:{EscapeText(uid)}");
        builder.AppendLine($"DTSTAMP:{item.LastModifiedUtc.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}");
        builder.AppendLine($"DTSTART:{item.Start.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}");
        builder.AppendLine($"DTEND:{item.End.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}");
        builder.AppendLine($"SUMMARY:{EscapeText(item.Title)}");

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            builder.AppendLine($"DESCRIPTION:{EscapeText(item.Description)}");
        }

        if (!string.IsNullOrWhiteSpace(item.Location))
        {
            builder.AppendLine($"LOCATION:{EscapeText(item.Location)}");
        }

        if (!string.IsNullOrWhiteSpace(item.Category))
        {
            builder.AppendLine($"CATEGORIES:{EscapeText(item.Category)}");
        }

        builder.AppendLine("END:VEVENT");
        builder.AppendLine("END:VCALENDAR");
        return builder.ToString();
    }

    public static IReadOnlyList<CalendarEventRecord> ParseEvents(string icsData, string source, string href = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (string.IsNullOrWhiteSpace(icsData))
        {
            return Array.Empty<CalendarEventRecord>();
        }

        var unfolded = UnfoldLines(icsData);
        var blocks = new List<List<string>>();
        List<string>? currentBlock = null;

        foreach (var line in unfolded)
        {
            if (string.Equals(line, "BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                currentBlock = new List<string>();
                continue;
            }

            if (string.Equals(line, "END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (currentBlock is not null && currentBlock.Count > 0)
                {
                    blocks.Add(currentBlock);
                }

                currentBlock = null;
                continue;
            }

            currentBlock?.Add(line);
        }

        return blocks.Select(block => ParseEventBlock(block, source, href)).ToList();
    }

    private static CalendarEventRecord ParseEventBlock(IReadOnlyList<string> lines, string source, string href)
    {
        var properties = lines
            .Select(ParseProperty)
            .Where(property => !string.IsNullOrWhiteSpace(property.Name))
            .ToList();

        var startProperty = properties.FirstOrDefault(property => string.Equals(property.Name, "DTSTART", StringComparison.OrdinalIgnoreCase));
        var endProperty = properties.FirstOrDefault(property => string.Equals(property.Name, "DTEND", StringComparison.OrdinalIgnoreCase));
        var start = ParseDateTime(startProperty);
        var end = ParseDateTime(endProperty);

        if (start == default)
        {
            start = DateTimeOffset.Now;
        }

        if (end == default || end <= start)
        {
            end = start.AddHours(1);
        }

        return new CalendarEventRecord
        {
            ExternalUid = GetPropertyValue(properties, "UID"),
            Title = GetPropertyValue(properties, "SUMMARY"),
            Category = Choose(GetPropertyValue(properties, "CATEGORIES"), source),
            Description = GetPropertyValue(properties, "DESCRIPTION"),
            Location = GetPropertyValue(properties, "LOCATION"),
            Start = start,
            End = end,
            Source = source,
            AppleEventHref = string.Equals(source, "Apple", StringComparison.OrdinalIgnoreCase) ? href : string.Empty
        };
    }

    private static List<string> UnfoldLines(string icsData)
    {
        var normalized = icsData.Replace("\r\n", "\n").Replace('\r', '\n');
        var rawLines = normalized.Split('\n');
        var lines = new List<string>();

        foreach (var rawLine in rawLines)
        {
            var line = rawLine.TrimEnd();

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if ((line[0] == ' ' || line[0] == '\t') && lines.Count > 0)
            {
                lines[^1] += line[1..];
                continue;
            }

            lines.Add(line);
        }

        return lines;
    }

    private static IcsProperty ParseProperty(string line)
    {
        var separatorIndex = line.IndexOf(':');
        if (separatorIndex < 0)
        {
            return default;
        }

        var descriptor = line[..separatorIndex];
        var value = line[(separatorIndex + 1)..];
        var parameterIndex = descriptor.IndexOf(';');

        return parameterIndex < 0
            ? new IcsProperty(descriptor, string.Empty, UnescapeText(value))
            : new IcsProperty(descriptor[..parameterIndex], descriptor[(parameterIndex + 1)..], UnescapeText(value));
    }

    private static string GetPropertyValue(IEnumerable<IcsProperty> properties, string name)
    {
        return properties.FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)).Value
            ?? string.Empty;
    }

    private static DateTimeOffset ParseDateTime(IcsProperty property)
    {
        if (string.IsNullOrWhiteSpace(property.Name) || string.IsNullOrWhiteSpace(property.Value))
        {
            return default;
        }

        if (property.Parameters.Contains("VALUE=DATE", StringComparison.OrdinalIgnoreCase) || property.Value.Length == 8)
        {
            if (DateTime.TryParseExact(property.Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
            }

            return default;
        }

        if (DateTimeOffset.TryParseExact(
            property.Value,
            "yyyyMMdd'T'HHmmss'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var utcValue))
        {
            return utcValue;
        }

        if (DateTimeOffset.TryParseExact(
            property.Value,
            "yyyyMMdd'T'HHmm'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out utcValue))
        {
            return utcValue;
        }

        if (DateTime.TryParseExact(
            property.Value,
            new[] { "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var localValue))
        {
            return new DateTimeOffset(localValue, TimeZoneInfo.Local.GetUtcOffset(localValue));
        }

        return default;
    }

    private static string EscapeText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal);
    }

    private static string UnescapeText(string value)
    {
        return value
            .Replace("\\n", Environment.NewLine, StringComparison.OrdinalIgnoreCase)
            .Replace("\\N", Environment.NewLine, StringComparison.Ordinal)
            .Replace("\\,", ",", StringComparison.Ordinal)
            .Replace("\\;", ";", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static string Choose(string primary, string fallback)
        => string.IsNullOrWhiteSpace(primary) ? fallback : primary.Trim();

    private readonly record struct IcsProperty(string Name, string Parameters, string Value);
}
