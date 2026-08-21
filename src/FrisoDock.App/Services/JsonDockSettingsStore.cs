using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Services;

/// <summary>
/// Persists the settings as JSON inside %APPDATA%\FrisoDock. That is all (SRP).
///
/// It writes only what the user controls through the settings screen. The panel metrics
/// (<see cref="DockMetrics"/>) are left out on purpose: they are dock design decisions, and
/// leaving them in the file would invite edits that break the layout with no warning.
/// </summary>
public sealed class JsonDockSettingsStore : IDockSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,

        // Enum by name: the file is hand-editable, and "WhenWindowOverlaps" says what "2" does not.
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public JsonDockSettingsStore()
        : this(GetDefaultFilePath())
    {
    }

    public JsonDockSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public DockSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new DockSettings();
            }

            string json = File.ReadAllText(_filePath);
            PersistedSettings? persisted = JsonSerializer.Deserialize<PersistedSettings>(json, SerializerOptions);

            if (persisted is null)
            {
                return new DockSettings();
            }

            return persisted.ToSettings();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new DockSettings();
        }
    }

    public void Save(DockSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(PersistedSettings.From(settings), SerializerOptions);
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
        return Path.Combine(appData, "FrisoDock", "settings.json");
    }

    /// <summary>
    /// The shape written to disk.
    ///
    /// It is a type separate from <see cref="DockSettings"/> on purpose: this way the domain model
    /// can gain computed fields or reorganize itself without changing the file format, and an
    /// old file still opens — whatever is missing becomes the default.
    /// </summary>
    private sealed record PersistedSettings
    {
        public bool HideNativeTaskbar { get; init; } = true;

        public bool ReserveScreenSpace { get; init; } = true;

        public DockHideMode HideMode { get; init; } = DockHideMode.Never;

        public bool EnableMagnification { get; init; } = true;

        public double MagnificationScale { get; init; } = 1.5;

        public bool EnableWindowPreviews { get; init; } = true;

        public bool ShowClockSeconds { get; init; }

        public static PersistedSettings From(DockSettings settings)
        {
            return new PersistedSettings
            {
                HideNativeTaskbar = settings.HideNativeTaskbar,
                ReserveScreenSpace = settings.ReserveScreenSpace,
                HideMode = settings.HideMode,
                EnableMagnification = settings.EnableMagnification,
                MagnificationScale = settings.MagnificationScale,
                EnableWindowPreviews = settings.EnableWindowPreviews,
                ShowClockSeconds = settings.ShowClockSeconds,
            };
        }

        public DockSettings ToSettings()
        {
            return new DockSettings
            {
                HideNativeTaskbar = HideNativeTaskbar,
                ReserveScreenSpace = ReserveScreenSpace,
                HideMode = HideMode,
                EnableMagnification = EnableMagnification,
                MagnificationScale = MagnificationScale,
                EnableWindowPreviews = EnableWindowPreviews,
                ShowClockSeconds = ShowClockSeconds,
            };
        }
    }
}
