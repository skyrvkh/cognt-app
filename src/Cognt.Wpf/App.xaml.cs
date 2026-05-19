using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Cognt.Composition;
using CogntApp.Diagnostics;
using CogntApp.DependencyInjection;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CogntApp;

public partial class App : Application
{
    private IServiceProvider? serviceProvider;
    private ILogger<App>? logger;
    private AppDiagnosticLogFile? diagnosticLogFile;
    private bool isShowingExceptionDialog;

    protected override void OnStartup(StartupEventArgs e)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        RegisterExceptionHandlers();
        LiveCharts.Configure(settings => settings.UseDefaults());
        serviceProvider = CreateServiceProvider();
        logger = serviceProvider.GetRequiredService<ILogger<App>>();
        diagnosticLogFile = serviceProvider.GetRequiredService<AppDiagnosticLogFile>();
        logger.LogInformation("Application startup began. LogFile={LogFile}", diagnosticLogFile.Path);
        base.OnStartup(e);

        try
        {
            MainWindow = serviceProvider.GetRequiredService<MainWindow>();
            MainWindow.Show();
            logger.LogInformation("MainWindow shown.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "MainWindow startup failed.");
            ShowException(
                "Startup Failed",
                "The app could not start.",
                ex);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        logger?.LogInformation("Application exiting. ExitCode={ExitCode}", e.ApplicationExitCode);
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
        string logFilePath = AppDiagnosticPaths.CreateSessionLogFilePath();
        services.AddSingleton(new AppDiagnosticLogFile(logFilePath));
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddCogntFile(logFilePath);
        });

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
        logger?.LogError(e.Exception, "Unhandled dispatcher exception.");
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
            logger?.LogCritical("Unhandled non-Exception domain failure. Object={ExceptionObject}", e.ExceptionObject);
            return;
        }

        logger?.LogCritical(ex, "Unhandled application domain exception.");
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
        logger?.LogError(e.Exception.GetBaseException(), "Unobserved task exception.");
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
        logger?.LogError(exception, "{Title}: {Message}", title, message);

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
            if (diagnosticLogFile is not null)
            {
                text = $"{text}{Environment.NewLine}{Environment.NewLine}Log: {diagnosticLogFile.Path}";
            }

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
