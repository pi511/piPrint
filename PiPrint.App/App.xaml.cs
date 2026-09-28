using System.Windows;

namespace PiPrint.App;

public partial class App : System.Windows.Application
{
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
