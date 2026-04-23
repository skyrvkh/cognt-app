namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardPresentationPolicies
{
    public static CardPresentationPolicy Resolve(CardContext context, CardSlot? slot = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return slot?.PresentationOverride
            ?? context.DefaultPresentationPolicy
            ?? FromLegacyCueModes(context.TextCueMode, context.ImageCueMode)
            ?? AppDefault;
    }

    public static CardPresentationPolicy Resolve(CardContextSnapshot snapshot, CardSlot? slot = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return slot?.PresentationOverride
            ?? snapshot.DefaultPresentationPolicy
            ?? AppDefault;
    }
}
