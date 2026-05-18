using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cognt.Modules.PersonalKnowledgeManagement.Application.Notes;
using Cognt.Wpf.EditorHost.Models.Editor;

namespace CogntApp.ViewModels;

public sealed partial class MainViewModel
{
    private static readonly JsonSerializerOptions BlockSuiteJsonOptions = new()
    {
        WriteIndented = true
    };

    public string CreateExtractNote(string? sourcePath, string selectedContent, string selectedPlainText)
    {
        ExtractNoteCreationRequest request = PrepareExtractNoteCreation(sourcePath);
        KnowledgeCreatedNoteReadModel createdNote = CreateExtractNoteCore(
            request,
            selectedContent,
            selectedPlainText);
        return CompleteExtractNoteCreation(createdNote);
    }

    public async Task<string> CreateExtractNoteAsync(
        string? sourcePath,
        string selectedContent,
        string selectedPlainText)
    {
        ExtractNoteCreationRequest request = PrepareExtractNoteCreation(sourcePath);
        KnowledgeCreatedNoteReadModel createdNote = await Task.Run(() => CreateExtractNoteCore(
            request,
            selectedContent,
            selectedPlainText));
        return CompleteExtractNoteCreation(createdNote);
    }

    private ExtractNoteCreationRequest PrepareExtractNoteCreation(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || string.IsNullOrWhiteSpace(sourcePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The extract source is not the current note file.");
        }

        long? parentId = currentNoteItemId;
        NoteTreeNodeViewModel? parentNode = parentId.HasValue
            ? FindTreeNode(parentId.Value)
            : null;

        if (parentNode is { CanContainChildren: true })
        {
            parentNode.IsExpanded = true;
            LoadChildren(parentNode);
        }

        return new ExtractNoteCreationRequest(
            openCollectionPath,
            parentId,
            GetRequiredRelativeNotePath(sourcePath),
            "Extract");
    }

    private KnowledgeCreatedNoteReadModel CreateExtractNoteCore(
        ExtractNoteCreationRequest request,
        string selectedContent,
        string selectedPlainText)
    {
        string content = CreateExtractContent(selectedContent, selectedPlainText, request.Title);
        return noteService.CreateExtractNote(
            request.CollectionPath,
            request.ParentId,
            request.SourceRelativePath,
            content);
    }

    private string CompleteExtractNoteCreation(KnowledgeCreatedNoteReadModel createdNote)
    {
        ApplyCreatedNoteState(createdNote);
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return createdNote.RelativePath;
    }

    private string CreateExtractContent(string selectedContent, string selectedPlainText, string title)
    {
        string? normalizedBlockSuiteContent = NormalizeBlockSuiteExtractContent(selectedContent);
        if (!string.IsNullOrWhiteSpace(normalizedBlockSuiteContent))
        {
            return normalizedBlockSuiteContent;
        }

        if (string.IsNullOrWhiteSpace(selectedPlainText))
        {
            throw new InvalidOperationException("The selected content is empty.");
        }

        return documentContentFactory.FromPlainText(selectedPlainText, title);
    }

