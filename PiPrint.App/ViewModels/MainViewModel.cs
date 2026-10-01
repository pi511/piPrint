using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Win32;
using PiPrint.App.Common;
using PiPrint.App.Models;
using PiPrint.App.Services;

namespace PiPrint.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ImpositionEngine _impositionEngine = new();
    private readonly XpsDocumentService _xpsService = new();
    private readonly PrinterManager _printerManager = new();
    private readonly SpoolWatcherService _spoolWatcher;

    private PageItem? _selectedPage;
    private SheetLayout? _currentSheet;
    private int _currentSheetIndex = 1;
    private LayoutMode _currentLayoutMode = LayoutMode.OneUp;
    private DuplexMode _currentDuplexMode = DuplexMode.Simplex;
    private WatermarkType _currentWatermark = WatermarkType.None;
    private string _customWatermarkText = "CONFIDENTIAL";
    private bool _isGrayscale;
    private PrinterInfo? _selectedPrinter;
    private int _copies = 1;
    private double _zoomLevel = 1.0;
    private string _statusMessage = "PiPrint is ready. Waiting for print jobs...";

    public ObservableCollection<PageItem> Pages { get; } = new();
    public ObservableCollection<SheetLayout> Sheets { get; } = new();
    public ObservableCollection<PrinterInfo> InstalledPrinters { get; } = new();

    public bool HasPages => Pages.Count > 0;

    public IReadOnlyList<LanguageOption> Languages => LocalizationService.SupportedLanguages;

    public LanguageOption SelectedLanguage
    {
        get => Languages.FirstOrDefault(l => l.Code == LocalizationService.Instance.CurrentLanguageCode) ?? Languages[0];
        set
        {
            if (value != null && value.Code != LocalizationService.Instance.CurrentLanguageCode)
            {
                LocalizationService.Instance.SetLanguage(value.Code);
                OnPropertyChanged();
                OnPropertyChanged(nameof(SavingsStatistics));
                OnPropertyChanged(nameof(CurrentSheetNumberDisplay));
            }
        }
    }

    public PageItem? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (SetField(ref _selectedPage, value))
            {
                foreach (var p in Pages) p.IsSelected = (p == value);
                JumpToSheetContainingPage(value);
            }
        }
    }

    public SheetLayout? CurrentSheet
    {
        get => _currentSheet;
        set => SetField(ref _currentSheet, value);
    }

    public int CurrentSheetIndex
    {
        get => _currentSheetIndex;
        set
        {
            if (SetField(ref _currentSheetIndex, value))
            {
                if (value >= 1 && value <= Sheets.Count)
                {
                    CurrentSheet = Sheets[value - 1];
                }
                OnPropertyChanged(nameof(CurrentSheetNumberDisplay));
            }
        }
    }

    public string CurrentSheetNumberDisplay => Sheets.Count > 0 
        ? $"Sheet {CurrentSheetIndex} of {Sheets.Count}"
        : "No sheets";

    public LayoutMode CurrentLayoutMode
    {
        get => _currentLayoutMode;
        set
        {
            if (SetField(ref _currentLayoutMode, value))
            {
                RecalculateSheets();
                OnPropertyChanged(nameof(IsOneUp));
                OnPropertyChanged(nameof(IsTwoUp));
                OnPropertyChanged(nameof(IsFourUp));
                OnPropertyChanged(nameof(IsEightUp));
                OnPropertyChanged(nameof(IsBooklet));
            }
        }
    }

    public bool IsOneUp => CurrentLayoutMode == LayoutMode.OneUp;
    public bool IsTwoUp => CurrentLayoutMode == LayoutMode.TwoUp;
    public bool IsFourUp => CurrentLayoutMode == LayoutMode.FourUp;
    public bool IsEightUp => CurrentLayoutMode == LayoutMode.EightUp;
    public bool IsBooklet => CurrentLayoutMode == LayoutMode.Booklet;

    public DuplexMode CurrentDuplexMode
    {
        get => _currentDuplexMode;
        set
        {
            if (SetField(ref _currentDuplexMode, value))
            {
                UpdateSavingsStats();
            }
        }
    }

    public WatermarkType CurrentWatermark
    {
        get => _currentWatermark;
        set
        {
            if (SetField(ref _currentWatermark, value))
            {
                OnPropertyChanged(nameof(IsCustomWatermark));
                RecalculateSheets();
            }
        }
    }

    public bool IsCustomWatermark => CurrentWatermark == WatermarkType.Custom;

    public string CustomWatermarkText
    {
        get => _customWatermarkText;
        set
        {
            if (SetField(ref _customWatermarkText, value))
            {
                RecalculateSheets();
            }
        }
    }

    public bool IsGrayscale
    {
        get => _isGrayscale;
        set
        {
            if (SetField(ref _isGrayscale, value))
            {
                RecalculateSheets();
            }
        }
    }

    public PrinterInfo? SelectedPrinter
    {
        get => _selectedPrinter;
        set => SetField(ref _selectedPrinter, value);
    }

    public int Copies
    {
        get => _copies;
        set => SetField(ref _copies, Math.Max(1, value));
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set => SetField(ref _zoomLevel, Math.Clamp(value, 0.25, 3.0));
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string SavingsStatistics
    {
        get
        {
            int originalPages = Pages.Count(p => !p.IsBlank);
            if (originalPages == 0) return "0 pages • 0 sheets";

            int sheetsCount = Sheets.Count;
            int physicalSheets = CurrentDuplexMode != DuplexMode.Simplex
                ? (sheetsCount + 1) / 2
                : sheetsCount;

            double savedPct = Math.Max(0, (1.0 - (double)physicalSheets / originalPages) * 100);
            return $"{originalPages} pages ➔ {physicalSheets} sheets (Paper Saved: {savedPct:F0}%)";
        }
    }

    // Commands
    public ICommand DeleteSelectedPageCommand { get; }
    public ICommand DeleteBlankPagesCommand { get; }
    public ICommand RotateCWCommand { get; }
    public ICommand RotateCCWCommand { get; }
    public ICommand MovePageUpCommand { get; }
    public ICommand MovePageDownCommand { get; }
    public ICommand NextSheetCommand { get; }
    public ICommand PreviousSheetCommand { get; }
    public ICommand SetLayoutCommand { get; }
    public ICommand LoadSampleCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand SaveAsPdfCommand { get; }
    public ICommand SaveAsXpsCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ZoomFitCommand { get; }

    public MainViewModel()
    {
        DeleteSelectedPageCommand = new RelayCommand(DeleteSelectedPage, () => SelectedPage != null);
        DeleteBlankPagesCommand = new RelayCommand(DeleteBlankPages, () => Pages.Any(p => p.IsBlank));
        RotateCWCommand = new RelayCommand(() => RotateSelected(90), () => SelectedPage != null);
        RotateCCWCommand = new RelayCommand(() => RotateSelected(-90), () => SelectedPage != null);
        MovePageUpCommand = new RelayCommand(MovePageUp, () => CanMovePageUp());
        MovePageDownCommand = new RelayCommand(MovePageDown, () => CanMovePageDown());
        NextSheetCommand = new RelayCommand(NextSheet, () => CurrentSheetIndex < Sheets.Count);
        PreviousSheetCommand = new RelayCommand(PreviousSheet, () => CurrentSheetIndex > 1);
        SetLayoutCommand = new RelayCommand(param =>
        {
            if (param is LayoutMode mode) CurrentLayoutMode = mode;
            else if (Enum.TryParse<LayoutMode>(param?.ToString(), out var parsed)) CurrentLayoutMode = parsed;
        });

        LoadSampleCommand = new RelayCommand(LoadSampleReport);
        OpenFileCommand = new RelayCommand(OpenFile);
        PrintCommand = new RelayCommand(Print, () => HasPages && SelectedPrinter != null);
        SaveAsPdfCommand = new RelayCommand(SaveAsPdf, () => HasPages);
        SaveAsXpsCommand = new RelayCommand(SaveAsXps, () => HasPages);
        ClearAllCommand = new RelayCommand(ClearAll, () => HasPages);

        ZoomInCommand = new RelayCommand(() => ZoomLevel += 0.15);
        ZoomOutCommand = new RelayCommand(() => ZoomLevel -= 0.15);
        ZoomFitCommand = new RelayCommand(() => ZoomLevel = 1.0);

        LoadPrinters();

        // Start background spool watcher
        _spoolWatcher = new SpoolWatcherService();
        _spoolWatcher.JobReceived += OnSpoolJobReceived;
        _spoolWatcher.Start();

        LocalizationService.Instance.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(SavingsStatistics));
            OnPropertyChanged(nameof(CurrentSheetNumberDisplay));
            OnPropertyChanged(nameof(CurrentWatermark));
            RecalculateSheets();
            StatusMessage = LocalizationService.Instance.GetString("Str_StatusReady");
        };

        StatusMessage = LocalizationService.Instance.GetString("Str_StatusReady");
    }

    private void LoadPrinters()
    {
        InstalledPrinters.Clear();
        var list = _printerManager.GetInstalledPrinters();
        foreach (var p in list)
        {
            InstalledPrinters.Add(p);
        }

        SelectedPrinter = InstalledPrinters.FirstOrDefault(p => p.IsDefault)
            ?? InstalledPrinters.FirstOrDefault();
    }

    public void LoadSampleReport()
    {
        Pages.Clear();
        var sample = _xpsService.GenerateSampleDocument();
        foreach (var p in sample)
        {
            Pages.Add(p);
        }

        RenumberPages();
        RecalculateSheets();
        SelectedPage = Pages.FirstOrDefault();
        OnPropertyChanged(nameof(HasPages));
        StatusMessage = "Sample document loaded with 7 pages (including 1 blank page for testing).";
    }

    public void OpenFile()
    {
        string initialDir = _spoolWatcher.SpoolDirectory;
        if (!Directory.Exists(initialDir))
        {
            Directory.CreateDirectory(initialDir);
        }

        var ofd = new OpenFileDialog
        {
            Title = "Open Print Document (from Spool or Disk)",
            Filter = "XPS Documents (*.xps;*.oxps)|*.xps;*.oxps|All Files (*.*)|*.*",
            InitialDirectory = initialDir
        };

        if (ofd.ShowDialog() == true)
        {
            LoadDocumentFile(ofd.FileName, append: false);
        }
    }

    public void LoadDocumentFile(string filePath, bool append = false)
    {
        try
        {
            if (!append)
            {
                Pages.Clear();
                _xpsService.ClearActivePackages();
            }

            var loaded = _xpsService.LoadFromXpsFile(filePath);
            if (loaded.Count == 0)
            {
                MessageBox.Show("No printable pages found in this file.", "PiPrint", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var p in loaded)
            {
                Pages.Add(p);
            }

            RenumberPages();
            RecalculateSheets();
            SelectedPage = Pages.FirstOrDefault();
            OnPropertyChanged(nameof(HasPages));
            StatusMessage = $"Loaded {loaded.Count} pages from {Path.GetFileName(filePath)}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load print document: {ex.Message}", "PiPrint Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnSpoolJobReceived(byte[] bytes, string jobName)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                // Append if we already have a document open, or set as new
                bool append = Pages.Count > 0;
                if (!append)
                {
                    Pages.Clear();
                    _xpsService.ClearActivePackages();
                }

                var loaded = _xpsService.LoadFromXpsBytes(bytes, jobName);
                if (loaded.Count == 0) return;

                foreach (var p in loaded)
                {
                    Pages.Add(p);
                }

                RenumberPages();
                RecalculateSheets();
                SelectedPage = Pages.FirstOrDefault();
                OnPropertyChanged(nameof(HasPages));

                StatusMessage = $"Intercepted print job: {loaded.Count} page(s) from '{jobName}'!";

                // Bring window to foreground on user's desktop
                if (Application.Current.MainWindow != null)
                {
                    Application.Current.MainWindow.Show();
                    if (Application.Current.MainWindow.WindowState == WindowState.Minimized)
                    {
                        Application.Current.MainWindow.WindowState = WindowState.Normal;
                    }
                    Application.Current.MainWindow.Activate();
                    Application.Current.MainWindow.Topmost = true;
                    Application.Current.MainWindow.Topmost = false;
                    Application.Current.MainWindow.Focus();
                }

                try
                {
                    SystemSounds.Asterisk.Play();
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading intercepted job: {ex.Message}";
            }
        });
    }

    public void DeleteSelectedPage()
    {
        if (SelectedPage == null) return;
        int idx = Pages.IndexOf(SelectedPage);
        Pages.Remove(SelectedPage);
        RenumberPages();
        RecalculateSheets();

        if (Pages.Count > 0)
        {
            SelectedPage = Pages[Math.Min(idx, Pages.Count - 1)];
        }
        else
        {
            SelectedPage = null;
        }

        OnPropertyChanged(nameof(HasPages));
        StatusMessage = "Page deleted.";
    }

    public void DeleteBlankPages()
    {
        var blanks = Pages.Where(p => p.IsBlank).ToList();
        if (blanks.Count == 0)
        {
            MessageBox.Show("No blank pages detected.", "PiPrint", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var b in blanks)
        {
            Pages.Remove(b);
        }

        RenumberPages();
        RecalculateSheets();
        SelectedPage = Pages.FirstOrDefault();
        OnPropertyChanged(nameof(HasPages));
        StatusMessage = $"Removed {blanks.Count} blank page(s).";
    }

    public void RotateSelected(int delta)
    {
        if (SelectedPage == null) return;
        SelectedPage.RotationAngle = (SelectedPage.RotationAngle + delta + 360) % 360;
        RecalculateSheets();
    }

    private bool CanMovePageUp() => SelectedPage != null && Pages.IndexOf(SelectedPage) > 0;
    private bool CanMovePageDown() => SelectedPage != null && Pages.IndexOf(SelectedPage) < Pages.Count - 1;

    public void MovePageUp()
    {
        if (!CanMovePageUp() || SelectedPage == null) return;
        int idx = Pages.IndexOf(SelectedPage);
        Pages.Move(idx, idx - 1);
        RenumberPages();
        RecalculateSheets();
    }

    public void MovePageDown()
    {
        if (!CanMovePageDown() || SelectedPage == null) return;
        int idx = Pages.IndexOf(SelectedPage);
        Pages.Move(idx, idx + 1);
        RenumberPages();
        RecalculateSheets();
    }

    public void MovePage(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Pages.Count || newIndex < 0 || newIndex >= Pages.Count || oldIndex == newIndex)
            return;

        Pages.Move(oldIndex, newIndex);
        RenumberPages();
        RecalculateSheets();
        SelectedPage = Pages[newIndex];
        StatusMessage = LocalizationService.Instance.GetString("Str_PageMoved", newIndex + 1);
    }

    public void NextSheet()
    {
        if (CurrentSheetIndex < Sheets.Count)
        {
            CurrentSheetIndex++;
        }
    }

    public void PreviousSheet()
    {
        if (CurrentSheetIndex > 1)
        {
            CurrentSheetIndex--;
        }
    }

    private void JumpToSheetContainingPage(PageItem? page)
    {
        if (page == null) return;
        for (int i = 0; i < Sheets.Count; i++)
        {
            if (Sheets[i].Slots.Any(s => s.Page == page))
            {
                CurrentSheetIndex = i + 1;
                break;
            }
        }
    }

    public void ClearAll()
    {
        if (Pages.Count == 0) return;
        if (MessageBox.Show("Clear all pages from the current session?", "PiPrint - Clear Session", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            Pages.Clear();
            _xpsService.ClearActivePackages();
            Sheets.Clear();
            CurrentSheet = null;
            SelectedPage = null;
            CurrentSheetIndex = 1;
            OnPropertyChanged(nameof(HasPages));
            UpdateSavingsStats();
            StatusMessage = "Session cleared. Ready for next print job.";
        }
    }

    public void RecalculateSheets()
    {
        Sheets.Clear();
        var computed = _impositionEngine.ComputeSheets(Pages, CurrentLayoutMode);
        foreach (var s in computed)
        {
            Sheets.Add(s);
        }

        if (Sheets.Count > 0)
        {
            int targetIndex = Math.Clamp(CurrentSheetIndex, 1, Sheets.Count);
            _currentSheetIndex = targetIndex;
            CurrentSheet = Sheets[targetIndex - 1];
            OnPropertyChanged(nameof(CurrentSheetIndex));
        }
        else
        {
            _currentSheetIndex = 1;
            CurrentSheet = null;
            OnPropertyChanged(nameof(CurrentSheetIndex));
        }

        UpdateSavingsStats();
        OnPropertyChanged(nameof(CurrentSheetNumberDisplay));
    }

    private void RenumberPages()
    {
        for (int i = 0; i < Pages.Count; i++)
        {
            Pages[i].DisplayNumber = i + 1;
        }
    }

    private void UpdateSavingsStats()
    {
        OnPropertyChanged(nameof(SavingsStatistics));
    }

    public void Print()
    {
        if (Sheets.Count == 0)
        {
            MessageBox.Show("No sheets to print.", "PiPrint", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedPrinter == null)
        {
            MessageBox.Show("Please select a target printer.", "PiPrint", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var composedDoc = _xpsService.BuildComposedDocument(
                Sheets.ToList(),
                CurrentWatermark,
                CustomWatermarkText,
                IsGrayscale);

            if (CurrentDuplexMode == DuplexMode.ManualDuplex)
            {
                ExecuteManualDuplexPrint(composedDoc, SelectedPrinter.Name);
            }
            else
            {
                _printerManager.PrintDocument(composedDoc, SelectedPrinter.Name, Copies, CurrentDuplexMode);
                StatusMessage = $"Successfully sent {Sheets.Count} sheet(s) to '{SelectedPrinter.Name}'.";
                MessageBox.Show($"Print job sent to {SelectedPrinter.Name}!", "Print Succeeded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            try
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PiPrint");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"), $"[{DateTime.Now}] Print Error: {ex}\n\n");
            }
            catch { }
            MessageBox.Show($"Error sending print job: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteManualDuplexPrint(FixedDocument fullDoc, string printerName)
    {
        var oddSheets = Sheets.Where((s, idx) => idx % 2 == 0).ToList();
        var oddDoc = _xpsService.BuildComposedDocument(oddSheets, CurrentWatermark, CustomWatermarkText, IsGrayscale);
        _printerManager.PrintDocument(oddDoc, printerName, Copies, DuplexMode.Simplex);

        var res = MessageBox.Show(
            "Side 1 (Front sheets) has been printed.\n\n" +
            "1. Take the printed stack from the output tray.\n" +
            "2. Without changing sheet orientation, place the stack back into the paper tray.\n" +
            "3. Click OK to print Side 2 (Back sheets), or Cancel to abort.",
            "PiPrint - Manual Duplex Helper",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);

        if (res == MessageBoxResult.OK)
        {
            var evenSheets = Sheets.Where((s, idx) => idx % 2 == 1).ToList();
            if (evenSheets.Count > 0)
            {
                var evenDoc = _xpsService.BuildComposedDocument(evenSheets, CurrentWatermark, CustomWatermarkText, IsGrayscale);
                _printerManager.PrintDocument(evenDoc, printerName, Copies, DuplexMode.Simplex);
                StatusMessage = "Manual duplex printing completed successfully.";
            }
        }
    }

    public void SaveAsPdf()
    {
        if (Sheets.Count == 0) return;

        var sfd = new SaveFileDialog
        {
            Title = "Save As PDF",
            Filter = "PDF Document (*.pdf)|*.pdf",
            FileName = "PiPrint_Document.pdf"
        };

        if (sfd.ShowDialog() != true) return;

        string targetPath = sfd.FileName;
        StatusMessage = "Exporting to PDF...";

        try
        {
            var pdfPrinter = InstalledPrinters.FirstOrDefault(p => p.Name.Contains("PDF", StringComparison.OrdinalIgnoreCase));
            string printerName = pdfPrinter?.Name ?? "Microsoft Print to PDF";

            var composedDoc = _xpsService.BuildComposedDocument(Sheets.ToList(), CurrentWatermark, CustomWatermarkText, IsGrayscale);
            var tempXps = Path.GetTempFileName() + ".xps";

            try
            {
                _printerManager.SaveAsXps(composedDoc, tempXps);

                bool success = _printerManager.PrintXpsFileToPdf(tempXps, targetPath, printerName);
                if (success && File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
                {
                    StatusMessage = $"PDF saved successfully to {Path.GetFileName(targetPath)}.";
                    MessageBox.Show($"PDF saved successfully to:\n{targetPath}", "PiPrint", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Could not generate PDF directly via the Windows PDF driver. You can also save as XPS.", "PDF Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusMessage = "PDF export failed.";
                }
            }
            finally
            {
                if (File.Exists(tempXps))
                {
                    try { File.Delete(tempXps); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error saving PDF.";
            try
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PiPrint");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"), $"[{DateTime.Now}] PDF Export Error: {ex}\n\n");
            }
            catch { }
            MessageBox.Show($"Failed to save PDF: {ex.Message}", "PDF Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void SaveAsXps()
    {
        if (Sheets.Count == 0) return;

        var sfd = new SaveFileDialog
        {
            Title = "Save As XPS",
            Filter = "XPS Document (*.xps)|*.xps",
            FileName = "PiPrint_Document.xps"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var composedDoc = _xpsService.BuildComposedDocument(Sheets.ToList(), CurrentWatermark, CustomWatermarkText, IsGrayscale);
                _printerManager.SaveAsXps(composedDoc, sfd.FileName);
                StatusMessage = $"Saved to {sfd.FileName}.";
                MessageBox.Show("Saved successfully!", "PiPrint", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error saving XPS.";
                MessageBox.Show($"Failed to save XPS: {ex.Message}", "XPS Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
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
