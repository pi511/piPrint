using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PiPrint.App.Models;
using PiPrint.App.Services;
using PiPrint.App.ViewModels;

namespace PiPrint.App.Views;

public partial class SheetPreviewControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty CurrentSheetProperty =
        DependencyProperty.Register(nameof(CurrentSheet), typeof(SheetLayout), typeof(SheetPreviewControl),
            new PropertyMetadata(null, OnSheetChanged));

    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(SheetPreviewControl),
            new PropertyMetadata(1.0, OnSheetChanged));

    public static readonly DependencyProperty WatermarkProperty =
        DependencyProperty.Register(nameof(Watermark), typeof(WatermarkType), typeof(SheetPreviewControl),
            new PropertyMetadata(WatermarkType.None, OnSheetChanged));

    public static readonly DependencyProperty CustomWatermarkTextProperty =
        DependencyProperty.Register(nameof(CustomWatermarkText), typeof(string), typeof(SheetPreviewControl),
            new PropertyMetadata("", OnSheetChanged));

    public static readonly DependencyProperty IsGrayscaleProperty =
        DependencyProperty.Register(nameof(IsGrayscale), typeof(bool), typeof(SheetPreviewControl),
            new PropertyMetadata(false, OnSheetChanged));

    public SheetLayout? CurrentSheet
    {
        get => (SheetLayout?)GetValue(CurrentSheetProperty);
        set => SetValue(CurrentSheetProperty, value);
    }

    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    public WatermarkType Watermark
    {
        get => (WatermarkType)GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public string CustomWatermarkText
    {
        get => (string)GetValue(CustomWatermarkTextProperty);
        set => SetValue(CustomWatermarkTextProperty, value);
    }

    public bool IsGrayscale
    {
        get => (bool)GetValue(IsGrayscaleProperty);
        set => SetValue(IsGrayscaleProperty, value);
    }

    public SheetPreviewControl()
    {
        InitializeComponent();
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            if (DataContext is MainViewModel vm)
            {
                if (e.Delta > 0)
                {
                    vm.ZoomInCommand.Execute(null);
                }
                else if (e.Delta < 0)
                {
                    vm.ZoomOutCommand.Execute(null);
                }
            }
        }
        base.OnPreviewMouseWheel(e);
    }

    private static void OnSheetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SheetPreviewControl ctrl)
        {
            ctrl.RenderSheet();
        }
    }

    private void RenderSheet()
    {
        SlotCanvas.Children.Clear();

        if (CurrentSheet == null)
        {
            SheetBorder.Visibility = Visibility.Collapsed;
            return;
        }

        SheetBorder.Visibility = Visibility.Visible;

        double baseScale = 0.85 * ZoomLevel;
        double sheetW = CurrentSheet.Width * baseScale;
        double sheetH = CurrentSheet.Height * baseScale;

        SheetBorder.Width = sheetW;
        SheetBorder.Height = sheetH;
        SheetContainer.Width = sheetW;
        SheetContainer.Height = sheetH;
        SlotCanvas.Width = sheetW;
        SlotCanvas.Height = sheetH;

        string side1 = LocalizationService.Instance.GetString("Str_Side1");
        string side2 = LocalizationService.Instance.GetString("Str_Side2");
        string orient = CurrentSheet.IsLandscape 
            ? LocalizationService.Instance.GetString("Str_Landscape") 
            : LocalizationService.Instance.GetString("Str_Portrait");
        string sideLabel = CurrentSheet.IsBackSide ? side2 : side1;
        SheetTagText.Text = $"{CurrentSheet.SheetIndex} • {sideLabel} • {orient}";

        string wm = Watermark switch
        {
            WatermarkType.Draft => LocalizationService.Instance.GetString("Str_WmDraft"),
            WatermarkType.Confidential => LocalizationService.Instance.GetString("Str_WmConfidential"),
            WatermarkType.Copy => LocalizationService.Instance.GetString("Str_WmCopy"),
            WatermarkType.Custom => CustomWatermarkText,
            _ => ""
        };

        if (!string.IsNullOrEmpty(wm))
        {
            WatermarkText.Text = wm;
            WatermarkText.FontSize = Math.Max(32, sheetH * 0.12);
            WatermarkText.Visibility = Visibility.Visible;
        }
        else
        {
            WatermarkText.Visibility = Visibility.Collapsed;
        }

        foreach (var slot in CurrentSheet.Slots)
        {
            double slotX = slot.Bounds.X * baseScale;
            double slotY = slot.Bounds.Y * baseScale;
            double slotW = slot.Bounds.Width * baseScale;
            double slotH = slot.Bounds.Height * baseScale;

            var slotContainer = new Border
            {
                Width = slotW,
                Height = slotH,
                Background = Brushes.White,
                BorderBrush = slot.HasBorder ? new SolidColorBrush(Color.FromRgb(203, 213, 225)) : Brushes.Transparent,
                BorderThickness = slot.HasBorder ? new Thickness(1) : new Thickness(0),
                CornerRadius = new CornerRadius(2),
                ClipToBounds = true
            };

            var slotGrid = new Grid();
            slotContainer.Child = slotGrid;

            if (slot.Page != null)
            {
                var imageSource = slot.Page.PreviewImage ?? slot.Page.Thumbnail;
                if (imageSource != null)
                {
                    var img = new Image
                    {
                        Source = imageSource,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

                    if (slot.SlotRotation != 0)
                    {
                        img.RenderTransformOrigin = new Point(0.5, 0.5);
                        img.RenderTransform = new RotateTransform(slot.SlotRotation);
                    }

                    slotGrid.Children.Add(img);
                }
                else if (slot.Page.PageVisual != null)
                {
                    double pWidth = slot.Page.Width > 0 ? slot.Page.Width : 816;
                    double pHeight = slot.Page.Height > 0 ? slot.Page.Height : 1056;

                    bool isRotated90 = (Math.Abs(slot.SlotRotation) % 180) != 0;
                    double effW = isRotated90 ? pHeight : pWidth;
                    double effH = isRotated90 ? pWidth : pHeight;
                    double scale = Math.Min(slotW / effW, slotH / effH);

                    var visualHost = new Canvas
                    {
                        Width = pWidth,
                        Height = pHeight,
                        RenderTransformOrigin = new Point(0.5, 0.5),
                        Background = new VisualBrush(slot.Page.PageVisual)
                        {
                            Stretch = Stretch.Uniform,
                            Viewbox = new Rect(0, 0, pWidth, pHeight),
                            ViewboxUnits = BrushMappingMode.Absolute
                        }
                    };

                    RenderOptions.SetBitmapScalingMode(visualHost, BitmapScalingMode.HighQuality);
                    TextOptions.SetTextRenderingMode(visualHost, TextRenderingMode.ClearType);

                    var transformGroup = new TransformGroup();
                    if (slot.SlotRotation != 0)
                    {
                        transformGroup.Children.Add(new RotateTransform(slot.SlotRotation));
                    }
                    transformGroup.Children.Add(new ScaleTransform(scale, scale));
                    visualHost.RenderTransform = transformGroup;

                    Canvas.SetLeft(visualHost, (slotW - pWidth) / 2);
                    Canvas.SetTop(visualHost, (slotH - pHeight) / 2);

                    var vectorContainer = new Canvas
                    {
                        Width = slotW,
                        Height = slotH,
                        ClipToBounds = true
                    };
                    vectorContainer.Children.Add(visualHost);
                    slotGrid.Children.Add(vectorContainer);
                }

                var pageBadge = new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = new SolidColorBrush(Color.FromArgb(190, 30, 41, 59)),
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 0, 6, 6),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                var pageText = new TextBlock
                {
                    Text = $"p. {slot.Page.DisplayNumber}",
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };
                pageBadge.Child = pageText;
                slotGrid.Children.Add(pageBadge);
            }
            else
            {
                var blankText = new TextBlock
                {
                    Text = "[Blank Page]",
                    Foreground = new SolidColorBrush(Color.FromRgb(160, 174, 192)),
                    FontSize = 12,
                    FontStyle = FontStyles.Italic,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                slotGrid.Children.Add(blankText);
            }

            Canvas.SetLeft(slotContainer, slotX);
            Canvas.SetTop(slotContainer, slotY);
            SlotCanvas.Children.Add(slotContainer);
        }
    }
}
