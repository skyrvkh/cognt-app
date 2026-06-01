using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.ViewModels;

internal static class CardContextDraftDocumentEditor
{
    internal static CardContextDraftNoteDocument ReplaceContext(
        CardContextDraftNoteDocument document,
        CardContext updatedContext)
    {
        int contextIndex = FindContextIndexById(document.Contexts, updatedContext.Id);

        if (contextIndex < 0)
        {
            return document with { Contexts = AppendContext(document.Contexts, updatedContext) };
        }

        CardContext[] contexts = document.Contexts.ToArray();
        contexts[contextIndex] = updatedContext;
        return document with { Contexts = contexts };
    }

    internal static CardContext ResolveRequestedContext(
        CardContextDraftNoteDocument document,
        string? cardContextId)
    {
        if (string.IsNullOrWhiteSpace(cardContextId))
        {
            return document.Contexts[0];
        }

        int contextIndex = FindContextIndexById(document.Contexts, cardContextId);
        if (contextIndex < 0)
        {
            throw new InvalidOperationException("The selected card candidate group no longer exists.");
        }

        return document.Contexts[contextIndex];
    }

    private static CardContext[] AppendContext(
        IReadOnlyList<CardContext> contexts,
        CardContext context)
    {
        CardContext[] nextContexts = new CardContext[contexts.Count + 1];
        for (int index = 0; index < contexts.Count; index++)
        {
            nextContexts[index] = contexts[index];
        }

        nextContexts[^1] = context;
        return nextContexts;
    }

    private static int FindContextIndexById(IReadOnlyList<CardContext> contexts, string contextId)
    {
        for (int index = 0; index < contexts.Count; index++)
        {
            if (string.Equals(contexts[index].Id, contextId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
