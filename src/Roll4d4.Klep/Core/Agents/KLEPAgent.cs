using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// One decision owner over a passive Neuron. The Agent owns arbitration,
    /// lifecycle progress, confidence learning, and optional Observer use.
    /// </summary>
    public sealed class KLEPAgent
    {
        private static readonly StringComparer IdComparer = StringComparer.Ordinal;
        private readonly KLEPAgentDecisionRuntime decisionRuntime;
        private readonly IKLEPExecutableStructuralObserver structuralObserver;
        private readonly IKLEPCandidateStateProjectionObserver
            candidateStateProjectionObserver;
        private readonly KLEPProjectedSatisfactionPolicy satisfactionPolicy;

        private readonly Dictionary<KLEPKeyEnvironmentSignature, StateRecord> states =
            new Dictionary<KLEPKeyEnvironmentSignature, StateRecord>();
        private readonly Dictionary<RunKey, ActiveRun> activeRuns =
            new Dictionary<RunKey, ActiveRun>();
        private readonly List<PendingOutcome> pendingOutcomes =
            new List<PendingOutcome>();
        private KLEPKeyEnvironmentSignature lastBoundaryEnvironment =
            KLEPKeyEnvironmentSignature.Empty;
        private float lastBoundaryBestEligibleQValue;
        private bool hasBoundaryEnvironment;
        private long lastObservedNeuronCycle;
        private long agentTickOrdinal;
        private bool isTicking;
        private KLEPGuidanceAdvice pendingGuidanceAdvice;
        private KLEPKeyEnvironmentSignature lastConsultedEnvironment;
        private KLEPGuidanceEvidenceFingerprint
            lastConsultedEvidenceFingerprint;
        private readonly List<string> lastConsultedEligibleIds =
            new List<string>();
        private readonly string guidanceObserverStableId;
        private readonly string guidanceObserverVersion;
        private bool hasOpenGuidanceConsultation;

        public KLEPAgent(
            KLEPNeuron neuron,
            KLEPAgentConfiguration configuration = null)
            : this(neuron, configuration, null)
        {
        }

        public KLEPAgent(
            KLEPNeuron neuron,
            KLEPAgentConfiguration configuration,
            IKLEPGuidanceObserver guidanceObserver)
            : this(
                neuron,
                configuration,
                guidanceObserver,
                guidanceObserver as IKLEPExecutableStructuralObserver,
                null)
        {
        }

        public KLEPAgent(
            KLEPNeuron neuron,
            KLEPAgentConfiguration configuration,
            IKLEPGuidanceObserver guidanceObserver,
            IKLEPExecutableStructuralObserver structuralObserver,
            KLEPProjectedSatisfactionPolicy satisfactionPolicy,
            IKLEPCandidateStateProjectionObserver
                candidateStateProjectionObserver = null)
        {
            Neuron = neuron ?? throw new ArgumentNullException(nameof(neuron));
            Configuration = configuration ?? KLEPAgentConfiguration.Default;
            if (guidanceObserver != null)
            {
                string observerStableId = guidanceObserver.StableId;
                string observerVersion = guidanceObserver.Version;
                ValidateStableId(observerStableId, nameof(guidanceObserver));
                ValidateStableId(observerVersion, nameof(guidanceObserver));
                guidanceObserverStableId = observerStableId;
                guidanceObserverVersion = observerVersion;
            }

            GuidanceObserver = guidanceObserver;
            this.structuralObserver = structuralObserver ??
                KLEPBaselineStructuralObserver.Instance;
            this.candidateStateProjectionObserver =
                candidateStateProjectionObserver ??
                structuralObserver as IKLEPCandidateStateProjectionObserver ??
                guidanceObserver as IKLEPCandidateStateProjectionObserver ??
                KLEPBaselineCandidateStateProjectionObserver.Instance;
            this.satisfactionPolicy = satisfactionPolicy;
            ValidateStableId(
                this.structuralObserver.StableId,
                nameof(structuralObserver));
            ValidateStableId(
                this.structuralObserver.Version,
                nameof(structuralObserver));
            ValidateStableId(
                this.candidateStateProjectionObserver.StableId,
                nameof(candidateStateProjectionObserver));
            ValidateStableId(
                this.candidateStateProjectionObserver.Version,
                nameof(candidateStateProjectionObserver));
            Neuron.ClaimDecisionOwner(this);
            decisionRuntime = new KLEPAgentDecisionRuntime(Neuron);
            lastObservedNeuronCycle = Neuron.CycleIndex;
        }

        public KLEPNeuron Neuron { get; }
        public KLEPAgentConfiguration Configuration { get; }
        public IKLEPGuidanceObserver GuidanceObserver { get; }
        public IKLEPExecutableStructuralObserver StructuralObserver =>
            structuralObserver;
        public IKLEPCandidateStateProjectionObserver
            CandidateStateProjectionObserver =>
                candidateStateProjectionObserver;
        public KLEPProjectedSatisfactionPolicy SatisfactionPolicy =>
            satisfactionPolicy;
        public KLEPExecutableStructuralMap ExecutableMap =>
            decisionRuntime.AcceptedStructuralMap;
        public KLEPExecutableStructuralMap LastExecutableMapAttempt =>
            decisionRuntime.LastStructuralMapAttempt;
        public KLEPGuidanceAdvice PendingGuidanceAdvice => pendingGuidanceAdvice;
        public KLEPAgentTickTrace LastTrace { get; private set; } =
            KLEPAgentTickTrace.Empty;
        public string CurrentSoloExecutableId =>
            decisionRuntime.CurrentSoloExecutableId;

        public IReadOnlyList<KLEPExecutableRuntimeSnapshot>
            GetRootExecutableRuntimeSnapshot()
        {
            return decisionRuntime.GetRootExecutableRuntimeSnapshot();
        }

        public KLEPAgentTickTrace Tick()
        {
            if (isTicking)
            {
                throw new InvalidOperationException("A KLEPAgent cannot Tick recursively.");
            }

            if (Neuron.CycleIndex != lastObservedNeuronCycle)
            {
                throw new InvalidOperationException(
                    $"Neuron '{Neuron.StableId}' advanced outside its Agent. " +
                    "Once attached, KLEPAgent.Tick must be its sole Tick path.");
            }

            if (agentTickOrdinal == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The Agent Tick ordinal is exhausted.");
            }

            Neuron.EnterAgentDecisionBoundary(this);
            isTicking = true;
            try
            {
                long neuronCycleBeforeTick = Neuron.CycleIndex;
                KLEPGuidanceAdvice offeredAdvice = pendingGuidanceAdvice;
                pendingGuidanceAdvice = null;
                KLEPDecisionTrace decision;
                try
                {
                    decision = decisionRuntime.Tick(
                        Configuration.ActionCertaintyThreshold,
                        offeredAdvice,
                        structuralObserver,
                        satisfactionPolicy,
                        candidateStateProjectionObserver);
                    agentTickOrdinal++;
                    lastObservedNeuronCycle = decision.CycleIndex;
                }
                catch
                {
                    bool didAdvanceNeuronCycle =
                        Neuron.CycleIndex != neuronCycleBeforeTick;
                    if (didAdvanceNeuronCycle)
                    {
                        agentTickOrdinal++;
                    }

                    lastObservedNeuronCycle = Neuron.CycleIndex;
                    ObserveFaultedExecutions(decisionRuntime.LastTrace);
                    LastTrace = BuildFaultTrace(
                        decisionRuntime.LastTrace);
                    if (!ReferenceEquals(
                            decisionRuntime.LastTrace.KeySnapshot,
                            KLEPKeySnapshot.Empty))
                    {
                        RememberBoundary(LastTrace);
                    }

                    throw;
                }

                KLEPKeyEnvironmentSignature observed =
                    KLEPKeyEnvironmentSignature.FromSnapshot(decision.KeySnapshot);
                KLEPGuidanceEvidenceFingerprint evidenceFingerprint =
                    KLEPGuidanceEvidenceFingerprint.FromSnapshot(
                        decision.KeySnapshot);
                StateRecord state = GetOrCreateState(observed);
                if (state.VisitCount == long.MaxValue)
                {
                    throw new InvalidOperationException(
                        "The Agent environment visit count is exhausted.");
                }

                List<string> eligibleIds = GetEligibleSoloIds(decision);
                List<KLEPAgentLearningUpdate> updates =
                    FinalizePendingOutcomes(state, eligibleIds);

                long priorVisits = state.VisitCount;
                float familiarity = CalculateFamiliarity(priorVisits);
                float bestEligibleQ = GetBestEligibleQ(state, eligibleIds, true);
                float confidence = CalculateConfidence(familiarity, bestEligibleQ);
                bool isNewEnvironment = priorVisits == 0;

                KLEPGuidanceRequest guidanceRequest =
                    confidence <= Configuration.GuidanceConfidenceThreshold
                        ? new KLEPGuidanceRequest(
                            decision.CycleIndex,
                            state.Environment,
                            evidenceFingerprint,
                            confidence,
                            Configuration.GuidanceConfidenceThreshold,
                            isNewEnvironment,
                            eligibleIds)
                        : null;

                QueueCurrentOutcomes(decision, state.Environment);
                state.VisitCount = checked(state.VisitCount + 1);

                KLEPAgentTickTrace completedTrace = new KLEPAgentTickTrace(
                    decision,
                    state.Environment,
                    priorVisits,
                    state.VisitCount,
                    familiarity,
                    bestEligibleQ,
                    confidence,
                    isNewEnvironment,
                    true,
                    updates,
                    false,
                    null,
                    guidanceRequest);

                if (!ShouldConsultObserver(completedTrace))
                {
                    LastTrace = completedTrace;
                    RememberBoundary(LastTrace);
                    return LastTrace;
                }

                RememberGuidanceConsultation(guidanceRequest);
                LastTrace = new KLEPAgentTickTrace(
                    decision,
                    state.Environment,
                    priorVisits,
                    state.VisitCount,
                    familiarity,
                    bestEligibleQ,
                    confidence,
                    isNewEnvironment,
                    true,
                    updates,
                    true,
                    null,
                    guidanceRequest);
                RememberBoundary(LastTrace);

                KLEPGuidanceAdvice prepared = PrepareGuidanceAdvice(LastTrace);
                pendingGuidanceAdvice = prepared;
                LastTrace = new KLEPAgentTickTrace(
                    decision,
                    state.Environment,
                    priorVisits,
                    state.VisitCount,
                    familiarity,
                    bestEligibleQ,
                    confidence,
                    isNewEnvironment,
                    true,
                    updates,
                    true,
                    prepared,
                    guidanceRequest);
                RememberBoundary(LastTrace);
                return LastTrace;
            }
            finally
            {
                isTicking = false;
                Neuron.ExitAgentDecisionBoundary(this);
            }
        }

        internal KLEPAgentTickTrace TickWithPreparedGuidance(
            KLEPGuidanceAdvice guidanceAdvice)
        {
            if (guidanceAdvice == null)
            {
                throw new ArgumentNullException(nameof(guidanceAdvice));
            }

            if (pendingGuidanceAdvice != null)
            {
                throw new InvalidOperationException(
                    "The Agent already has prepared guidance for its next Tick.");
            }

            pendingGuidanceAdvice = guidanceAdvice;
            return Tick();
        }

        public float GetQValue(
            KLEPKeyEnvironmentSignature environment,
            string executableStableId)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            ValidateStableId(executableStableId, nameof(executableStableId));
            return states.TryGetValue(environment, out StateRecord state) &&
                   state.Actions.TryGetValue(executableStableId, out ActionRecord action)
                ? action.QValue
                : 0f;
        }

        public void RequestExecutableRemap()
        {
            if (isTicking)
            {
                throw new InvalidOperationException(
                    "An Executable remap cannot be requested during Agent.Tick.");
            }

            decisionRuntime.RequestStructuralRemap();
        }

        public long GetVisitCount(KLEPKeyEnvironmentSignature environment)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            return states.TryGetValue(environment, out StateRecord state)
                ? state.VisitCount
                : 0;
        }

        public IReadOnlyList<KLEPAgentExperience> GetExperienceSnapshot()
        {
            var snapshot = new List<KLEPAgentExperience>();
            foreach (StateRecord state in states.Values)
            {
                foreach (KeyValuePair<string, ActionRecord> pair in state.Actions)
                {
                    snapshot.Add(new KLEPAgentExperience(
                        state.Environment,
                        pair.Key,
                        pair.Value.QValue,
                        pair.Value.SampleCount));
                }
            }

            snapshot.Sort((left, right) =>
            {
                int environmentComparison =
                    left.Environment.CompareTo(right.Environment);
                return environmentComparison != 0
                    ? environmentComparison
                    : IdComparer.Compare(
                        left.ExecutableStableId,
                        right.ExecutableStableId);
            });
            return new ReadOnlyCollection<KLEPAgentExperience>(snapshot);
        }

        private KLEPGuidanceAdvice PrepareGuidanceAdvice(
            KLEPAgentTickTrace trace)
        {
            if (GuidanceObserver == null || !trace.NeedsGuidance)
            {
                return null;
            }

            IReadOnlyList<KLEPExecutableDefinition> roots =
                Neuron.GetRootExecutableDefinitionsSnapshot();
            var context = new KLEPAgentGuidanceContext(
                trace,
                roots,
                GetExperienceSnapshot(),
                Configuration.ActionCertaintyThreshold);

            ValidateObserverIdentity();
            long cycleBeforeObservation = Neuron.CycleIndex;
            KLEPGuidanceAdvice advice = GuidanceObserver.Observe(context);
            ValidateObserverIdentity();
            if (Neuron.CycleIndex != cycleBeforeObservation)
            {
                throw new InvalidOperationException(
                    "Observer code advanced the Neuron. Observer callbacks " +
                    "must be read-only.");
            }

            if (advice == null)
            {
                return null;
            }

            if (!IdComparer.Equals(
                    advice.ObserverStableId, guidanceObserverStableId) ||
                !IdComparer.Equals(
                    advice.ObserverVersion, guidanceObserverVersion))
            {
                throw new InvalidOperationException(
                    "Observer advice provenance does not match the injected Observer.");
            }

            KLEPGuidanceRequest request = trace.GuidanceRequest;
            if (advice.RequestCycleIndex != request.CycleIndex ||
                !advice.Environment.Equals(request.Environment))
            {
                throw new InvalidOperationException(
                    "Observer advice must retain the exact guidance cycle and environment.");
            }

            advice = advice.BindEvidenceFingerprint(
                request.EvidenceFingerprint);

            bool wasEligible = false;
            for (int index = 0;
                 index < request.EligibleExecutableIds.Count;
                 index++)
            {
                if (IdComparer.Equals(
                        request.EligibleExecutableIds[index],
                        advice.TargetExecutableId))
                {
                    wasEligible = true;
                    break;
                }
            }

            if (!wasEligible)
            {
                throw new InvalidOperationException(
                    "Observer advice may target only an eligible root Solo.");
            }

            KLEPExecutableDefinition target = null;
            for (int index = 0; index < roots.Count; index++)
            {
                if (IdComparer.Equals(
                        roots[index].StableId,
                        advice.TargetExecutableId))
                {
                    target = roots[index];
                    break;
                }
            }

            bool supportedTargetKind = target != null &&
                (target.Kind == KLEPExecutableKind.Action ||
                 target.Kind == KLEPExecutableKind.Goal);
            if (!supportedTargetKind ||
                target.ExecutionMode != KLEPExecutionMode.Solo)
            {
                throw new InvalidOperationException(
                    "Observer advice target is not a registered root Solo " +
                    "Goal or action definition.");
            }

            for (int index = 0; index < advice.PolishedLockIds.Count; index++)
            {
                if (!DefinitionContainsLock(
                        target, advice.PolishedLockIds[index]))
                {
                    throw new InvalidOperationException(
                        $"Observer advice cites unknown Lock " +
                        $"'{advice.PolishedLockIds[index]}' on " +
                        $"'{target.StableId}'.");
                }
            }

            int authoredLockCount = CountDistinctLocks(target);
            if (advice.PolishedLockIds.Count != authoredLockCount)
            {
                throw new InvalidOperationException(
                    $"Observer advice for '{target.StableId}' must cite every " +
                    "authored validation and execution Lock exactly once.");
            }

            CandidateEvaluation targetCandidate = default;
            bool foundTargetCandidate = false;
            for (int index = 0; index < trace.Decision.Candidates.Count; index++)
            {
                CandidateEvaluation candidate = trace.Decision.Candidates[index];
                if (IdComparer.Equals(candidate.StableId, target.StableId))
                {
                    targetCandidate = candidate;
                    foundTargetCandidate = true;
                    break;
                }
            }

            if (!foundTargetCandidate ||
                !targetCandidate.IsEligible ||
                !targetCandidate.Score.HasValue)
            {
                throw new InvalidOperationException(
                    "Observer advice target lost its eligible authored score.");
            }

            double effectiveScore =
                (double)targetCandidate.Score.Value + advice.ScoreDelta;
            if (double.IsNaN(effectiveScore) ||
                double.IsInfinity(effectiveScore) ||
                effectiveScore > float.MaxValue ||
                effectiveScore < -float.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Observer advice for '{target.StableId}' exceeds the " +
                    "finite executable score range.");
            }

            object registrationToken =
                decisionRuntime.GetRootExecutableRegistrationToken(target.StableId);
            if (registrationToken == null)
            {
                throw new InvalidOperationException(
                    $"Observer advice target '{target.StableId}' lost its " +
                    "root registration before advice could be prepared.");
            }

            return advice.BindTargetRegistration(registrationToken);
        }

        private void ValidateObserverIdentity()
        {
            if (!IdComparer.Equals(
                    guidanceObserverStableId,
                    GuidanceObserver.StableId) ||
                !IdComparer.Equals(
                    guidanceObserverVersion,
                    GuidanceObserver.Version))
            {
                throw new InvalidOperationException(
                    $"Guidance Observer '{guidanceObserverStableId}' changed " +
                    "its stable ID or version after Agent registration.");
            }
        }

        private bool ShouldConsultObserver(KLEPAgentTickTrace trace)
        {
            if (!trace.NeedsGuidance)
            {
                ResetGuidanceConsultationEpisode();
                return false;
            }

            if (GuidanceObserver == null)
            {
                return false;
            }

            KLEPGuidanceRequest request = trace.GuidanceRequest;
            return !hasOpenGuidanceConsultation ||
                   !lastConsultedEnvironment.Equals(request.Environment) ||
                   !lastConsultedEvidenceFingerprint.Equals(
                       request.EvidenceFingerprint) ||
                   !SameIds(
                       lastConsultedEligibleIds,
                       request.EligibleExecutableIds) ||
                   trace.LearningUpdates.Count > 0 ||
                   trace.Decision.GuidanceAdvice?.Kind ==
                       KLEPGuidanceAdviceApplicationKind
                           .TargetRegistrationChanged;
        }

        private void RememberGuidanceConsultation(KLEPGuidanceRequest request)
        {
            lastConsultedEnvironment = request.Environment;
            lastConsultedEvidenceFingerprint =
                request.EvidenceFingerprint;
            lastConsultedEligibleIds.Clear();
            for (int index = 0;
                 index < request.EligibleExecutableIds.Count;
                 index++)
            {
                lastConsultedEligibleIds.Add(
                    request.EligibleExecutableIds[index]);
            }

            hasOpenGuidanceConsultation = true;
        }

        private void ResetGuidanceConsultationEpisode()
        {
            lastConsultedEnvironment = null;
            lastConsultedEvidenceFingerprint = null;
            lastConsultedEligibleIds.Clear();
            hasOpenGuidanceConsultation = false;
        }

        private static bool SameIds(
            IReadOnlyList<string> left,
            IReadOnlyList<string> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                if (!IdComparer.Equals(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountDistinctLocks(
            KLEPExecutableDefinition definition)
        {
            var ids = new HashSet<string>(IdComparer);
            for (int index = 0;
                 index < definition.ValidationLocks.Count;
                 index++)
            {
                ids.Add(definition.ValidationLocks[index].StableId);
            }

            for (int index = 0;
                 index < definition.ExecutionLocks.Count;
                 index++)
            {
                ids.Add(definition.ExecutionLocks[index].StableId);
            }

            return ids.Count;
        }

        private static bool DefinitionContainsLock(
            KLEPExecutableDefinition definition,
            string lockId)
        {
            for (int index = 0; index < definition.ValidationLocks.Count; index++)
            {
                if (IdComparer.Equals(
                        definition.ValidationLocks[index].StableId, lockId))
                {
                    return true;
                }
            }

            for (int index = 0; index < definition.ExecutionLocks.Count; index++)
            {
                if (IdComparer.Equals(
                        definition.ExecutionLocks[index].StableId, lockId))
                {
                    return true;
                }
            }

            return false;
        }

        private List<KLEPAgentLearningUpdate> FinalizePendingOutcomes(
            StateRecord nextState,
            IReadOnlyList<string> nextEligibleIds)
        {
            var updates = new List<KLEPAgentLearningUpdate>(pendingOutcomes.Count);
            if (pendingOutcomes.Count == 0)
            {
                return updates;
            }

            pendingOutcomes.Sort(PendingOutcome.Compare);
            float nextBestQ = GetBestEligibleQ(nextState, nextEligibleIds, false);
            var commits = new List<LearningCommit>();
            foreach (PendingOutcome pending in pendingOutcomes)
            {
                if (!states.TryGetValue(
                        pending.Environment, out StateRecord startState))
                {
                    throw new InvalidOperationException(
                        "A pending Agent outcome lost its starting environment.");
                }

                LearningCommit commit = FindOrCreateCommit(
                    commits,
                    startState,
                    pending.ExecutableStableId);
                float previousQ = commit.QValue;
                double discount = Math.Pow(
                    Configuration.DiscountFactor,
                    pending.ElapsedTicks);
                double bootstrapValue = discount * nextBestQ;
                float bootstrap = ToFiniteSingle(
                    bootstrapValue,
                    "The Agent Q bootstrap exceeded its finite range.");
                double targetValue =
                    (double)pending.Reward + bootstrapValue;
                float target = ToFiniteSingle(
                    targetValue,
                    "The Agent Q target exceeded its finite range.");
                double updatedValue = previousQ +
                    (double)Configuration.LearningRate *
                    (targetValue - previousQ);
                float updated = ToFiniteSingle(
                    updatedValue,
                    "The Agent Q update exceeded its finite range.");
                long sampleCount = checked(commit.SampleCount + 1);

                commit.QValue = updated;
                commit.SampleCount = sampleCount;
                updates.Add(new KLEPAgentLearningUpdate(
                    startState.Environment,
                    nextState.Environment,
                    pending.ExecutableStableId,
                    pending.RunIndex,
                    pending.ElapsedTicks,
                    pending.Outcome,
                    pending.Reward,
                    previousQ,
                    nextBestQ,
                    bootstrap,
                    target,
                    updated,
                    sampleCount));
            }

            // No learning state is mutated until every update in the batch has
            // been calculated and validated.
            foreach (LearningCommit commit in commits)
            {
                ActionRecord action = commit.Action ??
                    commit.State.GetOrCreateAction(commit.ExecutableStableId);
                action.QValue = commit.QValue;
                action.SampleCount = commit.SampleCount;
            }

            pendingOutcomes.Clear();
            return updates;
        }

        private void QueueCurrentOutcomes(
            KLEPDecisionTrace decision,
            KLEPKeyEnvironmentSignature environment)
        {
            foreach (KLEPExecutableStepTrace step in decision.Executions)
            {
                KLEPExecutionResult result = step.Result;
                var runKey = new RunKey(result.ExecutableStableId, result.RunIndex);

                if (step.Kind == KLEPExecutableStepKind.Solo)
                {
                    if (!activeRuns.TryGetValue(runKey, out ActiveRun active))
                    {
                        active = new ActiveRun(
                            environment,
                            result.ExecutableStableId,
                            result.RunIndex,
                            agentTickOrdinal);
                        activeRuns.Add(runKey, active);
                    }

                    if (result.State == KLEPExecutableState.Succeeded)
                    {
                        QueueOutcome(
                            active,
                            agentTickOrdinal,
                            KLEPAgentLearningOutcome.Succeeded,
                            Configuration.SuccessReward);
                        activeRuns.Remove(runKey);
                    }
                    else if (result.State == KLEPExecutableState.Failed)
                    {
                        QueueOutcome(
                            active,
                            agentTickOrdinal,
                            KLEPAgentLearningOutcome.Failed,
                            Configuration.FailureReward);
                        activeRuns.Remove(runKey);
                    }
                    else if (result.IsTerminal)
                    {
                        activeRuns.Remove(runKey);
                    }

                    continue;
                }

                if (step.Kind != KLEPExecutableStepKind.Cancellation ||
                    !activeRuns.TryGetValue(runKey, out ActiveRun cancelled))
                {
                    continue;
                }

                if (result.ExitReason == KLEPExecutableExitReason.Interrupted)
                {
                    QueueOutcome(
                        cancelled,
                        agentTickOrdinal,
                        KLEPAgentLearningOutcome.Interrupted,
                        Configuration.InterruptionReward);
                }

                activeRuns.Remove(runKey);
            }
        }

        private void QueueOutcome(
            ActiveRun active,
            long endTickOrdinal,
            KLEPAgentLearningOutcome outcome,
            float reward)
        {
            long elapsedTicks = checked(
                endTickOrdinal - active.StartTickOrdinal + 1);
            if (elapsedTicks <= 0)
            {
                throw new InvalidOperationException(
                    "An Agent run ended before its recorded starting Tick.");
            }

            pendingOutcomes.Add(new PendingOutcome(
                active.Environment,
                active.ExecutableStableId,
                active.RunIndex,
                elapsedTicks,
                outcome,
                reward));
        }

        private KLEPAgentTickTrace BuildFaultTrace(KLEPDecisionTrace decision)
        {
            if (ReferenceEquals(
                    decision.KeySnapshot,
                    KLEPKeySnapshot.Empty) &&
                hasBoundaryEnvironment)
            {
                long retainedVisits = GetVisitCount(lastBoundaryEnvironment);
                float retainedFamiliarity =
                    CalculateFamiliarity(retainedVisits);
                return new KLEPAgentTickTrace(
                    decision,
                    lastBoundaryEnvironment,
                    retainedVisits,
                    retainedVisits,
                    retainedFamiliarity,
                    lastBoundaryBestEligibleQValue,
                    CalculateConfidence(
                        retainedFamiliarity,
                        lastBoundaryBestEligibleQValue),
                    retainedVisits == 0,
                    false,
                    Array.Empty<KLEPAgentLearningUpdate>(),
                    false,
                    null,
                    null);
            }

            KLEPKeyEnvironmentSignature environment =
                KLEPKeyEnvironmentSignature.FromSnapshot(decision.KeySnapshot);
            bool known = states.TryGetValue(environment, out StateRecord state);
            long visits = known ? state.VisitCount : 0;
            List<string> eligibleIds = GetEligibleSoloIds(decision);
            float familiarity = CalculateFamiliarity(visits);
            float bestQ = known
                ? GetBestEligibleQ(state, eligibleIds, true)
                : 0f;
            float confidence = CalculateConfidence(familiarity, bestQ);
            return new KLEPAgentTickTrace(
                decision,
                known ? state.Environment : environment,
                visits,
                visits,
                familiarity,
                bestQ,
                confidence,
                !known,
                false,
                Array.Empty<KLEPAgentLearningUpdate>(),
                false,
                null,
                null);
        }

        private void RememberBoundary(KLEPAgentTickTrace trace)
        {
            lastBoundaryEnvironment = trace.Environment;
            lastBoundaryBestEligibleQValue = trace.BestEligibleQValue;
            hasBoundaryEnvironment = true;
        }

        private void ObserveFaultedExecutions(KLEPDecisionTrace decision)
        {
            foreach (KLEPExecutableStepTrace step in decision.Executions)
            {
                KLEPExecutionResult result = step.Result;
                var runKey = new RunKey(
                    result.ExecutableStableId,
                    result.RunIndex);

                if (step.Kind == KLEPExecutableStepKind.Cancellation &&
                    activeRuns.TryGetValue(runKey, out ActiveRun cancelled))
                {
                    // A challenger can fault after the Neuron has already
                    // committed this interruption. Preserve that valid sample;
                    // the faulting challenger itself remains unsampled.
                    if (result.ExitReason == KLEPExecutableExitReason.Interrupted)
                    {
                        QueueOutcome(
                            cancelled,
                            agentTickOrdinal,
                            KLEPAgentLearningOutcome.Interrupted,
                            Configuration.InterruptionReward);
                    }

                    activeRuns.Remove(runKey);
                }
                else if (step.Kind == KLEPExecutableStepKind.Solo &&
                         result.IsTerminal)
                {
                    activeRuns.Remove(runKey);
                }
            }
        }

        private static LearningCommit FindOrCreateCommit(
            List<LearningCommit> commits,
            StateRecord state,
            string executableStableId)
        {
            foreach (LearningCommit candidate in commits)
            {
                if (ReferenceEquals(candidate.State, state) &&
                    IdComparer.Equals(
                        candidate.ExecutableStableId,
                        executableStableId))
                {
                    return candidate;
                }
            }

            state.Actions.TryGetValue(
                executableStableId, out ActionRecord existing);
            var created = new LearningCommit(
                state,
                executableStableId,
                existing,
                existing?.QValue ?? 0f,
                existing?.SampleCount ?? 0);
            commits.Add(created);
            return created;
        }

        private float CalculateFamiliarity(long priorVisits)
        {
            if (priorVisits <= 0)
            {
                return 0f;
            }

            return priorVisits / (priorVisits + Configuration.FamiliarityScale);
        }

        private float CalculateConfidence(float familiarity, float bestEligibleQ)
        {
            float normalized = bestEligibleQ <= 0f
                ? 0f
                : bestEligibleQ / Configuration.PositiveQBound;
            if (normalized > 1f)
            {
                normalized = 1f;
            }

            return familiarity * normalized;
        }

        private static List<string> GetEligibleSoloIds(KLEPDecisionTrace decision)
        {
            var eligible = new List<string>();
            foreach (CandidateEvaluation candidate in decision.Candidates)
            {
                if (candidate.IsEligible)
                {
                    eligible.Add(candidate.StableId);
                }
            }

            return eligible;
        }

        private static float GetBestEligibleQ(
            StateRecord state,
            IReadOnlyList<string> eligibleIds,
            bool positiveOnly)
        {
            if (eligibleIds.Count == 0)
            {
                return 0f;
            }

            float best = float.NegativeInfinity;
            foreach (string executableId in eligibleIds)
            {
                float qValue = state.Actions.TryGetValue(
                    executableId, out ActionRecord action)
                    ? action.QValue
                    : 0f;
                if (qValue > best)
                {
                    best = qValue;
                }
            }

            return positiveOnly && best < 0f ? 0f : best;
        }

        private StateRecord GetOrCreateState(
            KLEPKeyEnvironmentSignature environment)
        {
            if (!states.TryGetValue(environment, out StateRecord state))
            {
                state = new StateRecord(environment);
                states.Add(environment, state);
            }

            return state;
        }

        private static void ValidateStableId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable Executable ID is required.",
                    parameterName);
            }
        }

        private static float ToFiniteSingle(double value, string message)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value > float.MaxValue ||
                value < -float.MaxValue)
            {
                throw new InvalidOperationException(message);
            }

            return (float)value;
        }

        private sealed class StateRecord
        {
            internal StateRecord(KLEPKeyEnvironmentSignature environment)
            {
                Environment = environment;
            }

            internal KLEPKeyEnvironmentSignature Environment { get; }
            internal long VisitCount { get; set; }
            internal Dictionary<string, ActionRecord> Actions { get; } =
                new Dictionary<string, ActionRecord>(IdComparer);

            internal ActionRecord GetOrCreateAction(string executableStableId)
            {
                if (!Actions.TryGetValue(executableStableId, out ActionRecord action))
                {
                    action = new ActionRecord();
                    Actions.Add(executableStableId, action);
                }

                return action;
            }
        }

        private sealed class ActionRecord
        {
            internal float QValue { get; set; }
            internal long SampleCount { get; set; }
        }

        private sealed class LearningCommit
        {
            internal LearningCommit(
                StateRecord state,
                string executableStableId,
                ActionRecord action,
                float qValue,
                long sampleCount)
            {
                State = state;
                ExecutableStableId = executableStableId;
                Action = action;
                QValue = qValue;
                SampleCount = sampleCount;
            }

            internal StateRecord State { get; }
            internal string ExecutableStableId { get; }
            internal ActionRecord Action { get; }
            internal float QValue { get; set; }
            internal long SampleCount { get; set; }
        }

        private readonly struct RunKey : IEquatable<RunKey>
        {
            internal RunKey(string executableStableId, long runIndex)
            {
                ExecutableStableId = executableStableId;
                RunIndex = runIndex;
            }

            internal string ExecutableStableId { get; }
            internal long RunIndex { get; }
            public bool Equals(RunKey other) =>
                RunIndex == other.RunIndex &&
                IdComparer.Equals(ExecutableStableId, other.ExecutableStableId);
            public override bool Equals(object obj) =>
                obj is RunKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    return (IdComparer.GetHashCode(ExecutableStableId) * 397) ^
                        RunIndex.GetHashCode();
                }
            }
        }

        private sealed class ActiveRun
        {
            internal ActiveRun(
                KLEPKeyEnvironmentSignature environment,
                string executableStableId,
                long runIndex,
                long startTickOrdinal)
            {
                Environment = environment;
                ExecutableStableId = executableStableId;
                RunIndex = runIndex;
                StartTickOrdinal = startTickOrdinal;
            }

            internal KLEPKeyEnvironmentSignature Environment { get; }
            internal string ExecutableStableId { get; }
            internal long RunIndex { get; }
            internal long StartTickOrdinal { get; }
        }

        private sealed class PendingOutcome
        {
            internal PendingOutcome(
                KLEPKeyEnvironmentSignature environment,
                string executableStableId,
                long runIndex,
                long elapsedTicks,
                KLEPAgentLearningOutcome outcome,
                float reward)
            {
                Environment = environment;
                ExecutableStableId = executableStableId;
                RunIndex = runIndex;
                ElapsedTicks = elapsedTicks;
                Outcome = outcome;
                Reward = reward;
            }

            internal KLEPKeyEnvironmentSignature Environment { get; }
            internal string ExecutableStableId { get; }
            internal long RunIndex { get; }
            internal long ElapsedTicks { get; }
            internal KLEPAgentLearningOutcome Outcome { get; }
            internal float Reward { get; }

            internal static int Compare(PendingOutcome left, PendingOutcome right)
            {
                int environmentComparison =
                    left.Environment.CompareTo(right.Environment);
                if (environmentComparison != 0)
                {
                    return environmentComparison;
                }

                int executableComparison = IdComparer.Compare(
                    left.ExecutableStableId,
                    right.ExecutableStableId);
                return executableComparison != 0
                    ? executableComparison
                    : left.RunIndex.CompareTo(right.RunIndex);
            }
        }
    }
}
