using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Search;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private string globalSearchQuery = string.Empty;
    private string globalSearchStateMessage = string.Empty;
    private string globalSearchErrorMessage = string.Empty;
    private bool isGlobalSearchLoading;
    private bool hasGlobalSearchError;
    private bool isGlobalSearchPopupOpen;
    private CancellationTokenSource? globalSearchCancellation;

    public string GlobalSearchQuery
    {
        get => globalSearchQuery;
        set
        {
            if (SetProperty(ref globalSearchQuery, value))
            {
                IsGlobalSearchPopupOpen = !string.IsNullOrWhiteSpace(value);
                QueueGlobalSearch(immediate: false);
                OnPropertyChanged(nameof(ShouldShowGlobalSearchPopup));
            }
        }
    }

    public bool IsGlobalSearchLoading
    {
        get => isGlobalSearchLoading;
        private set
        {
            if (SetProperty(ref isGlobalSearchLoading, value))
            {
                NotifyGlobalSearchStateChanged();
            }
        }
    }

    public bool HasGlobalSearchError
    {
        get => hasGlobalSearchError;
        private set
        {
            if (SetProperty(ref hasGlobalSearchError, value))
            {
                NotifyGlobalSearchStateChanged();
            }
        }
    }

    public string GlobalSearchErrorMessage
    {
        get => globalSearchErrorMessage;
        private set => SetProperty(ref globalSearchErrorMessage, value);
    }

    public string GlobalSearchStateMessage
    {
        get => globalSearchStateMessage;
        private set
        {
            if (SetProperty(ref globalSearchStateMessage, value))
            {
                NotifyGlobalSearchStateChanged();
            }
        }
    }

    public bool IsGlobalSearchPopupOpen
    {
        get => isGlobalSearchPopupOpen;
        set
        {
            if (SetProperty(ref isGlobalSearchPopupOpen, value))
            {
                NotifyGlobalSearchStateChanged();
            }
        }
    }

    public bool HasGlobalSearchResults => GlobalSearchResults.Count > 0;

    public bool ShouldShowGlobalSearchResults =>
        !IsGlobalSearchLoading
        && !HasGlobalSearchError
        && HasGlobalSearchResults;

    public bool ShouldShowGlobalSearchState =>
        !IsGlobalSearchLoading
        && !ShouldShowGlobalSearchResults
        && !HasGlobalSearchError
        && !string.IsNullOrWhiteSpace(GlobalSearchStateMessage);

    public bool ShouldShowGlobalSearchPopup =>
        IsGlobalSearchPopupOpen
        && !string.IsNullOrWhiteSpace(GlobalSearchQuery)
        && (IsGlobalSearchLoading
            || HasGlobalSearchError
            || ShouldShowGlobalSearchResults
            || ShouldShowGlobalSearchState);

    public void DismissGlobalSearchPopup()
    {
        IsGlobalSearchPopupOpen = false;
    }

    public void ShowGlobalSearchPopup()
    {
        if (!string.IsNullOrWhiteSpace(GlobalSearchQuery))
        {
            IsGlobalSearchPopupOpen = true;
        }
    }
}
