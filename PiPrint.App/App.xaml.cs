using System.Windows;

namespace PiPrint.App;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            try
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PiPrint");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "startup_error.log"), $"[{DateTime.Now}] Unhandled Exception: {e.Exception}\n\n");
            }
            catch { }
            MessageBox.Show($"An error occurred: {e.Exception.Message}", "PiPrint Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PiPrint");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "startup_error.log"), $"[{DateTime.Now}] Domain Exception: {e.ExceptionObject}\n\n");
            }
            catch { }
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Services.LocalizationService.Instance.Initialize();
        base.OnStartup(e);

        if (e.Args.Length > 0 && System.IO.File.Exists(e.Args[0]))
        {
            string filePath = e.Args[0];
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MainWindow?.DataContext is ViewModels.MainViewModel vm)
                {
                    vm.LoadDocumentFile(filePath, append: false);
                }
            }));
        }
    }
}
