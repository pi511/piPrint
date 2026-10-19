using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace PiPrint.App.Models;

public class PageItem : INotifyPropertyChanged
{
    private int _displayNumber;
    private int _rotationAngle;
    private bool _isSelected;
    private bool _isBlank;
    private ImageSource? _thumbnail;

    public int Id { get; }
    public int OriginalIndex { get; set; }
    public string SourceJobName { get; set; } = "Print Job";
    public double Width { get; set; } = 816; // Standard 8.5" at 96 DPI
    public double Height { get; set; } = 1056; // Standard 11" at 96 DPI

    public Visual? PageVisual { get; set; }

    public int DisplayNumber
    {
        get => _displayNumber;
        set => SetField(ref _displayNumber, value);
    }

    public int RotationAngle
    {
        get => _rotationAngle;
        set => SetField(ref _rotationAngle, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public bool IsBlank
    {
        get => _isBlank;
        set => SetField(ref _isBlank, value);
    }

    private ImageSource? _previewImage;

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set => SetField(ref _thumbnail, value);
    }

    public ImageSource? PreviewImage
    {
        get => _previewImage ?? _thumbnail;
        set => SetField(ref _previewImage, value);
    }

    public double OriginalWidth { get; set; } = 0;
    public double OriginalHeight { get; set; } = 0;
    public Visual? OriginalPageVisual { get; set; }
    public ImageSource? OriginalPreviewImage { get; set; }
    public ImageSource? OriginalThumbnail { get; set; }
    public Rect? CropRect { get; set; }
    public bool IsCropped => CropRect.HasValue && CropRect.Value.Width > 0 && CropRect.Value.Height > 0;

    public PageItem(int id, int originalIndex, string sourceJobName = "Print Job")
    {
        Id = id;
        OriginalIndex = originalIndex;
        DisplayNumber = originalIndex + 1;
        SourceJobName = sourceJobName;
    }

    public void ApplyCrop(Rect rect)
    {
        if (!IsCropped)
        {
            OriginalPageVisual = PageVisual;
            OriginalPreviewImage = PreviewImage;
            OriginalThumbnail = Thumbnail;
            OriginalWidth = Width;
            OriginalHeight = Height;
        }

        double ow = OriginalWidth > 0 ? OriginalWidth : Width;
        double oh = OriginalHeight > 0 ? OriginalHeight : Height;

        double cx = Math.Clamp(rect.X, 0, Math.Max(0, ow - 10));
        double cy = Math.Clamp(rect.Y, 0, Math.Max(0, oh - 10));
        double cw = Math.Clamp(rect.Width, 10, ow - cx);
        double ch = Math.Clamp(rect.Height, 10, oh - cy);

        var finalRect = new Rect(cx, cy, cw, ch);
        CropRect = finalRect;
        Width = cw;
        Height = ch;

        // Crop PreviewImage and Thumbnail
        var baseBmp = (OriginalPreviewImage ?? PreviewImage) as System.Windows.Media.Imaging.BitmapSource;
        if (baseBmp != null)
        {
            double scaleX = (double)baseBmp.PixelWidth / ow;
            double scaleY = (double)baseBmp.PixelHeight / oh;

            int px = Math.Clamp((int)Math.Round(finalRect.X * scaleX), 0, Math.Max(0, baseBmp.PixelWidth - 1));
            int py = Math.Clamp((int)Math.Round(finalRect.Y * scaleY), 0, Math.Max(0, baseBmp.PixelHeight - 1));
            int pw = Math.Clamp((int)Math.Round(finalRect.Width * scaleX), 1, baseBmp.PixelWidth - px);
            int ph = Math.Clamp((int)Math.Round(finalRect.Height * scaleY), 1, baseBmp.PixelHeight - py);

            var croppedBmp = new System.Windows.Media.Imaging.CroppedBitmap(baseBmp, new System.Windows.Int32Rect(px, py, pw, ph));
            croppedBmp.Freeze();
            PreviewImage = croppedBmp;

            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(180, 240, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 180, 240));
                var brush = new ImageBrush(croppedBmp) { Stretch = Stretch.Uniform };
                dc.DrawRectangle(brush, null, new Rect(0, 0, 180, 240));
            }
            rtb.Render(dv);
            rtb.Freeze();
            Thumbnail = rtb;
        }

        // Crop PageVisual
        var sourceVisual = OriginalPageVisual ?? PageVisual;
        if (sourceVisual != null)
        {
            var croppedCanvas = new System.Windows.Controls.Canvas
            {
                Width = cw,
                Height = ch,
                ClipToBounds = true
            };

            var innerCanvas = new System.Windows.Controls.Canvas
            {
                Width = ow,
                Height = oh,
                Background = new VisualBrush(sourceVisual)
                {
                    Stretch = Stretch.Fill,
                    Viewbox = new Rect(0, 0, ow, oh),
                    ViewboxUnits = BrushMappingMode.Absolute
                }
            };
            System.Windows.Controls.Canvas.SetLeft(innerCanvas, -cx);
            System.Windows.Controls.Canvas.SetTop(innerCanvas, -cy);
            croppedCanvas.Children.Add(innerCanvas);
            PageVisual = croppedCanvas;
        }

        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        OnPropertyChanged(nameof(IsCropped));
        OnPropertyChanged(nameof(PreviewImage));
        OnPropertyChanged(nameof(Thumbnail));
    }

    public void ResetCrop()
    {
        if (!IsCropped) return;

        Width = OriginalWidth;
        Height = OriginalHeight;
        PageVisual = OriginalPageVisual;
        PreviewImage = OriginalPreviewImage;
        Thumbnail = OriginalThumbnail;
        CropRect = null;

        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        OnPropertyChanged(nameof(IsCropped));
        OnPropertyChanged(nameof(PreviewImage));
        OnPropertyChanged(nameof(Thumbnail));
    }

    public void RotateClockwise()
    {
        RotationAngle = (RotationAngle + 90) % 360;
    }

    public void RotateCounterClockwise()
    {
        RotationAngle = (RotationAngle + 270) % 360;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
