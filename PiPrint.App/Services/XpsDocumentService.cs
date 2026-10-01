using System.IO;
using System.IO.Packaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Xps.Packaging;
using PiPrint.App.Models;

namespace PiPrint.App.Services;

public class XpsDocumentService
{
    private int _pageIdCounter = 1;

    public List<PageItem> LoadFromXpsFile(string filePath, string jobName = "")
    {
        var result = new List<PageItem>();
        if (!File.Exists(filePath)) return result;

        if (string.IsNullOrEmpty(jobName))
        {
            jobName = System.IO.Path.GetFileNameWithoutExtension(filePath);
        }

        byte[]? bytes = TryReadFileBytesWithRetry(filePath, maxAttempts: 15, delayMs: 200);
        if (bytes == null || bytes.Length == 0)
        {
            throw new IOException($"Could not read file '{filePath}' because it is still being written to by another process.");
        }

        return LoadFromXpsBytes(bytes, jobName);
    }

    private readonly List<(Uri PackUri, Package Package, MemoryStream Stream)> _activePackages = new();
    private readonly object _lock = new();

    public void ClearActivePackages()
    {
        lock (_lock)
        {
            foreach (var item in _activePackages)
            {
                try { PackageStore.RemovePackage(item.PackUri); } catch { }
                try { item.Package.Close(); } catch { }
                try { item.Stream.Dispose(); } catch { }
            }
            _activePackages.Clear();
        }
    }

    public List<PageItem> LoadFromXpsBytes(byte[] bytes, string jobName = "Print Job")
    {
        var result = new List<PageItem>();
        if (bytes == null || bytes.Length == 0) return result;

        // Auto-convert OpenXPS (OXPS) to MSXPS so WPF natively renders it
        bytes = EnsureMsXpsCompatibility(bytes);

        var memoryStream = new MemoryStream(bytes);
        var package = Package.Open(memoryStream, FileMode.Open, FileAccess.Read);

        var packageUri = new Uri("http://piprint/" + Guid.NewGuid().ToString("N") + ".xps");
        var packUri = PackUriHelper.Create(packageUri);
        PackageStore.AddPackage(packUri, package);

        lock (_lock)
        {
            _activePackages.Add((packUri, package, memoryStream));
        }

        try
        {
            var xpsDoc = new XpsDocument(package, CompressionOption.Normal, packUri.AbsoluteUri);
            var docSeq = xpsDoc.GetFixedDocumentSequence();
            if (docSeq == null) return result;

            var paginator = docSeq.DocumentPaginator;
            int count = paginator.PageCount;

            for (int i = 0; i < count; i++)
            {
                var docPage = paginator.GetPage(i);
                if (docPage?.Visual == null) continue;

                var pageItem = new PageItem(_pageIdCounter++, i, jobName)
                {
                    Width = docPage.Size.Width,
                    Height = docPage.Size.Height,
                    PageVisual = docPage.Visual
                };

                pageItem.Thumbnail = RenderVisualToBitmap(docPage.Visual, 180, 240);
                int previewW = (int)Math.Max(1200, docPage.Size.Width * 1.75);
                int previewH = (int)Math.Max(1600, docPage.Size.Height * 1.75);
                pageItem.PreviewImage = RenderVisualToBitmap(docPage.Visual, previewW, previewH);
                pageItem.IsBlank = DetectIfBlank(docPage.Visual);

                result.Add(pageItem);
            }
        }
        catch
        {
            lock (_lock)
            {
                _activePackages.Remove((packUri, package, memoryStream));
            }
            try { PackageStore.RemovePackage(packUri); } catch { }
            try { package.Close(); } catch { }
            try { memoryStream.Dispose(); } catch { }
            throw;
        }

        return result;
    }

    public static byte[]? TryReadFileBytesWithRetry(string filePath, int maxAttempts = 15, int delayMs = 200)
    {
        long lastLength = -1;
        int stableCount = 0;

        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var fi = new FileInfo(filePath);
                    if (fi.Length > 0)
                    {
                        if (fi.Length == lastLength)
                        {
                            stableCount++;
                        }
                        else
                        {
                            lastLength = fi.Length;
                            stableCount = 0;
                        }

                        // When file length is stable for at least 2 checks, attempt to read
                        if (stableCount >= 2 || i > 5)
                        {
                            try
                            {
                                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                using var ms = new MemoryStream();
                                fs.CopyTo(ms);
                                if (ms.Length > 0)
                                {
                                    ms.Position = 0;
                                    using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read, true);
                                    if (zip.Entries.Count > 0)
                                    {
                                        return ms.ToArray();
                                    }
                                }
                            }
                            catch (InvalidDataException)
                            {
                                // Spooler still writing file, continue waiting
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
                // File lock is still held by the print spooler
            }
            catch (UnauthorizedAccessException)
            {
            }

            Thread.Sleep(delayMs);
        }

        return null;
    }

