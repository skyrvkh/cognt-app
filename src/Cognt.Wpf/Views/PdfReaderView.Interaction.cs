using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CogntApp.Views;

public partial class PdfReaderView
{
    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        FindText();
        e.Handled = true;
    }

    private void FindButton_Click(object sender, RoutedEventArgs e)
    {
        FindText();
    }

    private void PreviousSearchResultButton_Click(object sender, RoutedEventArgs e)
    {
        MoveSearchResult(-1);
    }

    private void NextSearchResultButton_Click(object sender, RoutedEventArgs e)
    {
        MoveSearchResult(1);
    }

    private void FindText()
    {
        searchHits.Clear();
        currentSearchHitIndex = -1;
        string query = NormalizeSearchText(SearchTextBox.Text);
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchStatusText.Text = "Enter text to search.";
            RefreshCurrentPage();
            return;
        }

        foreach (PdfPageStudyData page in pages)
        {
            AddSearchHits(page, query, searchHits);
        }

        if (searchHits.Count == 0)
        {
            SearchStatusText.Text = "No matches.";
            RefreshCurrentPage();
            return;
        }

        currentSearchHitIndex = 0;
        SearchStatusText.Text = $"1 of {searchHits.Count}";
        _ = ShowPageAsync(searchHits[0].PageNumber);
    }

    private void MoveSearchResult(int direction)
    {
        if (searchHits.Count == 0)
        {
            FindText();
            return;
        }

        currentSearchHitIndex = (currentSearchHitIndex + direction + searchHits.Count) % searchHits.Count;
        PdfTextSearchHit hit = searchHits[currentSearchHitIndex];
        SearchStatusText.Text = $"{currentSearchHitIndex + 1} of {searchHits.Count}";
        _ = ShowPageAsync(hit.PageNumber);
    }

    private static void AddSearchHits(
        PdfPageStudyData page,
        string normalizedQuery,
        ICollection<PdfTextSearchHit> destination)
    {
        string[] queryTerms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (queryTerms.Length == 0 || page.Words.Count == 0)
        {
            return;
        }

        for (int start = 0; start <= page.Words.Count - queryTerms.Length; start++)
