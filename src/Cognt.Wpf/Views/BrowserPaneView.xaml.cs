using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace CogntApp.Views;

public partial class BrowserPaneView : UserControl
{
    private readonly ILogger<BrowserPaneView>? logger;

    public BrowserPaneView(ILogger<BrowserPaneView>? logger = null)
    {
        this.logger = logger;
        InitializeComponent();
    }

    internal void FocusAddressBox()
    {
        AddressTextBox.Focus();
        AddressTextBox.SelectAll();
    }

    internal async Task NavigateAsync(string address)
    {
        string? target = NormalizeAddress(address);
        if (target is null)
        {
            ShowState("Browser", "Enter a web address or search term.");
            return;
        }

        AddressTextBox.Text = target;
        ShowState("Loading", target);

        try
        {
            await BrowserWebView.EnsureCoreWebView2Async();
            BrowserWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            BrowserWebView.CoreWebView2.Settings.IsStatusBarEnabled = true;
            BrowserWebView.CoreWebView2.Navigate(target);
            BrowserWebView.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Browser navigation failed before dispatch. Target={Target}", target);
            ShowState("Failed to open page", ex.Message);
        }
    }

    private async void GoButton_Click(object sender, RoutedEventArgs e)
    {
        await NavigateAsync(AddressTextBox.Text);
    }

    private async void AddressTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await NavigateAsync(AddressTextBox.Text);
    }

    private void BrowserWebView_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            StatePanel.Visibility = Visibility.Collapsed;
            BrowserWebView.Visibility = Visibility.Visible;
            return;
        }

        logger?.LogWarning("Browser navigation completed with error. Uri={Uri} WebErrorStatus={WebErrorStatus}", BrowserWebView.Source, e.WebErrorStatus);
        ShowState("Failed to open page", $"WebView2 could not open the page. Error: {e.WebErrorStatus}");
    }

    private void ShowState(string title, string message)
    {
        BrowserWebView.Visibility = Visibility.Collapsed;
        StatePanel.Visibility = Visibility.Visible;
        StateTitleText.Text = title;
        StateMessageText.Text = message;
    }

    private static string? NormalizeAddress(string address)
    {
        string value = address.Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? absolute)
            && absolute.Scheme is "http" or "https")
        {
            return absolute.AbsoluteUri;
        }

        if (!value.Contains(' ', StringComparison.Ordinal)
            && value.Contains('.', StringComparison.Ordinal))
        {
            return $"https://{value.TrimStart('/')}";
        }

        return $"https://www.bing.com/search?q={Uri.EscapeDataString(value)}";
    }
}
