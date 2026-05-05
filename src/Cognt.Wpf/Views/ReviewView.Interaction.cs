using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace CogntApp.Views;

public partial class ReviewView
{
    private void ReviewView_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void ReviewView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
        if (currentMultimodalContent?.ImageOcclusion is not null)
        {
            RenderMaskCanvas(currentMultimodalContent.ImageOcclusion);
        }
    }

    private void ExitReview_Click(object sender, RoutedEventArgs e)
    {
        ExitReviewRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReviewView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        if (e.Key is Key.Space or Key.Enter)
        {
            if (CanRevealAnswer)
            {
                RevealAnswerCommand.Execute(null);
            }

            e.Handled = true;
            return;
        }

        if (!CanRateCurrentCard)
        {
            return;
        }

        ICommand? command = e.Key switch
        {
            Key.D1 or Key.NumPad1 => RateAgainCommand,
            Key.D2 or Key.NumPad2 => RateHardCommand,
            Key.D3 or Key.NumPad3 => RateGoodCommand,
            Key.D4 or Key.NumPad4 => RateEasyCommand,
            _ => null
        };

        if (command is not null)
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private static bool IsTextInputFocused()
    {
        if (Keyboard.FocusedElement is not DependencyObject focused)
        {
            return false;
        }

        for (DependencyObject? current = focused; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is TextBoxBase or PasswordBox)
            {
                return true;
            }

            if (current is ComboBox { IsEditable: true })
            {
                return true;
            }
        }

        return false;
    }
}
