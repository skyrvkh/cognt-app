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
        {
            bool matches = true;
            for (int offset = 0; offset < queryTerms.Length; offset++)
            {
                string word = NormalizeSearchText(page.Words[start + offset].Text);
                if (!word.Contains(queryTerms[offset], StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
            {
                continue;
            }

            int[] indexes = Enumerable.Range(start, queryTerms.Length).ToArray();
            destination.Add(new PdfTextSearchHit(page.PageNumber, indexes));
        }
    }

    private static string NormalizeSearchText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private void PageCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas canvas || canvas.Tag is not PdfPageStudyData)
        {
            return;
        }

        selectionStart = e.GetPosition(canvas);
        canvas.CaptureMouse();
        RemoveDragSelectionOverlay(canvas);
        e.Handled = true;
    }

    private void PageCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (selectionStart is null || sender is not Canvas canvas || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point end = e.GetPosition(canvas);
        Rect bounds = CreateSelectionBounds(selectionStart.Value, end);
        ShowDragSelectionOverlay(canvas, bounds);
    }

    private void PageCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (selectionStart is null || sender is not Canvas canvas || canvas.Tag is not PdfPageStudyData page)
        {
            return;
        }

        Point end = e.GetPosition(canvas);
        Rect selectionBounds = CreateSelectionBounds(selectionStart.Value, end);
        selectionStart = null;
        canvas.ReleaseMouseCapture();
        RemoveDragSelectionOverlay(canvas);

        selectedWordIndexes.Clear();
        if (selectionBounds.Width >= 2d || selectionBounds.Height >= 2d)
        {
            for (int index = 0; index < page.Words.Count; index++)
            {
                Rect wordBounds = GetWordBounds(canvas, page, page.Words[index]);
                if (wordBounds.IntersectsWith(selectionBounds))
                {
                    selectedWordIndexes.Add(index);
                }
            }
        }

        RefreshCurrentPage();
        if (selectedWordIndexes.Count > 0)
        {
            PdfStatusText.Text = $"Selected {selectedWordIndexes.Count} word{(selectedWordIndexes.Count == 1 ? string.Empty : "s")}.";
        }

        e.Handled = true;
    }

    private static Rect CreateSelectionBounds(Point first, Point second)
    {
        return new Rect(
            new Point(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y)),
            new Point(Math.Max(first.X, second.X), Math.Max(first.Y, second.Y)));
    }

    private static Rect GetWordBounds(Canvas canvas, PdfPageStudyData page, PdfTextWord word)
    {
        double left = Math.Clamp(word.Left / page.Width, 0d, 1d) * canvas.Width;
        double top = Math.Clamp((page.Height - word.Top) / page.Height, 0d, 1d) * canvas.Height;
        double width = Math.Max(2d, (word.Right - word.Left) / page.Width * canvas.Width);
        double height = Math.Max(2d, (word.Top - word.Bottom) / page.Height * canvas.Height);
        return new Rect(left, top, width, height);
    }

    private void ShowDragSelectionOverlay(Canvas canvas, Rect bounds)
    {
        RemoveDragSelectionOverlay(canvas);
        dragSelectionOverlay = new Border
        {
            Width = Math.Max(1d, bounds.Width),
            Height = Math.Max(1d, bounds.Height),
            Background = new SolidColorBrush(Color.FromArgb(35, 2, 132, 199)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(210, 2, 132, 199)),
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(dragSelectionOverlay, bounds.Left);
        Canvas.SetTop(dragSelectionOverlay, bounds.Top);
        Panel.SetZIndex(dragSelectionOverlay, 5);
        canvas.Children.Add(dragSelectionOverlay);
    }

    private void RemoveDragSelectionOverlay(Canvas canvas)
    {
        if (dragSelectionOverlay is not null)
        {
            canvas.Children.Remove(dragSelectionOverlay);
            dragSelectionOverlay = null;
        }
    }
}
