using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PiPrint.App.Models;
using PiPrint.App.Services;
using PiPrint.App.ViewModels;

namespace PiPrint.App.Views;

public partial class PageCropperControl : UserControl
{
    private enum DragMode
    {
        None,
        DrawingNew,
        MovingBox,
        ResizeNW,
        ResizeN,
        ResizeNE,
        ResizeE,
        ResizeSE,
        ResizeS,
        ResizeSW,
        ResizeW
    }

    private DragMode _currentMode = DragMode.None;
    private Point _startPoint;
    private Rect _startRect;
    private Rect _currentCropRect; // in Page coordinates [0..Page.OriginalWidth, 0..Page.OriginalHeight]
    private double _canvasScale = 1.0;

    private PageItem? CurrentPage => (DataContext as MainViewModel)?.CroppingPage 
                                  ?? (DataContext as MainViewModel)?.SelectedPage;

    public PageCropperControl()
    {
        InitializeComponent();
        Loaded += PageCropperControl_Loaded;
        DataContextChanged += PageCropperControl_DataContextChanged;
        IsVisibleChanged += PageCropperControl_IsVisibleChanged;
    }

    private void PageCropperControl_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeCropper();
    }

    private void PageCropperControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        InitializeCropper();
    }

    private void PageCropperControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            Dispatcher.BeginInvoke(new Action(InitializeCropper), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    public void InitializeCropper()
    {
        var page = CurrentPage;
        if (page == null) return;

        double pw = page.OriginalWidth > 0 ? page.OriginalWidth : page.Width;
        double ph = page.OriginalHeight > 0 ? page.OriginalHeight : page.Height;

        TitleText.Text = $"Crop Page {page.DisplayNumber}";
        SubtitleText.Text = $"Original Size: {pw:F0} × {ph:F0} pt • Source: {page.SourceJobName}";
        BtnResetOriginal.Visibility = page.IsCropped ? Visibility.Visible : Visibility.Collapsed;

        // Set Image Source
        var bmp = (page.OriginalPreviewImage ?? page.PreviewImage ?? page.Thumbnail) as BitmapSource;
        PageImage.Source = bmp;

        // Fit canvas to a comfortable editing height (e.g., 680 pt)
        double availableH = ActualHeight > 100 ? ActualHeight : (Parent as FrameworkElement)?.ActualHeight ?? 650;
        double targetH = Math.Min(700, Math.Max(350, availableH - 140));
        if (targetH < 300) targetH = 600;
        _canvasScale = targetH / ph;
        double canvasW = pw * _canvasScale;
        double canvasH = ph * _canvasScale;

        CropperCanvas.Width = canvasW;
        CropperCanvas.Height = canvasH;
        PageImage.Width = canvasW;
        PageImage.Height = canvasH;

        if (page.IsCropped && page.CropRect.HasValue)
        {
            _currentCropRect = page.CropRect.Value;
        }
        else
        {
            // Default to Auto-Trim content bounds or 92% box
            _currentCropRect = PageCropService.AutoDetectContentBounds(page);
        }

        UpdateVisuals();
        Focus();
    }

    private void CanvasContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var page = CurrentPage;
        if (page == null) return;

        double ph = page.OriginalHeight > 0 ? page.OriginalHeight : page.Height;
        double pw = page.OriginalWidth > 0 ? page.OriginalWidth : page.Width;

        double targetH = Math.Min(700, Math.Max(350, ActualHeight - 140));
        if (targetH > 0 && ph > 0)
        {
            _canvasScale = targetH / ph;
            CropperCanvas.Width = pw * _canvasScale;
            CropperCanvas.Height = ph * _canvasScale;
            PageImage.Width = CropperCanvas.Width;
            PageImage.Height = CropperCanvas.Height;
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        var page = CurrentPage;
        if (page == null) return;

        double pw = page.OriginalWidth > 0 ? page.OriginalWidth : page.Width;
        double ph = page.OriginalHeight > 0 ? page.OriginalHeight : page.Height;
        double cw = CropperCanvas.Width;
        double ch = CropperCanvas.Height;

        // Normalize crop rect within page bounds
        double cx = Math.Clamp(_currentCropRect.X, 0, Math.Max(0, pw - 5));
        double cy = Math.Clamp(_currentCropRect.Y, 0, Math.Max(0, ph - 5));
        double cw_crop = Math.Clamp(_currentCropRect.Width, 5, pw - cx);
        double ch_crop = Math.Clamp(_currentCropRect.Height, 5, ph - cy);
        _currentCropRect = new Rect(cx, cy, cw_crop, ch_crop);

        // Convert page coordinates to canvas UI coordinates
        double uiX = cx * _canvasScale;
        double uiY = cy * _canvasScale;
        double uiW = cw_crop * _canvasScale;
        double uiH = ch_crop * _canvasScale;

        // Position Selection Box
        Canvas.SetLeft(SelectionBox, uiX);
        Canvas.SetTop(SelectionBox, uiY);
        SelectionBox.Width = uiW;
        SelectionBox.Height = uiH;

        // Position 4 Dark Shading Masks
        // Top Mask
        Canvas.SetLeft(MaskTop, 0);
        Canvas.SetTop(MaskTop, 0);
        MaskTop.Width = cw;
        MaskTop.Height = Math.Max(0, uiY);

        // Bottom Mask
        Canvas.SetLeft(MaskBottom, 0);
        Canvas.SetTop(MaskBottom, uiY + uiH);
        MaskBottom.Width = cw;
        MaskBottom.Height = Math.Max(0, ch - (uiY + uiH));

        // Left Mask
        Canvas.SetLeft(MaskLeft, 0);
        Canvas.SetTop(MaskLeft, uiY);
        MaskLeft.Width = Math.Max(0, uiX);
        MaskLeft.Height = uiH;

        // Right Mask
        Canvas.SetLeft(MaskRight, uiX + uiW);
        Canvas.SetTop(MaskRight, uiY);
        MaskRight.Width = Math.Max(0, cw - (uiX + uiW));
        MaskRight.Height = uiH;

        // Position Rule-of-Thirds Grid Lines
        double thirdW = uiW / 3.0;
        double thirdH = uiH / 3.0;

        GridLineV1.X1 = uiX + thirdW; GridLineV1.Y1 = uiY; GridLineV1.X2 = uiX + thirdW; GridLineV1.Y2 = uiY + uiH;
        GridLineV2.X1 = uiX + thirdW * 2; GridLineV2.Y1 = uiY; GridLineV2.X2 = uiX + thirdW * 2; GridLineV2.Y2 = uiY + uiH;

        GridLineH1.X1 = uiX; GridLineH1.Y1 = uiY + thirdH; GridLineH2.X1 = uiX; GridLineH2.Y1 = uiY + thirdH * 2;
        GridLineH1.X2 = uiX + uiW; GridLineH1.Y2 = uiY + thirdH; GridLineH2.X2 = uiX + uiW; GridLineH2.Y2 = uiY + thirdH * 2;

        // Position 8 Handles
        PositionHandle(HandleNW, uiX - 6, uiY - 6);
        PositionHandle(HandleN,  uiX + uiW / 2 - 6, uiY - 6);
        PositionHandle(HandleNE, uiX + uiW - 6, uiY - 6);
        PositionHandle(HandleE,  uiX + uiW - 6, uiY + uiH / 2 - 6);
        PositionHandle(HandleSE, uiX + uiW - 6, uiY + uiH - 6);
        PositionHandle(HandleS,  uiX + uiW / 2 - 6, uiY + uiH - 6);
        PositionHandle(HandleSW, uiX - 6, uiY + uiH - 6);
        PositionHandle(HandleW,  uiX - 6, uiY + uiH / 2 - 6);

        // Position & Update Dimension Tag
        double pctW = (cw_crop / pw) * 100.0;
        double pctH = (ch_crop / ph) * 100.0;
        DimensionText.Text = $"{cw_crop:F0} × {ch_crop:F0} pt ({pctW:F0}% × {pctH:F0}%)";

        double tagLeft = Math.Clamp(uiX + (uiW - 130) / 2, 4, Math.Max(4, cw - 140));
        double tagTop = uiY + uiH + 8;
        if (tagTop + 24 > ch) tagTop = uiY - 26; // Place above if near bottom edge
        Canvas.SetLeft(DimensionTag, tagLeft);
        Canvas.SetTop(DimensionTag, tagTop);
    }

    private void PositionHandle(FrameworkElement handle, double x, double y)
    {
        Canvas.SetLeft(handle, x);
        Canvas.SetTop(handle, y);
    }

    private void CropperCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        CropperCanvas.CaptureMouse();
        _startPoint = e.GetPosition(CropperCanvas);
        _startRect = _currentCropRect;

        // Determine what was clicked
        var hit = e.OriginalSource as DependencyObject;
        if (hit == HandleNW) _currentMode = DragMode.ResizeNW;
        else if (hit == HandleN)  _currentMode = DragMode.ResizeN;
        else if (hit == HandleNE) _currentMode = DragMode.ResizeNE;
        else if (hit == HandleE)  _currentMode = DragMode.ResizeE;
        else if (hit == HandleSE) _currentMode = DragMode.ResizeSE;
        else if (hit == HandleS)  _currentMode = DragMode.ResizeS;
        else if (hit == HandleSW) _currentMode = DragMode.ResizeSW;
        else if (hit == HandleW)  _currentMode = DragMode.ResizeW;
        else if (hit == SelectionBox) _currentMode = DragMode.MovingBox;
        else
        {
            // Clicked on empty space: start drawing a brand new rectangle!
            _currentMode = DragMode.DrawingNew;
            double pageX = _startPoint.X / _canvasScale;
            double pageY = _startPoint.Y / _canvasScale;
            _currentCropRect = new Rect(pageX, pageY, 1, 1);
            UpdateVisuals();
        }
    }

    private void CropperCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_currentMode == DragMode.None) return;

        Point currentPt = e.GetPosition(CropperCanvas);
        double deltaX = (currentPt.X - _startPoint.X) / _canvasScale;
        double deltaY = (currentPt.Y - _startPoint.Y) / _canvasScale;

        var page = CurrentPage;
        if (page == null) return;
        double pw = page.OriginalWidth > 0 ? page.OriginalWidth : page.Width;
        double ph = page.OriginalHeight > 0 ? page.OriginalHeight : page.Height;

        switch (_currentMode)
        {
            case DragMode.DrawingNew:
            {
                double x1 = Math.Clamp(_startPoint.X / _canvasScale, 0, pw);
                double y1 = Math.Clamp(_startPoint.Y / _canvasScale, 0, ph);
                double x2 = Math.Clamp(currentPt.X / _canvasScale, 0, pw);
                double y2 = Math.Clamp(currentPt.Y / _canvasScale, 0, ph);

                double rx = Math.Min(x1, x2);
                double ry = Math.Min(y1, y2);
                double rw = Math.Max(5, Math.Abs(x2 - x1));
                double rh = Math.Max(5, Math.Abs(y2 - y1));
                _currentCropRect = new Rect(rx, ry, rw, rh);
                break;
            }

            case DragMode.MovingBox:
            {
                double nx = Math.Clamp(_startRect.X + deltaX, 0, pw - _startRect.Width);
                double ny = Math.Clamp(_startRect.Y + deltaY, 0, ph - _startRect.Height);
                _currentCropRect = new Rect(nx, ny, _startRect.Width, _startRect.Height);
                break;
            }

            case DragMode.ResizeNW:
            {
                double nx = Math.Clamp(_startRect.X + deltaX, 0, _startRect.Right - 10);
                double ny = Math.Clamp(_startRect.Y + deltaY, 0, _startRect.Bottom - 10);
                _currentCropRect = new Rect(nx, ny, _startRect.Right - nx, _startRect.Bottom - ny);
                break;
            }

            case DragMode.ResizeN:
            {
                double ny = Math.Clamp(_startRect.Y + deltaY, 0, _startRect.Bottom - 10);
                _currentCropRect = new Rect(_startRect.X, ny, _startRect.Width, _startRect.Bottom - ny);
                break;
            }

            case DragMode.ResizeNE:
            {
                double ny = Math.Clamp(_startRect.Y + deltaY, 0, _startRect.Bottom - 10);
                double nw = Math.Clamp(_startRect.Width + deltaX, 10, pw - _startRect.X);
                _currentCropRect = new Rect(_startRect.X, ny, nw, _startRect.Bottom - ny);
                break;
            }

            case DragMode.ResizeE:
            {
                double nw = Math.Clamp(_startRect.Width + deltaX, 10, pw - _startRect.X);
                _currentCropRect = new Rect(_startRect.X, _startRect.Y, nw, _startRect.Height);
                break;
            }

            case DragMode.ResizeSE:
            {
                double nw = Math.Clamp(_startRect.Width + deltaX, 10, pw - _startRect.X);
                double nh = Math.Clamp(_startRect.Height + deltaY, 10, ph - _startRect.Y);
                _currentCropRect = new Rect(_startRect.X, _startRect.Y, nw, nh);
                break;
            }

            case DragMode.ResizeS:
            {
                double nh = Math.Clamp(_startRect.Height + deltaY, 10, ph - _startRect.Y);
                _currentCropRect = new Rect(_startRect.X, _startRect.Y, _startRect.Width, nh);
                break;
            }

            case DragMode.ResizeSW:
            {
                double nx = Math.Clamp(_startRect.X + deltaX, 0, _startRect.Right - 10);
                double nh = Math.Clamp(_startRect.Height + deltaY, 10, ph - _startRect.Y);
                _currentCropRect = new Rect(nx, _startRect.Y, _startRect.Right - nx, nh);
                break;
            }

            case DragMode.ResizeW:
            {
                double nx = Math.Clamp(_startRect.X + deltaX, 0, _startRect.Right - 10);
                _currentCropRect = new Rect(nx, _startRect.Y, _startRect.Right - nx, _startRect.Height);
                break;
            }
        }

        UpdateVisuals();
    }

    private void CropperCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_currentMode != DragMode.None)
        {
            _currentMode = DragMode.None;
            CropperCanvas.ReleaseMouseCapture();
            UpdateVisuals();
        }
    }

    private void BtnAutoTrim_Click(object sender, RoutedEventArgs e)
    {
        var page = CurrentPage;
        if (page == null) return;

        _currentCropRect = PageCropService.AutoDetectContentBounds(page);
        UpdateVisuals();
        StatusBarText.Text = "✨ Content automatically detected! All outer margins have been framed for trimming.";
    }

    private void BtnPreset5_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPage == null) return;
        _currentCropRect = PageCropService.GetPresetCropRect(CurrentPage, CropPreset.Trim5Percent);
        UpdateVisuals();
    }

    private void BtnPresetSquare_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPage == null) return;
        _currentCropRect = PageCropService.GetPresetCropRect(CurrentPage, CropPreset.Square1x1);
        UpdateVisuals();
    }

    private void BtnPresetWide_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPage == null) return;
        _currentCropRect = PageCropService.GetPresetCropRect(CurrentPage, CropPreset.Widescreen16x9);
        UpdateVisuals();
    }

    private void BtnPresetTop_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPage == null) return;
        _currentCropRect = PageCropService.GetPresetCropRect(CurrentPage, CropPreset.TopHalf);
        UpdateVisuals();
    }

    private void BtnPresetBottom_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPage == null) return;
        _currentCropRect = PageCropService.GetPresetCropRect(CurrentPage, CropPreset.BottomHalf);
        UpdateVisuals();
    }

    private void BtnPresetFull_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPage == null) return;
        _currentCropRect = PageCropService.GetPresetCropRect(CurrentPage, CropPreset.FullPage);
        UpdateVisuals();
    }

    private void BtnApplyCrop_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ApplyCropCommand.Execute(_currentCropRect);
        }
    }

    private void BtnApplyAll_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ApplyCropToAllCommand.Execute(_currentCropRect);
        }
    }

    private void BtnResetOriginal_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ResetCropCommand.Execute(null);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CancelCropCommand.Execute(null);
        }
    }

    private void UserControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnApplyCrop_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            BtnCancel_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            BtnAutoTrim_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}
