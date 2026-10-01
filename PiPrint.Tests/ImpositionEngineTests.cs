using PiPrint.App.Models;
using PiPrint.App.Services;

namespace PiPrint.Tests;

public class ImpositionEngineTests
{
    private readonly ImpositionEngine _engine = new();

    private List<PageItem> CreatePages(int count)
    {
        var list = new List<PageItem>();
        for (int i = 0; i < count; i++)
        {
            list.Add(new PageItem(i + 1, i, "TestJob") { DisplayNumber = i + 1 });
        }
        return list;
    }

    [Fact]
    public void TestLoadArchivedFile()
    {
        var archiveDir = @"C:\ProgramData\PiPrint\Spool\Archive";
        if (!System.IO.Directory.Exists(archiveDir)) return;

        var files = System.IO.Directory.GetFiles(archiveDir, "*.xps");
        if (files.Length == 0) return;

        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var service = new XpsDocumentService();
                var lastFile = files[^1];
                var pages = service.LoadFromXpsFile(lastFile);
                Assert.NotEmpty(pages);

                var highResBmp = pages[0].PreviewImage;
                Assert.NotNull(highResBmp);

                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create((System.Windows.Media.Imaging.BitmapSource)highResBmp));
                using var stream = new System.IO.MemoryStream();
                encoder.Save(stream);
                Assert.True(stream.Length > 0);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void TestSaveAsPdfQueue()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var service = new XpsDocumentService();
                var pages = service.GenerateSampleDocument();
                var engine = new ImpositionEngine();
                var sheets = engine.ComputeSheets(pages, LayoutMode.OneUp);
                var doc = service.BuildComposedDocument(sheets);

                var tempXps = System.IO.Path.GetTempFileName() + ".xps";
                var tempPdf = System.IO.Path.GetTempFileName() + ".pdf";

