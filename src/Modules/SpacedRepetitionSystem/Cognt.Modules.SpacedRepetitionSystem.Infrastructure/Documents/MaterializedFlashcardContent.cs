using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Documents;

internal sealed class MaterializedFlashcardContent
{
    internal MaterializedFlashcardContent(
        string frontContent,
        string backContent,
        string frontText,
        string backText,
        string editableContent,
        IReadOnlyList<string> tags,
        FlashcardContentKind frontKind,
        FlashcardContentKind backKind,
        FlashcardMaterializationStatus status,
        MultimodalClozeReadModel? frontMultimodalContent = null,
        MultimodalClozeReadModel? backMultimodalContent = null)
    {
        FrontContent = frontContent;
        BackContent = backContent;
        FrontText = frontText;
        BackText = backText;
        EditableContent = editableContent;
        Tags = tags;
        FrontKind = frontKind;
        BackKind = backKind;
        Status = status;
        FrontMultimodalContent = frontMultimodalContent;
        BackMultimodalContent = backMultimodalContent;
    }

    internal string FrontContent { get; }

    internal string BackContent { get; }

    internal string FrontText { get; }

    internal string BackText { get; }

    internal string EditableContent { get; }

    internal IReadOnlyList<string> Tags { get; }

    internal FlashcardContentKind FrontKind { get; }

    internal FlashcardContentKind BackKind { get; }

    internal FlashcardMaterializationStatus Status { get; }

    internal MultimodalClozeReadModel? FrontMultimodalContent { get; }

    internal MultimodalClozeReadModel? BackMultimodalContent { get; }
}
