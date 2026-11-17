using FrisoDock.App.Services;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.Tests;

public sealed class JsonPinnedAppStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"frisodock-pins-{Guid.NewGuid():N}.json");

    [Fact]
    public void UnpinningEveryApp_RemainsEmptyAfterRestart()
    {
        var store = new JsonPinnedAppStore(_path);
        var app = new PinnedApp("Alpha", @"C:\Apps\Alpha.exe");
        store.Save([app]);
        var pins = new PinnedAppsService(store, new PinnedAppsReorder());
        pins.TogglePin(new DockItem(app.Key, app.DisplayName, app.IconSource, app, []));

        Assert.Empty(new JsonPinnedAppStore(_path).Load());
        Assert.Empty(new PinnedAppsService(new JsonPinnedAppStore(_path), new PinnedAppsReorder()).Current);
    }

    [Fact]
    public void ChangedOrderAndShellLaunchMetadata_SurviveRestart()
    {
        var first = new PinnedApp("Calculator", @"shell:AppsFolder\Test.Calculator!App",
            IconPath: @"shell:AppsFolder\Test.Calculator!App", MatchExecutablePath: @"C:\Apps\Calculator.exe");
        var second = new PinnedApp("Beta", @"C:\Apps\Beta.exe", "--profile=work", @"C:\Icons\Beta.ico");
        var store = new JsonPinnedAppStore(_path);
        store.Save([first, second]);
        var pins = new PinnedAppsService(store, new PinnedAppsReorder());
        pins.Reorder(second.Key, 0, persist: false);
        pins.CommitReorder();

        Assert.Equal([second, first], new JsonPinnedAppStore(_path).Load());
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void FailedTemporaryWrite_PreservesThePreviousValidFile()
    {
        var first = new PinnedApp("Alpha", @"C:\Apps\Alpha.exe");
        var store = new JsonPinnedAppStore(_path);
        store.Save([first]);
        Directory.CreateDirectory(_path + ".tmp");
        store.Save([new PinnedApp("Beta", @"C:\Apps\Beta.exe")]);
        Assert.Equal(first, Assert.Single(new JsonPinnedAppStore(_path).Load()));
    }

    public void Dispose()
    {
        File.Delete(_path);
        string temporary = _path + ".tmp";
        if (Directory.Exists(temporary))
        {
            Directory.Delete(temporary);
        }
        else
        {
            File.Delete(temporary);
        }
    }
}
