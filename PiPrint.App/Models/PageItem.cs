using System.ComponentModel;
using System.Runtime.CompilerServices;
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

    public PageItem(int id, int originalIndex, string sourceJobName = "Print Job")
    {
        Id = id;
        OriginalIndex = originalIndex;
        DisplayNumber = originalIndex + 1;
        SourceJobName = sourceJobName;
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
