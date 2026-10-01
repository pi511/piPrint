using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PiPrint.App.Models;
using PiPrint.App.ViewModels;

namespace PiPrint.App;

public partial class MainWindow : Window
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 101;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONDBLCLK = 0x0203;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const int IDI_APPLICATION = 32512;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public uint uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    private const int WM_GETICON = 0x007F;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;

    private bool _isExplicitExit;
    private HwndSource? _hwndSource;
    private ContextMenu? _trayMenu;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeTray();

        string[] args = Environment.GetCommandLineArgs();
        if (args.Any(a => a.Equals("--background", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("--tray", StringComparison.OrdinalIgnoreCase)))
        {
            Hide();
        }
    }

    private void InitializeTray()
    {
        var helper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);

        IntPtr hIcon = SendMessage(helper.Handle, WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero);
        if (hIcon == IntPtr.Zero)
        {
            hIcon = SendMessage(helper.Handle, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero);
        }
        if (hIcon == IntPtr.Zero)
        {
            hIcon = LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
        }

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = helper.Handle,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = hIcon,
            szTip = "PiPrint - Print Preview & Page Organizer"
        };
        Shell_NotifyIcon(NIM_ADD, ref nid);

        // WPF ContextMenu for Tray Icon
        _trayMenu = new ContextMenu();
        var showItem = new MenuItem { Header = "Show PiPrint", FontWeight = FontWeights.Bold };
        showItem.Click += (s, e) => ShowAndActivate();
        _trayMenu.Items.Add(showItem);

        var sampleItem = new MenuItem { Header = "Load Sample Report" };
        sampleItem.Click += (s, e) =>
        {
            ShowAndActivate();
            if (DataContext is MainViewModel vm) vm.LoadSampleReport();
        };
        _trayMenu.Items.Add(sampleItem);

        _trayMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit PiPrint" };
        exitItem.Click += (s, e) =>
        {
            _isExplicitExit = true;
            RemoveTrayIcon();
            Application.Current.Shutdown();
        };
        _trayMenu.Items.Add(exitItem);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int action = lParam.ToInt32();
            if (action == WM_LBUTTONUP || action == WM_LBUTTONDBLCLK)
            {
                ShowAndActivate();
                handled = true;
            }
            else if (action == WM_RBUTTONUP)
            {
                if (_trayMenu != null)
                {
                    _trayMenu.IsOpen = true;
                }
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void RemoveTrayIcon()
    {
        var helper = new WindowInteropHelper(this);
        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = helper.Handle,
            uID = 1001
        };
        Shell_NotifyIcon(NIM_DELETE, ref nid);
    }

    private Point _dragStartPoint;
    private bool _isDragging;

    private void PagesListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private void PagesListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
        {
            Point position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var sourceElement = e.OriginalSource as DependencyObject;
                var item = FindAncestor<ListBoxItem>(sourceElement);
                if (item?.DataContext is PageItem pageItem)
                {
                    _isDragging = true;
                    DragDrop.DoDragDrop(item, pageItem, DragDropEffects.Move);
                    _isDragging = false;
                }
            }
        }
    }

    private void PagesListBox_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(PageItem)))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void PagesListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(PageItem)) is PageItem draggedItem && DataContext is MainViewModel vm)
        {
            var sourceElement = e.OriginalSource as DependencyObject;
            var targetItemContainer = FindAncestor<ListBoxItem>(sourceElement);
            int oldIndex = vm.Pages.IndexOf(draggedItem);

            if (targetItemContainer?.DataContext is PageItem targetItem)
            {
                int newIndex = vm.Pages.IndexOf(targetItem);
                if (oldIndex != -1 && newIndex != -1 && oldIndex != newIndex)
                {
                    vm.MovePage(oldIndex, newIndex);
                }
            }
            else if (oldIndex != -1 && vm.Pages.Count > 1)
            {
                vm.MovePage(oldIndex, vm.Pages.Count - 1);
            }
        }
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.ZoomInCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.ZoomOutCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.ZoomFitCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            RemoveTrayIcon();
            base.OnClosing(e);
        }
    }
}