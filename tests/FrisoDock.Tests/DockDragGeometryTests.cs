using System.Windows;
using FrisoDock.App.Views;

namespace FrisoDock.Tests;

public sealed class DockDragGeometryTests
{
    [Theory]
    [InlineData(1, 1, false)]
    [InlineData(3, 3, false)]
    [InlineData(4, 0, true)]
    [InlineData(-4, 0, true)]
    [InlineData(0, 4, true)]
    [InlineData(0, -4, true)]
    public void GestureThreshold_DistinguishesClicksFromDragsOnBothAxes(double x, double y, bool dragging)
    {
        Assert.Equal(dragging, DockDragGeometry.HasMoved(new Point(20, 30), new Point(20 + x, 30 + y), 4, 4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Target_UsesTheDockAxisIncludingGapsAndOutsideEnds(bool vertical)
    {
        Rect[] slots = Enumerable.Range(0, 3).Select(index => vertical
            ? new Rect(0, index * 50, 40, 40) : new Rect(index * 50, 0, 40, 40)).ToArray();
        Point At(double coordinate) => vertical ? new Point(20, coordinate) : new Point(coordinate, 20);

        Assert.Equal(0, DockDragGeometry.FindTarget(At(-20), slots, vertical));
        Assert.Equal(1, DockDragGeometry.FindTarget(At(49), slots, vertical));
        Assert.Equal(2, DockDragGeometry.FindTarget(At(500), slots, vertical));
    }

    [Fact]
    public void MissingContainers_AreSkippedWithoutInventingATarget()
    {
        Assert.Equal(-1, DockDragGeometry.FindTarget(new Point(), [], false));
        Assert.Equal(-1, DockDragGeometry.FindTarget(new Point(), [Rect.Empty], true));
        Assert.Equal(1, DockDragGeometry.FindTarget(new Point(20, 20), [Rect.Empty, new Rect(0, 0, 40, 40)], false));
    }
}
