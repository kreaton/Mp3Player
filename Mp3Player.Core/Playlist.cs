namespace Mp3Player.Core;

/// <summary>
/// The list of tracks found in a folder tree, plus the order they are played in.
/// Ordered mode walks the files sorted by folder and name. Shuffle mode plays a random
/// permutation, so no track repeats until every track has been played; then it reshuffles.
/// </summary>
internal sealed class Playlist
{
    private readonly List<string> _all = new();   // every track, sorted
    private List<string> _order = new();          // the order tracks are played in
    private int _pos = -1;
    private readonly Random _rng = new();

    public bool Shuffle { get; private set; }
    public int Count => _all.Count;

    /// <summary>0-based index of the current track in the play order.</summary>
    public int Position => _pos;

    public string? Current => _pos >= 0 && _pos < _order.Count ? _order[_pos] : null;

    /// <summary>Finds all .mp3 files in the directory and all its subdirectories (can run on a worker thread).</summary>
    public static List<string> Scan(string directory)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
        };
        var list = Directory.EnumerateFiles(directory, "*.mp3", options)
            .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            .ToList();
        list.Sort(CompareTracks);
        return list;
    }

    private static int CompareTracks(string a, string b)
    {
        int c = string.Compare(Path.GetDirectoryName(a), Path.GetDirectoryName(b), StringComparison.OrdinalIgnoreCase);
        return c != 0 ? c : string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase);
    }

    private static string DirectoryPrefix(string directory)
    {
        string d = Path.TrimEndingDirectorySeparator(directory);
        return d + Path.DirectorySeparatorChar;
    }

    public void SetTracks(IEnumerable<string> files)
    {
        _all.Clear();
        _all.AddRange(files);
        Rebuild(null);
    }

    public void SetShuffle(bool on)
    {
        if (Shuffle == on) return;
        Shuffle = on;
        Rebuild(Current);   // the current track stays current
    }

    public string? MoveNext()
    {
        if (_order.Count == 0) return null;
        if (_pos + 1 < _order.Count)
        {
            _pos++;
        }
        else if (Shuffle)
        {
            Reshuffle(_order[^1]);      // everything has been played: new random round
        }
        else
        {
            _pos = 0;                    // wrap around
        }
        return Current;
    }

    public string? MovePrevious()
    {
        if (_order.Count == 0) return null;
        if (_pos > 0) _pos--;
        else if (!Shuffle) _pos = _order.Count - 1;   // wrap around (shuffle: stay on the first track of the round)
        return Current;
    }

    /// <summary>Number of tracks located in the directory or any of its subdirectories.</summary>
    public int CountUnder(string directory)
    {
        string prefix = DirectoryPrefix(directory);
        return _all.Count(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Removes the current track from the list and returns the track that follows it (null if none left).</summary>
    public string? RemoveCurrentAndAdvance()
    {
        string? current = Current;
        if (current == null) return null;

        _all.Remove(current);
        _order.RemoveAt(_pos);

        if (_order.Count == 0)
        {
            _pos = -1;
            return null;
        }
        if (_pos >= _order.Count)
        {
            if (Shuffle) Reshuffle(null);
            else _pos = 0;
        }
        return Current;
    }

    /// <summary>
    /// Removes every track in the directory (and its subdirectories) and returns the first track after the
    /// current one that is NOT in that directory, wrapping around if needed (null if no tracks are left).
    /// </summary>
    public string? RemoveDirectoryAndAdvance(string directory)
    {
        string prefix = DirectoryPrefix(directory);
        bool Under(string p) => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        string? next = null;
        for (int i = _pos + 1; i < _order.Count; i++)
        {
            if (!Under(_order[i])) { next = _order[i]; break; }
        }

        _all.RemoveAll(Under);
        _order.RemoveAll(Under);

        if (_order.Count == 0)
        {
            _pos = -1;
            return null;
        }
        if (next != null) _pos = _order.IndexOf(next);
        else if (Shuffle) Reshuffle(null);
        else _pos = 0;
        return Current;
    }

    private void Rebuild(string? keep)
    {
        if (_all.Count == 0)
        {
            _order = new List<string>();
            _pos = -1;
            return;
        }

        _order = new List<string>(_all);
        if (Shuffle)
        {
            ShuffleList(_order);
            if (keep != null)
            {
                int i = _order.IndexOf(keep);
                if (i > 0)
                {
                    _order.RemoveAt(i);
                    _order.Insert(0, keep);
                }
            }
            _pos = 0;
        }
        else
        {
            _pos = keep != null ? Math.Max(0, _order.IndexOf(keep)) : 0;
        }
    }

    private void Reshuffle(string? avoidFirst)
    {
        _order = new List<string>(_all);
        ShuffleList(_order);
        if (avoidFirst != null && _order.Count > 1 && _order[0] == avoidFirst)
        {
            int j = _rng.Next(1, _order.Count);
            (_order[0], _order[j]) = (_order[j], _order[0]);
        }
        _pos = 0;
    }

    private void ShuffleList(List<string> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
