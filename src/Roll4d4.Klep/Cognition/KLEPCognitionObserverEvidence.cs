using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Text;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Emotion;
using Roll4d4.Klep.Memory;
using Roll4d4.Klep.Observer;

namespace Roll4d4.Klep.Cognition
{
    /// <summary>
    /// A project policy's signed Observer proposal plus structured provenance.
    /// Positive and negative values are both allowed; KLEP assigns neither a
    /// moral nor emotional meaning to their sign.
    /// </summary>
    public sealed class KLEPCognitionEvidenceContribution
    {
        private readonly ReadOnlyCollection<string> evidenceIds;

        public KLEPCognitionEvidenceContribution(
            float value,
            string reasonCode,
            string explanation = "",
            IReadOnlyList<string> evidenceIds = null)
        {
            Value = KLEPCognitionValidation.RequireFinite(value, nameof(value));
            ReasonCode = KLEPCognitionValidation.RequireId(
                reasonCode,
                nameof(reasonCode));
            Explanation = explanation ?? string.Empty;
            var copiedIds = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (evidenceIds != null)
            {
                for (int i = 0; i < evidenceIds.Count; i++)
                {
                    string id = KLEPCognitionValidation.RequireId(
                        evidenceIds[i],
                        nameof(evidenceIds));
                    if (!seen.Add(id))
                    {
                        throw new ArgumentException(
                            $"Evidence ID '{id}' occurs more than once.",
                            nameof(evidenceIds));
                    }

                    copiedIds.Add(id);
                }
            }

            this.evidenceIds = new ReadOnlyCollection<string>(copiedIds);
        }

        public float Value { get; }
        public string ReasonCode { get; }
        public string Explanation { get; }
        public IReadOnlyList<string> EvidenceIds => evidenceIds;

        internal string BuildObserverExplanation()
        {
            var text = new StringBuilder(ReasonCode);
            if (!string.IsNullOrEmpty(Explanation))
            {
                text.Append(": ").Append(Explanation);
            }

            if (evidenceIds.Count > 0)
            {
                text.Append(" [evidence=");
                for (int i = 0; i < evidenceIds.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(',');
                    }

                    text.Append(evidenceIds[i]);
                }

                text.Append(']');
            }

