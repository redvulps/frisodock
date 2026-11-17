namespace FrisoDock.Core.Models;

/// <summary>Shell namespace path for an installed application's registered identity.</summary>
public static class ApplicationShellPath
{
    public const string Prefix = @"shell:AppsFolder\";

    public static string? FromApplicationId(string? applicationId) => string.IsNullOrWhiteSpace(applicationId)
        ? null : Prefix + applicationId;

    public static bool IsApplication(string? path) => path?.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) == true;
}
