using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.Review;
using CogntApp.Shell.Navigation;
using CogntApp.Shell.Overlay;
using CogntApp.Shell.Editor;
using CogntApp.Shell.Review;
using CogntApp.ViewModels;
using CogntApp.ViewModels.Navigation;
using Microsoft.Extensions.Logging;

namespace CogntApp;

public partial class MainWindow
{
    private ShellCommandRouting CreateShellCommandRouting(
        IFlashcardReviewQueueQuery reviewQueueQuery,
        IRecordFlashcardReviewCommand recordFlashcardReviewCommand,
        ICramSessionEngine cramSessionEngine)
    {
        OverlayHostController createdOverlayHost = new(
            OverlayHost,
            EditorViewControl);
        ReviewOverlayCoordinator createdReviewOverlay = new(
            createdOverlayHost,
            shellDialogs,
            reviewQueueQuery,
            recordFlashcardReviewCommand,
            cramSessionEngine,
            () => viewModel.OpenCollectionPath,
            loggerFactory.CreateLogger<ReviewOverlayCoordinator>(),
            loggerFactory);

        ShellCommandRouting routing = new(
            createdOverlayHost,
            new OverlayNavigationCoordinator(
                shellDialogs,
                viewModel,
                allNotesViewModel,
                graphViewModel,
                dashboardView,
                allNotesView,
                graphView,
                decksView,
                settingsView,
                createdOverlayHost,
                EditorViewControl,
                createdReviewOverlay,
                loggerFactory.CreateLogger<OverlayNavigationCoordinator>(),
                loggerFactory),
            createdReviewOverlay,
            new StartupWorkflowController(
                viewModel,
                shellDialogs,
                loggerFactory.CreateLogger<StartupWorkflowController>()),
            new BreadcrumbCommandController(
                viewModel,
                shellDialogs,
                loggerFactory.CreateLogger<BreadcrumbCommandController>()),
            new NoteTreeInteractionController(
                NotesTree,
                viewModel,
                shellDialogs,
                loggerFactory.CreateLogger<NoteTreeInteractionController>()),
            new NoteTreeCommandController(
                viewModel,
                shellDialogs,
                loggerFactory.CreateLogger<NoteTreeCommandController>()));
        return routing;
    }

    private void CollectionSelectorButton_Click(object sender, RoutedEventArgs e)
    {
        collectionWorkflow.OpenSelector(CollectionSelectorPopup);
    }

    private void CollectionOptionButton_Click(object sender, RoutedEventArgs e)
    {
        CollectionOptionViewModel? collection = (sender as FrameworkElement)?.DataContext as CollectionOptionViewModel;
        collectionWorkflow.SelectCollection(
            collection,
            CollectionSelectorPopup);
    }

    private void AddCollectionButton_Click(object sender, RoutedEventArgs e)
    {
        collectionWorkflow.CreateCollection(CollectionSelectorPopup);
    }

    private void OpenExistingCollectionButton_Click(object sender, RoutedEventArgs e)
    {
        collectionWorkflow.OpenExistingCollection(CollectionSelectorPopup);
    }

    private void RemoveCollectionFromListButton_Click(object sender, RoutedEventArgs e)
    {
        CollectionOptionViewModel? collection = (sender as FrameworkElement)?.DataContext as CollectionOptionViewModel;
        collectionWorkflow.RemoveCollectionFromList(
            collection,
            CollectionSelectorPopup);
        e.Handled = true;
    }

    private void DeleteCollectionButton_Click(object sender, RoutedEventArgs e)
    {
        CollectionOptionViewModel? collection = (sender as FrameworkElement)?.DataContext as CollectionOptionViewModel;
        collectionWorkflow.DeleteCollectionFromDisk(
            collection,
            CollectionSelectorPopup);
        e.Handled = true;
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        attachmentWorkflow.ShowDocumentImport(this);
    }

    private async void ImportAttachmentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string importKind = (sender as FrameworkElement)?.Tag as string ?? "Document";
        await attachmentWorkflow.ImportAttachmentAsync(this, importKind);
    }

    private void SidebarSearchTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        viewModel.ShowGlobalSearchPopup();
    }

    private void SidebarSearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        viewModel.DismissGlobalSearchPopup();
        e.Handled = true;
    }

    private void EditorViewHost_DragOver(object sender, DragEventArgs e)
    {
        attachmentWorkflow.HandleDragOver(e);
    }

    private async void EditorViewHost_Drop(object sender, DragEventArgs e)
    {
        await attachmentWorkflow.HandleDropAsync(e);
    }

    private sealed record ShellCommandRouting(
        OverlayHostController OverlayHost,
        OverlayNavigationCoordinator OverlayNavigation,
        ReviewOverlayCoordinator ReviewOverlay,
        StartupWorkflowController StartupWorkflow,
        BreadcrumbCommandController BreadcrumbCommands,
        NoteTreeInteractionController NoteTreeInteractions,
        NoteTreeCommandController NoteTreeCommands);
}
