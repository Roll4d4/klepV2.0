using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Roll4d4.Klep.Core
{
    public enum KLEPAgentLearningOutcome
    {
        Succeeded,
        Failed,
        Interrupted
    }

    public enum KLEPGuidanceReason
    {
        UnfamiliarEnvironment,
        ConfidenceAtOrBelowThreshold
    }

    public sealed class KLEPAgentLearningUpdate
    {
        internal KLEPAgentLearningUpdate(
            KLEPKeyEnvironmentSignature environment,
            KLEPKeyEnvironmentSignature nextEnvironment,
            string executableStableId,
            long runIndex,
            long elapsedTicks,
            KLEPAgentLearningOutcome outcome,
            float reward,
            float previousQValue,
            float nextStateBestEligibleQValue,
            float discountedBootstrapValue,
            float targetQValue,
            float updatedQValue,
            long sampleCount)
        {
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            NextEnvironment = nextEnvironment ??
                throw new ArgumentNullException(nameof(nextEnvironment));
            ExecutableStableId = executableStableId ??
                throw new ArgumentNullException(nameof(executableStableId));
            RunIndex = runIndex;
            ElapsedTicks = elapsedTicks;
            Outcome = outcome;
            Reward = reward;
            PreviousQValue = previousQValue;
            NextStateBestEligibleQValue = nextStateBestEligibleQValue;
            DiscountedBootstrapValue = discountedBootstrapValue;
            TargetQValue = targetQValue;
            UpdatedQValue = updatedQValue;
            SampleCount = sampleCount;
        }

        public KLEPKeyEnvironmentSignature Environment { get; }
        public KLEPKeyEnvironmentSignature NextEnvironment { get; }
        public string ExecutableStableId { get; }
        public long RunIndex { get; }
        public long ElapsedTicks { get; }
        public KLEPAgentLearningOutcome Outcome { get; }
        public float Reward { get; }
        public float PreviousQValue { get; }
        public float NextStateBestEligibleQValue { get; }
        public float DiscountedBootstrapValue { get; }
        public float TargetQValue { get; }
        public float UpdatedQValue { get; }
        public long SampleCount { get; }
    }

    public sealed class KLEPAgentExperience
    {
        internal KLEPAgentExperience(
            KLEPKeyEnvironmentSignature environment,
            string executableStableId,
            float qValue,
            long sampleCount)
        {
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            ExecutableStableId = executableStableId ??
                throw new ArgumentNullException(nameof(executableStableId));
            QValue = qValue;
            SampleCount = sampleCount;
        }

        public KLEPKeyEnvironmentSignature Environment { get; }
        public string ExecutableStableId { get; }
        public float QValue { get; }
        public long SampleCount { get; }
    }

    public sealed class KLEPGuidanceRequest
    {
        private readonly ReadOnlyCollection<string> eligibleExecutableIds;

        internal KLEPGuidanceRequest(
            long cycleIndex,
            KLEPKeyEnvironmentSignature environment,
            KLEPGuidanceEvidenceFingerprint evidenceFingerprint,
            float confidence,
            float threshold,
            bool isNewEnvironment,
            IEnumerable<string> eligibleExecutableIds)
        {
            CycleIndex = cycleIndex;
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            EvidenceFingerprint = evidenceFingerprint ??
                throw new ArgumentNullException(nameof(evidenceFingerprint));
            Confidence = confidence;
            Threshold = threshold;
            IsNewEnvironment = isNewEnvironment;
            Reason = isNewEnvironment
                ? KLEPGuidanceReason.UnfamiliarEnvironment
                : KLEPGuidanceReason.ConfidenceAtOrBelowThreshold;

            var copy = new List<string>();
            if (eligibleExecutableIds != null)
            {
                foreach (string executableId in eligibleExecutableIds)
                {
                    copy.Add(executableId ?? throw new ArgumentException(
                        "Eligible Executable IDs cannot contain null.",
                        nameof(eligibleExecutableIds)));
                }
            }

            this.eligibleExecutableIds = new ReadOnlyCollection<string>(copy);
        }

        public long CycleIndex { get; }
        public KLEPKeyEnvironmentSignature Environment { get; }
        public KLEPGuidanceEvidenceFingerprint EvidenceFingerprint { get; }
        public float Confidence { get; }
        public float Threshold { get; }
        public bool IsNewEnvironment { get; }
        public KLEPGuidanceReason Reason { get; }
        public IReadOnlyList<string> EligibleExecutableIds => eligibleExecutableIds;
    }

    /// <summary>
    /// One immutable Observer polish prepared from a completed guidance request.
    /// The Agent offers it to the following Agent Tick exactly once.
    /// </summary>
    public sealed class KLEPGuidanceAdvice
    {
        private readonly ReadOnlyCollection<string> polishedLockIds;

        public KLEPGuidanceAdvice(
            string observerStableId,
            string observerVersion,
            long requestCycleIndex,
            KLEPKeyEnvironmentSignature environment,
            string targetExecutableId,
            IEnumerable<string> polishedLockIds,
            float scoreDelta)
            : this(
                observerStableId,
                observerVersion,
                requestCycleIndex,
                environment,
                targetExecutableId,
                polishedLockIds,
                scoreDelta,
                null,
                null)
        {
        }

        public KLEPGuidanceAdvice(
            string observerStableId,
            string observerVersion,
            long requestCycleIndex,
            KLEPKeyEnvironmentSignature environment,
            string targetExecutableId,
            IEnumerable<string> polishedLockIds,
            float scoreDelta,
            KLEPGuidanceEvidenceFingerprint evidenceFingerprint)
            : this(
                observerStableId,
                observerVersion,
                requestCycleIndex,
                environment,
                targetExecutableId,
                polishedLockIds,
                scoreDelta,
                evidenceFingerprint ?? throw new ArgumentNullException(
                    nameof(evidenceFingerprint)),
                null)
        {
        }

        private KLEPGuidanceAdvice(
            string observerStableId,
            string observerVersion,
            long requestCycleIndex,
            KLEPKeyEnvironmentSignature environment,
            string targetExecutableId,
            IEnumerable<string> polishedLockIds,
            float scoreDelta,
            KLEPGuidanceEvidenceFingerprint evidenceFingerprint,
            object targetRegistrationToken)
        {
            ObserverStableId = RequireId(
                observerStableId, nameof(observerStableId));
            ObserverVersion = RequireId(
                observerVersion, nameof(observerVersion));
            if (requestCycleIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestCycleIndex));
            }

            RequestCycleIndex = requestCycleIndex;
            Environment = environment ??
                throw new ArgumentNullException(nameof(environment));
            TargetExecutableId = RequireId(
                targetExecutableId, nameof(targetExecutableId));
            if (float.IsNaN(scoreDelta) ||
                float.IsInfinity(scoreDelta) ||
                scoreDelta <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(scoreDelta));
            }

            ScoreDelta = scoreDelta;
            var copiedLocks = new List<string>();
            var seenLocks = new HashSet<string>(StringComparer.Ordinal);
            if (polishedLockIds != null)
            {
                foreach (string lockId in polishedLockIds)
                {
                    string validated = RequireId(lockId, nameof(polishedLockIds));
                    if (!seenLocks.Add(validated))
                    {
                        throw new ArgumentException(
                            $"Lock ID '{validated}' is polished more than once.",
                            nameof(polishedLockIds));
                    }

                    copiedLocks.Add(validated);
                }
            }

            this.polishedLockIds =
                new ReadOnlyCollection<string>(copiedLocks);
            EvidenceFingerprint = evidenceFingerprint;
            TargetRegistrationToken = targetRegistrationToken;
        }

        public string ObserverStableId { get; }
        public string ObserverVersion { get; }
        public long RequestCycleIndex { get; }
        public KLEPKeyEnvironmentSignature Environment { get; }
        public string TargetExecutableId { get; }
        public IReadOnlyList<string> PolishedLockIds => polishedLockIds;
        public float ScoreDelta { get; }
        public KLEPGuidanceEvidenceFingerprint EvidenceFingerprint { get; }
        internal object TargetRegistrationToken { get; }

        internal KLEPGuidanceAdvice BindTargetRegistration(object token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return new KLEPGuidanceAdvice(
                ObserverStableId,
                ObserverVersion,
                RequestCycleIndex,
                Environment,
                TargetExecutableId,
                polishedLockIds,
                ScoreDelta,
                EvidenceFingerprint,
                token);
        }

        internal KLEPGuidanceAdvice BindEvidenceFingerprint(
            KLEPGuidanceEvidenceFingerprint evidenceFingerprint)
        {
            if (evidenceFingerprint == null)
            {
                throw new ArgumentNullException(nameof(evidenceFingerprint));
            }

            if (EvidenceFingerprint != null &&
                !EvidenceFingerprint.Equals(evidenceFingerprint))
            {
                throw new InvalidOperationException(
                    "Observer advice evidence does not match its guidance request.");
            }

            if (EvidenceFingerprint != null)
            {
                return this;
            }

            return new KLEPGuidanceAdvice(
                ObserverStableId,
                ObserverVersion,
                RequestCycleIndex,
                Environment,
                TargetExecutableId,
                polishedLockIds,
                ScoreDelta,
                evidenceFingerprint,
                TargetRegistrationToken);
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable ID is required.", parameterName);
            }

            return value;
        }
    }

    public enum KLEPGuidanceAdviceApplicationKind
    {
        Applied,
        StaleEnvironment,
        StaleEvidence,
        TargetMissing,
        TargetIneligible,
        TargetRegistrationChanged,
        TickFaultedBeforeApplication
    }

    public sealed class KLEPGuidanceAdviceApplicationTrace
    {
        internal KLEPGuidanceAdviceApplicationTrace(
            KLEPGuidanceAdvice advice,
            KLEPGuidanceAdviceApplicationKind kind,
            KLEPKeyEnvironmentSignature observedEnvironment,
            KLEPGuidanceEvidenceFingerprint observedEvidenceFingerprint,
            float? preObserverScore,
            float? effectiveScore)
        {
            Advice = advice ?? throw new ArgumentNullException(nameof(advice));
            Kind = kind;
            ObservedEnvironment = observedEnvironment ??
                throw new ArgumentNullException(nameof(observedEnvironment));
            ObservedEvidenceFingerprint = observedEvidenceFingerprint ??
                throw new ArgumentNullException(nameof(observedEvidenceFingerprint));
            PreObserverScore = preObserverScore;
            EffectiveScore = effectiveScore;
        }

        public KLEPGuidanceAdvice Advice { get; }
        public KLEPGuidanceAdviceApplicationKind Kind { get; }
        public KLEPKeyEnvironmentSignature ObservedEnvironment { get; }
        public KLEPGuidanceEvidenceFingerprint ObservedEvidenceFingerprint { get; }
        public float? PreObserverScore { get; }
        // Retained for source compatibility. A Goal's pre-Observer score may
        // now include a runtime intrinsic-attraction component, so the more
        // precise name is PreObserverScore.
        public float? AuthoredScore => PreObserverScore;
        public float? EffectiveScore { get; }
        public bool WasApplied => Kind == KLEPGuidanceAdviceApplicationKind.Applied;
    }

    /// <summary>
    /// Read-only evidence supplied when the Agent asks higher reasoning for a
    /// direction. This context exposes no Neuron mutation surface; injected
    /// implementations still have a contractual read-only purity obligation.
    /// </summary>
    public sealed class KLEPAgentGuidanceContext
    {
        private readonly ReadOnlyCollection<KLEPExecutableDefinition> rootExecutables;
        private readonly ReadOnlyCollection<KLEPAgentExperience> experiences;

        internal KLEPAgentGuidanceContext(
            KLEPAgentTickTrace trace,
            IEnumerable<KLEPExecutableDefinition> rootExecutables,
            IEnumerable<KLEPAgentExperience> experiences,
            float actionCertaintyThreshold)
        {
            Trace = trace ?? throw new ArgumentNullException(nameof(trace));
            if (!Trace.NeedsGuidance)
            {
                throw new ArgumentException(
                    "An Observer context requires a guidance request.",
                    nameof(trace));
            }

            if (float.IsNaN(actionCertaintyThreshold) ||
                float.IsInfinity(actionCertaintyThreshold))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actionCertaintyThreshold));
            }

            ActionCertaintyThreshold = actionCertaintyThreshold;
            this.rootExecutables = CopyDefinitions(rootExecutables);
            this.experiences = CopyExperiences(experiences);
        }

        public KLEPAgentTickTrace Trace { get; }
        public KLEPGuidanceRequest Request => Trace.GuidanceRequest;
        public IReadOnlyList<KLEPExecutableDefinition> RootExecutables =>
            rootExecutables;
        public IReadOnlyList<KLEPAgentExperience> Experiences => experiences;
        public float ActionCertaintyThreshold { get; }

        private static ReadOnlyCollection<KLEPExecutableDefinition> CopyDefinitions(
            IEnumerable<KLEPExecutableDefinition> source)
        {
            var copy = new List<KLEPExecutableDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (source != null)
            {
                foreach (KLEPExecutableDefinition definition in source)
                {
                    if (definition == null)
                    {
                        throw new ArgumentException(
                            "Root Executable definitions cannot contain null.",
                            nameof(source));
                    }

                    if (!ids.Add(definition.StableId))
                    {
                        throw new ArgumentException(
                            $"Root Executable ID '{definition.StableId}' occurs more than once.",
                            nameof(source));
                    }

                    copy.Add(definition);
                }
            }

            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.StableId, right.StableId));
            return new ReadOnlyCollection<KLEPExecutableDefinition>(copy);
        }

        private static ReadOnlyCollection<KLEPAgentExperience> CopyExperiences(
            IEnumerable<KLEPAgentExperience> source)
        {
            var copy = new List<KLEPAgentExperience>();
            if (source != null)
            {
                foreach (KLEPAgentExperience experience in source)
                {
                    copy.Add(experience ?? throw new ArgumentException(
                        "Agent experiences cannot contain null.", nameof(source)));
                }
            }

            return new ReadOnlyCollection<KLEPAgentExperience>(copy);
        }
    }

    public enum KLEPStructuralMapTrigger
    {
        None,
        InitialCatalog,
        RevisionChanged,
        ExplicitRemap,
        UnchangedReuse,
        RegistrationRollbackRecovery
    }

    public enum KLEPStructuralMapDisposition
    {
        NotReached,
        Accepted,
        Reused,
        Rejected,
        Faulted
    }

    /// <summary>
    /// Frozen fault evidence for the structural-Observer boundary. This is
    /// deliberately separate from Executable lifecycle faults because catalog
    /// assessment neither selects nor advances an Executable.
    /// </summary>
    public sealed class KLEPStructuralMapFaultTrace
    {
        internal KLEPStructuralMapFaultTrace(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            ExceptionType = exception.GetType().FullName ??
                exception.GetType().Name;
            Message = exception.Message ?? string.Empty;
        }

        public string ExceptionType { get; }
        public string Message { get; }
    }

    /// <summary>
    /// One immutable account of the structural assessment used by a decision
    /// Tick. The requested snapshot, attempted assessment, and retained active
    /// assessment are frozen independently so a later remap cannot rewrite
    /// historical evidence.
    /// </summary>
    public sealed class KLEPStructuralMapDecisionTrace
    {
        internal static readonly KLEPStructuralMapDecisionTrace Empty =
            new KLEPStructuralMapDecisionTrace(
                string.Empty,
                string.Empty,
                KLEPStructuralMapTrigger.None,
                KLEPStructuralMapDisposition.NotReached,
                null,
                null,
                null,
                false,
                null);

        internal KLEPStructuralMapDecisionTrace(
            string observerStableId,
            string observerVersion,
            KLEPStructuralMapTrigger trigger,
            KLEPStructuralMapDisposition disposition,
            KLEPExecutableCatalogSnapshot requestedCatalog,
            KLEPExecutableStructuralMap attemptedAssessment,
            KLEPExecutableStructuralMap activeAssessment,
            bool rejectedCatalogProposal,
            KLEPStructuralMapFaultTrace fault)
        {
            ObserverStableId = observerStableId ?? string.Empty;
            ObserverVersion = observerVersion ?? string.Empty;
            Trigger = trigger;
            Disposition = disposition;
            RequestedCatalog = requestedCatalog;
            AttemptedAssessment = attemptedAssessment;
            ActiveAssessment = activeAssessment;
            RejectedCatalogProposal = rejectedCatalogProposal;
            Fault = fault;

            if (activeAssessment != null && !activeAssessment.IsValid)
            {
                throw new ArgumentException(
                    "A retained active structural assessment must be valid.",
                    nameof(activeAssessment));
            }

            if (disposition == KLEPStructuralMapDisposition.Faulted)
            {
                if (fault == null)
                {
                    throw new ArgumentException(
                        "A faulted structural-map trace requires fault evidence.",
                        nameof(fault));
                }
            }
            else if (fault != null)
            {
                throw new ArgumentException(
                    "Only a faulted structural-map trace may retain a fault.",
                    nameof(fault));
            }

            if (rejectedCatalogProposal &&
                disposition != KLEPStructuralMapDisposition.Rejected)
            {
                throw new ArgumentException(
                    "A rejected catalog proposal requires a rejected disposition.",
                    nameof(rejectedCatalogProposal));
            }
        }

        public string ObserverStableId { get; }
        public string ObserverVersion { get; }
        public KLEPStructuralMapTrigger Trigger { get; }
        public KLEPStructuralMapDisposition Disposition { get; }
        public KLEPExecutableCatalogSnapshot RequestedCatalog { get; }
        public KLEPExecutableStructuralMap AttemptedAssessment { get; }
        public KLEPExecutableStructuralMap ActiveAssessment { get; }
        public bool RejectedCatalogProposal { get; }
        public KLEPStructuralMapFaultTrace Fault { get; }
        public bool DidObserve =>
            Disposition != KLEPStructuralMapDisposition.NotReached &&
            Disposition != KLEPStructuralMapDisposition.Reused;
        public bool DidReuse =>
            Disposition == KLEPStructuralMapDisposition.Reused;
        public string ProposedRevision =>
            RequestedCatalog?.ProposedCatalogRevision ?? string.Empty;
        public KLEPStructuralMapFingerprint ProposedFingerprint =>
            RequestedCatalog?.Fingerprint;
        public string ActiveRevision =>
            ActiveAssessment?.Snapshot.ProposedCatalogRevision ?? string.Empty;
        public KLEPStructuralMapFingerprint ActiveFingerprint =>
            ActiveAssessment?.Fingerprint;
    }

    /// <summary>
    /// Core dependency-inversion seam for a separate Observer module.
    /// Returning null means that higher reasoning abstained.
    /// </summary>
    public interface IKLEPGuidanceObserver
    {
        string StableId { get; }
        string Version { get; }
        KLEPGuidanceAdvice Observe(KLEPAgentGuidanceContext context);
    }

    public sealed class KLEPAgentTickTrace
    {
        private readonly ReadOnlyCollection<KLEPAgentLearningUpdate> learningUpdates;

        internal static readonly KLEPAgentTickTrace Empty = new KLEPAgentTickTrace(
            KLEPDecisionTrace.Empty,
            KLEPIntentionSnapshot.Empty,
            KLEPKeyEnvironmentSignature.Empty,
            0,
            0,
            0f,
            0f,
            0f,
            true,
            false,
            Array.Empty<KLEPAgentLearningUpdate>(),
            false,
            null,
            null);

        internal KLEPAgentTickTrace(
            KLEPDecisionTrace decision,
            KLEPIntentionSnapshot intentionSnapshot,
            KLEPKeyEnvironmentSignature environment,
            long priorVisitCount,
            long visitCountAfterTick,
            float familiarity,
            float bestEligibleQValue,
            float confidence,
            bool isNewEnvironment,
            bool didCompleteObservation,
            IEnumerable<KLEPAgentLearningUpdate> learningUpdates,
            bool wasObserverConsulted,
            KLEPGuidanceAdvice preparedGuidanceAdvice,
            KLEPGuidanceRequest guidanceRequest)
        {
            Decision = decision ?? throw new ArgumentNullException(nameof(decision));
            IntentionSnapshot = intentionSnapshot ??
                throw new ArgumentNullException(nameof(intentionSnapshot));
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            EvidenceFingerprint =
                KLEPGuidanceEvidenceFingerprint.FromSnapshot(Decision.KeySnapshot);
            PriorVisitCount = priorVisitCount;
            VisitCountAfterTick = visitCountAfterTick;
            Familiarity = familiarity;
            BestEligibleQValue = bestEligibleQValue;
            Confidence = confidence;
            IsNewEnvironment = isNewEnvironment;
            DidCompleteObservation = didCompleteObservation;
            GuidanceRequest = guidanceRequest;
            WasObserverConsulted = wasObserverConsulted;
            PreparedGuidanceAdvice = preparedGuidanceAdvice;
            if (guidanceRequest != null &&
                !guidanceRequest.EvidenceFingerprint.Equals(EvidenceFingerprint))
            {
                throw new ArgumentException(
                    "A guidance request must describe the trace's visible evidence.",
                    nameof(guidanceRequest));
            }
            if (preparedGuidanceAdvice != null && !wasObserverConsulted)
            {
                throw new ArgumentException(
                    "Prepared advice requires an Observer consultation.",
                    nameof(preparedGuidanceAdvice));
            }

            var copy = new List<KLEPAgentLearningUpdate>();
            if (learningUpdates != null)
            {
                foreach (KLEPAgentLearningUpdate update in learningUpdates)
                {
                    copy.Add(update ?? throw new ArgumentException(
                        "Agent learning traces cannot contain null.",
                        nameof(learningUpdates)));
                }
            }

            this.learningUpdates =
                new ReadOnlyCollection<KLEPAgentLearningUpdate>(copy);
        }

        public KLEPDecisionTrace Decision { get; }
        public KLEPIntentionSnapshot IntentionSnapshot { get; }
        public KLEPKeyEnvironmentSignature Environment { get; }
        public KLEPGuidanceEvidenceFingerprint EvidenceFingerprint { get; }
        // VisitCount is the prior-observation N used by the confidence formula.
        public long VisitCount => PriorVisitCount;
        public long PriorVisitCount { get; }
        public long VisitCountAfterTick { get; }
        public float Familiarity { get; }
        public float BestEligibleQValue { get; }
        public float Confidence { get; }
        public bool IsNewEnvironment { get; }
        public bool DidCompleteObservation { get; }
        public IReadOnlyList<KLEPAgentLearningUpdate> LearningUpdates => learningUpdates;
        public bool WasObserverConsulted { get; }
        public KLEPGuidanceAdvice PreparedGuidanceAdvice { get; }
        public KLEPGuidanceRequest GuidanceRequest { get; }
        public bool NeedsGuidance => GuidanceRequest != null;
    }
}
