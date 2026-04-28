using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using CogntApp.ViewModels.Navigation;
using CogntApp.Views;

namespace CogntApp;

public partial class MainWindow
{
    private const string NoteTreeDragDataFormat = "CogntApp.NoteTree.ItemId";
    private const double RightViewerDefaultWidth = 420;
    private const double RightViewerMinWidth = 300;

    private GridLength lastRightViewerWidth = new(RightViewerDefaultWidth);
    private PdfReaderView? rightPanePdfView;
    private BrowserPaneView? rightPaneBrowserView;
}
