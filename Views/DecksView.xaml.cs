using System.Windows.Controls;

namespace CogntApp.Views;

public partial class DecksView : UserControl
{
    public event EventHandler? PracticeRequested;

    public DecksView()
    {
        InitializeComponent();
    }

    private void PracticeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        PracticeRequested?.Invoke(this, EventArgs.Empty);
    }
}
