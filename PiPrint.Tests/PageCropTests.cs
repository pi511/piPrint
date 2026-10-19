using System.Windows;
using PiPrint.App.Models;
using PiPrint.App.Services;

namespace PiPrint.Tests;

public class PageCropTests
{
    [Fact]
    public void ApplyCrop_UpdatesDimensionsAndSetsIsCropped()
    {
        var page = new PageItem(1, 0, "TestJob")
        {
            Width = 800,
            Height = 1000
        };

        var cropRect = new Rect(100, 150, 600, 700);
        page.ApplyCrop(cropRect);

        Assert.True(page.IsCropped);
        Assert.Equal(600, page.Width);
        Assert.Equal(700, page.Height);
        Assert.Equal(800, page.OriginalWidth);
        Assert.Equal(1000, page.OriginalHeight);
        Assert.Equal(cropRect, page.CropRect);
    }

    [Fact]
    public void ResetCrop_RestoresOriginalDimensions()
    {
        var page = new PageItem(1, 0, "TestJob")
        {
            Width = 800,
            Height = 1000
        };

        var cropRect = new Rect(50, 50, 400, 500);
        page.ApplyCrop(cropRect);
        Assert.True(page.IsCropped);

        page.ResetCrop();
        Assert.False(page.IsCropped);
        Assert.Equal(800, page.Width);
        Assert.Equal(1000, page.Height);
        Assert.Null(page.CropRect);
    }

    [Fact]
    public void GetPresetCropRect_Trim5Percent_CalculatesCorrectRect()
    {
        var page = new PageItem(1, 0, "TestJob")
        {
            Width = 1000,
            Height = 2000
        };

        var rect = PageCropService.GetPresetCropRect(page, CropPreset.Trim5Percent);

        Assert.Equal(50, rect.X);
        Assert.Equal(100, rect.Y);
        Assert.Equal(900, rect.Width);
        Assert.Equal(1800, rect.Height);
    }

    [Fact]
    public void GetPresetCropRect_TopHalf_CalculatesCorrectRect()
    {
        var page = new PageItem(1, 0, "TestJob")
        {
            Width = 800,
            Height = 1000
        };

        var rect = PageCropService.GetPresetCropRect(page, CropPreset.TopHalf);

        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(800, rect.Width);
        Assert.Equal(500, rect.Height);
    }

    [Fact]
    public void AutoDetectContentBounds_FallbackWhenNoBitmap_ReturnsMargins()
    {
        var page = new PageItem(1, 0, "TestJob")
        {
            Width = 1000,
            Height = 1000
        };

        var rect = PageCropService.AutoDetectContentBounds(page);

        Assert.True(rect.Width > 0 && rect.Height > 0);
        Assert.Equal(60, rect.X);
        Assert.Equal(60, rect.Y);
        Assert.Equal(880, rect.Width);
        Assert.Equal(880, rect.Height);
    }

    [Fact]
    public void ImpositionEngine_ComputesSheetsWithCroppedPages()
    {
        var engine = new ImpositionEngine();
        var pages = new List<PageItem>
        {
            new PageItem(1, 0, "Job") { Width = 600, Height = 800 },
            new PageItem(2, 1, "Job") { Width = 600, Height = 800 }
        };

        pages[0].ApplyCrop(new Rect(50, 50, 500, 700));

        var sheets = engine.ComputeSheets(pages, LayoutMode.TwoUp);
        Assert.Single(sheets);
        Assert.Equal(2, sheets[0].Slots.Count);
        Assert.NotNull(sheets[0].Slots[0].Page);
        Assert.Equal(500, sheets[0].Slots[0].Page!.Width);
        Assert.Equal(700, sheets[0].Slots[0].Page!.Height);
    }
}
