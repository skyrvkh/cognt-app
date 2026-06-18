using System.IO;
using CefSharp;
using CefSharp.Handler;

namespace CogntApp.Shell.Browser;

internal sealed class BrowserPopupHandler(Action<string> openInNewTab) : LifeSpanHandler
{
    protected override bool OnBeforePopup(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        string targetUrl,
        string targetFrameName,
        WindowOpenDisposition targetDisposition,
        bool userGesture,
        IPopupFeatures popupFeatures,
        IWindowInfo windowInfo,
        IBrowserSettings browserSettings,
        ref bool noJavascriptAccess,
        out IWebBrowser? newBrowser)
    {
        newBrowser = null;
        if (Uri.TryCreate(targetUrl, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme is "http" or "https"
                || uri.IsFile && string.Equals(Path.GetExtension(uri.LocalPath), ".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            openInNewTab(targetUrl);
            return true;
        }

        return false;
    }
}

internal sealed class CollectionDownloadHandler(
    CollectionBrowserSandbox sandbox,
    Action<string> downloadStarted) : DownloadHandler
{
    protected override bool OnBeforeDownload(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        DownloadItem downloadItem,
        IBeforeDownloadCallback callback)
    {
        string path = CollectionBrowserEnvironmentProvider.CreateDownloadPath(
            sandbox,
            downloadItem.SuggestedFileName,
            downloadItem.Url);
        callback.Continue(path, showDialog: false);
        downloadStarted(Path.GetFileName(path));
        return true;
    }
}
