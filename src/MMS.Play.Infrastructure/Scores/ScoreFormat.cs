using System.Diagnostics.CodeAnalysis;

namespace MMS.Play.Infrastructure.Scores;

/// <summary>
/// File formats alphaTab can render. Every format checks the first bytes of the file,
/// so a renamed image or archive is rejected on upload instead of failing in the browser.
/// </summary>
public sealed class ScoreFormat
{
    /// <summary>
    /// Enough for every signature below plus a UTF-8 BOM and leading whitespace of XML files.
    /// </summary>
    public const int HeaderLength = 64;

    private delegate bool SignatureMatcher(ReadOnlySpan<byte> header);

    // Guitar Pro 3-5: a length-prefixed version string, "FICHIER GUITAR PRO v5.00"
    private static readonly SignatureMatcher GuitarProLegacy = header => header.Length > 1 && header[1..].StartsWith("FICHIER GUITAR"u8);

    // Guitar Pro 6: BCFZ (compressed) or BCFS container
    private static readonly SignatureMatcher GuitarPro6 = header => header.StartsWith("BCFZ"u8) || header.StartsWith("BCFS"u8);

    // Guitar Pro 7+: ZIP archive
    private static readonly SignatureMatcher GuitarPro7 = header => header.StartsWith("PK\x03\x04"u8);

    private static readonly SignatureMatcher Xml = IsXml;

    // the compiler embeds these as static data, no allocation per call
    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];
    private static ReadOnlySpan<byte> Utf16LittleEndianBom => [0xFF, 0xFE];
    private static ReadOnlySpan<byte> Utf16BigEndianBom => [0xFE, 0xFF];

    private static readonly Dictionary<string, ScoreFormat> FormatsByExtension = new ScoreFormat[]
    {
        new("gp", "application/octet-stream", GuitarPro7),
        new("gp3", "application/octet-stream", GuitarProLegacy),
        new("gp4", "application/octet-stream", GuitarProLegacy),
        new("gp5", "application/octet-stream", GuitarProLegacy),
        new("gpx", "application/octet-stream", GuitarPro6),
        new("musicxml", "application/vnd.recordare.musicxml+xml", Xml),
        new("xml", "application/xml", Xml)
    }.ToDictionary(format => format.Extension, StringComparer.OrdinalIgnoreCase);

    private readonly SignatureMatcher _matches;

    private ScoreFormat(string extension, string contentType, SignatureMatcher matches)
    {
        Extension = extension;
        ContentType = contentType;
        _matches = matches;
    }

    /// <summary>
    /// Lower case, without the dot.
    /// </summary>
    public string Extension { get; }

    public string ContentType { get; }

    public static IEnumerable<string> SupportedExtensions => FormatsByExtension.Keys;

    /// <param name="extension">".gp5" or "gp5", case insensitive.</param>
    public static bool TryFind(string extension, [NotNullWhen(true)] out ScoreFormat? format) =>
        FormatsByExtension.TryGetValue(extension.TrimStart('.'), out format);

    public static ScoreFormat Get(string extension) =>
        TryFind(extension, out var format) ? format : throw new ArgumentException($"Unknown score format '{extension}'.", nameof(extension));

    public bool MatchesSignature(ReadOnlySpan<byte> header) => _matches(header);

    private static bool IsXml(ReadOnlySpan<byte> header)
    {
        // UTF-16 files start with a BOM, their first '<' is two bytes wide
        if (header.StartsWith(Utf16LittleEndianBom) || header.StartsWith(Utf16BigEndianBom))
            return true;

        if (header.StartsWith(Utf8Bom))
            header = header[Utf8Bom.Length..];

        header = header.TrimStart(" \t\r\n"u8);
        return header.Length > 0 && header[0] == (byte)'<';
    }
}
