namespace MMS.Play.Contracts.Extensions;

/// <summary>
/// Case-insensitive ordinal comparisons, the "i" prefix stands for "ignore case". Null is treated as an empty string.
/// Not translatable by EF Core: inside queries use the regular methods, the database collation decides about case.
/// </summary>
#pragma warning disable IDE1006 // the lower case "i" prefix is intentional
public static class StringExtensions
{
    public static bool iEquals(this string? str1, string? str2) =>
        string.Equals(str1 ?? string.Empty, str2 ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    public static bool iStartsWith(this string? str, string? value) =>
        (str ?? string.Empty).StartsWith(value ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    public static bool iEndsWith(this string? str, string? value) =>
        (str ?? string.Empty).EndsWith(value ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    public static bool iContains(this string? str, string? value) =>
        (str ?? string.Empty).Contains(value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
#pragma warning restore IDE1006
