namespace PiPrint.App.Models;

public enum LayoutMode
{
    OneUp,      // 1 page per sheet
    TwoUp,      // 2 pages per sheet (side by side)
    FourUp,     // 4 pages per sheet (2x2 grid)
    EightUp,    // 8 pages per sheet (4x2 grid)
    Booklet     // Folded saddle-stitch booklet imposition
}

public enum DuplexMode
{
    Simplex,        // Single-sided
    HardwareDuplex, // Automatic printer hardware duplex
    ManualDuplex    // Manual 2-pass duplex helper
}

public enum WatermarkType
{
    None,
    Draft,
    Confidential,
    Copy,
    Custom
}
