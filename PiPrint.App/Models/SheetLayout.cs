using System.Windows;

namespace PiPrint.App.Models;

public class PlacedPageSlot
{
    public PageItem? Page { get; set; }
    public Rect Bounds { get; set; }
    public double SlotRotation { get; set; }
    public bool HasBorder { get; set; } = true;
}

public class SheetLayout
{
    public int SheetIndex { get; set; }
    public double Width { get; set; } = 816; // Standard Letter (8.5 x 11 in @ 96 DPI)
    public double Height { get; set; } = 1056;
    public bool IsLandscape { get; set; }
    public bool IsBackSide { get; set; }
    public List<PlacedPageSlot> Slots { get; set; } = new();

    public SheetLayout(int sheetIndex, double width, double height, bool isLandscape = false)
    {
        SheetIndex = sheetIndex;
        Width = width;
        Height = height;
        IsLandscape = isLandscape;
    }
}
