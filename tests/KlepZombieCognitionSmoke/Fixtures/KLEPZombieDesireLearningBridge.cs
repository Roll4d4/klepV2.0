using System;
using Roll4d4.Klep.Desire;
using Roll4d4.Klep.LearnedExpectations;

namespace Roll4d4.Klep.ZombieTest
{
    /// <summary>
    /// Project-owned writer seam from one already-evaluated factual zombie
    /// Desire transition into its independent learned critic. It owns only the
    /// evidence sequence/replay guard; it neither evaluates Desire nor selects
    /// behavior.
    /// </summary>
    public sealed class KLEPZombieDesireLearningBridge
    {
        private readonly KLEPLearnedExpectations learnedExpectations;
        private long evidenceSequence;
        private long lastActionRunIndex;
        private string lastTransitionId = string.Empty;

        public KLEPZombieDesireLearningBridge(
            KLEPLearnedExpectations learnedExpectations)
        {
            this.learnedExpectations = learnedExpectations ??
                throw new ArgumentNullException(nameof(learnedExpectations));
            evidenceSequence =
                learnedExpectations.LastDesireEffectEvidenceSequence;
        }

        public long EvidenceSequence => evidenceSequence;
        public long LastActionRunIndex => lastActionRunIndex;
        public string LastTransitionId => lastTransitionId;

        public KLEPLearnedDesireEffectSnapshot RecordActionOwnedEffects(
            KLEPZombieDesireStep step)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            return RecordActionOwnedEffects(step.Effects);
        }

        /// <summary>
        /// Accepts the same immutable effect vector after factual Memory has
        /// closed its experience. The critic remains a derived consumer and
        /// cannot cause the Memory transaction to exist.
        /// </summary>
        public KLEPLearnedDesireEffectSnapshot RecordActionOwnedEffects(
            KLEPDesireEffectVector effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            KLEPDesireAttributionEvidence attribution = effects.Attribution;
            if (!attribution.IsEligibleForAutomaticExpectationLearning ||
                !attribution.ActionRunIndex.HasValue)
            {
                throw new ArgumentException(
                    "The zombie critic bridge accepts only an exact " +
                    "ActionOwned Desire transition.",
                    nameof(effects));
            }

            long actionRunIndex = attribution.ActionRunIndex.Value;
            if (actionRunIndex <= lastActionRunIndex ||
                StringComparer.Ordinal.Equals(
                    effects.TransitionId,
                    lastTransitionId))
            {
                throw new InvalidOperationException(
                    "A zombie Desire action transition cannot train the " +
                    "critic twice or arrive out of run order.");
            }

            if (evidenceSequence == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The zombie Desire evidence sequence is exhausted.");
            }

            long nextSequence = evidenceSequence + 1;
            KLEPLearnedDesireEffectSnapshot snapshot =
                learnedExpectations.RecordDesireEffects(
                    new KLEPLearnedDesireEffectTrial(
                        learnedExpectations.OwnerStableId,
                        learnedExpectations.OwnerVersion,
                        nextSequence,
                        effects));

            // Advance local guards only after the critic atomically accepts
            // the entire vector.
            evidenceSequence = nextSequence;
            lastActionRunIndex = actionRunIndex;
            lastTransitionId = effects.TransitionId;
            return snapshot;
        }
    }
}
