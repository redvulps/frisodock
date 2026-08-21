using System.Text;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Locates and delivers an app's jump list. That is all (SRP): reading the format belongs to
/// <see cref="CustomDestinationsParser"/>.
///
/// How the file is found: Windows names each file with a hash of the app's AppUserModelID.
/// Reproducing that hash is fragile — the algorithm is undocumented, changes with path
/// normalization, and apps calling SetCurrentProcessExplicitAppUserModelID escape it entirely
/// (measured here: Brave publishes the AUMID "Brave" on its window, and no CRC64 variant of that
/// text reproduces its file name).
///
/// So instead of computing the name, we search the content: the executable path appears
/// inside the file, written by the shortcuts themselves. Sweeping the whole folder costs ~50 ms and the
/// result is cached.
/// </summary>
public sealed class JumpListProvider : IJumpListProvider
{
    private readonly CustomDestinationsParser _parser;
    private readonly string _directory;
    private readonly Dictionary<string, JumpList> _cache = new(StringComparer.OrdinalIgnoreCase);

    public JumpListProvider(CustomDestinationsParser parser)
        : this(parser, GetDefaultDirectory())
    {
    }

    public JumpListProvider(CustomDestinationsParser parser, string directory)
    {
        _parser = parser;
        _directory = directory;
    }

    public JumpList GetFor(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return JumpList.Empty;
        }

        if (_cache.TryGetValue(executablePath, out JumpList? cached))
        {
            return cached;
        }

        JumpList jumpList = Load(executablePath);
        _cache[executablePath] = jumpList;

        return jumpList;
    }

    public void Invalidate()
    {
        _cache.Clear();
    }

    private JumpList Load(string executablePath)
    {
        FileInfo? file = FindFileFor(executablePath);
        if (file is null)
        {
            return JumpList.Empty;
        }

        try
        {
            return _parser.Parse(File.ReadAllBytes(file.FullName));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return JumpList.Empty;
        }
    }

    /// <summary>
    /// File whose content references this executable.
    ///
    /// An app with several profiles (browsers, for instance) keeps one file per profile, and they all
    /// mention the same executable. In that tie the most recent one wins, which is the profile in use —
    /// the same list the native taskbar would show at that moment.
    /// </summary>
    private FileInfo? FindFileFor(string executablePath)
    {
        var directory = new DirectoryInfo(_directory);
        if (!directory.Exists)
        {
            return null;
        }

        byte[] utf16Needle = Encoding.Unicode.GetBytes(executablePath);
        byte[] asciiNeedle = Encoding.ASCII.GetBytes(executablePath);

        FileInfo? best = null;

        foreach (FileInfo candidate in directory.EnumerateFiles("*.customDestinations-ms"))
        {
            if (best is not null && candidate.LastWriteTimeUtc <= best.LastWriteTimeUtc)
            {
                continue;
            }

            byte[]? content = TryRead(candidate);
            if (content is null)
            {
                continue;
            }

            if (ContainsIgnoringCase(content, utf16Needle) || ContainsIgnoringCase(content, asciiNeedle))
            {
                best = candidate;
            }
        }

        return best;
    }

    private static byte[]? TryRead(FileInfo file)
    {
        try
        {
            return File.ReadAllBytes(file.FullName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Case-insensitive subsequence search, byte by byte. The path appears in the
    /// shortcut sometimes in UTF-16, sometimes in ANSI, and the case varies with whoever wrote it.
    /// </summary>
    private static bool ContainsIgnoringCase(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        int limit = haystack.Length - needle.Length;
        for (int start = 0; start <= limit; start++)
        {
            if (MatchesAt(haystack, needle, start))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAt(byte[] haystack, byte[] needle, int start)
    {
        for (int offset = 0; offset < needle.Length; offset++)
        {
            if (ToLowerAscii(haystack[start + offset]) != ToLowerAscii(needle[offset]))
            {
                return false;
            }
        }

        return true;
    }

    private static byte ToLowerAscii(byte value)
    {
        const byte UpperA = (byte)'A';
        const byte UpperZ = (byte)'Z';

        if (value >= UpperA && value <= UpperZ)
        {
            return (byte)(value + 32);
        }

        return value;
    }

    private static string GetDefaultDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Microsoft", "Windows", "Recent", "CustomDestinations");
    }
}
