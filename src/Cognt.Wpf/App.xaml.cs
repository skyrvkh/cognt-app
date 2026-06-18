using System.IO;
using System.Windows;
using System.Windows.Threading;
using Cognt.Composition;
using CogntApp.Diagnostics;
using CogntApp.DependencyInjection;
using CogntApp.Shell.Browser;
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
        diagnosticLogFile = new AppDiagnosticLogFile(AppDiagnosticPaths.CreateSessionLogFilePath());
        CrashSafeLog.StartSession(diagnosticLogFile.Path);

        try
        {
            RegisterExceptionHandlers();
            CefSharpRuntime.Initialize();
            LiveCharts.Configure(settings => settings.UseDefaults());
            serviceProvider = CreateServiceProvider(diagnosticLogFile.Path);
            logger = serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Application startup began. LogFile={LogFile}", diagnosticLogFile.Path);
            base.OnStartup(e);

            MainWindow = serviceProvider.GetRequiredService<MainWindow>();
            MainWindow.Show();
            logger.LogInformation("Application startup completed.");
        }
        catch (Exception ex)
        {
            CrashSafeLog.RecordFatal("Application startup failed.", ex, diagnosticLogFile.Path);
            logger?.LogCritical(ex, "Application startup failed.");
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

        try
        {
            UnregisterExceptionHandlers();

            if (serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            CefSharpRuntime.Shutdown();
        }
        catch (Exception ex)
        {
            CrashSafeLog.RecordFatal("Application exit cleanup failed.", ex, diagnosticLogFile?.Path);
            logger?.LogError(ex, "Application exit cleanup failed.");
        }
        finally
        {
            CrashSafeLog.MarkCleanExit(e.ApplicationExitCode);
        }

        base.OnExit(e);
    }

    private static IServiceProvider CreateServiceProvider(string logFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        ServiceCollection services = new();
        services.AddSingleton(new AppDiagnosticLogFile(logFilePath));
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("Microsoft", LogLevel.Warning);
            builder.AddFilter("System", LogLevel.Warning);
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
        CrashSafeLog.RecordFatal("Unhandled dispatcher exception.", e.Exception, diagnosticLogFile?.Path);
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
            CrashSafeLog.RecordFatal("Unhandled non-Exception domain failure.", e.ExceptionObject, diagnosticLogFile?.Path);
            logger?.LogCritical("Unhandled non-Exception domain failure. Object={ExceptionObject}", e.ExceptionObject);
            return;
        }

        CrashSafeLog.RecordFatal("Unhandled application domain exception.", ex, diagnosticLogFile?.Path);
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
        CrashSafeLog.RecordFatal("Unobserved task exception.", e.Exception.GetBaseException(), diagnosticLogFile?.Path);
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
        catch (Exception ex)
        {
            CrashSafeLog.RecordFatal("Failed to show exception dialog.", ex, diagnosticLogFile?.Path);
            logger?.LogError(ex, "Failed to show exception dialog. Title={Title}", title);
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