                try
                {
                    var pm = new PrinterManager();
                    pm.SaveAsXps(doc, tempXps);
                    Assert.True(System.IO.File.Exists(tempXps));

                    // Test printing directly to PDF file via winspool
                    bool converted = pm.PrintXpsFileToPdf(tempXps, tempPdf);
                    Console.WriteLine("Converted via Win32: " + converted);
                    if (converted)
                    {
                        Assert.True(System.IO.File.Exists(tempPdf));
                        var bytes = System.IO.File.ReadAllBytes(tempPdf);
                        Assert.True(bytes.Length > 0);
                        var header = System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 5));
                        Console.WriteLine("PDF Header: " + header + ", Size: " + bytes.Length);
                        Assert.Equal("%PDF-", header);
                    }
                }
                finally
                {
                    if (System.IO.File.Exists(tempXps)) System.IO.File.Delete(tempXps);
                    if (System.IO.File.Exists(tempPdf)) System.IO.File.Delete(tempPdf);
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void TestPrintComposedDocumentFromArchivedXps()
    {
        var archiveDir = @"C:\ProgramData\PiPrint\Spool\Archive";
        if (!System.IO.Directory.Exists(archiveDir)) return;

        var files = System.IO.Directory.GetFiles(archiveDir, "*.xps");
        if (files.Length == 0) return;

        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var service = new XpsDocumentService();
                var lastFile = files[^1];
                var pages = service.LoadFromXpsFile(lastFile);
                Assert.NotEmpty(pages);

                var engine = new ImpositionEngine();
                var sheets = engine.ComputeSheets(pages, LayoutMode.OneUp);
                var compDoc = service.BuildComposedDocument(sheets);

                var tempFile = System.IO.Path.GetTempFileName() + ".xps";
                try
                {
                    var pm = new PrinterManager();
                    pm.SaveAsXps(compDoc, tempFile);
                    Assert.True(System.IO.File.Exists(tempFile));
                    Console.WriteLine("SUCCESS! Saved file length: " + new System.IO.FileInfo(tempFile).Length);
                }
                finally
                {
                    service.ClearActivePackages();
                    if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            Console.WriteLine("FULL EXCEPTION:\n" + threadEx.ToString());
            throw new Exception("Reproduction: " + threadEx.ToString(), threadEx);
        }
    }

    [Fact]
    public void OneUp_GeneratesCorrectSheetCount()
    {
        var pages = CreatePages(5);
        var sheets = _engine.ComputeSheets(pages, LayoutMode.OneUp);

        Assert.Equal(5, sheets.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Single(sheets[i].Slots);
            Assert.Equal(i + 1, sheets[i].Slots[0].Page?.DisplayNumber);
        }
    }

    [Fact]
    public void TwoUp_GeneratesCorrectPairs()
    {
        var pages = CreatePages(5);
        var sheets = _engine.ComputeSheets(pages, LayoutMode.TwoUp);

        Assert.Equal(3, sheets.Count);

        // Sheet 1: Pages 1 and 2
        Assert.Equal(2, sheets[0].Slots.Count);
        Assert.Equal(1, sheets[0].Slots[0].Page?.DisplayNumber);
        Assert.Equal(2, sheets[0].Slots[1].Page?.DisplayNumber);

        // Sheet 2: Pages 3 and 4
        Assert.Equal(2, sheets[1].Slots.Count);
        Assert.Equal(3, sheets[1].Slots[0].Page?.DisplayNumber);
        Assert.Equal(4, sheets[1].Slots[1].Page?.DisplayNumber);

        // Sheet 3: Page 5 only
        Assert.Single(sheets[2].Slots);
        Assert.Equal(5, sheets[2].Slots[0].Page?.DisplayNumber);
    }

    [Fact]
    public void FourUp_GeneratesFourPerSheet()
    {
        var pages = CreatePages(7);
        var sheets = _engine.ComputeSheets(pages, LayoutMode.FourUp);

        Assert.Equal(2, sheets.Count);
        Assert.Equal(4, sheets[0].Slots.Count);
        Assert.Equal(3, sheets[1].Slots.Count);
    }

    [Fact]
    public void Booklet_EightPages_GeneratesExactSaddleStitchSequence()
    {
        var pages = CreatePages(8);
        var sheets = _engine.ComputeSheets(pages, LayoutMode.Booklet);

        Assert.Equal(4, sheets.Count);

        // Sheet 1 (Outer Front): Left=8, Right=1
        Assert.Equal(8, sheets[0].Slots[0].Page?.DisplayNumber);
        Assert.Equal(1, sheets[0].Slots[1].Page?.DisplayNumber);

        // Sheet 2 (Outer Back): Left=2, Right=7
        Assert.Equal(2, sheets[1].Slots[0].Page?.DisplayNumber);
        Assert.Equal(7, sheets[1].Slots[1].Page?.DisplayNumber);

        // Sheet 3 (Inner Front): Left=6, Right=3
        Assert.Equal(6, sheets[2].Slots[0].Page?.DisplayNumber);
        Assert.Equal(3, sheets[2].Slots[1].Page?.DisplayNumber);

        // Sheet 4 (Inner Center Spread): Left=4, Right=5
        Assert.Equal(4, sheets[3].Slots[0].Page?.DisplayNumber);
        Assert.Equal(5, sheets[3].Slots[1].Page?.DisplayNumber);
    }

    [Fact]
    public void Booklet_FivePages_PadsToEightWithBlanks()
    {
        var pages = CreatePages(5);
        var sheets = _engine.ComputeSheets(pages, LayoutMode.Booklet);

        Assert.Equal(4, sheets.Count);

        Assert.Null(sheets[0].Slots[0].Page); // Page 8 slot is blank
        Assert.Equal(1, sheets[0].Slots[1].Page?.DisplayNumber); // Page 1

        Assert.Equal(2, sheets[1].Slots[0].Page?.DisplayNumber); // Page 2
        Assert.Null(sheets[1].Slots[1].Page); // Page 7 slot is blank

        Assert.Null(sheets[2].Slots[0].Page); // Page 6 slot is blank
        Assert.Equal(3, sheets[2].Slots[1].Page?.DisplayNumber); // Page 3

        Assert.Equal(4, sheets[3].Slots[0].Page?.DisplayNumber); // Page 4
        Assert.Equal(5, sheets[3].Slots[1].Page?.DisplayNumber); // Page 5
    }

    [Fact]
    public void PageReordering_UpdatesDisplayNumbers()
    {
        var thread = new Thread(() =>
        {
            var vm = new PiPrint.App.ViewModels.MainViewModel();
            var p1 = new PageItem(1, 0) { DisplayNumber = 1 };
            var p2 = new PageItem(2, 1) { DisplayNumber = 2 };
            var p3 = new PageItem(3, 2) { DisplayNumber = 3 };
            vm.Pages.Add(p1);
            vm.Pages.Add(p2);
            vm.Pages.Add(p3);

            // Drag page 1 to position 3 (oldIndex 0 -> newIndex 2)
            vm.MovePage(0, 2);

            Assert.Equal(3, vm.Pages.Count);
            Assert.Equal(2, vm.Pages[0].Id);
            Assert.Equal(1, vm.Pages[0].DisplayNumber);
            Assert.Equal(3, vm.Pages[1].Id);
            Assert.Equal(2, vm.Pages[1].DisplayNumber);
            Assert.Equal(1, vm.Pages[2].Id);
            Assert.Equal(3, vm.Pages[2].DisplayNumber);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
