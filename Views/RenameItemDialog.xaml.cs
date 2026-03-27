using System.Windows;

namespace CogntApp.Views;

public partial class RenameItemDialog : Window
{
    public RenameItemDialog(string itemKind, string currentName)
    {
        InitializeComponent();
        Title = $"Rename {itemKind}";
        PromptTextBlock.Text = $"New {itemKind} name";
        NameTextBox.Text = currentName;
    }

    public string ItemName => NameTextBox.Text;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ItemName))
        {
            MessageBox.Show(
                "Name is required.",
                "Rename",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
