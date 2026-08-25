using System.Collections.Concurrent;
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
/// inside the file, written by the shortcuts themselves. Sweeping the whole folder costs tens of
/// milliseconds and the result is cached.
///
/// The cache is watched: without that it would freeze on the first read and a new recent entry would
/// only appear after restarting the dock.
/// </summary>
public sealed class JumpListProvider : IJumpListProvider, IDisposable
{
    private readonly CustomDestinationsParser _parser;
    private readonly AutomaticDestinationsParser _automaticParser;
    private readonly string _directory;
    private readonly string _automaticDirectory;
    private readonly ConcurrentDictionary<string, JumpList> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly object _watcherGate = new();

    private bool _watching;
    private bool _disposed;

    public JumpListProvider(CustomDestinationsParser parser, AutomaticDestinationsParser automaticParser)
        : this(parser, automaticParser, GetDefaultDirectory(), GetDefaultAutomaticDirectory())
    {
    }

    public JumpListProvider(
        CustomDestinationsParser parser,
        AutomaticDestinationsParser automaticParser,
        string directory,
        string automaticDirectory)
    {
        _parser = parser;
        _automaticParser = automaticParser;
        _directory = directory;
        _automaticDirectory = automaticDirectory;
    }

    public JumpList GetFor(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return JumpList.Empty;
        }

        EnsureWatching();

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

    public void Dispose()
    {
        lock (_watcherGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (FileSystemWatcher watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();
        }
    }

    /// <summary>
    /// It starts watching both folders on the first query, and not at construction: whoever never opens
    /// a jump list does not pay for two watchers.
    /// </summary>
    private void EnsureWatching()
    {
        lock (_watcherGate)
        {
            if (_watching || _disposed)
            {
                return;
            }

            _watching = true;

            Watch(_directory, "*.customDestinations-ms");
            Watch(_automaticDirectory, "*.automaticDestinations-ms");
        }
    }

    private void Watch(string directory, string filter)
    {
        // The folder only exists after some app publishes the first jump list.
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(directory, filter)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileChanged;

            // A watcher buffer overflow means a lost event: the cache is no longer trustworthy.
            watcher.Error += OnWatcherError;

            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // With no watcher, the cache goes back to what it was: correct until the file changes.
        }
    }

    /// <summary>
    /// Drops the whole cache, and not just the entry of the file that changed.
    ///
    /// The file name is a hash of the AppUserModelID, and tracing the path back to the
    /// executable is exactly what this class cannot do — it is the reason it searches
    /// by content. Besides, a newly created file may start matching an executable
    /// whose previous query found nothing, and that empty entry has to fall too.
    ///
    /// The price is one reread on each app's next open, which the hover warming covers.
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _cache.Clear();
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _cache.Clear();
    }

    private JumpList Load(string executablePath)
    {
        FileInfo? file = FindFileFor(executablePath);
        var categories = new List<JumpListCategory>();

        if (file is not null)
        {
            try
            {
                categories.AddRange(_parser.Parse(File.ReadAllBytes(file.FullName)).Categories);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Without the app's categories; the recent entries may still come.
            }
        }

        categories.AddRange(LoadAutomatic(executablePath, file).Categories);

        return new JumpList(categories);
    }

    /// <summary>
    /// The user's pinned and recent entries, which live in a separate file.
    ///
    /// There are two paths to it, and both are needed. The first is the name: an app's two files
    /// are named with the hash of the same AppUserModelID, changing only the folder and the extension —
    /// once the tasks one is found, the recent one comes for free. The second is content, as in the other
    /// folder: apps with no tasks file, like Remote Desktop Connection, only have the recent
    /// one, and there the executable appears because the entries themselves point at it.
    ///
    /// Known limitation: an app whose recent entries are only documents, and that has no tasks
    /// file, stays out of reach — nothing in its file mentions the executable.
    /// </summary>
    private JumpList LoadAutomatic(string executablePath, FileInfo? customFile)
    {
        string? path = FindAutomaticFile(executablePath, customFile);

        return path is null ? JumpList.Empty : _automaticParser.Parse(path);
    }

    private string? FindAutomaticFile(string executablePath, FileInfo? customFile)
    {
        if (customFile is not null)
        {
            string name = Path.GetFileNameWithoutExtension(customFile.Name);
            string byName = Path.Combine(_automaticDirectory, name + ".automaticDestinations-ms");

            if (File.Exists(byName))
            {
                return byName;
            }
        }

        var directory = new DirectoryInfo(_automaticDirectory);
        if (!directory.Exists)
        {
            return null;
        }

        byte[] utf16Needle = Encoding.Unicode.GetBytes(executablePath);
        byte[] asciiNeedle = Encoding.ASCII.GetBytes(executablePath);

        foreach (FileInfo candidate in directory.EnumerateFiles("*.automaticDestinations-ms"))
        {
            byte[]? content = TryRead(candidate);

            if (content is not null
                && (ContainsIgnoringCase(content, utf16Needle) || ContainsIgnoringCase(content, asciiNeedle)))
            {
                return candidate.FullName;
            }
        }

        return null;
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
        return RecentSubdirectory("CustomDestinations");
    }

    private static string GetDefaultAutomaticDirectory()
    {
        return RecentSubdirectory("AutomaticDestinations");
    }

    private static string RecentSubdirectory(string name)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Microsoft", "Windows", "Recent", name);
    }
}
