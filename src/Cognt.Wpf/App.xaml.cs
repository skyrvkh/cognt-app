using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Cognt.Composition;
using CogntApp.DependencyInjection;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;

namespace CogntApp;

public partial class App : Application
{
    private IServiceProvider? serviceProvider;
    private bool isShowingExceptionDialog;

    protected override void OnStartup(StartupEventArgs e)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        RegisterExceptionHandlers();
        LiveCharts.Configure(settings => settings.UseDefaults());
        serviceProvider = CreateServiceProvider();
        base.OnStartup(e);

        try
        {
            MainWindow = serviceProvider.GetRequiredService<MainWindow>();
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

    protected override void OnExit(ExitEventArgs e)
    {
        UnregisterExceptionHandlers();

        if (serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }

    private static IServiceProvider CreateServiceProvider()
    {
        ServiceCollection services = new();
        services
            .AddCogntModules()
            .AddCogntWpfShell();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void UnregisterExceptionHandlers()
    {
        DispatcherUnhandledException -= App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
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
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(exception);

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
        }
    }

    private void ShowExceptionOnUiThread(string title, string message, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(exception);

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
