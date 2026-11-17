using System.Windows;

namespace FrisoDock.App.Views;

internal static class DockDragGeometry
{
    public static bool HasMoved(Point origin, Point current, double horizontalThreshold, double verticalThreshold) =>
        Math.Abs(current.X - origin.X) >= horizontalThreshold || Math.Abs(current.Y - origin.Y) >= verticalThreshold;

    public static int FindTarget(Point point, IReadOnlyList<Rect> slots, bool vertical)
    {
        int nearest = -1;
        double distance = double.PositiveInfinity;
        double coordinate = vertical ? point.Y : point.X;
        for (int index = 0; index < slots.Count; index++)
        {
            Rect slot = slots[index];
            if (slot.IsEmpty)
            {
                continue;
            }

            double center = vertical ? slot.Top + slot.Height / 2 : slot.Left + slot.Width / 2;
            double current = Math.Abs(coordinate - center);
            if (current < distance)
            {
                nearest = index;
                distance = current;
            }
        }

        return nearest;
    }
}
