using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PiPrint.App.Models;

namespace PiPrint.App.Services;

public enum CropPreset
{
    AutoDetect,
    Trim5Percent,
    Trim10Percent,
    Square1x1,
    Widescreen16x9,
    TopHalf,
    BottomHalf,
    LeftHalf,
    RightHalf,
    FullPage
}

public static class PageCropService
{
    public static Rect GetPresetCropRect(PageItem page, CropPreset preset)
    {
        double ow = page.OriginalWidth > 0 ? page.OriginalWidth : page.Width;
        double oh = page.OriginalHeight > 0 ? page.OriginalHeight : page.Height;

        switch (preset)
        {
            case CropPreset.AutoDetect:
                return AutoDetectContentBounds(page);

            case CropPreset.Trim5Percent:
                return new Rect(ow * 0.05, oh * 0.05, ow * 0.90, oh * 0.90);

            case CropPreset.Trim10Percent:
                return new Rect(ow * 0.10, oh * 0.10, ow * 0.80, oh * 0.80);

            case CropPreset.Square1x1:
            {
                double side = Math.Min(ow, oh) * 0.90;
                double x = (ow - side) / 2.0;
                double y = (oh - side) / 2.0;
                return new Rect(x, y, side, side);
            }

            case CropPreset.Widescreen16x9:
            {
                double targetW = ow * 0.95;
                double targetH = targetW * (9.0 / 16.0);
                if (targetH > oh)
                {
                    targetH = oh * 0.95;
                    targetW = targetH * (16.0 / 9.0);
                }
                double x = (ow - targetW) / 2.0;
                double y = (oh - targetH) / 2.0;
                return new Rect(x, y, targetW, targetH);
            }

            case CropPreset.TopHalf:
                return new Rect(0, 0, ow, oh / 2.0);

            case CropPreset.BottomHalf:
                return new Rect(0, oh / 2.0, ow, oh / 2.0);

            case CropPreset.LeftHalf:
                return new Rect(0, 0, ow / 2.0, oh);

            case CropPreset.RightHalf:
                return new Rect(ow / 2.0, 0, ow / 2.0, oh);

            case CropPreset.FullPage:
            default:
                return new Rect(0, 0, ow, oh);
        }
    }

    public static Rect AutoDetectContentBounds(PageItem page)
    {
        double ow = page.OriginalWidth > 0 ? page.OriginalWidth : page.Width;
        double oh = page.OriginalHeight > 0 ? page.OriginalHeight : page.Height;

        var bmp = (page.OriginalPreviewImage ?? page.PreviewImage ?? page.Thumbnail) as BitmapSource;
        if (bmp == null || bmp.PixelWidth < 10 || bmp.PixelHeight < 10)
        {
            return new Rect(ow * 0.06, oh * 0.06, ow * 0.88, oh * 0.88);
        }

        try
        {
            var formatted = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            int w = formatted.PixelWidth;
            int h = formatted.PixelHeight;
            int stride = w * 4;
            byte[] pixels = new byte[stride * h];
            formatted.CopyPixels(pixels, stride, 0);

            // Sample background color by checking 4 corner areas
            int bgR = 0, bgG = 0, bgB = 0;
            int[,] cornerSamples = new int[,] {
                { 2, 2 },
                { Math.Max(0, w - 3), 2 },
                { 2, Math.Max(0, h - 3) },
                { Math.Max(0, w - 3), Math.Max(0, h - 3) }
            };

            for (int i = 0; i < 4; i++)
            {
                int sx = cornerSamples[i, 0];
                int sy = cornerSamples[i, 1];
                int idx = sy * stride + sx * 4;
                bgB += pixels[idx];
                bgG += pixels[idx + 1];
                bgR += pixels[idx + 2];
            }
            bgR /= 4;
            bgG /= 4;
            bgB /= 4;

            bool IsContentPixel(int x, int y)
            {
                int idx = y * stride + x * 4;
                int b = pixels[idx];
                int g = pixels[idx + 1];
                int r = pixels[idx + 2];
                int diff = Math.Abs(r - bgR) + Math.Abs(g - bgG) + Math.Abs(b - bgB);
                return diff > 45;
            }

            int minY = -1;
            int maxY = -1;
            int minX = -1;
            int maxX = -1;

            // Scan top
            for (int y = 0; y < h; y += 2)
            {
                int count = 0;
                for (int x = 0; x < w; x += 4)
                {
                    if (IsContentPixel(x, y))
                    {
                        count++;
                        if (count >= 2) { minY = y; break; }
                    }
                }
                if (minY != -1) break;
            }

            // Scan bottom
            for (int y = h - 1; y >= 0; y -= 2)
            {
                int count = 0;
                for (int x = 0; x < w; x += 4)
                {
                    if (IsContentPixel(x, y))
                    {
                        count++;
                        if (count >= 2) { maxY = y; break; }
                    }
                }
                if (maxY != -1) break;
            }

            if (minY == -1 || maxY == -1 || maxY <= minY)
            {
                return new Rect(ow * 0.06, oh * 0.06, ow * 0.88, oh * 0.88);
            }

            // Scan left
            for (int x = 0; x < w; x += 2)
            {
                int count = 0;
                for (int y = minY; y <= maxY; y += 4)
                {
                    if (IsContentPixel(x, y))
                    {
                        count++;
                        if (count >= 2) { minX = x; break; }
                    }
                }
                if (minX != -1) break;
            }

            // Scan right
            for (int x = w - 1; x >= 0; x -= 2)
            {
                int count = 0;
                for (int y = minY; y <= maxY; y += 4)
                {
                    if (IsContentPixel(x, y))
                    {
                        count++;
                        if (count >= 2) { maxX = x; break; }
                    }
                }
                if (maxX != -1) break;
            }

            if (minX == -1 || maxX == -1 || maxX <= minX)
            {
                return new Rect(ow * 0.06, oh * 0.06, ow * 0.88, oh * 0.88);
            }

            // Add 3.5% padding so text does not clip
            double padX = (maxX - minX) * 0.035;
            double padY = (maxY - minY) * 0.035;

            double leftPx = Math.Max(0, minX - padX);
            double rightPx = Math.Min(w, maxX + padX);
            double topPx = Math.Max(0, minY - padY);
            double bottomPx = Math.Min(h, maxY + padY);

            double cropX = (leftPx / w) * ow;
            double cropY = (topPx / h) * oh;
            double cropW = ((rightPx - leftPx) / w) * ow;
            double cropH = ((bottomPx - topPx) / h) * oh;

            return new Rect(cropX, cropY, cropW, cropH);
        }
        catch
        {
            return new Rect(ow * 0.06, oh * 0.06, ow * 0.88, oh * 0.88);
        }
    }
}
