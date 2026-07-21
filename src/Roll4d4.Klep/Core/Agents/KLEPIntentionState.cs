using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Roll4d4.Klep.Core
{
    public enum KLEPIntentionStatus
    {
        Active,
        Suspended,
        Completed,
        Abandoned
    }

    public enum KLEPIntentionTransitionKind
    {
        Adopted,
        Suspended,
        Resumed,
        Completed,
        Abandoned
    }

    public enum KLEPIntentionTransitionReason
    {
        GoalSelected,
        GoalInterrupted,
        GoalLocksClosed,
        GoalBelowThreshold,
        GoalSucceeded,
        GoalFailed,
        GoalFaulted,
        GoalRemoved,
        CatalogRemoved,
        RegistrationReplaced
    }

    /// <summary>
    /// Immutable state of one adopted root Solo Goal. Intention identity spans
    /// runtime interruption and resumption; Goal run identity does not.
    /// </summary>
    public sealed class KLEPIntentionRecordSnapshot
    {
        internal KLEPIntentionRecordSnapshot(
            long intentionSequence,
            string intentionId,
            string goalStableId,
            string rootTenureId,
            KLEPIntentionStatus status,
            long adoptedAgentTickOrdinal,
            long adoptedCoreCycleIndex,
            int adoptedWaveIndex,
            long latestGoalRunIndex,
            long lastTransitionAgentTickOrdinal,
            long lastTransitionCoreCycleIndex,
            int lastTransitionWaveIndex,
            KLEPIntentionTransitionReason lastTransitionReason,
            KLEPExecutableExitReason? lastExecutableExitReason,
            string relatedExecutableStableId,
            string relatedRootTenureId)
        {
            if (intentionSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(intentionSequence));
            }

            if (!Enum.IsDefined(typeof(KLEPIntentionStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (!Enum.IsDefined(
                    typeof(KLEPIntentionTransitionReason),
                    lastTransitionReason))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lastTransitionReason));
            }

            ValidateBoundary(
                adoptedAgentTickOrdinal,
                adoptedCoreCycleIndex,
                adoptedWaveIndex);
            ValidateBoundary(
                lastTransitionAgentTickOrdinal,
                lastTransitionCoreCycleIndex,
                lastTransitionWaveIndex);
            if (latestGoalRunIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(latestGoalRunIndex));
            }

            IntentionSequence = intentionSequence;
            IntentionId = RequireText(intentionId, nameof(intentionId));
            GoalStableId = RequireText(goalStableId, nameof(goalStableId));
            RootTenureId = RequireText(rootTenureId, nameof(rootTenureId));
            Status = status;
            AdoptedAgentTickOrdinal = adoptedAgentTickOrdinal;
            AdoptedCoreCycleIndex = adoptedCoreCycleIndex;
            AdoptedWaveIndex = adoptedWaveIndex;
            LatestGoalRunIndex = latestGoalRunIndex;
            LastTransitionAgentTickOrdinal = lastTransitionAgentTickOrdinal;
            LastTransitionCoreCycleIndex = lastTransitionCoreCycleIndex;
            LastTransitionWaveIndex = lastTransitionWaveIndex;
            LastTransitionReason = lastTransitionReason;
            LastExecutableExitReason = lastExecutableExitReason;
            RelatedExecutableStableId = relatedExecutableStableId ?? string.Empty;
            RelatedRootTenureId = relatedRootTenureId ?? string.Empty;
        }

        public long IntentionSequence { get; }
        public string IntentionId { get; }
        public string GoalStableId { get; }
        public string RootTenureId { get; }
        public KLEPIntentionStatus Status { get; }
        public long AdoptedAgentTickOrdinal { get; }
        public long AdoptedCoreCycleIndex { get; }
        public int AdoptedWaveIndex { get; }
        public long LatestGoalRunIndex { get; }
        public long LastTransitionAgentTickOrdinal { get; }
        public long LastTransitionCoreCycleIndex { get; }
        public int LastTransitionWaveIndex { get; }
        public KLEPIntentionTransitionReason LastTransitionReason { get; }
        public KLEPExecutableExitReason? LastExecutableExitReason { get; }
        public string RelatedExecutableStableId { get; }
        public string RelatedRootTenureId { get; }
        public bool IsOpen => Status == KLEPIntentionStatus.Active ||
            Status == KLEPIntentionStatus.Suspended;

        private static void ValidateBoundary(
            long agentTickOrdinal,
            long coreCycleIndex,
            int waveIndex)
        {
            if (agentTickOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(agentTickOrdinal));
            }

            if (coreCycleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(coreCycleIndex));
            }

            if (waveIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }
        }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Intention evidence requires a non-empty identity.",
                    parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// One ordered, immutable change to an adopted intention.
    /// </summary>
    public sealed class KLEPIntentionTransition
    {
        internal KLEPIntentionTransition(
            long transitionSequence,
            string intentionId,
            string goalStableId,
            string rootTenureId,
            KLEPIntentionTransitionKind kind,
            KLEPIntentionStatus? priorStatus,
            KLEPIntentionStatus status,
            KLEPIntentionTransitionReason reason,
            long agentTickOrdinal,
            long coreCycleIndex,
            int waveIndex,
            long goalRunIndex,
            KLEPExecutableExitReason? executableExitReason,
            string relatedExecutableStableId,
            string relatedRootTenureId)
        {
            if (transitionSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(transitionSequence));
            }

            if (!Enum.IsDefined(typeof(KLEPIntentionTransitionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (priorStatus.HasValue &&
                !Enum.IsDefined(typeof(KLEPIntentionStatus), priorStatus.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(priorStatus));
            }

            if (!Enum.IsDefined(typeof(KLEPIntentionStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (!Enum.IsDefined(typeof(KLEPIntentionTransitionReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }

            if (agentTickOrdinal < 0 || coreCycleIndex < 0 ||
                waveIndex < 0 || goalRunIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(agentTickOrdinal),
                    "Intention transition clocks and run identity must be valid.");
            }

            TransitionSequence = transitionSequence;
            IntentionId = RequireText(intentionId, nameof(intentionId));
            GoalStableId = RequireText(goalStableId, nameof(goalStableId));
            RootTenureId = RequireText(rootTenureId, nameof(rootTenureId));
            Kind = kind;
            PriorStatus = priorStatus;
            Status = status;
            Reason = reason;
            AgentTickOrdinal = agentTickOrdinal;
            CoreCycleIndex = coreCycleIndex;
            WaveIndex = waveIndex;
            GoalRunIndex = goalRunIndex;
            ExecutableExitReason = executableExitReason;
            RelatedExecutableStableId = relatedExecutableStableId ?? string.Empty;
            RelatedRootTenureId = relatedRootTenureId ?? string.Empty;
        }

        public long TransitionSequence { get; }
        public string IntentionId { get; }
        public string GoalStableId { get; }
        public string RootTenureId { get; }
        public KLEPIntentionTransitionKind Kind { get; }
        public KLEPIntentionStatus? PriorStatus { get; }
        public KLEPIntentionStatus Status { get; }
        public KLEPIntentionTransitionReason Reason { get; }
        public long AgentTickOrdinal { get; }
        public long CoreCycleIndex { get; }
        public int WaveIndex { get; }
        public long GoalRunIndex { get; }
        public KLEPExecutableExitReason? ExecutableExitReason { get; }
        public string RelatedExecutableStableId { get; }
        public string RelatedRootTenureId { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Intention evidence requires a non-empty identity.",
                    parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// Frozen post-decision view. Terminal records are retained only in the
    /// Tick that retired them; runner trace history supplies bounded history.
    /// </summary>
    public sealed class KLEPIntentionSnapshot
    {
        private readonly ReadOnlyCollection<KLEPIntentionRecordSnapshot>
            openIntentions;
        private readonly ReadOnlyCollection<KLEPIntentionRecordSnapshot>
            retiredIntentions;
        private readonly ReadOnlyCollection<KLEPIntentionTransition> transitions;

        internal static readonly KLEPIntentionSnapshot Empty =
            new KLEPIntentionSnapshot(
                0,
                string.Empty,
                0,
                0,
                string.Empty,
                Array.Empty<KLEPIntentionRecordSnapshot>(),
                Array.Empty<KLEPIntentionRecordSnapshot>(),
                Array.Empty<KLEPIntentionTransition>());

        internal KLEPIntentionSnapshot(
            long revision,
            string ownerNeuronStableId,
            long agentTickOrdinal,
            long coreCycleIndex,
            string activeIntentionId,
            IEnumerable<KLEPIntentionRecordSnapshot> openIntentions,
            IEnumerable<KLEPIntentionRecordSnapshot> retiredIntentions,
            IEnumerable<KLEPIntentionTransition> transitions)
        {
            if (revision < 0 || agentTickOrdinal < 0 || coreCycleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            Revision = revision;
            OwnerNeuronStableId = ownerNeuronStableId ?? string.Empty;
            AgentTickOrdinal = agentTickOrdinal;
            CoreCycleIndex = coreCycleIndex;
            ActiveIntentionId = activeIntentionId ?? string.Empty;
            this.openIntentions = Copy(openIntentions, nameof(openIntentions));
            this.retiredIntentions = Copy(
                retiredIntentions,
                nameof(retiredIntentions));
            this.transitions = Copy(transitions, nameof(transitions));

            int activeCount = 0;
            foreach (KLEPIntentionRecordSnapshot intention in
                     this.openIntentions)
            {
                if (!intention.IsOpen)
                {
                    throw new ArgumentException(
                        "The open intention list contains a terminal record.",
                        nameof(openIntentions));
                }

                if (intention.Status == KLEPIntentionStatus.Active)
                {
                    activeCount++;
                    if (!StringComparer.Ordinal.Equals(
                            intention.IntentionId,
                            ActiveIntentionId))
                    {
                        throw new ArgumentException(
                            "The active intention identity does not match its record.",
                            nameof(activeIntentionId));
                    }
                }
            }

            if (activeCount > 1 ||
                (activeCount == 0 && ActiveIntentionId.Length != 0))
            {
                throw new ArgumentException(
                    "An Intention snapshot may contain at most one active root Solo Goal.",
                    nameof(openIntentions));
            }

            foreach (KLEPIntentionRecordSnapshot intention in
                     this.retiredIntentions)
            {
                if (intention.IsOpen)
                {
                    throw new ArgumentException(
                        "The retired intention list contains an open record.",
                        nameof(retiredIntentions));
                }
            }
        }

        public long Revision { get; }
        public string OwnerNeuronStableId { get; }
        public long AgentTickOrdinal { get; }
        public long CoreCycleIndex { get; }
        public string ActiveIntentionId { get; }
        public IReadOnlyList<KLEPIntentionRecordSnapshot> OpenIntentions =>
            openIntentions;
        public IReadOnlyList<KLEPIntentionRecordSnapshot> RetiredIntentions =>
            retiredIntentions;
        public IReadOnlyList<KLEPIntentionTransition> Transitions => transitions;
        public bool HasActiveIntention => ActiveIntentionId.Length != 0;

        private static ReadOnlyCollection<T> Copy<T>(
            IEnumerable<T> source,
            string parameterName)
            where T : class
        {
            var copy = new List<T>();
            if (source != null)
            {
                foreach (T item in source)
                {
                    copy.Add(item ?? throw new ArgumentException(
                        "Intention evidence cannot contain null.",
                        parameterName));
                }
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }

    /// <summary>
    /// Agent-owned post-decision ledger for already-authored root Solo Goals.
    /// It observes committed lifecycle evidence and has no selection surface.
    /// </summary>
    public sealed class KLEPIntentionState
    {
        private static readonly StringComparer IdComparer = StringComparer.Ordinal;
        private readonly string ownerNeuronStableId;
        private List<MutableIntention> open = new List<MutableIntention>();
        private long nextIntentionSequence;
        private long nextTransitionSequence;
        private long revision;

        public KLEPIntentionSnapshot Snapshot { get; private set; } =
            KLEPIntentionSnapshot.Empty;

        internal KLEPIntentionState(string ownerNeuronStableId)
        {
            if (string.IsNullOrWhiteSpace(ownerNeuronStableId))
            {
                throw new ArgumentException(
                    "Intention state requires its owning Neuron identity.",
                    nameof(ownerNeuronStableId));
            }

            this.ownerNeuronStableId = ownerNeuronStableId;
            Snapshot = new KLEPIntentionSnapshot(
                0,
                ownerNeuronStableId,
                0,
                0,
                string.Empty,
                Array.Empty<KLEPIntentionRecordSnapshot>(),
                Array.Empty<KLEPIntentionRecordSnapshot>(),
                Array.Empty<KLEPIntentionTransition>());
        }

        internal KLEPIntentionSnapshot Observe(
            long agentTickOrdinal,
            KLEPDecisionTrace decision)
        {
            if (agentTickOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(agentTickOrdinal));
            }

            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            var working = Clone(open);
            var retired = new List<MutableIntention>();
            var transitions = new List<KLEPIntentionTransition>();
            long workingIntentionSequence = nextIntentionSequence;
            long workingTransitionSequence = nextTransitionSequence;
            KLEPExecutableStructuralMap map =
                decision.StructuralMap?.ActiveAssessment;

            RetireDisappearedIntentionsBeforeExecution(
                working,
                retired,
                transitions,
                decision,
                map,
                agentTickOrdinal,
                ref workingTransitionSequence);

            for (int stepIndex = 0;
                 stepIndex < decision.Executions.Count;
                 stepIndex++)
            {
                KLEPExecutableStepTrace step = decision.Executions[stepIndex];
                if (step.Kind != KLEPExecutableStepKind.Solo &&
                    step.Kind != KLEPExecutableStepKind.Cancellation)
                {
                    continue;
                }

                if (step.Kind == KLEPExecutableStepKind.Cancellation)
                {
                    MutableIntention cancelledIntention = FindOpenByGoalAndRun(
                        working,
                        step.ExecutableStableId,
                        step.Result.RunIndex);
                    if (cancelledIntention != null)
                    {
                        ApplyCancellation(
                            working,
                            retired,
                            transitions,
                            cancelledIntention,
                            step.Result,
                            decision,
                            agentTickOrdinal,
                            ref workingTransitionSequence);
                    }

                    continue;
                }

                bool isRootSoloGoal = TryGetRootSoloGoal(
                    map,
                    step.ExecutableStableId,
                    out KLEPExecutableStructuralNode goalNode);

                if (!isRootSoloGoal)
                {
                    continue;
                }

                string tenureId = goalNode.RootTenureId;
                MutableIntention intention = FindOpen(
                    working,
                    step.ExecutableStableId,
                    tenureId);

                if (intention == null)
                {
                    intention = Adopt(
                        working,
                        transitions,
                        step.Result,
                        tenureId,
                        agentTickOrdinal,
                        ref workingIntentionSequence,
                        ref workingTransitionSequence);
                }
                else if (intention.Status == KLEPIntentionStatus.Suspended)
                {
                    Transition(
                        intention,
                        KLEPIntentionTransitionKind.Resumed,
                        KLEPIntentionStatus.Active,
                        KLEPIntentionTransitionReason.GoalSelected,
                        step.Result,
                        agentTickOrdinal,
                        null,
                        string.Empty,
                        string.Empty,
                        transitions,
                        ref workingTransitionSequence);
                }

                // Output application can fault after Advance returned. The
                // trace deliberately preserves both the attempted result and
                // the later Faulted result for that exact run. Adoption or
                // resumption still happened at the first step, but only the
                // last Solo result is authoritative for terminal Intention
                // state.
                if (HasLaterSoloResultForSameRun(
                        decision.Executions,
                        stepIndex,
                        step.Result))
                {
                    intention.LatestGoalRunIndex = step.Result.RunIndex;
                    continue;
                }

                ApplySoloResult(
                    working,
                    retired,
                    transitions,
                    intention,
                    step.Result,
                    agentTickOrdinal,
                    ref workingTransitionSequence);
            }

            RetireAnyRemainingDisappearedIntentions(
                working,
                retired,
                transitions,
                decision,
                map,
                agentTickOrdinal,
                ref workingTransitionSequence);

            EnsureOnlyOneActive(working);
            if (revision == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The Intention-state revision is exhausted.");
            }

            long nextRevision = revision + 1;
            KLEPIntentionSnapshot next = Capture(
                nextRevision,
                agentTickOrdinal,
                decision.CycleIndex,
                working,
                retired,
                transitions);

            open = working;
            nextIntentionSequence = workingIntentionSequence;
            nextTransitionSequence = workingTransitionSequence;
            revision = nextRevision;
            Snapshot = next;
            return next;
        }

        private static bool HasLaterSoloResultForSameRun(
            IReadOnlyList<KLEPExecutableStepTrace> executions,
            int currentIndex,
            KLEPExecutionResult result)
        {
            for (int index = currentIndex + 1;
                 index < executions.Count;
                 index++)
            {
                KLEPExecutableStepTrace later = executions[index];
                if (later.Kind == KLEPExecutableStepKind.Solo &&
                    IdComparer.Equals(
                        later.ExecutableStableId,
                        result.ExecutableStableId) &&
                    later.Result.RunIndex == result.RunIndex)
                {
                    return true;
                }
            }

            return false;
        }

        internal KLEPIntentionSnapshot CaptureWithoutTransition(
            long agentTickOrdinal,
            long coreCycleIndex)
        {
            if (agentTickOrdinal < 0 || coreCycleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(agentTickOrdinal));
            }

            Snapshot = Capture(
                revision,
                agentTickOrdinal,
                coreCycleIndex,
                open,
                Array.Empty<MutableIntention>(),
                Array.Empty<KLEPIntentionTransition>());
            return Snapshot;
        }

        private static void RetireDisappearedIntentionsBeforeExecution(
            List<MutableIntention> working,
            List<MutableIntention> retired,
            List<KLEPIntentionTransition> transitions,
            KLEPDecisionTrace decision,
            KLEPExecutableStructuralMap map,
            long agentTickOrdinal,
            ref long transitionSequence)
        {
            if (map == null || !map.IsValid)
            {
                return;
            }

            var candidates = new List<MutableIntention>(working);
            candidates.Sort((left, right) =>
                left.IntentionSequence.CompareTo(right.IntentionSequence));
            foreach (MutableIntention intention in candidates)
            {
                if (IsSameTenureRootSoloGoal(map, intention))
                {
                    continue;
                }

                // A catalog-removal cancellation may itself fault during
                // Exit/Cleanup. Defer retirement whenever the trace contains
                // an exact cancellation for this run so its actual final exit
                // reason wins over the map-difference fallback.
                if (HasCancellationForExactRun(decision, intention))
                {
                    continue;
                }

                AbandonForCatalogChange(
                    working,
                    retired,
                    transitions,
                    intention,
                    decision,
                    map,
                    agentTickOrdinal,
                    ref transitionSequence);
            }
        }

        private static void RetireAnyRemainingDisappearedIntentions(
            List<MutableIntention> working,
            List<MutableIntention> retired,
            List<KLEPIntentionTransition> transitions,
            KLEPDecisionTrace decision,
            KLEPExecutableStructuralMap map,
            long agentTickOrdinal,
            ref long transitionSequence)
        {
            if (map == null || !map.IsValid)
            {
                return;
            }

            var candidates = new List<MutableIntention>(working);
            candidates.Sort((left, right) =>
                left.IntentionSequence.CompareTo(right.IntentionSequence));
            foreach (MutableIntention intention in candidates)
            {
                if (!IsSameTenureRootSoloGoal(map, intention))
                {
                    AbandonForCatalogChange(
                        working,
                        retired,
                        transitions,
                        intention,
                        decision,
                        map,
                        agentTickOrdinal,
                        ref transitionSequence);
                }
            }
        }

        private static void AbandonForCatalogChange(
            List<MutableIntention> working,
            List<MutableIntention> retired,
            List<KLEPIntentionTransition> transitions,
            MutableIntention intention,
            KLEPDecisionTrace decision,
            KLEPExecutableStructuralMap map,
            long agentTickOrdinal,
            ref long transitionSequence)
        {
            bool hasReplacement = map.TryGetExecutable(
                intention.GoalStableId,
                out KLEPExecutableStructuralNode replacement);
            string relatedId = hasReplacement
                ? replacement.StableExecutableId
                : decision.SelectedExecutableId ?? string.Empty;
            string relatedTenure = hasReplacement
                ? replacement.RootTenureId
                : GetRootTenure(map, relatedId);
            TransitionWithoutResult(
                intention,
                KLEPIntentionTransitionKind.Abandoned,
                KLEPIntentionStatus.Abandoned,
                hasReplacement
                    ? KLEPIntentionTransitionReason.RegistrationReplaced
                    : KLEPIntentionTransitionReason.CatalogRemoved,
                agentTickOrdinal,
                decision.CycleIndex,
                decision.KeySnapshot.WaveIndex,
                null,
                relatedId,
                relatedTenure,
                transitions,
                ref transitionSequence);
            working.Remove(intention);
            retired.Add(intention);
        }

        private static MutableIntention Adopt(
            List<MutableIntention> working,
            List<KLEPIntentionTransition> transitions,
            KLEPExecutionResult result,
            string rootTenureId,
            long agentTickOrdinal,
            ref long intentionSequence,
            ref long transitionSequence)
        {
            intentionSequence = CheckedNext(
                intentionSequence,
                "The Intention identity sequence is exhausted.");
            string intentionId = "intention:" +
                intentionSequence.ToString(CultureInfo.InvariantCulture) +
                ":" + result.ExecutableStableId + "@" + rootTenureId;
            var intention = new MutableIntention(
                intentionSequence,
                intentionId,
                result.ExecutableStableId,
                rootTenureId,
                KLEPIntentionStatus.Active,
                agentTickOrdinal,
                result.CycleIndex,
                result.WaveIndex,
                result.RunIndex,
                KLEPIntentionTransitionReason.GoalSelected);
            working.Add(intention);
            transitionSequence = CheckedNext(
                transitionSequence,
                "The Intention transition sequence is exhausted.");
            transitions.Add(new KLEPIntentionTransition(
                transitionSequence,
                intention.IntentionId,
                intention.GoalStableId,
                intention.RootTenureId,
                KLEPIntentionTransitionKind.Adopted,
                null,
                KLEPIntentionStatus.Active,
                KLEPIntentionTransitionReason.GoalSelected,
                agentTickOrdinal,
                result.CycleIndex,
                result.WaveIndex,
                result.RunIndex,
                null,
                string.Empty,
                string.Empty));
            return intention;
        }

        private static void ApplySoloResult(
            List<MutableIntention> working,
            List<MutableIntention> retired,
            List<KLEPIntentionTransition> transitions,
            MutableIntention intention,
            KLEPExecutionResult result,
            long agentTickOrdinal,
            ref long transitionSequence)
        {
            intention.LatestGoalRunIndex = result.RunIndex;
            if (result.State == KLEPExecutableState.Running)
            {
                return;
            }

            KLEPIntentionTransitionKind kind;
            KLEPIntentionStatus status;
            KLEPIntentionTransitionReason reason;
            if (result.State == KLEPExecutableState.Succeeded)
            {
                kind = KLEPIntentionTransitionKind.Completed;
                status = KLEPIntentionStatus.Completed;
                reason = KLEPIntentionTransitionReason.GoalSucceeded;
            }
            else if (result.State == KLEPExecutableState.Failed)
            {
                kind = KLEPIntentionTransitionKind.Abandoned;
                status = KLEPIntentionStatus.Abandoned;
                reason = KLEPIntentionTransitionReason.GoalFailed;
            }
            else if (result.State == KLEPExecutableState.Faulted)
            {
                kind = KLEPIntentionTransitionKind.Abandoned;
                status = KLEPIntentionStatus.Abandoned;
                reason = KLEPIntentionTransitionReason.GoalFaulted;
            }
            else
            {
                return;
            }

            Transition(
                intention,
                kind,
                status,
                reason,
                result,
                agentTickOrdinal,
                result.ExitReason,
                string.Empty,
                string.Empty,
                transitions,
                ref transitionSequence);
            working.Remove(intention);
            retired.Add(intention);
        }

        private static void ApplyCancellation(
            List<MutableIntention> working,
            List<MutableIntention> retired,
            List<KLEPIntentionTransition> transitions,
            MutableIntention intention,
            KLEPExecutionResult result,
            KLEPDecisionTrace decision,
            long agentTickOrdinal,
            ref long transitionSequence)
        {
            if (!result.ExitReason.HasValue)
            {
                return;
            }

            KLEPExecutableExitReason exitReason = result.ExitReason.Value;
            KLEPIntentionTransitionKind kind;
            KLEPIntentionStatus status;
            KLEPIntentionTransitionReason reason;
            switch (exitReason)
            {
                case KLEPExecutableExitReason.Interrupted:
                    kind = KLEPIntentionTransitionKind.Suspended;
                    status = KLEPIntentionStatus.Suspended;
                    reason = KLEPIntentionTransitionReason.GoalInterrupted;
                    break;
                case KLEPExecutableExitReason.LocksClosed:
                    kind = KLEPIntentionTransitionKind.Suspended;
                    status = KLEPIntentionStatus.Suspended;
                    reason = KLEPIntentionTransitionReason.GoalLocksClosed;
                    break;
                case KLEPExecutableExitReason.BelowThreshold:
                    kind = KLEPIntentionTransitionKind.Abandoned;
                    status = KLEPIntentionStatus.Abandoned;
                    reason = KLEPIntentionTransitionReason.GoalBelowThreshold;
                    break;
                case KLEPExecutableExitReason.Removed:
                    kind = KLEPIntentionTransitionKind.Abandoned;
                    status = KLEPIntentionStatus.Abandoned;
                    reason = KLEPIntentionTransitionReason.GoalRemoved;
                    break;
                case KLEPExecutableExitReason.Faulted:
                    kind = KLEPIntentionTransitionKind.Abandoned;
                    status = KLEPIntentionStatus.Abandoned;
                    reason = KLEPIntentionTransitionReason.GoalFaulted;
                    break;
                default:
                    return;
            }

            string relatedId = decision.SelectedExecutableId ?? string.Empty;
            string relatedTenure = GetRootTenure(
                decision.StructuralMap?.ActiveAssessment,
                relatedId);
            Transition(
                intention,
                kind,
                status,
                reason,
                result,
                agentTickOrdinal,
                exitReason,
                relatedId,
                relatedTenure,
                transitions,
                ref transitionSequence);
            if (status == KLEPIntentionStatus.Abandoned)
            {
                working.Remove(intention);
                retired.Add(intention);
            }
        }

        private static void Transition(
            MutableIntention intention,
            KLEPIntentionTransitionKind kind,
            KLEPIntentionStatus status,
            KLEPIntentionTransitionReason reason,
            KLEPExecutionResult result,
            long agentTickOrdinal,
            KLEPExecutableExitReason? exitReason,
            string relatedExecutableStableId,
            string relatedRootTenureId,
            List<KLEPIntentionTransition> transitions,
            ref long transitionSequence)
        {
            TransitionWithoutResult(
                intention,
                kind,
                status,
                reason,
                agentTickOrdinal,
                result.CycleIndex,
                result.WaveIndex,
                exitReason,
                relatedExecutableStableId,
                relatedRootTenureId,
                transitions,
                ref transitionSequence,
                result.RunIndex);
        }

        private static void TransitionWithoutResult(
            MutableIntention intention,
            KLEPIntentionTransitionKind kind,
            KLEPIntentionStatus status,
            KLEPIntentionTransitionReason reason,
            long agentTickOrdinal,
            long coreCycleIndex,
            int waveIndex,
            KLEPExecutableExitReason? exitReason,
            string relatedExecutableStableId,
            string relatedRootTenureId,
            List<KLEPIntentionTransition> transitions,
            ref long transitionSequence,
            long? goalRunIndex = null)
        {
            KLEPIntentionStatus prior = intention.Status;
            long effectiveRun = goalRunIndex ?? intention.LatestGoalRunIndex;
            intention.Status = status;
            intention.LatestGoalRunIndex = effectiveRun;
            intention.LastTransitionAgentTickOrdinal = agentTickOrdinal;
            intention.LastTransitionCoreCycleIndex = coreCycleIndex;
            intention.LastTransitionWaveIndex = waveIndex;
            intention.LastTransitionReason = reason;
            intention.LastExecutableExitReason = exitReason;
            intention.RelatedExecutableStableId =
                relatedExecutableStableId ?? string.Empty;
            intention.RelatedRootTenureId = relatedRootTenureId ?? string.Empty;

            transitionSequence = CheckedNext(
                transitionSequence,
                "The Intention transition sequence is exhausted.");
            transitions.Add(new KLEPIntentionTransition(
                transitionSequence,
                intention.IntentionId,
                intention.GoalStableId,
                intention.RootTenureId,
                kind,
                prior,
                status,
                reason,
                agentTickOrdinal,
                coreCycleIndex,
                waveIndex,
                effectiveRun,
                exitReason,
                relatedExecutableStableId,
                relatedRootTenureId));
        }

        private KLEPIntentionSnapshot Capture(
            long revision,
            long agentTickOrdinal,
            long coreCycleIndex,
            IEnumerable<MutableIntention> open,
            IEnumerable<MutableIntention> retired,
            IEnumerable<KLEPIntentionTransition> transitions)
        {
            var openSnapshots = new List<KLEPIntentionRecordSnapshot>();
            string activeId = string.Empty;
            foreach (MutableIntention intention in open)
            {
                KLEPIntentionRecordSnapshot snapshot = intention.Capture();
                openSnapshots.Add(snapshot);
                if (snapshot.Status == KLEPIntentionStatus.Active)
                {
                    activeId = snapshot.IntentionId;
                }
            }

            openSnapshots.Sort((left, right) =>
                left.IntentionSequence.CompareTo(right.IntentionSequence));
            var retiredSnapshots = new List<KLEPIntentionRecordSnapshot>();
            foreach (MutableIntention intention in retired)
            {
                retiredSnapshots.Add(intention.Capture());
            }

            retiredSnapshots.Sort((left, right) =>
                left.IntentionSequence.CompareTo(right.IntentionSequence));
            return new KLEPIntentionSnapshot(
                revision,
                ownerNeuronStableId,
                agentTickOrdinal,
                coreCycleIndex,
                activeId,
                openSnapshots,
                retiredSnapshots,
                transitions);
        }

        private static bool HasCancellationForExactRun(
            KLEPDecisionTrace decision,
            MutableIntention intention)
        {
            foreach (KLEPExecutableStepTrace step in decision.Executions)
            {
                if (step.Kind == KLEPExecutableStepKind.Cancellation &&
                    IdComparer.Equals(
                        step.ExecutableStableId,
                        intention.GoalStableId) &&
                    step.Result.RunIndex == intention.LatestGoalRunIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameTenureRootSoloGoal(
            KLEPExecutableStructuralMap map,
            MutableIntention intention)
        {
            return TryGetRootSoloGoal(
                       map,
                       intention.GoalStableId,
                       out KLEPExecutableStructuralNode node) &&
                   IdComparer.Equals(
                       node.RootTenureId,
                       intention.RootTenureId);
        }

        private static bool TryGetRootSoloGoal(
            KLEPExecutableStructuralMap map,
            string stableId,
            out KLEPExecutableStructuralNode node)
        {
            node = null;
            return map != null &&
                   map.IsValid &&
                   map.TryGetExecutable(stableId, out node) &&
                   node.IsRoot &&
                   node.IsGoalRecipe &&
                   node.ExecutionMode == KLEPExecutionMode.Solo;
        }

        private static string GetRootTenure(
            KLEPExecutableStructuralMap map,
            string stableId)
        {
            return !string.IsNullOrWhiteSpace(stableId) &&
                   map != null &&
                   map.IsValid &&
                   map.TryGetExecutable(
                       stableId,
                       out KLEPExecutableStructuralNode node) &&
                   node.IsRoot
                ? node.RootTenureId
                : string.Empty;
        }

        private static MutableIntention FindOpen(
            IEnumerable<MutableIntention> intentions,
            string goalStableId,
            string rootTenureId)
        {
            foreach (MutableIntention intention in intentions)
            {
                if (IdComparer.Equals(
                        intention.GoalStableId,
                        goalStableId) &&
                    IdComparer.Equals(
                        intention.RootTenureId,
                        rootTenureId))
                {
                    return intention;
                }
            }

            return null;
        }

        private static MutableIntention FindOpenByGoalAndRun(
            IEnumerable<MutableIntention> intentions,
            string goalStableId,
            long goalRunIndex)
        {
            foreach (MutableIntention intention in intentions)
            {
                if (IdComparer.Equals(
                        intention.GoalStableId,
                        goalStableId) &&
                    intention.LatestGoalRunIndex == goalRunIndex)
                {
                    return intention;
                }
            }

            return null;
        }

        private static List<MutableIntention> Clone(
            IEnumerable<MutableIntention> source)
        {
            var copy = new List<MutableIntention>();
            foreach (MutableIntention intention in source)
            {
                copy.Add(new MutableIntention(intention));
            }

            return copy;
        }

        private static void EnsureOnlyOneActive(
            IEnumerable<MutableIntention> intentions)
        {
            string activeId = null;
            foreach (MutableIntention intention in intentions)
            {
                if (intention.Status != KLEPIntentionStatus.Active)
                {
                    continue;
                }

                if (activeId != null)
                {
                    throw new InvalidOperationException(
                        $"Root Solo intentions '{activeId}' and " +
                        $"'{intention.IntentionId}' are both Active.");
                }

                activeId = intention.IntentionId;
            }
        }

        private static long CheckedNext(long current, string message)
        {
            if (current == long.MaxValue)
            {
                throw new InvalidOperationException(message);
            }

            return current + 1;
        }

        private sealed class MutableIntention
        {
            internal MutableIntention(
                long intentionSequence,
                string intentionId,
                string goalStableId,
                string rootTenureId,
                KLEPIntentionStatus status,
                long adoptedAgentTickOrdinal,
                long adoptedCoreCycleIndex,
                int adoptedWaveIndex,
                long latestGoalRunIndex,
                KLEPIntentionTransitionReason reason)
            {
                IntentionSequence = intentionSequence;
                IntentionId = intentionId;
                GoalStableId = goalStableId;
                RootTenureId = rootTenureId;
                Status = status;
                AdoptedAgentTickOrdinal = adoptedAgentTickOrdinal;
                AdoptedCoreCycleIndex = adoptedCoreCycleIndex;
                AdoptedWaveIndex = adoptedWaveIndex;
                LatestGoalRunIndex = latestGoalRunIndex;
                LastTransitionAgentTickOrdinal = adoptedAgentTickOrdinal;
                LastTransitionCoreCycleIndex = adoptedCoreCycleIndex;
                LastTransitionWaveIndex = adoptedWaveIndex;
                LastTransitionReason = reason;
                RelatedExecutableStableId = string.Empty;
                RelatedRootTenureId = string.Empty;
            }

            internal MutableIntention(MutableIntention source)
            {
                IntentionSequence = source.IntentionSequence;
                IntentionId = source.IntentionId;
                GoalStableId = source.GoalStableId;
                RootTenureId = source.RootTenureId;
                Status = source.Status;
                AdoptedAgentTickOrdinal = source.AdoptedAgentTickOrdinal;
                AdoptedCoreCycleIndex = source.AdoptedCoreCycleIndex;
                AdoptedWaveIndex = source.AdoptedWaveIndex;
                LatestGoalRunIndex = source.LatestGoalRunIndex;
                LastTransitionAgentTickOrdinal =
                    source.LastTransitionAgentTickOrdinal;
                LastTransitionCoreCycleIndex =
                    source.LastTransitionCoreCycleIndex;
                LastTransitionWaveIndex = source.LastTransitionWaveIndex;
                LastTransitionReason = source.LastTransitionReason;
                LastExecutableExitReason = source.LastExecutableExitReason;
                RelatedExecutableStableId = source.RelatedExecutableStableId;
                RelatedRootTenureId = source.RelatedRootTenureId;
            }

            internal long IntentionSequence { get; }
            internal string IntentionId { get; }
            internal string GoalStableId { get; }
            internal string RootTenureId { get; }
            internal KLEPIntentionStatus Status { get; set; }
            internal long AdoptedAgentTickOrdinal { get; }
            internal long AdoptedCoreCycleIndex { get; }
            internal int AdoptedWaveIndex { get; }
            internal long LatestGoalRunIndex { get; set; }
            internal long LastTransitionAgentTickOrdinal { get; set; }
            internal long LastTransitionCoreCycleIndex { get; set; }
            internal int LastTransitionWaveIndex { get; set; }
            internal KLEPIntentionTransitionReason LastTransitionReason
                { get; set; }
            internal KLEPExecutableExitReason? LastExecutableExitReason
                { get; set; }
            internal string RelatedExecutableStableId { get; set; }
            internal string RelatedRootTenureId { get; set; }

            internal KLEPIntentionRecordSnapshot Capture()
            {
                return new KLEPIntentionRecordSnapshot(
                    IntentionSequence,
                    IntentionId,
                    GoalStableId,
                    RootTenureId,
                    Status,
                    AdoptedAgentTickOrdinal,
                    AdoptedCoreCycleIndex,
                    AdoptedWaveIndex,
                    LatestGoalRunIndex,
                    LastTransitionAgentTickOrdinal,
                    LastTransitionCoreCycleIndex,
                    LastTransitionWaveIndex,
                    LastTransitionReason,
                    LastExecutableExitReason,
                    RelatedExecutableStableId,
                    RelatedRootTenureId);
            }
        }
    }
}
