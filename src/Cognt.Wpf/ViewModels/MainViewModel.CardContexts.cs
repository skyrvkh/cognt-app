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

        return new OpenImageMaskEditorRequest(
            openCollectionPath,
            relativePath,
            imageBlockId.Trim(),
            normalizedImageRelPath,
            imageFullPath,
            context,
            context.Id);
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

    internal CardContext CreateCardContextFromSelection(
        IReadOnlyList<string> selectedBlockIds,
        string? title,
        string noteContent)
    {
        ArgumentNullException.ThrowIfNull(selectedBlockIds);

        string relativePath = GetRequiredCurrentNoteRelativePath();
        SaveNoteIfChanged(noteContent);

        CardContextDraftNoteDocument document = ReadCardContextDraftDocument(relativePath);
        int order = GetNextDraftContextOrder(document.Contexts);
        CardContext context = CardContextAuthoring.CreateContextForSelectedBlocks(
            selectedBlockIds,
            title,
            order);

        CardContextDraftNoteDocument updatedDocument = document with
        {
            Contexts = AppendDraftContext(document.Contexts, context),
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
        var slots = new List<CardSlot>();
        var selectedSlotIds = new HashSet<string>(StringComparer.Ordinal);
        var selectedTargetIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (CardSlot slot in context.Slots)
        {
            if (!slotIds.Contains(slot.Id))
            {
                continue;
            }

            slots.Add(slot);
            selectedSlotIds.Add(slot.Id);

            foreach (string targetId in slot.TargetIds)
            {
                if (!string.IsNullOrWhiteSpace(targetId))
                {
                    selectedTargetIds.Add(targetId);
                }
            }
        }

        foreach (CardTarget target in context.Targets)
        {
            if (selectedSlotIds.Contains(target.SlotId))
            {
                selectedTargetIds.Add(target.Id);
            }
        }

        var targets = new List<CardTarget>();
        var selectedMaskIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CardTarget target in context.Targets)
        {
            if (!selectedTargetIds.Contains(target.Id))
            {
                continue;
            }

            targets.Add(target);

            foreach (ImageMaskAnchor anchor in target.ImageAnchors)
            {
                selectedMaskIds.Add(anchor.MaskId);
            }
        }

        var images = new List<CardContextImageSurface>();
        foreach (CardContextImageSurface image in context.Images)
        {
            var masks = new List<ImageOcclusionMask>();
            foreach (ImageOcclusionMask mask in image.Masks)
            {
                if (selectedTargetIds.Contains(mask.TargetId) || selectedMaskIds.Contains(mask.Id))
                {
                    masks.Add(mask);
                }
            }

            if (masks.Count > 0)
            {
                images.Add(image with { Masks = masks });
            }
        }

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
        noteToCardDeckNameCatalog = [];
        noteToCardDeckNameSet.Clear();

        if (!string.IsNullOrWhiteSpace(openCollectionPath) && Directory.Exists(openCollectionPath))
        {
            List<string> deckNames = LoadDistinctNoteToCardDeckNames(out string? firstDeckName);
            deckNames.Sort(StringComparer.OrdinalIgnoreCase);
            noteToCardDeckNameCatalog = deckNames;

            foreach (string deckName in deckNames)
            {
                NoteToCardDeckNames.Add(deckName);
                noteToCardDeckNameSet.Add(deckName);
            }

            if (string.IsNullOrWhiteSpace(previousSelection) && firstDeckName is not null)
            {
                previousSelection = firstDeckName;
            }
        }

        isUpdatingNoteToCardDeckCatalog = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(previousSelection))
            {
                NoteToCardDeckName = previousSelection;
            }
            else if (NoteToCardDeckNames.Count > 0)
            {
                NoteToCardDeckName = NoteToCardDeckNames[0];
            }
            else
            {
                NoteToCardDeckName = string.Empty;
            }
        }
        finally
        {
            isUpdatingNoteToCardDeckCatalog = false;
        }

        RefreshNoteToCardDeckSearchResults();
    }

    private List<string> LoadDistinctNoteToCardDeckNames(out string? firstDeckName)
    {
        var deckNames = new List<string>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        firstDeckName = null;

        foreach (FlashcardDeckSummaryReadModel deck in flashcardDeckSummariesQuery.Execute(
                     new FlashcardCollectionQueryRequest(openCollectionPath)))
        {
            string name = deck.Name;
            if (string.IsNullOrWhiteSpace(name) || !seenNames.Add(name))
            {
                continue;
            }

            firstDeckName ??= name;
            deckNames.Add(name);
        }

        return deckNames;
    }

    private void RefreshNoteToCardDeckSearchResults()
    {
        FilteredNoteToCardDeckNames.Clear();

        string query = NoteToCardDeckName.Trim();
        if (string.IsNullOrWhiteSpace(query)
            || noteToCardDeckNameSet.Contains(query))
        {
            OnPropertyChanged(nameof(HasFilteredNoteToCardDeckNames));
            return;
        }

        for (int index = 0; index < noteToCardDeckNameCatalog.Count && FilteredNoteToCardDeckNames.Count < 6; index++)
        {
            string deckName = noteToCardDeckNameCatalog[index];
            if (deckName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredNoteToCardDeckNames.Add(deckName);
            }
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
        List<string> addedTags = [];
        var knownTags = new HashSet<string>(NoteToCardTags, StringComparer.OrdinalIgnoreCase);

        foreach (string value in tags)
        {
            string tag = NormalizeTagDisplayName(value);
            if (tag.Length == 0 || !knownTags.Add(tag))
            {
                continue;
            }

            NoteToCardTags.Add(tag);
            addedTags.Add(tag);
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

    private static int GetNextDraftContextOrder(IReadOnlyList<CardContext> contexts)
    {
        if (contexts.Count == 0)
        {
            return 0;
        }

        int maxOrder = contexts[0].Order;
        for (int index = 1; index < contexts.Count; index++)
        {
            maxOrder = Math.Max(maxOrder, contexts[index].Order);
        }

        return maxOrder + 1;
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

    private static string NormalizeTagDisplayName(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> trimmedTag = tag.Trim();
        Span<char> normalized = trimmedTag.Length <= 256
            ? stackalloc char[trimmedTag.Length]
            : new char[trimmedTag.Length];
        int normalizedLength = 0;

        foreach (char character in trimmedTag)
        {
            if (!char.IsWhiteSpace(character))
            {
                normalized[normalizedLength++] = character;
            }
        }

        return normalizedLength == 0
            ? string.Empty
            : new string(normalized[..normalizedLength]);
    }
}