    public List<PageItem> GenerateSampleDocument(string title = "Sample Report")
    {
        var pages = new List<PageItem>();
        string[] pageTitles = [
            "Executive Summary & Project Overview",
            "Market Analysis & Growth Opportunities",
            "Technical Architecture & System Flow",
            "Financial Projections & Cost Reductions",
            "Implementation Roadmap & Milestones",
            "Appendix & System Specifications",
            "" // Trailing blank page for testing blank page detection!
        ];

        for (int i = 0; i < pageTitles.Length; i++)
        {
            string pTitle = pageTitles[i];
            bool isBlank = string.IsNullOrEmpty(pTitle);

            var visual = CreateSamplePageVisual(i + 1, pTitle, isBlank);
            var pageItem = new PageItem(_pageIdCounter++, i, "Sample Report.docx")
            {
                Width = 816,
                Height = 1056,
                PageVisual = visual,
                IsBlank = isBlank
            };

            pageItem.Thumbnail = RenderVisualToBitmap(visual, 180, 240);
            pageItem.PreviewImage = RenderVisualToBitmap(visual, 1428, 1848);
            pages.Add(pageItem);
        }

        return pages;
    }

    private Visual CreateSamplePageVisual(int pageNum, string title, bool isBlank)
    {
        var canvas = new Canvas
        {
            Width = 816,
            Height = 1056,
            Background = Brushes.White
        };

        if (isBlank)
        {
            canvas.Measure(new Size(816, 1056));
            canvas.Arrange(new Rect(0, 0, 816, 1056));
            return canvas;
        }

        // Header Bar
        var header = new Border
        {
            Width = 816,
            Height = 60,
            Background = new SolidColorBrush(Color.FromRgb(30, 60, 114))
        };
        canvas.Children.Add(header);

        var headerText = new TextBlock
        {
            Text = "PIPRINT PREVIEW & PAGE ORGANIZER - REPORT 2026",
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Margin = new Thickness(40, 20, 0, 0)
        };
        canvas.Children.Add(headerText);

        // Page Title
        var titleText = new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush(Color.FromRgb(20, 30, 50)),
            FontWeight = FontWeights.SemiBold,
            FontSize = 26
        };
        Canvas.SetTop(titleText, 100);
        Canvas.SetLeft(titleText, 50);
        canvas.Children.Add(titleText);

        // Divider
        var line = new Line
        {
            X1 = 50,
            Y1 = 145,
            X2 = 766,
            Y2 = 145,
            Stroke = new SolidColorBrush(Color.FromRgb(200, 210, 225)),
            StrokeThickness = 2
        };
        canvas.Children.Add(line);

        // Paragraph 1
        var p1 = new TextBlock
        {
            Text = "PiPrint intercepts documents sent from any Windows application (Word, Chrome, Notepad, etc.). " +
                   "It gives users complete visual control over page ordering, sheet imposition, ink consumption, and paper economy before routing to any physical printer.\n\n" +
                   "Key benefits include substantial paper savings through 2-up and 4-up layouts, automated saddle-stitch booklet creation, and instant deletion of accidental trailing blank pages.",
            TextWrapping = TextWrapping.Wrap,
            Width = 716,
            FontSize = 15,
            LineHeight = 24,
            Foreground = new SolidColorBrush(Color.FromRgb(60, 70, 85))
        };
        Canvas.SetTop(p1, 170);
        Canvas.SetLeft(p1, 50);
        canvas.Children.Add(p1);

