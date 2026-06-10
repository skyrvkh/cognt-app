using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace CogntApp;

public partial class MainWindow
{
    private int collectionRefreshRequestId;

    private void QueueActiveCollectionViewRefresh()
    {
        int requestId = ++collectionRefreshRequestId;
        _ = Dispatcher.BeginInvoke(
            async () =>
            {
                if (requestId != collectionRefreshRequestId)
                {
                    return;
                }

                try
                {
                    await overlayNavigation.RefreshActiveCollectionViewAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Active collection view refresh failed. RequestId={RequestId}", requestId);
                    shellDialogs.ShowWarning(
                        "Collection Refresh Failed",
                        $"The active view could not be refreshed for the selected collection: {ex.Message}");
                }
            },
            DispatcherPriority.Background);
    }
}
