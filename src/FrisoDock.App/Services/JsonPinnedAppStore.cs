using System.IO;
using System.Text.Json;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Services;

/// <summary>
/// Persists the pinned apps as JSON inside %APPDATA%\FrisoDock. That is all (SRP).
///
/// It never throws while reading: a corrupt file makes the dock fall back to the defaults
/// instead of blocking startup.
/// </summary>
public sealed class JsonPinnedAppStore : IPinnedAppStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public JsonPinnedAppStore()
        : this(GetDefaultFilePath())
    {
    }

    public JsonPinnedAppStore(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<PinnedApp> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return CreateDefaults();
            }

            string json = File.ReadAllText(_filePath);
            List<PinnedApp>? apps = JsonSerializer.Deserialize<List<PinnedApp>>(json, SerializerOptions);

            if (apps is null || apps.Count == 0)
            {
                return CreateDefaults();
            }

            return apps;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return CreateDefaults();
        }
    }

    public void Save(IReadOnlyList<PinnedApp> apps)
    {
        ArgumentNullException.ThrowIfNull(apps);

        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(apps, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A write failure must not kill the dock: the changes then apply to this session only.
        }
    }

    private static string GetDefaultFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "FrisoDock", "pinned.json");
    }

    /// <summary>
    /// First-run defaults: apps that exist in any Windows installation,
    /// so the dock never opens empty.
    /// </summary>
    private static IReadOnlyList<PinnedApp> CreateDefaults()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);

        var candidates = new[]
        {
            new PinnedApp("Explorador de Arquivos", Path.Combine(windows, "explorer.exe")),
            new PinnedApp("Bloco de Notas", Path.Combine(system32, "notepad.exe")),
            new PinnedApp("Terminal", Path.Combine(system32, "WindowsPowerShell", "v1.0", "powershell.exe")),
        };

        return candidates.Where(app => File.Exists(app.LaunchTarget)).ToList();
    }
}
