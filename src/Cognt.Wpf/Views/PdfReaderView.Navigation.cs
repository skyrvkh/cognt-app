using System.Windows;

namespace CogntApp.Views;

public partial class PdfReaderView
{
    private PdfPageStudyData? GetCurrentPageData()
    {
        int index = currentPageNumber - 1;
        return index >= 0 && index < pages.Count ? pages[index] : null;
    }

    private void TrimRenderedPageCache(int currentPage)
    {
        const int maximumCachedPages = 5;
        if (renderedPages.Count <= maximumCachedPages)
        {
            return;
        }

        int[] pagesToRemove = renderedPages.Keys
            .OrderBy(page => Math.Abs(page - currentPage))
            .Skip(maximumCachedPages)
            .ToArray();
        foreach (int page in pagesToRemove)
        {
            renderedPages.Remove(page);
        }
    }

    private void PreviousPageButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ShowPageAsync(currentPageNumber - 1);
    }

    private void NextPageButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ShowPageAsync(currentPageNumber + 1);
    }
}
