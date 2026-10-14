using System.Globalization;
using System.Text;

namespace OpenTablo.Api.Referential;

public static class SearchText
{
    /// <summary>Lowercase, accents removed, punctuation as single spaces: "Glacière" and "glaciere" match, so do "Saint-Michel" and "saint michel".</summary>
    public static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
        }
        return sb.ToString().TrimEnd();
    }
}
