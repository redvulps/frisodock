using System.Globalization;
using System.IO;
using FrisoDock.Core.Abstractions;

namespace FrisoDock.App.Services;

/// <summary>
/// Keeps the appbar's original state in a file inside %APPDATA%\FrisoDock. That is all (SRP).
///
/// It is an integer in plain text, on purpose: this file has to be readable and fixable by hand
/// in case the dock can no longer restore the taskbar on its own.
/// </summary>
public sealed class FileTaskbarStateStore : ITaskbarStateStore
{
    private readonly string _filePath;

    public FileTaskbarStateStore()
        : this(GetDefaultFilePath())
    {
    }

    public FileTaskbarStateStore(string filePath)
    {
        _filePath = filePath;
    }

    public int? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            string content = File.ReadAllText(_filePath).Trim();
            if (int.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out int state))
            {
                return state;
            }

            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(int state)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, state.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Without the record, the worst case is the next abnormal shutdown losing the original
            // state — the dock itself keeps working.
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string GetDefaultFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "FrisoDock", "taskbar-state");
    }
}