            return text.ToString();
        }
    }

    public interface IKLEPMemoryObserverEvidencePolicy
    {
        string StableId { get; }
        string Version { get; }

        /// <summary>
        /// Returns a project-owned factual cue, or null to abstain for this
        /// eligible target.
        /// </summary>
        KLEPMemoryCue CreateCue(
            KLEPObserverEvidenceContext context,
            KLEPExecutableDefinition eligibleTarget);

        /// <summary>
        /// Interprets pure recall evidence. Returning null abstains; the policy
        /// owns the sign and meaning of every returned contribution.
        /// </summary>
        KLEPCognitionEvidenceContribution Evaluate(
            KLEPObserverEvidenceContext context,
            KLEPExecutableDefinition eligibleTarget,
            KLEPMemoryRecallResult recall);
    }

    public sealed class KLEPMemoryObserverEvidenceEvaluation
    {
        internal KLEPMemoryObserverEvidenceEvaluation(
            KLEPExecutableDefinition target,
            KLEPMemoryCue cue,
            KLEPMemoryRecallResult recall,
            KLEPCognitionEvidenceContribution contribution)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Cue = cue;
            Recall = recall;
            Contribution = contribution;
        }

        public KLEPExecutableDefinition Target { get; }
        public string TargetExecutableId => Target.StableId;
        public KLEPMemoryCue Cue { get; }
        public KLEPMemoryRecallResult Recall { get; }
        public KLEPCognitionEvidenceContribution Contribution { get; }
        public bool Abstained => Contribution == null;
    }

    /// <summary>
    /// Calls only KLEPMemory.Recall for the Observer's already-eligible targets
    /// and delegates all interpretation to project policy.
    /// </summary>
    public sealed class KLEPMemoryObserverEvidenceSource :
        IKLEPObserverEvidenceSource
    {
        private readonly IKLEPMemoryObserverEvidencePolicy policy;
        private ReadOnlyCollection<KLEPMemoryObserverEvidenceEvaluation>
            lastEvaluations = new ReadOnlyCollection<
                KLEPMemoryObserverEvidenceEvaluation>(
                    new List<KLEPMemoryObserverEvidenceEvaluation>());
        private bool isEvaluating;

        public KLEPMemoryObserverEvidenceSource(
            KLEPMemory memory,
            IKLEPMemoryObserverEvidencePolicy policy,
            int maximumMatches = 8)
        {
            Memory = memory ?? throw new ArgumentNullException(nameof(memory));
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
            StableId = KLEPCognitionValidation.RequireId(
                policy.StableId,
                nameof(policy));
            Version = KLEPCognitionValidation.RequireId(
                policy.Version,
                nameof(policy));
            if (maximumMatches <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumMatches));
            }

            MaximumMatches = maximumMatches;
        }

        public string StableId { get; }
        public string Version { get; }
        public KLEPMemory Memory { get; }
        public int MaximumMatches { get; }
        public IReadOnlyList<KLEPMemoryObserverEvidenceEvaluation>
            LastEvaluations => lastEvaluations;

        public IReadOnlyList<KLEPObserverEvidence> Evaluate(
            KLEPObserverEvidenceContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (isEvaluating)
            {
                throw new InvalidOperationException(
                    $"Memory evidence source '{StableId}' cannot evaluate recursively.");
            }

            isEvaluating = true;
            try
            {
                lastEvaluations = new ReadOnlyCollection<
                    KLEPMemoryObserverEvidenceEvaluation>(
                        new List<KLEPMemoryObserverEvidenceEvaluation>());
                ValidatePolicyIdentity();
                long tickBefore = Memory.CurrentTick;
                KLEPMemorySnapshot snapshotBefore = Memory.Snapshot;
                int historyCountBefore = Memory.GetSnapshotHistory().Count;
                ReadOnlyCollection<KLEPObserverEvidence> result = null;
                Exception policyFailure = null;
                try
                {
                    var evaluations = new List<
                        KLEPMemoryObserverEvidenceEvaluation>(
                            context.EligibleTargets.Count);
                    var evidence = new List<KLEPObserverEvidence>();
                    for (int i = 0; i < context.EligibleTargets.Count; i++)
                    {
                        KLEPExecutableDefinition target =
                            context.EligibleTargets[i];
                        KLEPMemoryCue cue = policy.CreateCue(context, target);
                        ValidatePolicyIdentity();
                        if (cue == null)
                        {
                            evaluations.Add(
                                new KLEPMemoryObserverEvidenceEvaluation(
                                    target, null, null, null));
                            continue;
                        }

                        KLEPMemoryRecallResult recall = Memory.Recall(
                            cue,
                            MaximumMatches);
                        KLEPCognitionEvidenceContribution contribution =
                            policy.Evaluate(context, target, recall);
                        ValidatePolicyIdentity();
                        evaluations.Add(new KLEPMemoryObserverEvidenceEvaluation(
                            target,
                            cue,
                            recall,
                            contribution));
                        if (contribution != null)
                        {
                            evidence.Add(new KLEPObserverEvidence(
                                target.StableId,
                                contribution.Value,
                                contribution.BuildObserverExplanation()));
                        }
                    }

                    lastEvaluations = new ReadOnlyCollection<
                        KLEPMemoryObserverEvidenceEvaluation>(evaluations);
                    result = new ReadOnlyCollection<KLEPObserverEvidence>(
                        evidence);
                }
                catch (Exception failure)
                {
                    policyFailure = failure;
                }

                CompleteReadOnlyMemoryEvaluation(
                    tickBefore,
                    snapshotBefore,
                    historyCountBefore,
                    policyFailure);
                return result;
            }
            finally
            {
                isEvaluating = false;
            }
        }

        private void ValidatePolicyIdentity()
        {
            if (!StringComparer.Ordinal.Equals(policy.StableId, StableId) ||
                !StringComparer.Ordinal.Equals(policy.Version, Version))
            {
                throw new InvalidOperationException(
                    "A Memory evidence policy cannot change identity or version after registration.");
            }
        }

        private void CompleteReadOnlyMemoryEvaluation(
            long tick,
            KLEPMemorySnapshot snapshot,
            int historyCount,
            Exception policyFailure)
        {
            if (Memory.CurrentTick != tick ||
                !ReferenceEquals(Memory.Snapshot, snapshot) ||
                Memory.GetSnapshotHistory().Count != historyCount)
            {
                // Mutation is the boundary violation surfaced to the host.
                // When policy also failed, preserve that original failure as
                // InnerException; this adapter has no rollback authority.
                throw new InvalidOperationException(
                    $"Memory evidence policy '{StableId}' mutated its Memory during a read-only evaluation.",
                    policyFailure);
            }

            if (policyFailure != null)
            {
                ExceptionDispatchInfo.Capture(policyFailure).Throw();
            }
        }
    }

    /// <summary>
    /// Immutable value-copy of the current Emotion state supplied to policy.
    /// </summary>
    public sealed class KLEPEmotionObserverEvidenceState
    {
        internal KLEPEmotionObserverEvidenceState(KLEPEmotion emotion)
        {
            AxisXName = emotion.Configuration.AxisXName;
            AxisYName = emotion.Configuration.AxisYName;
            Tick = emotion.Tick;
            Position = emotion.Position;
            Velocity = emotion.Velocity;
            UnchangedPositionTickCount = emotion.UnchangedPositionTickCount;
            HasProducedSnapshot = emotion.LastSnapshot != null;
            LastNetInfluence = HasProducedSnapshot
                ? emotion.LastSnapshot.NetInfluence
                : KLEPEmotionVector.Zero;
        }

        public string AxisXName { get; }
        public string AxisYName { get; }
        public long Tick { get; }
        public KLEPEmotionVector Position { get; }
        public KLEPEmotionVector Velocity { get; }
        public long UnchangedPositionTickCount { get; }
        public bool HasProducedSnapshot { get; }
        public KLEPEmotionVector LastNetInfluence { get; }
        public bool IsAtRest => Velocity == KLEPEmotionVector.Zero;
    }

    public interface IKLEPEmotionObserverEvidencePolicy
    {
        string StableId { get; }
        string Version { get; }

        /// <summary>
        /// Interprets the copied Emotion state for one already-eligible target.
        /// Returning null abstains. KLEP assigns no preferred position or sign.
        /// </summary>
        KLEPCognitionEvidenceContribution Evaluate(
            KLEPObserverEvidenceContext context,
            KLEPExecutableDefinition eligibleTarget,
            KLEPEmotionObserverEvidenceState emotionState);
    }

    public sealed class KLEPEmotionObserverEvidenceEvaluation
    {
        internal KLEPEmotionObserverEvidenceEvaluation(
            KLEPExecutableDefinition target,
            KLEPEmotionObserverEvidenceState state,
            KLEPCognitionEvidenceContribution contribution)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            State = state ?? throw new ArgumentNullException(nameof(state));
            Contribution = contribution;
        }

        public KLEPExecutableDefinition Target { get; }
        public string TargetExecutableId => Target.StableId;
        public KLEPEmotionObserverEvidenceState State { get; }
        public KLEPCognitionEvidenceContribution Contribution { get; }
        public bool Abstained => Contribution == null;
    }

    /// <summary>
    /// Copies the current Emotion state once, then delegates its meaning for
    /// each already-eligible target to project policy. It never advances the
    /// emotional body.
    /// </summary>
    public sealed class KLEPEmotionObserverEvidenceSource :
        IKLEPObserverEvidenceSource
    {
        private readonly IKLEPEmotionObserverEvidencePolicy policy;
        private ReadOnlyCollection<KLEPEmotionObserverEvidenceEvaluation>
            lastEvaluations = new ReadOnlyCollection<
                KLEPEmotionObserverEvidenceEvaluation>(
                    new List<KLEPEmotionObserverEvidenceEvaluation>());
        private bool isEvaluating;

        public KLEPEmotionObserverEvidenceSource(
            KLEPEmotion emotion,
            IKLEPEmotionObserverEvidencePolicy policy)
        {
            Emotion = emotion ?? throw new ArgumentNullException(nameof(emotion));
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
            StableId = KLEPCognitionValidation.RequireId(
                policy.StableId,
                nameof(policy));
            Version = KLEPCognitionValidation.RequireId(
                policy.Version,
                nameof(policy));
        }

        public string StableId { get; }
        public string Version { get; }
        public KLEPEmotion Emotion { get; }
        public IReadOnlyList<KLEPEmotionObserverEvidenceEvaluation>
            LastEvaluations => lastEvaluations;

        public IReadOnlyList<KLEPObserverEvidence> Evaluate(
            KLEPObserverEvidenceContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (isEvaluating)
            {
                throw new InvalidOperationException(
                    $"Emotion evidence source '{StableId}' cannot evaluate recursively.");
            }

            isEvaluating = true;
            try
            {
                lastEvaluations = new ReadOnlyCollection<
                    KLEPEmotionObserverEvidenceEvaluation>(
                        new List<KLEPEmotionObserverEvidenceEvaluation>());
                ValidatePolicyIdentity();
                long tickBefore = Emotion.Tick;
                KLEPEmotionVector positionBefore = Emotion.Position;
                KLEPEmotionVector velocityBefore = Emotion.Velocity;
                KLEPEmotionSnapshot snapshotBefore = Emotion.LastSnapshot;
                int historyCountBefore = Emotion.GetSnapshotHistory().Count;
                var state = new KLEPEmotionObserverEvidenceState(Emotion);
                ReadOnlyCollection<KLEPObserverEvidence> result = null;
                Exception policyFailure = null;
                try
                {
                    var evaluations = new List<
                        KLEPEmotionObserverEvidenceEvaluation>(
                            context.EligibleTargets.Count);
                    var evidence = new List<KLEPObserverEvidence>();
                    for (int i = 0; i < context.EligibleTargets.Count; i++)
                    {
                        KLEPExecutableDefinition target =
                            context.EligibleTargets[i];
                        KLEPCognitionEvidenceContribution contribution =
                            policy.Evaluate(context, target, state);
                        ValidatePolicyIdentity();
                        evaluations.Add(
                            new KLEPEmotionObserverEvidenceEvaluation(
                                target,
                                state,
                                contribution));
                        if (contribution != null)
                        {
                            evidence.Add(new KLEPObserverEvidence(
                                target.StableId,
                                contribution.Value,
                                contribution.BuildObserverExplanation()));
                        }
                    }

                    lastEvaluations = new ReadOnlyCollection<
                        KLEPEmotionObserverEvidenceEvaluation>(evaluations);
                    result = new ReadOnlyCollection<KLEPObserverEvidence>(
                        evidence);
                }
                catch (Exception failure)
                {
                    policyFailure = failure;
                }

                CompleteReadOnlyEmotionEvaluation(
                    tickBefore,
                    positionBefore,
                    velocityBefore,
                    snapshotBefore,
                    historyCountBefore,
                    policyFailure);
                return result;
            }
            finally
            {
                isEvaluating = false;
            }
        }

        private void ValidatePolicyIdentity()
        {
            if (!StringComparer.Ordinal.Equals(policy.StableId, StableId) ||
                !StringComparer.Ordinal.Equals(policy.Version, Version))
            {
                throw new InvalidOperationException(
                    "An Emotion evidence policy cannot change identity or version after registration.");
            }
        }

        private void CompleteReadOnlyEmotionEvaluation(
            long tick,
            KLEPEmotionVector position,
            KLEPEmotionVector velocity,
            KLEPEmotionSnapshot snapshot,
            int historyCount,
            Exception policyFailure)
        {
            if (Emotion.Tick != tick ||
                Emotion.Position != position ||
                Emotion.Velocity != velocity ||
                !ReferenceEquals(Emotion.LastSnapshot, snapshot) ||
                Emotion.GetSnapshotHistory().Count != historyCount)
            {
                // Mutation is the boundary violation surfaced to the host.
                // When policy also failed, preserve that original failure as
                // InnerException; this adapter has no rollback authority.
                throw new InvalidOperationException(
                    $"Emotion evidence policy '{StableId}' mutated its emotional body during a read-only evaluation.",
                    policyFailure);
            }

            if (policyFailure != null)
            {
                ExceptionDispatchInfo.Capture(policyFailure).Throw();
            }
        }
    }
}
