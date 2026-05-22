using System.IO;
using Cognt.Modules.SpacedRepetitionSystem.Application.Commands;
using Cognt.Modules.SpacedRepetitionSystem.Application.Queries;
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
            CardSlot existingSlot = context.Slots.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, slotId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("The selected card slot no longer exists.");

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
                ContextBlockIds = context.ContextBlockIds
                    .Concat([imageBlockId.Trim()])
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
            };
        }

        return new OpenImageMaskEditorRequest(
            openCollectionPath,
            relativePath,
            imageBlockId.Trim(),
            normalizedImageRelPath,
            imageFullPath,
            context,
            context.Id);
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

    internal CardContext CreateCardContextFromSelection(
        IReadOnlyList<string> selectedBlockIds,
        string? title,
        string noteContent)
    {
        ArgumentNullException.ThrowIfNull(selectedBlockIds);

        string relativePath = GetRequiredCurrentNoteRelativePath();
        SaveNoteIfChanged(noteContent);

        CardContextDraftNoteDocument document = ReadCardContextDraftDocument(relativePath);
        int order = document.Contexts.Count == 0
            ? 0
            : document.Contexts.Max(context => context.Order) + 1;
        CardContext context = CardContextAuthoring.CreateContextForSelectedBlocks(
            selectedBlockIds,
            title,
            order);

        CardContextDraftNoteDocument updatedDocument = document with
        {
            Contexts = document.Contexts.Concat([context]).ToArray(),
        };

        PersistCardContextDraftDocument(relativePath, noteContent, updatedDocument);
        ReloadCurrentNoteToCardQueue(relativePath, noteContent);
        RaiseMessage(
            "Note Cards",
            $"Created a draft card context from {context.ContextBlockIds.Count} selected block(s).");
        return context;
    }

    private static CardContext FilterContextForGeneration(
        CardContext context,
        IReadOnlySet<string> slotIds)
    {
        CardSlot[] slots = context.Slots
            .Where(slot => slotIds.Contains(slot.Id))
            .ToArray();
        HashSet<string> selectedSlotIds = slots.Select(slot => slot.Id).ToHashSet(StringComparer.Ordinal);
        HashSet<string> selectedTargetIds = slots
            .SelectMany(slot => slot.TargetIds)
            .Where(targetId => !string.IsNullOrWhiteSpace(targetId))
            .ToHashSet(StringComparer.Ordinal);
        foreach (CardTarget target in context.Targets.Where(target => selectedSlotIds.Contains(target.SlotId)))
        {
            selectedTargetIds.Add(target.Id);
        }

        CardTarget[] targets = context.Targets
            .Where(target => selectedTargetIds.Contains(target.Id))
            .ToArray();
        HashSet<string> selectedMaskIds = targets
            .SelectMany(target => target.ImageAnchors)
            .Select(anchor => anchor.MaskId)
            .ToHashSet(StringComparer.Ordinal);
        CardContextImageSurface[] images = context.Images
            .Select(image => image with
            {
                Masks = image.Masks
                    .Where(mask => selectedTargetIds.Contains(mask.TargetId) || selectedMaskIds.Contains(mask.Id))
                    .ToArray(),
            })
            .Where(image => image.Masks.Count > 0)
            .ToArray();

        return context with
        {
            Slots = slots,
            Targets = targets,
            Images = images,
        };
    }


    private void ReloadNoteToCardDeckNames()
    {
        string previousSelection = NoteToCardDeckName;
        NoteToCardDeckNames.Clear();

        if (!string.IsNullOrWhiteSpace(openCollectionPath) && Directory.Exists(openCollectionPath))
        {
            foreach (string deckName in flashcardDeckSummariesQuery
                .Execute(new FlashcardCollectionQueryRequest(openCollectionPath))
                .Select(deck => deck.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                NoteToCardDeckNames.Add(deckName);
            }
        }

        if (!string.IsNullOrWhiteSpace(previousSelection))
        {
            NoteToCardDeckName = previousSelection;
        }
        else if (NoteToCardDeckNames.Count > 0)
        {
            NoteToCardDeckName = NoteToCardDeckNames[0];
        }

        RefreshNoteToCardDeckSearchResults();
    }

    private void RefreshNoteToCardDeckSearchResults()
    {
        FilteredNoteToCardDeckNames.Clear();

        string query = NoteToCardDeckName.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            OnPropertyChanged(nameof(HasFilteredNoteToCardDeckNames));
            return;
        }

        bool exactMatch = NoteToCardDeckNames.Any(deckName =>
            string.Equals(deckName, query, StringComparison.OrdinalIgnoreCase));
        if (exactMatch)
        {
            OnPropertyChanged(nameof(HasFilteredNoteToCardDeckNames));
            return;
        }

        foreach (string deckName in NoteToCardDeckNames
            .Where(deckName => deckName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(deckName => deckName, StringComparer.OrdinalIgnoreCase)
            .Take(6))
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

        string? existingTag = NoteToCardTags.FirstOrDefault(candidate =>
            string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase));
        if (existingTag is not null)
        {
            NoteToCardTags.Remove(existingTag);
            PersistCurrentNoteToCardTagRemovals([existingTag]);
        }
    }

    private string[] AppendNoteToCardTags(IEnumerable<string> tags)
    {
        List<string> addedTags = [];
        foreach (string tag in tags
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeTagDisplayName)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!NoteToCardTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                NoteToCardTags.Add(tag);
                addedTags.Add(tag);
            }
        }

        return addedTags.ToArray();
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

    private static string NormalizeTagDisplayName(string? tag)
    {
        return string.IsNullOrWhiteSpace(tag)
            ? string.Empty
            : string.Concat(tag.Trim().Where(character => !char.IsWhiteSpace(character)));
    }
}
