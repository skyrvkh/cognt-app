using System.IO;
using CefSharp;
using CefSharp.Wpf;

namespace CogntApp.Shell.Browser;

internal static class CefSharpRuntime
{
    private static bool initialized;

    internal static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        string rootCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cognt",
            "chromium");
        Directory.CreateDirectory(rootCachePath);

        CefSettings settings = new()
        {
            RootCachePath = rootCachePath,
            CachePath = Path.Combine(rootCachePath, "default"),
            LogSeverity = LogSeverity.Warning,
            WindowlessRenderingEnabled = true,
        };
        settings.CefCommandLineArgs["disable-background-networking"] = "1";
        settings.CefCommandLineArgs["disable-sync"] = "1";
        settings.CefCommandLineArgs["no-default-browser-check"] = "1";
        settings.CefCommandLineArgs["no-first-run"] = "1";

        if (!Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null))
        {
            throw new InvalidOperationException("Chromium could not be initialized.");
        }

        initialized = true;
    }

    internal static void Shutdown()
    {
        if (!initialized)
        {
            return;
        }

        Cef.Shutdown();
        initialized = false;
    }
}
