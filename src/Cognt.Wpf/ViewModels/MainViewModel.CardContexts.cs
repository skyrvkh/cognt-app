using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
using Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;
using CogntApp.Features.NoteToCard;
using CogntApp.Features.NoteToCard.ImageOcclusion;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    internal void SaveClozeDraft(
        string sourcePath,
        string blockId,
        string selectedPlainText,
        string clozeText,
        string content,
        string? cardContextId = null,
        string? slotId = null,
        int? displayClozeIndex = null)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || string.IsNullOrWhiteSpace(sourcePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The cloze source is not the current note file.");
        }

        string relativePath = GetRequiredRelativeNotePath(sourcePath);
        noteService.SaveNote(openCollectionPath, relativePath, content);
        lastSavedContent = content;

        CardContextDraftNoteDocument document = CardContextAuthoring.EnsureDefaultContext(
            ReadCardContextDraftDocument(relativePath));
        CardContext context = ResolveRequestedDraftContext(document, cardContextId);
        if (string.IsNullOrWhiteSpace(slotId))
        {
            int nextDisplayClozeIndex = displayClozeIndex
                ?? CardContextAuthoring.ParseDisplayClozeIndexFromClozeText(clozeText);
            context = CardContextAuthoring.AddTextClozeToNewSlot(
                context,
                blockId,
                selectedPlainText,
                nextDisplayClozeIndex);
        }
        else
        {
            CardSlot existingSlot = ResolveRequiredCardSlot(context.Slots, slotId);

            int visibleDisplayIndex = displayClozeIndex
                ?? CardContextAuthoring.ParseDisplayClozeIndexFromClozeText(clozeText);
            if (visibleDisplayIndex != existingSlot.DisplayClozeIndex)
            {
                throw new InvalidOperationException("The selected text was marked with a different cloze index than the selected card slot.");
            }

            context = CardContextAuthoring.AddTextClozeToExistingSlot(
                context,
                existingSlot.Id,
                blockId,
                selectedPlainText);
        }

        PersistCardContextDraftDocument(relativePath, content, ReplaceDraftContext(document, context));
        ReloadCurrentNoteToCardQueue(relativePath, content);
    }

    internal OpenImageMaskEditorRequest CreateImageMaskEditorRequest(
        string imageBlockId,
        string imageRelPath,
        string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageBlockId);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageRelPath);

        string relativePath = GetRequiredCurrentNoteRelativePath();
        SaveNoteIfChanged(content);

        string normalizedImageRelPath = NormalizeCollectionImagePath(imageRelPath);
        normalizedImageRelPath = noteService.ResolveImageAttachmentRelativePath(
                openCollectionPath,
                relativePath,
                normalizedImageRelPath)
            ?? normalizedImageRelPath;
        string imageFullPath = ResolveCollectionFilePath(openCollectionPath, normalizedImageRelPath);
        if (!File.Exists(imageFullPath))
        {
            throw new FileNotFoundException("The selected image file could not be found in the collection.", imageFullPath);
        }

        CardContextDraftNoteDocument document = CardContextAuthoring.EnsureDefaultContext(
            ReadCardContextDraftDocument(relativePath));
        CardContext context = document.Contexts[0];
        if (!context.ContextBlockIds.Contains(imageBlockId, StringComparer.Ordinal))
        {
            context = context with
            {
                ContextBlockIds = AddContextBlockId(context.ContextBlockIds, imageBlockId.Trim()),
            };
        }

        OpenImageMaskEditorRequest request = new(
            openCollectionPath,
            relativePath,
            imageBlockId.Trim(),
            normalizedImageRelPath,
            imageFullPath,
            context,
            context.Id);
        return request;
    }

    private static IReadOnlyList<string> AddContextBlockId(
        IReadOnlyList<string> contextBlockIds,
        string blockId)
    {
        var uniqueBlockIds = new List<string>(contextBlockIds.Count + 1);
        var seenBlockIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (string contextBlockId in contextBlockIds)
        {
            if (seenBlockIds.Add(contextBlockId))
            {
                uniqueBlockIds.Add(contextBlockId);
            }
        }

        if (seenBlockIds.Add(blockId))
        {
            uniqueBlockIds.Add(blockId);
        }

        return uniqueBlockIds;
    }

    internal void SaveImageMaskEditorResult(ImageMaskEditorResult result, string noteContent)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Saved)
        {
            return;
        }

        string relativePath = GetRequiredCurrentNoteRelativePath();
        if (!string.Equals(result.SourceRelPath, relativePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The image mask editor result does not belong to the current note.");
        }

        SaveNoteIfChanged(noteContent);
        CardContextDraftNoteDocument document = CardContextAuthoring.EnsureDefaultContext(
            ReadCardContextDraftDocument(relativePath));
        PersistCardContextDraftDocument(relativePath, noteContent, ReplaceDraftContext(document, result.UpdatedContext));
        ReloadCurrentNoteToCardQueue(relativePath, noteContent);
    }

    private void ReloadNoteToCardDeckNames()
    {
        string previousSelection = NoteToCardDeckName;
        NoteToCardDeckNames.Clear();
        noteToCardDeckNameCatalog = [];
        noteToCardDeckNameSet.Clear();

        if (!string.IsNullOrWhiteSpace(openCollectionPath) && Directory.Exists(openCollectionPath))
        {
            NoteToCardDeckCatalog catalog = LoadNoteToCardDeckCatalog();
            noteToCardDeckNameCatalog = catalog.DeckNames;

            foreach (string deckName in catalog.DeckNames)
            {
                NoteToCardDeckNames.Add(deckName);
                noteToCardDeckNameSet.Add(deckName);
            }

            previousSelection = NoteToCardDeckCatalogBuilder.ResolveSelection(
                previousSelection,
                catalog.DeckNames,
                catalog.FirstDeckName);
        }

        isUpdatingNoteToCardDeckCatalog = true;
        try
        {
            NoteToCardDeckName = NoteToCardDeckCatalogBuilder.ResolveSelection(
                previousSelection,
                noteToCardDeckNameCatalog,
                firstDeckName: null);
        }
        finally
        {
            isUpdatingNoteToCardDeckCatalog = false;
        }

        RefreshNoteToCardDeckSearchResults();
    }

    private NoteToCardDeckCatalog LoadNoteToCardDeckCatalog()
    {
        return NoteToCardDeckCatalogBuilder.Create(flashcardDeckSummariesQuery.Execute(
            new FlashcardCollectionQueryRequest(openCollectionPath)));
    }

    private void RefreshNoteToCardDeckSearchResults()
    {
        FilteredNoteToCardDeckNames.Clear();

        foreach (string deckName in NoteToCardDeckCatalogBuilder.FilterSearchResults(
                     noteToCardDeckNameCatalog,
                     noteToCardDeckNameSet,
                     NoteToCardDeckName,
                     limit: 6))
        {
            FilteredNoteToCardDeckNames.Add(deckName);
        }

        OnPropertyChanged(nameof(HasFilteredNoteToCardDeckNames));
    }

    private void SelectNoteToCardDeck(object? parameter)
    {
        if (parameter is not string deckName || string.IsNullOrWhiteSpace(deckName))
        {
            return;
        }

        NoteToCardDeckName = deckName;
    }

    private void CommitPendingNoteToCardTags()
    {
        string[] addedTags = AppendNoteToCardTags(NoteToCardPendingTagText.Split(
            ',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        NoteToCardPendingTagText = string.Empty;

        if (addedTags.Length > 0)
        {
            PersistCurrentNoteToCardTagAdditions(addedTags);
        }
    }

    private void RemoveNoteToCardTag(object? parameter)
    {
        if (parameter is not string tag || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        string? existingTag = FindNoteToCardTag(tag);
        if (existingTag is not null)
        {
            NoteToCardTags.Remove(existingTag);
            PersistCurrentNoteToCardTagRemovals([existingTag]);
        }
    }

    private string[] AppendNoteToCardTags(IEnumerable<string> tags)
    {
        string[] addedTags = NoteToCardTagSetBuilder.CreateAdditions(NoteToCardTags, tags);

        foreach (string tag in addedTags)
        {
            NoteToCardTags.Add(tag);
        }

        return addedTags;
    }

    private void ClearNoteToCardTags()
    {
        NoteToCardTags.Clear();
        NoteToCardPendingTagText = string.Empty;
    }

    private void LoadNoteToCardTagsForCurrentNote()
    {
        ClearNoteToCardTags();
        if (string.IsNullOrWhiteSpace(openCollectionPath) || currentNoteItemId is not > 0)
        {
            return;
        }

        AppendNoteToCardTags(noteService.LoadAssignedTags(openCollectionPath, [currentNoteItemId.Value]));
    }

    private void PersistCurrentNoteToCardTagAdditions(IReadOnlyList<string> tags)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath) || currentNoteItemId is not > 0 || tags.Count == 0)
        {
            return;
        }

        noteService.AssignTags(openCollectionPath, [currentNoteItemId.Value], tags, includeDescendants: false);
        RefreshNotesTree(preserveExpansion: true);
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PersistCurrentNoteToCardTagRemovals(IReadOnlyList<string> tags)
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath) || currentNoteItemId is not > 0 || tags.Count == 0)
        {
            return;
        }

        noteService.RemoveTags(openCollectionPath, [currentNoteItemId.Value], tags, includeDescendants: false);
        RefreshNotesTree(preserveExpansion: true);
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static CardSlot? FindCardSlotById(IReadOnlyList<CardSlot> slots, string slotId)
    {
        foreach (CardSlot slot in slots)
        {
            if (string.Equals(slot.Id, slotId, StringComparison.Ordinal))
            {
                return slot;
            }
        }

        return null;
    }

    private static CardSlot ResolveRequiredCardSlot(IReadOnlyList<CardSlot> slots, string slotId)
    {
        return FindCardSlotById(slots, slotId)
            ?? throw new InvalidOperationException("The selected card slot no longer exists.");
    }

    private string? FindNoteToCardTag(string tag)
    {
        foreach (string candidate in NoteToCardTags)
        {
            if (string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

}
