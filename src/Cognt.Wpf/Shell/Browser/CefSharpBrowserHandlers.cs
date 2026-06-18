using System.Collections.Concurrent;
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

internal enum BrowserContextMenuCommand
{
    ExtractPage,
    RevealLocalPdf,
    ReloadLocalPdf,
}

internal sealed class BrowserContextMenuHandler(
    Func<string?> pageAddressProvider,
    Action<BrowserContextMenuCommand> executeCommand) : CefSharp.Wpf.Handler.ContextMenuHandler
{
    private const int CogntMenuCommandId = 26500;
    private const int ExtractPageCommandId = CogntMenuCommandId + 1;
    private const int RevealLocalPdfCommandId = CogntMenuCommandId + 2;
    private const int ReloadLocalPdfCommandId = CogntMenuCommandId + 3;

    protected override void OnBeforeContextMenu(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        IContextMenuParams parameters,
        IMenuModel model)
    {
        base.OnBeforeContextMenu(chromiumWebBrowser, browser, frame, parameters, model);

        string? pageAddress = pageAddressProvider();
        if (IsWebUri(pageAddress))
        {
            IMenuModel cogntMenu = AddCogntMenu(model);
            cogntMenu.AddItem((CefMenuCommand)ExtractPageCommandId, "Extract page as note");
        }
        else if (IsLocalPdfUri(pageAddress))
        {
            IMenuModel cogntMenu = AddCogntMenu(model);
            cogntMenu.AddItem((CefMenuCommand)RevealLocalPdfCommandId, "Reveal PDF in Explorer");
            cogntMenu.AddItem((CefMenuCommand)ReloadLocalPdfCommandId, "Reload PDF from disk");
        }
    }

    protected override bool OnContextMenuCommand(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        IContextMenuParams parameters,
        CefMenuCommand commandId,
        CefEventFlags eventFlags)
    {
        BrowserContextMenuCommand? command = commandId switch
        {
            (CefMenuCommand)ExtractPageCommandId => BrowserContextMenuCommand.ExtractPage,
            (CefMenuCommand)RevealLocalPdfCommandId => BrowserContextMenuCommand.RevealLocalPdf,
            (CefMenuCommand)ReloadLocalPdfCommandId => BrowserContextMenuCommand.ReloadLocalPdf,
            _ => null,
        };

        if (command is null)
        {
            return base.OnContextMenuCommand(chromiumWebBrowser, browser, frame, parameters, commandId, eventFlags);
        }

        executeCommand(command.Value);
        return true;
    }

    private static IMenuModel AddCogntMenu(IMenuModel model)
    {
        if (model.Count > 0)
        {
            model.AddSeparator();
        }

        return model.AddSubMenu((CefMenuCommand)CogntMenuCommandId, "Cognt");
    }

    private static bool IsWebUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "http" or "https";
    }

    private static bool IsLocalPdfUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && uri.IsFile
            && string.Equals(Path.GetExtension(uri.LocalPath), ".pdf", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class CollectionDownloadHandler(
    CollectionBrowserSandbox sandbox,
    Func<string?> pageAddressProvider,
    Func<PdfAnnotationSaveRequest, Task<bool>> confirmAnnotatedPdfOverwriteAsync,
    Func<PdfAnnotationSaveRequest, Task> completeAnnotatedPdfSaveAsync,
    Action<string> downloadStarted,
    Action<string> downloadCancelled) : DownloadHandler
{
    private readonly ConcurrentDictionary<long, PdfAnnotationSaveRequest> pendingAnnotationDownloads = new();

    protected override bool OnBeforeDownload(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        DownloadItem downloadItem,
        IBeforeDownloadCallback callback)
    {
        if (PdfAnnotationSaveCoordinator.TryCreateRequest(
                pageAddressProvider(),
                downloadItem.Id,
                sandbox.CollectionPath,
                sandbox.DownloadsPath,
                out PdfAnnotationSaveRequest? annotationRequest)
            && annotationRequest is not null)
        {
            pendingAnnotationDownloads[downloadItem.Id] = annotationRequest;
            _ = ConfirmAndContinueAnnotatedPdfSaveAsync(annotationRequest, callback);
            return true;
        }

        string path = CollectionBrowserEnvironmentProvider.CreateDownloadPath(
            sandbox,
            downloadItem.SuggestedFileName,
            downloadItem.Url);
        callback.Continue(path, showDialog: false);
        downloadStarted(Path.GetFileName(path));
        return true;
    }

    protected override void OnDownloadUpdated(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        DownloadItem downloadItem,
        IDownloadItemCallback callback)
    {
        if (!pendingAnnotationDownloads.TryGetValue(downloadItem.Id, out PdfAnnotationSaveRequest? request))
        {
            return;
        }

        if (downloadItem.IsComplete && pendingAnnotationDownloads.TryRemove(downloadItem.Id, out PdfAnnotationSaveRequest? completedRequest))
        {
            _ = completeAnnotatedPdfSaveAsync(completedRequest);
        }
        else if ((downloadItem.IsCancelled || downloadItem.IsInterrupted)
                 && pendingAnnotationDownloads.TryRemove(downloadItem.Id, out PdfAnnotationSaveRequest? cancelledRequest))
        {
            PdfAnnotationSaveCoordinator.Cancel(cancelledRequest);
            downloadCancelled(Path.GetFileName(cancelledRequest.SourcePath));
        }
    }

    private async Task ConfirmAndContinueAnnotatedPdfSaveAsync(
        PdfAnnotationSaveRequest request,
        IBeforeDownloadCallback callback)
    {
        try
        {
            if (!await confirmAnnotatedPdfOverwriteAsync(request).ConfigureAwait(false))
            {
                pendingAnnotationDownloads.TryRemove(request.DownloadId, out _);
                PdfAnnotationSaveCoordinator.Cancel(request);
                downloadCancelled(Path.GetFileName(request.SourcePath));
                return;
            }

            if (callback.IsDisposed)
            {
                pendingAnnotationDownloads.TryRemove(request.DownloadId, out _);
                PdfAnnotationSaveCoordinator.Cancel(request);
                return;
            }

            callback.Continue(request.TemporaryPath, showDialog: false);
            downloadStarted($"annotations for {Path.GetFileName(request.SourcePath)}");
        }
        catch
        {
            pendingAnnotationDownloads.TryRemove(request.DownloadId, out _);
            PdfAnnotationSaveCoordinator.Cancel(request);
            downloadCancelled(Path.GetFileName(request.SourcePath));
        }
        finally
        {
            callback.Dispose();
        }
    }
}
