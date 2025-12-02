
using System.Windows;
using MainWindow = AnimeBingeDownloader.Views.MainWindow;

namespace AnimeBingeDownloader;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
            
        // Create and show the main window
        var mainApp = new MainWindow();
        mainApp.Show();
    }
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        MessageBox.Show(
            $"An unhandled exception occurred: {exception?.Message}\n\nThe application will now close.",
            "Critical Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An error occurred: {e.Exception.Message}",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
            
        e.Handled = true;
    }
}