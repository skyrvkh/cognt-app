using Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

namespace CogntApp.ViewModels;

internal static partial class NoteToCardPresentationPolicyApplicator
{
    private static CardContext ApplyToWholeContext(CardContext context, CardPresentationPolicy policy)
    {
        bool alreadyApplied = Equals(context.DefaultPresentationPolicy, policy)
            && context.TextCueMode is null
            && context.ImageCueMode is null
            && context.Slots.All(slot => slot.PresentationOverride is null);
        if (alreadyApplied)
        {
            return context;
        }

        CardSlot[] updatedSlots = context.Slots
            .Select(slot => slot with { PresentationOverride = null })
            .ToArray();

        return context with
        {
            DefaultPresentationPolicy = policy,
            Slots = updatedSlots,
            TextCueMode = null,
            ImageCueMode = null,
        };
    }
}
