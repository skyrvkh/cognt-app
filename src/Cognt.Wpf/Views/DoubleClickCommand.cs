using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CogntApp.Views;

internal static class DoubleClickCommand
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(DoubleClickCommand),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(DoubleClickCommand),
            new PropertyMetadata(null));

    public static void SetCommand(DependencyObject element, ICommand? value)
    {
        element.SetValue(CommandProperty, value);
    }

    public static ICommand? GetCommand(DependencyObject element)
    {
        return (ICommand?)element.GetValue(CommandProperty);
    }

    public static void SetCommandParameter(DependencyObject element, object? value)
    {
        element.SetValue(CommandParameterProperty, value);
    }

    public static object? GetCommandParameter(DependencyObject element)
    {
        return element.GetValue(CommandParameterProperty);
    }

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Control control)
        {
            return;
        }

        control.MouseDoubleClick -= Control_MouseDoubleClick;

        if (e.NewValue is ICommand)
        {
            control.MouseDoubleClick += Control_MouseDoubleClick;
        }
    }

    private static void Control_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DependencyObject element)
        {
            return;
        }

        ICommand? command = GetCommand(element);
        object? parameter = GetCommandParameter(element);
        if (command is null || !command.CanExecute(parameter))
        {
            return;
        }

        command.Execute(parameter);
        e.Handled = true;
    }
}
