using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace CogntApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private bool isShowingExceptionDialog;

    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterExceptionHandlers();
        LiveCharts.Configure(settings => settings.UseDefaults());
        base.OnStartup(e);

        try
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            ShowException(
                "Startup Failed",
                "The app could not start.",
                ex);
            Shutdown(1);
        }
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowException(
            "Unexpected Error",
            "The app hit an unexpected UI error.",
            e.Exception);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex)
        {
            return;
        }

        ShowException(
            "Unexpected Error",
            "The app hit an unrecoverable error.",
            ex);
    }

    private void TaskScheduler_UnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        ShowException(
            "Background Error",
            "A background operation failed.",
            e.Exception.GetBaseException());
    }

    private void ShowException(string title, string message, Exception exception)
    {
        Debug.WriteLine(exception);

        try
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    () => ShowExceptionOnUiThread(title, message, exception),
                    DispatcherPriority.Normal);
                return;
            }

            ShowExceptionOnUiThread(title, message, exception);
        }
        catch
        {
            // Do not let the exception reporting path cause another crash.
        }
    }

    private void ShowExceptionOnUiThread(string title, string message, Exception exception)
    {
        if (isShowingExceptionDialog)
        {
            return;
        }

        isShowingExceptionDialog = true;

        try
        {
            string text = $"{message}{Environment.NewLine}{exception.Message}";
            Window? owner = MainWindow?.IsLoaded == true ? MainWindow : null;

            if (owner is null)
            {
                MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(owner, text, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            isShowingExceptionDialog = false;
        }
    }
}
