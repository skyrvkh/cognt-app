using AppReadModels = Cognt.Modules.SpacedRepetitionSystem.Application.ReadModels;
using AppQueueCandidate = Cognt.Modules.SpacedRepetitionSystem.Application.Review.FlashcardReviewQueueCandidate;
using AppQueueCategory = Cognt.Modules.SpacedRepetitionSystem.Application.Review.FlashcardReviewQueueCategory;
using AppQueueCounts = Cognt.Modules.SpacedRepetitionSystem.Application.Review.FlashcardReviewQueueCounts;
using AppReviewLimitUsage = Cognt.Modules.SpacedRepetitionSystem.Application.Review.FlashcardReviewLimitUsage;
using PersistenceQueueCandidate = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewQueueCandidate;
using PersistenceQueueCategory = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewQueueCategory;
using PersistenceQueueCounts = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewQueueCounts;
using PersistenceReviewLimitUsage = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewLimitUsage;
using PersistenceSettings = Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Persistence.FlashcardReviewSettingsReadModel;

namespace Cognt.Modules.SpacedRepetitionSystem.Infrastructure.Flashcards;

internal sealed partial class SqliteFlashcardStore
{
    private static AppQueueCandidate[] CreateReviewQueueCandidates(IReadOnlyList<PersistenceQueueCandidate> candidates)
    {
        AppQueueCandidate[] queueCandidates = new AppQueueCandidate[candidates.Count];
        for (int index = 0; index < candidates.Count; index++)
        {
            queueCandidates[index] = ToReadModel(candidates[index]);
        }

        return queueCandidates;
    }

    private static AppReadModels.FlashcardReviewSettingsReadModel ToReadModel(
        PersistenceSettings settings)
    {
        return new AppReadModels.FlashcardReviewSettingsReadModel(
            settings.MaximumReviewCardsPerDay,
            settings.MaximumNewCardsPerDay,
            settings.DesiredRetention,
            settings.LearningSteps,
            settings.RelearningSteps,
            settings.IsOptimizationEnabled,
            settings.FsrsParameters,
            settings.IsCramEnabled,
            settings.ShowFlashcardsInNavigationTree).Normalize();
    }

    private static AppQueueCounts ToReadModel(PersistenceQueueCounts counts)
    {
        return new AppQueueCounts(
            counts.LearningDueCards,
            counts.RelearningDueCards,
            counts.OverdueCards,
            counts.DueCards,
            counts.NewCards,
            counts.SuspendedCardsExcluded)
        {
            FutureReviewCards = counts.FutureReviewCards,
        };
    }

    private static AppReadModels.FlashcardReviewQueueSummaryReadModel CreateCramSummary(
        AppQueueCounts counts,
        int queuedCards,
        int selectedCards,
        int skippedInvalidCards)
    {
        return new AppReadModels.FlashcardReviewQueueSummaryReadModel(
            counts.LearningDueCards,
            counts.RelearningDueCards,
            counts.OverdueCards,
            counts.DueCards,
            counts.NewCards,
            counts.SuspendedCardsExcluded,
            0,
            0,
            queuedCards)
        {
            FutureReviewCards = counts.FutureReviewCards,
            SelectedCards = selectedCards,
            SkippedInvalidCards = skippedInvalidCards,
        };
    }

    private static AppReviewLimitUsage ToReadModel(PersistenceReviewLimitUsage usage)
    {
        return new AppReviewLimitUsage(
            usage.ReviewCards,
            usage.NewCards);
    }

    private static AppQueueCandidate ToReadModel(PersistenceQueueCandidate candidate)
    {
        return new AppQueueCandidate(
            candidate.CardId,
            candidate.Category switch
            {
                PersistenceQueueCategory.Learning => AppQueueCategory.Learning,
                PersistenceQueueCategory.Relearning => AppQueueCategory.Relearning,
                PersistenceQueueCategory.Overdue => AppQueueCategory.Overdue,
                PersistenceQueueCategory.Due => AppQueueCategory.Due,
                PersistenceQueueCategory.New => AppQueueCategory.New,
                _ => AppQueueCategory.New,
            },
            candidate.DueUtc);
    }
}
