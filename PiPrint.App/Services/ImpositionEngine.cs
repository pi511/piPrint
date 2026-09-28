using System.Windows;
using PiPrint.App.Models;

namespace PiPrint.App.Services;

public class ImpositionEngine
{
    public double BasePageWidth { get; set; } = 816;   // 8.5" at 96 DPI
    public double BasePageHeight { get; set; } = 1056; // 11" at 96 DPI
    public double SheetMargin { get; set; } = 24;      // 0.25"
    public double SlotGap { get; set; } = 12;

    public List<SheetLayout> ComputeSheets(IReadOnlyList<PageItem> pages, LayoutMode mode)
    {
        var activePages = pages.Where(p => !p.IsBlank).ToList();
        if (activePages.Count == 0)
        {
            return new List<SheetLayout>();
        }

        return mode switch
        {
            LayoutMode.OneUp => ComputeOneUp(activePages),
            LayoutMode.TwoUp => ComputeTwoUp(activePages),
            LayoutMode.FourUp => ComputeFourUp(activePages),
            LayoutMode.EightUp => ComputeEightUp(activePages),
            LayoutMode.Booklet => ComputeBooklet(activePages),
            _ => ComputeOneUp(activePages)
        };
    }

    private List<SheetLayout> ComputeOneUp(List<PageItem> pages)
    {
        var sheets = new List<SheetLayout>();
        for (int i = 0; i < pages.Count; i++)
        {
            var sheet = new SheetLayout(i + 1, BasePageWidth, BasePageHeight, isLandscape: false)
            {
                IsBackSide = (i % 2 == 1)
            };

            var bounds = new Rect(
                SheetMargin,
                SheetMargin,
                BasePageWidth - 2 * SheetMargin,
                BasePageHeight - 2 * SheetMargin
            );

            sheet.Slots.Add(new PlacedPageSlot
            {
                Page = pages[i],
                Bounds = bounds,
                SlotRotation = pages[i].RotationAngle,
                HasBorder = false
            });

            sheets.Add(sheet);
        }
        return sheets;
    }

    private List<SheetLayout> ComputeTwoUp(List<PageItem> pages)
    {
        var sheets = new List<SheetLayout>();
        double sheetW = BasePageHeight; // 1056 (Landscape)
        double sheetH = BasePageWidth;  // 816

        double availableW = sheetW - 2 * SheetMargin - SlotGap;
        double slotW = availableW / 2.0;
        double slotH = sheetH - 2 * SheetMargin;

        int sheetIndex = 1;
        for (int i = 0; i < pages.Count; i += 2)
        {
            var sheet = new SheetLayout(sheetIndex++, sheetW, sheetH, isLandscape: true)
            {
                IsBackSide = ((sheetIndex - 2) % 2 == 1)
            };

            // Left slot
            sheet.Slots.Add(new PlacedPageSlot
            {
                Page = pages[i],
                Bounds = new Rect(SheetMargin, SheetMargin, slotW, slotH),
                SlotRotation = pages[i].RotationAngle,
                HasBorder = true
            });

            // Right slot (if exists)
            if (i + 1 < pages.Count)
            {
                sheet.Slots.Add(new PlacedPageSlot
                {
                    Page = pages[i + 1],
                    Bounds = new Rect(SheetMargin + slotW + SlotGap, SheetMargin, slotW, slotH),
                    SlotRotation = pages[i + 1].RotationAngle,
                    HasBorder = true
                });
            }

            sheets.Add(sheet);
        }
        return sheets;
    }

    private List<SheetLayout> ComputeFourUp(List<PageItem> pages)
    {
        var sheets = new List<SheetLayout>();
        double sheetW = BasePageWidth;  // 816 (Portrait)
        double sheetH = BasePageHeight; // 1056

        double availableW = sheetW - 2 * SheetMargin - SlotGap;
        double availableH = sheetH - 2 * SheetMargin - SlotGap;
        double slotW = availableW / 2.0;
        double slotH = availableH / 2.0;

        int sheetIndex = 1;
        for (int i = 0; i < pages.Count; i += 4)
        {
            var sheet = new SheetLayout(sheetIndex++, sheetW, sheetH, isLandscape: false)
            {
                IsBackSide = ((sheetIndex - 2) % 2 == 1)
            };

            int[] dx = [0, 1, 0, 1];
            int[] dy = [0, 0, 1, 1];

            for (int slot = 0; slot < 4 && (i + slot) < pages.Count; slot++)
            {
                double x = SheetMargin + dx[slot] * (slotW + SlotGap);
                double y = SheetMargin + dy[slot] * (slotH + SlotGap);

                sheet.Slots.Add(new PlacedPageSlot
                {
                    Page = pages[i + slot],
                    Bounds = new Rect(x, y, slotW, slotH),
                    SlotRotation = pages[i + slot].RotationAngle,
                    HasBorder = true
                });
            }

            sheets.Add(sheet);
        }
        return sheets;
    }

