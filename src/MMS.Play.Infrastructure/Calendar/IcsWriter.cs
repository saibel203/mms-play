using System.Globalization;
using System.Text;

namespace MMS.Play.Infrastructure.Calendar;

/// <summary>
/// Writes iCalendar (RFC 5545) text: CRLF line ends, TEXT values escaped, lines folded at 75 octets
/// without splitting a UTF-8 character. Only what the lesson feed needs.
/// </summary>
public sealed class IcsWriter
{
    private const int MaxLineOctets = 75;
    private const string LineEnd = "\r\n";

    private readonly StringBuilder _builder = new();

    /// <summary>
    /// A property with a value written as is (dates, enumerated values, durations).
    /// </summary>
    public IcsWriter Raw(string name, string value)
    {
        Fold($"{name}:{value}");
        return this;
    }

    /// <summary>
    /// A TEXT property: backslash, semicolon, comma and line breaks are escaped.
    /// </summary>
    public IcsWriter Text(string name, string value) => Raw(name, Escape(value));

    /// <summary>
    /// A UTC date-time: 20261109T140000Z.
    /// </summary>
    public IcsWriter Utc(string name, DateTime utc) =>
        Raw(name, utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));

    public IcsWriter Begin(string component) => Raw("BEGIN", component);

    public IcsWriter End(string component) => Raw("END", component);

    public override string ToString() => _builder.ToString();

    public static string Escape(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n");

    /// <summary>
    /// Content lines longer than 75 octets go on in lines starting with a space (which the space counts into).
    /// </summary>
    private void Fold(string line)
    {
        var octets = 0;
        var limit = MaxLineOctets;

        for (var i = 0; i < line.Length; i++)
        {
            // a surrogate pair is one character of 4 octets, it must not be split
            var length = char.IsHighSurrogate(line[i]) && i + 1 < line.Length ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(line.AsSpan(i, length));

            if (octets + size > limit)
            {
                _builder.Append(LineEnd).Append(' ');
                octets = 0;
                // the leading space takes one octet of the next line
                limit = MaxLineOctets - 1;
            }

            _builder.Append(line, i, length);
            octets += size;
            i += length - 1;
        }

        _builder.Append(LineEnd);
    }
}