        // Colored Diagram Box
        var diagramBox = new Border
        {
            Width = 716,
            Height = 240,
            Background = new SolidColorBrush(Color.FromRgb(240, 245, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(180, 205, 245)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
        Canvas.SetTop(diagramBox, 330);
        Canvas.SetLeft(diagramBox, 50);
        canvas.Children.Add(diagramBox);

        var boxCanvas = new Canvas { Width = 716, Height = 240 };
        diagramBox.Child = boxCanvas;

        Color[] barColors = [
            Color.FromRgb(59, 130, 246),
            Color.FromRgb(16, 185, 129),
            Color.FromRgb(245, 158, 11),
            Color.FromRgb(239, 68, 68)
        ];
        string[] labels = [ "Q1 Print Cost", "Q2 Ink Savings", "Q3 Paper Recycled", "Q4 Efficiency" ];
        double[] heights = [ 120, 180, 150, 210 ];

        for (int b = 0; b < 4; b++)
        {
            var bar = new Border
            {
                Width = 70,
                Height = heights[b],
                Background = new SolidColorBrush(barColors[b]),
                CornerRadius = new CornerRadius(4, 4, 0, 0)
            };
            Canvas.SetLeft(bar, 80 + b * 160);
            Canvas.SetBottom(bar, 40);
            boxCanvas.Children.Add(bar);

            var barLabel = new TextBlock
            {
                Text = labels[b],
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.DimGray,
                TextAlignment = TextAlignment.Center,
                Width = 120
            };
            Canvas.SetLeft(barLabel, 55 + b * 160);
            Canvas.SetBottom(barLabel, 15);
            boxCanvas.Children.Add(barLabel);
        }

        // Paragraph 2
        var p2 = new TextBlock
        {
            Text = "Advanced imposition math maps logical pages directly onto physical front/back sheets. " +
                   "Whether printing to high-volume laser copiers or compact desktop inkjets, vector scaling guarantees ultra-sharp text and raster fidelity.",
            TextWrapping = TextWrapping.Wrap,
            Width = 716,
            FontSize = 14,
            LineHeight = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(80, 90, 100))
        };
        Canvas.SetTop(p2, 600);
        Canvas.SetLeft(p2, 50);
        canvas.Children.Add(p2);

        // Footer
        var footer = new TextBlock
        {
            Text = $"Page {pageNum} | PiPrint Document",
            Foreground = Brushes.Gray,
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            Width = 816
        };
        Canvas.SetTop(footer, 1000);
        Canvas.SetLeft(footer, 0);
        canvas.Children.Add(footer);

        canvas.Measure(new Size(816, 1056));
        canvas.Arrange(new Rect(0, 0, 816, 1056));
        return canvas;
    }

    public FixedDocument BuildComposedDocument(
        List<SheetLayout> sheets,
        WatermarkType watermark = WatermarkType.None,
        string customWatermarkText = "",
        bool isGrayscale = false)
    {
        var fixedDoc = new FixedDocument();

        for (int i = 0; i < sheets.Count; i++)
        {
            var sheet = sheets[i];
            var fixedPage = new FixedPage
            {
                Width = sheet.Width,
                Height = sheet.Height,
                Background = Brushes.White
            };

            foreach (var slot in sheet.Slots)
            {
                if (slot.Page == null || (slot.Page.PageVisual == null && slot.Page.PreviewImage == null))
                {
                    if (slot.HasBorder)
                    {
                        var blankBox = new Border
                        {
                            Width = slot.Bounds.Width,
                            Height = slot.Bounds.Height,
                            BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                            BorderThickness = new Thickness(1),
                            Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
                        };
                        FixedPage.SetLeft(blankBox, slot.Bounds.X);
                        FixedPage.SetTop(blankBox, slot.Bounds.Y);
                        fixedPage.Children.Add(blankBox);
                    }
                    continue;
                }

                var pageContainer = new Grid
                {
                    Width = slot.Bounds.Width,
                    Height = slot.Bounds.Height,
                    ClipToBounds = true
                };

                if (slot.HasBorder)
                {
                    var slotBorder = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(200, 205, 215)),
                        BorderThickness = new Thickness(0.8),
                        Background = Brushes.White
                    };
                    pageContainer.Children.Add(slotBorder);
                }

                double pW = slot.Page.Width > 0 ? slot.Page.Width : 816;
                double pH = slot.Page.Height > 0 ? slot.Page.Height : 1056;

                bool isRotated90 = (Math.Abs(slot.SlotRotation) % 180) != 0;
                double effW = isRotated90 ? pH : pW;
                double effH = isRotated90 ? pW : pH;
                double scale = Math.Min(slot.Bounds.Width / effW, slot.Bounds.Height / effH);

                Brush pageBrush;
                if (slot.Page.PageVisual != null)
                {
                    pageBrush = new VisualBrush(slot.Page.PageVisual)
                    {
                        Stretch = Stretch.Uniform,
                        Viewbox = new Rect(0, 0, pW, pH),
                        ViewboxUnits = BrushMappingMode.Absolute
                    };
                }
                else
                {
                    pageBrush = new ImageBrush(slot.Page.PreviewImage)
                    {
                        Stretch = Stretch.Uniform,
                        Viewbox = new Rect(0, 0, pW, pH),
                        ViewboxUnits = BrushMappingMode.Absolute
                    };
                }

                var visualHost = new Canvas
                {
                    Width = pW,
                    Height = pH,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    Background = pageBrush
                };

                var transformGroup = new TransformGroup();
                if (slot.SlotRotation != 0)
                {
                    transformGroup.Children.Add(new RotateTransform(slot.SlotRotation));
                }
                transformGroup.Children.Add(new ScaleTransform(scale, scale));
                visualHost.RenderTransform = transformGroup;

                Canvas.SetLeft(visualHost, (slot.Bounds.Width - pW) / 2);
                Canvas.SetTop(visualHost, (slot.Bounds.Height - pH) / 2);

                var innerCanvas = new Canvas
                {
                    Width = slot.Bounds.Width,
                    Height = slot.Bounds.Height,
                    ClipToBounds = true
                };
                innerCanvas.Children.Add(visualHost);
                pageContainer.Children.Add(innerCanvas);

                FixedPage.SetLeft(pageContainer, slot.Bounds.X);
                FixedPage.SetTop(pageContainer, slot.Bounds.Y);
                fixedPage.Children.Add(pageContainer);
            }

            // Overlay Watermark
            string watermarkText = watermark switch
            {
                WatermarkType.Draft => LocalizationService.Instance.GetString("Str_WmDraft"),
                WatermarkType.Confidential => LocalizationService.Instance.GetString("Str_WmConfidential"),
                WatermarkType.Copy => LocalizationService.Instance.GetString("Str_WmCopy"),
                WatermarkType.Custom => customWatermarkText,
                _ => ""
            };

            if (!string.IsNullOrEmpty(watermarkText))
            {
                var watermarkBlock = new TextBlock
                {
                    Text = watermarkText,
                    FontSize = sheet.Height * 0.12,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromArgb(35, 220, 38, 38)),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new RotateTransform(-35)
                };

                var watermarkGrid = new Grid
                {
                    Width = sheet.Width,
                    Height = sheet.Height,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                watermarkGrid.Children.Add(watermarkBlock);
                fixedPage.Children.Add(watermarkGrid);
            }

            var pageContent = new PageContent();
            ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
            fixedDoc.Pages.Add(pageContent);
        }

