using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CogntApp.Views;

public partial class PdfReaderToolbar : UserControl
{
    public PdfReaderToolbar() => InitializeComponent();

    public event RoutedEventHandler? PreviousPageRequested;
    public event RoutedEventHandler? NextPageRequested;
    public event RoutedEventHandler? ZoomOutRequested;
    public event RoutedEventHandler? ZoomInRequested;
    public event RoutedEventHandler? FindRequested;
    public event RoutedEventHandler? PreviousSearchResultRequested;
    public event RoutedEventHandler? NextSearchResultRequested;
    public event RoutedEventHandler? CopySelectionRequested;
    public event RoutedEventHandler? HighlightSelectionRequested;
    public event RoutedEventHandler? AddCommentRequested;
    public event RoutedEventHandler? RemoveLatestAnnotationRequested;
    public event RoutedEventHandler? ExportAnnotationsRequested;
    public event KeyEventHandler? SearchKeyDown;

    public TextBlock PdfTitleTextControl => PdfTitleText;
    public TextBlock PdfStatusTextControl => PdfStatusText;
    public TextBlock PageTextControl => PageText;
    public TextBlock ZoomTextControl => ZoomText;
    public TextBlock SearchStatusTextControl => SearchStatusText;
    public TextBlock AnnotationCountTextControl => AnnotationCountText;
    public Button PreviousPageButtonControl => PreviousPageButton;
    public Button NextPageButtonControl => NextPageButton;
    public Button ZoomOutButtonControl => ZoomOutButton;
    public Button ZoomInButtonControl => ZoomInButton;
    public TextBox SearchTextBoxControl => SearchTextBox;
    public Button CopySelectionButtonControl => CopySelectionButton;
    public Button HighlightSelectionButtonControl => HighlightSelectionButton;
    public TextBox CommentTextBoxControl => CommentTextBox;
    public Button AddCommentButtonControl => AddCommentButton;
    public Button RemoveLatestAnnotationButtonControl => RemoveLatestAnnotationButton;
    public Button ExportAnnotationsButtonControl => ExportAnnotationsButton;

    private void PreviousPageButton_Click(object sender, RoutedEventArgs e) => PreviousPageRequested?.Invoke(sender, e);
    private void NextPageButton_Click(object sender, RoutedEventArgs e) => NextPageRequested?.Invoke(sender, e);
    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => ZoomOutRequested?.Invoke(sender, e);
    private void ZoomInButton_Click(object sender, RoutedEventArgs e) => ZoomInRequested?.Invoke(sender, e);
    private void FindButton_Click(object sender, RoutedEventArgs e) => FindRequested?.Invoke(sender, e);
    private void PreviousSearchResultButton_Click(object sender, RoutedEventArgs e) => PreviousSearchResultRequested?.Invoke(sender, e);
    private void NextSearchResultButton_Click(object sender, RoutedEventArgs e) => NextSearchResultRequested?.Invoke(sender, e);
    private void CopySelectionButton_Click(object sender, RoutedEventArgs e) => CopySelectionRequested?.Invoke(sender, e);
    private void HighlightSelectionButton_Click(object sender, RoutedEventArgs e) => HighlightSelectionRequested?.Invoke(sender, e);
    private void AddCommentButton_Click(object sender, RoutedEventArgs e) => AddCommentRequested?.Invoke(sender, e);
    private void RemoveLatestAnnotationButton_Click(object sender, RoutedEventArgs e) => RemoveLatestAnnotationRequested?.Invoke(sender, e);
    private void ExportAnnotationsButton_Click(object sender, RoutedEventArgs e) => ExportAnnotationsRequested?.Invoke(sender, e);
    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e) => SearchKeyDown?.Invoke(sender, e);
}