    private static string? NormalizeBlockSuiteExtractContent(string selectedContent)
    {
        if (string.IsNullOrWhiteSpace(selectedContent))
        {
            return null;
        }

        try
        {
            if (JsonNode.Parse(selectedContent) is not JsonObject root
                || root["kind"]?.GetValue<string>() != "cognt-blocksuite-doc"
                || root["snapshot"] is not JsonObject snapshot
                || snapshot["blocks"] is not JsonObject blocks)
            {
                return null;
            }

            if (!BlockSuiteBlockHasBodyContent(blocks, isRootBlock: true))
            {
                return null;
            }

            NormalizeExtractRootChildren(blocks);
            return root.ToJsonString(BlockSuiteJsonOptions) + Environment.NewLine;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void NormalizeExtractRootChildren(JsonObject blocks)
    {
        if (blocks["children"] is not JsonArray children)
        {
            return;
        }

        for (int index = children.Count - 1; index >= 0; index--)
        {
            if (children[index] is JsonObject child
                && string.Equals(child["flavour"]?.GetValue<string>(), "affine:surface", StringComparison.Ordinal))
            {
                children.RemoveAt(index);
            }
        }

        foreach (JsonNode? child in children)
        {
            if (child is JsonObject childBlock
                && string.Equals(childBlock["flavour"]?.GetValue<string>(), "affine:note", StringComparison.Ordinal)
                && childBlock["props"] is JsonObject props)
            {
                props["xywh"] = "[0,0,498,92]";
                props["background"] = "--affine-note-background-white";
                props["lockedBySelf"] = false;
            }
        }
    }

    private static bool BlockSuiteBlockHasBodyContent(JsonObject block, bool isRootBlock = false)
    {
        if (!isRootBlock
            && block["props"] is JsonObject props
            && BlockSuiteTextValueHasBodyContent(props["text"]))
        {
            return true;
        }

        if (block["children"] is not JsonArray children)
        {
            return false;
        }

        foreach (JsonNode? child in children)
        {
            if (child is JsonObject childBlock
                && BlockSuiteBlockHasBodyContent(childBlock))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BlockSuiteTextValueHasBodyContent(JsonNode? textValue)
    {
        if (textValue is not JsonObject textObject
            || textObject["delta"] is not JsonArray delta)
        {
            return false;
        }

        foreach (JsonNode? entryNode in delta)
        {
            if (entryNode is JsonObject entry
                && entry["insert"]?.GetValue<string>() is { } insertedText
                && !string.IsNullOrWhiteSpace(insertedText))
            {
                return true;
            }
        }

        return false;
    }

    public CreateLinkedNoteResult CreateLinkedNote(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(currentNotePath)
            || !string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(currentNotePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The linked note source is not the current note file.");
        }

        NoteTreeNodeViewModel? parentNode = currentNoteItemId.HasValue
            ? FindTreeNode(currentNoteItemId.Value)
            : null;

        long? parentId = parentNode is { CanContainChildren: true }
            ? parentNode.Id
            : parentNode?.ParentId;

        if (parentNode is { CanContainChildren: true })
        {
            parentNode.IsExpanded = true;
            LoadChildren(parentNode);
        }

        const string title = "Untitled";
        string documentContent = documentContentFactory.FromPlainText(string.Empty, title);
        KnowledgeCreatedNoteReadModel createdNote = noteService.CreateNote(
            openCollectionPath,
            parentId,
            title,
            documentContent);

        ApplyCreatedNoteState(createdNote);
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        return CreateLinkedNoteResult.Success(createdNote.RelativePath, title, documentContent);
    }

    private void AddNote()
    {
        if (string.IsNullOrWhiteSpace(openCollectionPath))
        {
            return;
        }

        KnowledgeCreatedNoteReadModel createdNote = noteService.CreateNote(
            openCollectionPath,
            parentId: null,
            "Untitled",
            documentContentFactory.FromPlainText(string.Empty, "Untitled"));
        ApplyCreatedNoteState(createdNote);
        RefreshDashboardData();
        RefreshAvailableCollections();
        MentionTargetsChanged?.Invoke(this, EventArgs.Empty);
        OpenNoteByRelativePath(createdNote.RelativePath, selectInTree: true);
    }

    private void ApplyCreatedNoteState(
        KnowledgeCreatedNoteReadModel createdNote,
        long? previousSiblingId = null,
        long? nextSiblingId = null)
    {
        string notePath = ResolveCollectionFilePath(openCollectionPath, createdNote.RelativePath);
        KnowledgeTreeItemReadModel item = new(
            createdNote.ItemId,
            createdNote.ParentId,
            KnowledgeItemKind.Note,
            createdNote.Title,
            createdNote.RelativePath,
            0);
        AddLoadedTreeNode(item, previousSiblingId, nextSiblingId);
        AddOrUpdateRecentNote(item, notePath);
    }

    private readonly record struct ExtractNoteCreationRequest(
        string CollectionPath,
        long? ParentId,
        string SourceRelativePath,
        string Title);
}