        return fixedDoc;
    }

    public static ImageSource RenderVisualToBitmap(Visual visual, int width, int height)
    {
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            var brush = new VisualBrush(visual) { Stretch = Stretch.Uniform };
            dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
        }
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    private static bool DetectIfBlank(Visual visual)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(visual);
        if (childCount == 0) return true;

        if (visual is Canvas c && c.Children.Count == 0)
        {
            return true;
        }

        return false;
    }

    public static byte[] EnsureMsXpsCompatibility(byte[] bytes)
    {
        try
        {
            const string oxpsNs = "http://schemas.openxps.org/oxps/v1.0";
            const string msxpsNs = "http://schemas.microsoft.com/xps/2005/06";

            using var inMs = new MemoryStream(bytes);
            using var outMs = new MemoryStream();

            using (var inZip = new System.IO.Compression.ZipArchive(inMs, System.IO.Compression.ZipArchiveMode.Read))
            using (var outZip = new System.IO.Compression.ZipArchive(outMs, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                bool isOxps = false;

                foreach (var entry in inZip.Entries)
                {
                    var newEntry = outZip.CreateEntry(entry.FullName, entry.CompressedLength > 0 ? System.IO.Compression.CompressionLevel.Optimal : System.IO.Compression.CompressionLevel.NoCompression);
                    using var inStream = entry.Open();
                    using var outStream = newEntry.Open();

                    if (entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.EndsWith(".fdseq", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.EndsWith(".fdoc", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.EndsWith(".fpage", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.EndsWith(".dict", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(inStream, System.Text.Encoding.UTF8);
                        string content = reader.ReadToEnd();
                        if (content.Contains(oxpsNs))
                        {
                            isOxps = true;
                            content = content.Replace(oxpsNs, msxpsNs);
                        }
                        using var writer = new StreamWriter(outStream, System.Text.Encoding.UTF8);
                        writer.Write(content);
                    }
                    else
                    {
                        inStream.CopyTo(outStream);
                    }
                }

                if (!isOxps)
                {
                    return bytes;
                }
            }

            outMs.Position = 0;
            return outMs.ToArray();
        }
        catch
        {
            return bytes;
        }
    }
}
