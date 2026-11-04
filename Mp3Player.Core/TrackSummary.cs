using System.Globalization;
using System.Text;

namespace Mp3Player.Core;

/// <summary>One search result: a track's file and the tags that can be searched.</summary>
public sealed record TrackSummary(string FilePath, string Title, string Artist, string Album)
{
    /// <summary>The title tag, or the file name for an untagged track.</summary>
    public string DisplayTitle => Title.Length > 0 ? Title : Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>"Artist • Album", or the folder name for an untagged track.</summary>
    public string DisplaySubtitle =>
        Artist.Length > 0 && Album.Length > 0 ? Artist + " • " + Album
        : Artist.Length > 0 ? Artist
        : Album.Length > 0 ? Album
        : Path.GetFileName(Path.GetDirectoryName(FilePath)) ?? "";

    // Folded title, artist, album and file name, in that order, separated by '\n'.
    private string? _searchKey;
    private string SearchKey => _searchKey ??=
        string.Join('\n', Fold(Title), Fold(Artist), Fold(Album), Fold(Path.GetFileNameWithoutExtension(FilePath)));

    /// <summary>A summary from the file name alone, used until the tags have been read.</summary>
    internal static TrackSummary FromFileName(string path) => new(path, "", "", "");

    /// <summary>Reads the tags without decoding anything else. Never throws (can run on a worker thread).</summary>
    internal static TrackSummary Read(string path)
    {
        try
        {
            using var f = TagLib.File.Create(path, TagLib.ReadStyle.None);
            var t = f.Tag;
            return new TrackSummary(path, Clean(t.Title), Clean(t.JoinedPerformers), Clean(t.Album));
        }
        catch
        {
            return FromFileName(path);      // unreadable or missing tags: the file name still matches
        }
    }

    /// <summary>
    /// Splits a query into the folded words <see cref="Matches"/> looks for.
    /// </summary>
    internal static string[] SplitQuery(string query) =>
        Fold(query).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>True if every word occurs somewhere in the title, artist, album or file name.</summary>
    internal bool Matches(string[] words)
    {
        string key = SearchKey;
        foreach (string word in words)
        {
            if (!key.Contains(word, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    /// <summary>Sort order of a match: 0 title starts with the query, 1 title contains it, 2 anything else.</summary>
    internal int Rank(string[] words)
    {
        string phrase = string.Join(' ', words);
        ReadOnlySpan<char> title = SearchKey.AsSpan(0, SearchKey.IndexOf('\n'));
        return title.StartsWith(phrase, StringComparison.Ordinal) ? 0
            : title.Contains(phrase, StringComparison.Ordinal) ? 1
            : 2;
    }

    private static string Clean(string? tag) => (tag ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();

    /// <summary>Lower case without accents, so "beyonce" finds "Beyoncé".</summary>
    private static string Fold(string text)
    {
        string decomposed;
        try
        {
            decomposed = text.Normalize(NormalizationForm.FormD);
        }
        catch (ArgumentException)
        {
            return text.ToLowerInvariant();      // invalid Unicode in a tag
        }

        var sb = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