    private List<SheetLayout> ComputeEightUp(List<PageItem> pages)
    {
        var sheets = new List<SheetLayout>();
        double sheetW = BasePageHeight; // 1056 (Landscape)
        double sheetH = BasePageWidth;  // 816

        int cols = 4;
        int rows = 2;
        double availableW = sheetW - 2 * SheetMargin - (cols - 1) * SlotGap;
        double availableH = sheetH - 2 * SheetMargin - (rows - 1) * SlotGap;
        double slotW = availableW / cols;
        double slotH = availableH / rows;

        int sheetIndex = 1;
        for (int i = 0; i < pages.Count; i += 8)
        {
            var sheet = new SheetLayout(sheetIndex++, sheetW, sheetH, isLandscape: true)
            {
                IsBackSide = ((sheetIndex - 2) % 2 == 1)
            };

            for (int slot = 0; slot < 8 && (i + slot) < pages.Count; slot++)
            {
                int c = slot % cols;
                int r = slot / cols;
                double x = SheetMargin + c * (slotW + SlotGap);
                double y = SheetMargin + r * (slotH + SlotGap);

                sheet.Slots.Add(new PlacedPageSlot
                {
                    Page = pages[i + slot],
                    Bounds = new Rect(x, y, slotW, slotH),
                    SlotRotation = pages[i + slot].RotationAngle,
                    HasBorder = true
                });
            }

            sheets.Add(sheet);
        }
        return sheets;
    }

    public List<SheetLayout> ComputeBooklet(List<PageItem> pages)
    {
        var sheets = new List<SheetLayout>();
        int count = pages.Count;
        if (count == 0) return sheets;

        // In a saddle-stitch booklet, page count must be a multiple of 4
        int totalBookletPages = ((count + 3) / 4) * 4;
        int physicalSheets = totalBookletPages / 4;

        double sheetW = BasePageHeight; // 1056 (Landscape)
        double sheetH = BasePageWidth;  // 816

        double availableW = sheetW - 2 * SheetMargin - SlotGap;
        double slotW = availableW / 2.0;
        double slotH = sheetH - 2 * SheetMargin;

        int sheetNumber = 1;
        for (int s = 0; s < physicalSheets; s++)
        {
            // Front Side
            int frontLeftPageNum = totalBookletPages - 2 * s;
            int frontRightPageNum = 2 * s + 1;

            var frontSheet = new SheetLayout(sheetNumber++, sheetW, sheetH, isLandscape: true)
            {
                IsBackSide = false
            };

            AddBookletSlot(frontSheet, pages, frontLeftPageNum, SheetMargin, SheetMargin, slotW, slotH);
            AddBookletSlot(frontSheet, pages, frontRightPageNum, SheetMargin + slotW + SlotGap, SheetMargin, slotW, slotH);
            sheets.Add(frontSheet);

            // Back Side
            int backLeftPageNum = 2 * s + 2;
            int backRightPageNum = totalBookletPages - 2 * s - 1;

            var backSheet = new SheetLayout(sheetNumber++, sheetW, sheetH, isLandscape: true)
            {
                IsBackSide = true
            };

            AddBookletSlot(backSheet, pages, backLeftPageNum, SheetMargin, SheetMargin, slotW, slotH);
            AddBookletSlot(backSheet, pages, backRightPageNum, SheetMargin + slotW + SlotGap, SheetMargin, slotW, slotH);
            sheets.Add(backSheet);
        }

        return sheets;
    }

    private void AddBookletSlot(SheetLayout sheet, List<PageItem> pages, int pageNum1Based, double x, double y, double w, double h)
    {
        PageItem? page = null;
        int zeroIndex = pageNum1Based - 1;
        if (zeroIndex >= 0 && zeroIndex < pages.Count)
        {
            page = pages[zeroIndex];
        }

        sheet.Slots.Add(new PlacedPageSlot
        {
            Page = page,
            Bounds = new Rect(x, y, w, h),
            SlotRotation = page?.RotationAngle ?? 0,
            HasBorder = true
        });
    }
}
