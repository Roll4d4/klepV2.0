using System;
using System.Collections.Generic;
using System.Reflection;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.LearnedExpectations;
using Roll4d4.Klep.Observer;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        VerifyHolisticDirectionAppliesOnceAndTarnishes();
        VerifyStableIdBreaksHolisticTie();
        VerifyNoEvidenceAndUnsupportedKindsAbstain();
        VerifyPolishCanCrossCertaintyThreshold();
        VerifyEqualPolishRetainsRunningSolo();
        VerifyStrictlyHigherPolishInterruptsRunningSolo();
        VerifyChangedEnvironmentRejectsStaleAdvice();
        VerifyPayloadChangeRejectsStaleAdviceAndReconsults();
        VerifySupportedPayloadEvidenceDrivesAdviceFreshness();
        VerifyIneligibleTargetCannotBeUnlockedByAdvice();
        VerifyInvalidEvidencePublishesNoPartialAdvice();
        VerifyObserverInputsAndOutputsAreImmutable();
        VerifyEquivalentRunsAreDeterministic();
        VerifyEligibleGoalCanWinOverAction();
        VerifyGoalIntrinsicAttractionPrecedesObserverPolish();
        VerifyBlockedTargetIsHiddenAndCannotBeTargeted();
        VerifyZeroAndNegativeDirectionsAbstain();
        VerifySourceRegistrationOrderIsDeterministic();
        VerifyUnchangedLowConfidenceEpisodeConsultsOnce();
        VerifyMissingAdviceTargetIsRejected();
        VerifyReplacementRegistrationRejectsOldAdviceAndReconsults();
        VerifyDuplicateEvidenceFromOneSourceIsRejected();
        VerifyAuthoredScoreAndPolishOverflowBeforeQueueing();
        VerifyAdviceIsConsumedWhenTandemFaultsBeforeSolo();
        VerifyLegacyNullConfigurationConstructorRemainsUnambiguous();
        VerifyEvidenceBoundSelfModelUsesAcceptedAssessment();
        VerifyStructuralDependencyProposalPreservesAuthoredAlternatives();
        VerifyStructuralDependencyDiagnosticsAndGoalOwnership();
        VerifyStructuralReasoningIsPureImmutableAndIdentityBound();
        VerifyExpectationLedgerOwnershipAndOwnerBinding();
        VerifyExpectationEvidenceMathSeparationReplayAndImmutability();
        VerifyExpectationReasoningIsNonAuthoritative();
        VerifyEmpiricalWanderExpectationBesideAuthoredStructure();

        Console.WriteLine($"KLEP Observer smoke passed: {assertions} assertions.");
    }

    private static void VerifyHolisticDirectionAppliesOnceAndTarnishes()
    {
        KLEPKeyDefinition permit = Key("observer.permit");
        KLEPLock alphaLock = Lock("observer.alpha.lock", permit);
        KLEPLock betaLock = Lock("observer.beta.lock", permit);
        var alpha = new ProbeExecutable(
            Definition("observer.alpha", 2f, validationLocks: new[] { alphaLock }),
            KLEPExecutableTickStatus.Succeeded);
        var beta = new ProbeExecutable(
            Definition("observer.beta", 1f, validationLocks: new[] { betaLock }),
            KLEPExecutableTickStatus.Succeeded);
        var lateSource = new StaticEvidenceSource(
            "z.memory",
            new KLEPObserverEvidence("observer.beta", 2f, "remembered success"));
        var earlySource = new StaticEvidenceSource(
            "a.health",
            new KLEPObserverEvidence("observer.alpha", -1f, "current strain"));
        var observer = new KLEPObserver(
            "observer.main",
            "1",
            new IKLEPObserverEvidenceSource[] { lateSource, earlySource },
            new KLEPObserverConfiguration(polishAmount: 2f));
        var neuron = new KLEPNeuron("observer.holistic.neuron");
        neuron.InitializeKey(permit);
        neuron.RegisterExecutable(alpha);
        neuron.RegisterExecutable(beta);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        KLEPAgentTickTrace requesting = agent.Tick();

        Expect(requesting.NeedsGuidance &&
               requesting.Decision.SelectedExecutableId == alpha.StableId,
            "the requesting Tick uses ordinary authored scores before advice exists");
        Expect(requesting.WasObserverConsulted &&
               requesting.PreparedGuidanceAdvice == agent.PendingGuidanceAdvice,
            "Agent trace records the consultation and exact prepared advice");
        Expect(agent.PendingGuidanceAdvice != null &&
               agent.PendingGuidanceAdvice.TargetExecutableId == beta.StableId,
            "holistic evidence prepares beta as the next-Tick direction");
        Expect(observer.LastTrace.ProvidedDirection &&
               observer.LastTrace.Targets.Count == 2 &&
               observer.LastTrace.SelectedExecutableId == beta.StableId,
            "Observer trace exposes considered targets and selected direction");
        KLEPObserverTargetTrace betaTrace = FindTarget(
            observer.LastTrace, beta.StableId);
        Expect(betaTrace.Evidence.Count == 1 &&
               betaTrace.Evidence[0].SourceId == "z.memory" &&
               betaTrace.HolisticValue == 2f,
            "each target retains source provenance and holistic total");
        Expect(agent.PendingGuidanceAdvice.PolishedLockIds.Count == 1 &&
               agent.PendingGuidanceAdvice.PolishedLockIds[0] == betaLock.StableId,
            "advice identifies the authored Lock being polished without mutating it");
        Expect(betaLock.Attractiveness == 0f,
            "polishing leaves authored Lock attractiveness unchanged");

        KLEPAgentTickTrace influenced = agent.Tick();

        Expect(influenced.Decision.GuidanceAdvice != null &&
               influenced.Decision.GuidanceAdvice.WasApplied &&
               influenced.Decision.SelectedExecutableId == beta.StableId,
            "matching next-Tick advice changes ranking and selects beta");
        Expect(influenced.Decision.GuidanceAdvice.AuthoredScore == 1f &&
               influenced.Decision.GuidanceAdvice.EffectiveScore == 3f,
            "application trace separates authored and polished scores");
        Expect(HasObserverComponent(
                influenced.Decision, beta.StableId, "observer.main", 2f),
            "candidate trace contains a distinct Observer score component");
        Expect(agent.PendingGuidanceAdvice == null &&
               lateSource.EvaluationCount == 1 &&
               earlySource.EvaluationCount == 1,
            "positive learned confidence avoids another identical consultation");

        KLEPAgentTickTrace afterUse = agent.Tick();
        Expect(afterUse.Decision.GuidanceAdvice == null,
            "one-use polish tarnishes and is absent from the later Tick");
    }

    private static void VerifyStableIdBreaksHolisticTie()
    {
        var a = new ProbeExecutable(
            Definition("observer.tie.a", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var b = new ProbeExecutable(
            Definition("observer.tie.b", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var source = new StaticEvidenceSource(
            "observer.tie.source",
            new KLEPObserverEvidence(b.StableId, 1f),
            new KLEPObserverEvidence(a.StableId, 1f));
        var observer = new KLEPObserver(
            "observer.tie",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.tie.neuron");
        neuron.RegisterExecutable(b);
        neuron.RegisterExecutable(a);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        agent.Tick();

        Expect(agent.PendingGuidanceAdvice.TargetExecutableId == a.StableId,
            "root stable ID deterministically breaks equal holistic totals");
        Expect(observer.LastTrace.Targets[0].ExecutableStableId == a.StableId &&
               observer.LastTrace.Targets[1].ExecutableStableId == b.StableId,
            "Observer target traces are stable-ID ordered");
    }

    private static void VerifyNoEvidenceAndUnsupportedKindsAbstain()
    {
        var action = new ProbeExecutable(
            Definition("observer.noevidence.action", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var emptyObserver = new KLEPObserver("observer.empty", "1");
        var actionNeuron = new KLEPNeuron("observer.empty.neuron");
        actionNeuron.RegisterExecutable(action);
        var actionAgent = new KLEPAgent(
            actionNeuron,
            configuration: null,
            guidanceObserver: emptyObserver);

        actionAgent.Tick();

        Expect(actionAgent.PendingGuidanceAdvice == null &&
               emptyObserver.LastTrace.AbstentionReason ==
                   KLEPObserverAbstentionReason.NoEvidence,
            "Observer abstains when higher reasoning has no supplied evidence");

        var router = new ProbeExecutable(
            Definition(
                "observer.router",
                1f,
                kind: KLEPExecutableKind.Router),
            KLEPExecutableTickStatus.Succeeded);
        var routerObserver = new KLEPObserver("observer.router.only", "1");
        var routerNeuron = new KLEPNeuron("observer.router.neuron");
        routerNeuron.RegisterExecutable(router);
        var routerAgent = new KLEPAgent(
            routerNeuron,
            configuration: null,
            guidanceObserver: routerObserver);

        routerAgent.Tick();

        Expect(routerAgent.PendingGuidanceAdvice == null &&
               routerObserver.LastTrace.AbstentionReason ==
                   KLEPObserverAbstentionReason.NoEligibleGoalOrAction,
            "root Routers and Sensors are not holistic direction targets");
    }

    private static void VerifyPolishCanCrossCertaintyThreshold()
    {
        var action = new ProbeExecutable(
            Definition("observer.threshold.action", -1f),
            KLEPExecutableTickStatus.Succeeded);
        var source = new StaticEvidenceSource(
            "observer.threshold.need",
            new KLEPObserverEvidence(action.StableId, 1f));
        var observer = new KLEPObserver(
            "observer.threshold",
            "1",
            new[] { source },
            new KLEPObserverConfiguration(polishAmount: 2f));
        var neuron = new KLEPNeuron("observer.threshold.neuron");
        neuron.RegisterExecutable(action);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        KLEPAgentTickTrace patient = agent.Tick();
        KLEPAgentTickTrace directed = agent.Tick();

        Expect(patient.Decision.IsPatient && patient.NeedsGuidance,
            "eligible action below certainty remains patient before guidance");
        Expect(directed.Decision.SelectedExecutableId == action.StableId &&
               directed.Decision.GuidanceAdvice.EffectiveScore == 1f,
            "polish may lift an eligible action above certainty");
    }

    private static void VerifyEqualPolishRetainsRunningSolo()
    {
        var current = new ProbeExecutable(
            Definition("observer.equal.current", 2f),
            KLEPExecutableTickStatus.Running);
        var challenger = new ProbeExecutable(
            Definition("observer.equal.challenger", 1f),
            KLEPExecutableTickStatus.Running);
        var source = new StaticEvidenceSource(
            "observer.equal.source",
            new KLEPObserverEvidence(challenger.StableId, 1f));
        var observer = new KLEPObserver(
            "observer.equal",
            "1",
            new[] { source },
            new KLEPObserverConfiguration(polishAmount: 1f));
        var neuron = new KLEPNeuron("observer.equal.neuron");
        neuron.RegisterExecutable(current);
        neuron.RegisterExecutable(challenger);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        agent.Tick();
        KLEPAgentTickTrace equal = agent.Tick();

        Expect(equal.Decision.GuidanceAdvice.WasApplied &&
               equal.Decision.GuidanceAdvice.EffectiveScore == 2f,
            "challenger receives an effective score equal to current");
        Expect(equal.Decision.SelectedExecutableId == current.StableId &&
               agent.CurrentSoloExecutableId == current.StableId,
            "equal influenced score retains the Running Solo");
    }

    private static void VerifyStrictlyHigherPolishInterruptsRunningSolo()
    {
        var current = new ProbeExecutable(
            Definition("observer.interrupt.current", 1.5f),
            KLEPExecutableTickStatus.Running);
        var challenger = new ProbeExecutable(
            Definition("observer.interrupt.challenger", 1f),
            KLEPExecutableTickStatus.Running);
        var source = new StaticEvidenceSource(
            "observer.interrupt.source",
            new KLEPObserverEvidence(challenger.StableId, 1f));
        var observer = new KLEPObserver(
            "observer.interrupt",
            "1",
            new[] { source },
            new KLEPObserverConfiguration(polishAmount: 1f));
        var neuron = new KLEPNeuron("observer.interrupt.neuron");
        neuron.RegisterExecutable(current);
        neuron.RegisterExecutable(challenger);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        agent.Tick();
        KLEPAgentTickTrace interrupted = agent.Tick();

        Expect(interrupted.Decision.SelectedExecutableId == challenger.StableId &&
               agent.CurrentSoloExecutableId == challenger.StableId,
            "strictly higher influenced challenger interrupts current");
        Expect(HasCancellation(
                interrupted.Decision,
                current.StableId,
                KLEPExecutableExitReason.Interrupted),
            "Observer-driven interruption uses the ordinary lifecycle contract");
    }

    private static void VerifyChangedEnvironmentRejectsStaleAdvice()
    {
        var action = new ProbeExecutable(
            Definition("observer.stale.action", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var source = new StaticEvidenceSource(
            "observer.stale.source",
            new KLEPObserverEvidence(action.StableId, 1f));
        var observer = new KLEPObserver(
            "observer.stale",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.stale.neuron");
        neuron.RegisterExecutable(action);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        agent.Tick();
        neuron.AddKey(Key("observer.stale.novel"));
        KLEPAgentTickTrace changed = agent.Tick();

        Expect(changed.Decision.GuidanceAdvice != null &&
               changed.Decision.GuidanceAdvice.Kind ==
                   KLEPGuidanceAdviceApplicationKind.StaleEnvironment &&
               !changed.Decision.GuidanceAdvice.WasApplied,
            "a changed Key environment rejects prior guidance as stale");
        Expect(!HasObserverComponent(
                changed.Decision, action.StableId, observer.StableId, 1f),
            "stale advice contributes no candidate score");
    }

    private static void VerifyIneligibleTargetCannotBeUnlockedByAdvice()
    {
        KLEPKeyDefinition missing = Key("observer.closed.missing");
        var blocked = new ProbeExecutable(
            Definition(
                "observer.closed.action",
                1f,
                validationLocks: new[] { Lock("observer.closed.lock", missing) }),
            KLEPExecutableTickStatus.Succeeded);
        var neuron = new KLEPNeuron("observer.closed.neuron");
        neuron.RegisterExecutable(blocked);
        var advice = new KLEPGuidanceAdvice(
            "observer.closed",
            "1",
            1,
            KLEPKeyEnvironmentSignature.Empty,
            blocked.StableId,
            new[] { "observer.closed.lock" },
            float.MaxValue);

        var agent = new KLEPAgent(neuron);
        KLEPDecisionTrace trace =
            agent.TickWithPreparedGuidance(advice).Decision;

        Expect(trace.GuidanceAdvice.Kind ==
                   KLEPGuidanceAdviceApplicationKind.TargetIneligible &&
               trace.SelectedExecutableId == null &&
               trace.IsPatient,
            "even extreme polish cannot make a closed-lock action eligible");
        CandidateEvaluation candidate = FindCandidate(trace, blocked.StableId);
        Expect(!candidate.IsEligible && !candidate.Score.HasValue,
            "ineligible candidate remains unscored after rejected advice");
    }

    private static void VerifyPayloadChangeRejectsStaleAdviceAndReconsults()
    {
        KLEPKeyDefinition condition = Key("observer.evidence.condition");
        var running = new ProbeExecutable(
            Definition("observer.evidence.action", 1f),
            KLEPExecutableTickStatus.Running);
        var source = new StaticEvidenceSource(
            "observer.evidence.source",
            new KLEPObserverEvidence(running.StableId, 1f));
        var observer = new KLEPObserver(
            "observer.evidence",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.evidence.neuron");
        neuron.InitializeKey(
            condition,
            Payload("health", 10L),
            sourceId: "observer.evidence.first");
        neuron.RegisterExecutable(running);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        KLEPAgentTickTrace first = agent.Tick();
        KLEPGuidanceAdvice originalAdvice = agent.PendingGuidanceAdvice;
        Expect(originalAdvice != null &&
               originalAdvice.EvidenceFingerprint.Equals(
                   first.EvidenceFingerprint) &&
               source.EvaluationCount == 1,
            "prepared advice is bound to the payload evidence that requested it");
        Expect(first.Decision.KeySnapshot.TryGetFirst(
                condition.Id, out KLEPKeyFact firstFact),
            "payload staleness test observes its initialized Key fact");

        neuron.ReplaceKey(
            firstFact,
            Payload("health", 9L),
            sourceId: "observer.evidence.changed");
        KLEPAgentTickTrace changed = agent.Tick();

        Expect(first.Environment.Equals(changed.Environment) &&
               !first.EvidenceFingerprint.Equals(
                   changed.EvidenceFingerprint),
            "a payload change preserves learning identity while changing guidance evidence");
        Expect(changed.Decision.GuidanceAdvice != null &&
               changed.Decision.GuidanceAdvice.Kind ==
                   KLEPGuidanceAdviceApplicationKind.StaleEvidence &&
               changed.Decision.GuidanceAdvice.Advice == originalAdvice &&
               changed.Decision.GuidanceAdvice.ObservedEvidenceFingerprint.Equals(
                   changed.EvidenceFingerprint) &&
               !changed.Decision.GuidanceAdvice.WasApplied,
            "advice prepared from an older payload is explicitly rejected as stale evidence");
        Expect(!HasObserverComponent(
                   changed.Decision,
                   running.StableId,
                   observer.StableId,
                   1f),
            "stale payload advice contributes no candidate score");
        Expect(changed.WasObserverConsulted &&
               source.EvaluationCount == 2 &&
               agent.PendingGuidanceAdvice != null &&
               agent.PendingGuidanceAdvice.EvidenceFingerprint.Equals(
                   changed.EvidenceFingerprint),
            "changed payload evidence opens one fresh consultation and queues current advice");

        Expect(changed.Decision.KeySnapshot.TryGetFirst(
                condition.Id, out KLEPKeyFact changedFact),
            "changed payload remains available for metadata-only replacement");
        neuron.ReplaceKey(
            changedFact,
            Payload("health", 9L),
            sourceId: "observer.evidence.same-payload-new-authority");
        KLEPAgentTickTrace stable = agent.Tick();
        Expect(stable.Decision.GuidanceAdvice != null &&
               stable.Decision.GuidanceAdvice.WasApplied &&
               !stable.WasObserverConsulted &&
               source.EvaluationCount == 2,
            "fresh advice applies across metadata-only replacement and does not reconsult");
    }

    private static void VerifySupportedPayloadEvidenceDrivesAdviceFreshness()
    {
        const string delimiterRichText =
            "3:a;1:b;:Omega=\u03A9;rocket=\uD83D\uDE80;e\u0301";
        KLEPKeyDefinition condition = Key("observer.evidence.kinds.condition");
        var running = new ProbeExecutable(
            Definition("observer.evidence.kinds.action", 1f),
            KLEPExecutableTickStatus.Running);
        var source = new StaticEvidenceSource(
            "observer.evidence.kinds.source",
            new KLEPObserverEvidence(running.StableId, 1f));
        var observer = new KLEPObserver(
            "observer.evidence.kinds",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.evidence.kinds.neuron");
        KLEPKeyPayload currentPayload = EvidencePayload(
            false,
            long.MinValue,
            double.MinValue,
            string.Empty);
        neuron.InitializeKey(
            condition,
            currentPayload,
            sourceId: "observer.evidence.kinds.initial");
        neuron.RegisterExecutable(running);
        var agent = new KLEPAgent(
            neuron,
            configuration: null,
            guidanceObserver: observer);

        KLEPAgentTickTrace first = agent.Tick();
        Expect(first.Decision.KeySnapshot.TryGetFirst(
                condition.Id, out KLEPKeyFact currentFact) &&
               agent.PendingGuidanceAdvice != null &&
               source.EvaluationCount == 1,
            "supported-kind freshness fixture begins with advice bound to rich payload evidence");

        KLEPAgentTickTrace ChangeAndReject(
            KLEPKeyPayload nextPayload,
            string sourceSuffix,
            int expectedEvaluations)
        {
            KLEPGuidanceAdvice offered = agent.PendingGuidanceAdvice;
            neuron.ReplaceKey(
                currentFact,
                nextPayload,
                sourceId: "observer.evidence.kinds." + sourceSuffix);
            KLEPAgentTickTrace changed = agent.Tick();
            Expect(changed.Decision.KeySnapshot.TryGetFirst(
                    condition.Id, out KLEPKeyFact replacement),
                sourceSuffix + " replacement remains visible");
            currentFact = replacement;
            currentPayload = nextPayload;
            Expect(offered != null &&
                   changed.Decision.GuidanceAdvice != null &&
                   changed.Decision.GuidanceAdvice.Advice == offered &&
                   changed.Decision.GuidanceAdvice.Kind ==
                       KLEPGuidanceAdviceApplicationKind.StaleEvidence &&
                   !changed.Decision.GuidanceAdvice.WasApplied &&
                   !HasObserverComponent(
                       changed.Decision,
                       running.StableId,
                       observer.StableId,
                       1f) &&
                   changed.WasObserverConsulted &&
                   source.EvaluationCount == expectedEvaluations &&
                   agent.PendingGuidanceAdvice != null &&
                   agent.PendingGuidanceAdvice.EvidenceFingerprint.Equals(
                       changed.EvidenceFingerprint),
                sourceSuffix + " payload evidence rejects stale advice before score influence and reconsults once");
            return changed;
        }

        ChangeAndReject(
            EvidencePayload(
                true,
                long.MinValue,
                double.MinValue,
                string.Empty),
            "boolean",
            2);
        ChangeAndReject(
            EvidencePayload(
                true,
                long.MaxValue,
                double.MinValue,
                string.Empty),
            "integer-max",
            3);
        ChangeAndReject(
            EvidencePayload(
                true,
                long.MaxValue,
                double.MaxValue,
                string.Empty),
            "number-max",
            4);
        KLEPAgentTickTrace negativeZero = ChangeAndReject(
            EvidencePayload(
                true,
                long.MaxValue,
                BitConverter.Int64BitsToDouble(long.MinValue),
                string.Empty),
            "number-negative-zero",
            5);

        KLEPGuidanceAdvice zeroAdvice = agent.PendingGuidanceAdvice;
        neuron.ReplaceKey(
            currentFact,
            EvidencePayload(
                true,
                long.MaxValue,
                0d,
                string.Empty),
            sourceId: "observer.evidence.kinds.number-positive-zero");
        KLEPAgentTickTrace positiveZero = agent.Tick();
        Expect(positiveZero.Decision.KeySnapshot.TryGetFirst(
                condition.Id, out currentFact) &&
               negativeZero.EvidenceFingerprint.Equals(
                   positiveZero.EvidenceFingerprint) &&
               negativeZero.EvidenceFingerprint.GetHashCode() ==
                   positiveZero.EvidenceFingerprint.GetHashCode() &&
               positiveZero.Decision.GuidanceAdvice != null &&
               positiveZero.Decision.GuidanceAdvice.Advice == zeroAdvice &&
               positiveZero.Decision.GuidanceAdvice.WasApplied &&
               !positiveZero.WasObserverConsulted &&
               source.EvaluationCount == 5,
            "signed-zero reissue keeps advice fresh and does not reopen consultation");
        currentPayload = EvidencePayload(
            true,
            long.MaxValue,
            0d,
            string.Empty);

        neuron.ReplaceKey(
            currentFact,
            EvidencePayload(
                true,
                long.MaxValue,
                0d,
                delimiterRichText),
            sourceId: "observer.evidence.kinds.text-delimited");
        KLEPAgentTickTrace textConsulted = agent.Tick();
        Expect(textConsulted.Decision.KeySnapshot.TryGetFirst(
                condition.Id, out currentFact) &&
               !positiveZero.EvidenceFingerprint.Equals(
                   textConsulted.EvidenceFingerprint) &&
               textConsulted.Decision.GuidanceAdvice == null &&
               textConsulted.WasObserverConsulted &&
               source.EvaluationCount == 6 &&
               agent.PendingGuidanceAdvice != null,
            "delimiter-rich Unicode text changes evidence identity and opens one fresh consultation");

        KLEPAgentTickTrace changedText = ChangeAndReject(
            EvidencePayload(
                true,
                long.MaxValue,
                0d,
                delimiterRichText + "!"),
            "text-delimited-changed",
            7);

        KLEPGuidanceAdvice oneFactAdvice = agent.PendingGuidanceAdvice;
        neuron.AddKey(
            condition,
            currentPayload,
            sourceId: "observer.evidence.kinds.duplicate-added");
        KLEPAgentTickTrace duplicateAdded = agent.Tick();
        IReadOnlyList<KLEPKeyFact> duplicateFacts =
            duplicateAdded.Decision.KeySnapshot.FindAll(condition.Id);
        Expect(duplicateFacts.Count == 2 &&
               duplicateAdded.Environment.Equals(changedText.Environment) &&
               !duplicateAdded.EvidenceFingerprint.Equals(
                   changedText.EvidenceFingerprint) &&
               duplicateAdded.Decision.GuidanceAdvice != null &&
               duplicateAdded.Decision.GuidanceAdvice.Advice == oneFactAdvice &&
               duplicateAdded.Decision.GuidanceAdvice.Kind ==
                   KLEPGuidanceAdviceApplicationKind.StaleEvidence &&
               duplicateAdded.WasObserverConsulted &&
               source.EvaluationCount == 8,
            "adding one duplicate payload occurrence invalidates advice without splitting the presence-only environment");

        KLEPGuidanceAdvice twoFactAdvice = agent.PendingGuidanceAdvice;
        Expect(neuron.RemoveKey(duplicateFacts[1]),
            "duplicate freshness fixture removes one exact occurrence");
        KLEPAgentTickTrace duplicateRemoved = agent.Tick();
        Expect(duplicateRemoved.Decision.KeySnapshot.TryGetFirst(
                condition.Id, out currentFact) &&
               duplicateRemoved.Decision.KeySnapshot.FindAll(condition.Id).Count == 1 &&
               duplicateRemoved.EvidenceFingerprint.Equals(
                   changedText.EvidenceFingerprint) &&
               duplicateRemoved.Decision.GuidanceAdvice != null &&
               duplicateRemoved.Decision.GuidanceAdvice.Advice == twoFactAdvice &&
               duplicateRemoved.Decision.GuidanceAdvice.Kind ==
                   KLEPGuidanceAdviceApplicationKind.StaleEvidence &&
               duplicateRemoved.WasObserverConsulted &&
               source.EvaluationCount == 9,
            "removing one duplicate occurrence restores canonical evidence but rejects advice prepared for two occurrences");

        KLEPGuidanceAdvice metadataAdvice = agent.PendingGuidanceAdvice;
        KLEPKeyPayload reorderedSamePayload = EvidencePayloadReversed(
            true,
            long.MaxValue,
            0d,
            delimiterRichText + "!");
        neuron.ReplaceKey(
            currentFact,
            reorderedSamePayload,
            lifetime: KLEPKeyLifetime.OneCycle,
            sourceId: "observer.evidence.kinds.metadata-reissue");
        KLEPAgentTickTrace metadataReissue = agent.Tick();
        Expect(metadataReissue.EvidenceFingerprint.Equals(
                   duplicateRemoved.EvidenceFingerprint) &&
               metadataReissue.EvidenceFingerprint.GetHashCode() ==
                   duplicateRemoved.EvidenceFingerprint.GetHashCode() &&
               metadataReissue.Decision.GuidanceAdvice != null &&
               metadataReissue.Decision.GuidanceAdvice.Advice == metadataAdvice &&
               metadataReissue.Decision.GuidanceAdvice.WasApplied &&
               !metadataReissue.WasObserverConsulted &&
               source.EvaluationCount == 9,
            "same payload with reordered authoring, new authority, source, lifetime, and Tick keeps advice fresh");
    }

    private static void VerifyInvalidEvidencePublishesNoPartialAdvice()
    {
        var action = new ProbeExecutable(
            Definition("observer.overflow.action", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var first = new StaticEvidenceSource(
            "observer.overflow.a",
            new KLEPObserverEvidence(action.StableId, float.MaxValue));
        var second = new StaticEvidenceSource(
            "observer.overflow.b",
            new KLEPObserverEvidence(action.StableId, float.MaxValue));
        var observer = new KLEPObserver(
            "observer.overflow",
            "1",
            new[] { second, first });
        var neuron = new KLEPNeuron("observer.overflow.neuron");
        neuron.RegisterExecutable(action);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        Exception caught = Catch(() => agent.Tick());

        Expect(caught is InvalidOperationException &&
               caught.Message.Contains("finite score range"),
            "overflowing holistic evidence fails explicitly");
        Expect(agent.PendingGuidanceAdvice == null &&
               observer.LastTrace == null,
            "faulting higher reasoning publishes no partial advice or trace");
        Expect(agent.LastTrace.Decision.CycleIndex == 1 &&
               agent.LastTrace.NeedsGuidance,
            "the already completed Agent decision remains inspectable");
    }

    private static void VerifyObserverInputsAndOutputsAreImmutable()
    {
        var action = new ProbeExecutable(
            Definition("observer.immutable.action", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var source = new StaticEvidenceSource(
            "observer.immutable.source",
            new KLEPObserverEvidence(action.StableId, 1f));
        var observer = new KLEPObserver(
            "observer.immutable",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.immutable.neuron");
        neuron.RegisterExecutable(action);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        agent.Tick();

        Expect(HasNoPublicSetters(agent.PendingGuidanceAdvice) &&
               HasNoPublicSetters(observer.LastTrace) &&
               HasNoPublicSetters(observer.LastTrace.Targets[0]) &&
               HasNoPublicSetters(observer.LastTrace.Targets[0].Evidence[0]),
            "Observer advice and evidence traces expose no public mutation path");
    }

    private static void VerifyEquivalentRunsAreDeterministic()
    {
        string first = RunDeterministicObserver("observer.repeat.one");
        string second = RunDeterministicObserver("observer.repeat.two");

        Expect(first == second,
            "equivalent evidence and definitions produce identical direction traces");
    }

    private static void VerifyEligibleGoalCanWinOverAction()
    {
        var action = new ProbeExecutable(
            Definition("observer.goal.action", 2f),
            KLEPExecutableTickStatus.Succeeded);
        var goal = new KLEPGoal(
            Definition(
                "observer.goal.goal",
                1f,
                kind: KLEPExecutableKind.Goal));
        var source = new StaticEvidenceSource(
            "observer.goal.memory",
            new KLEPObserverEvidence(action.StableId, 1f, "usable action"),
            new KLEPObserverEvidence(goal.StableId, 4f, "trusted goal"));
        var observer = new KLEPObserver(
            "observer.goal",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.goal.neuron");
        neuron.RegisterExecutable(action);
        neuron.RegisterExecutable(goal);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        KLEPAgentTickTrace trace = agent.Tick();

        Expect(trace.Decision.SelectedExecutableId == action.StableId &&
               agent.PendingGuidanceAdvice.TargetExecutableId == goal.StableId,
            "holistic evidence may direct the next Tick toward an eligible Goal over the locally stronger Action");
        Expect(FindTarget(observer.LastTrace, goal.StableId).Kind ==
                   KLEPExecutableKind.Goal &&
               FindTarget(observer.LastTrace, action.StableId).Kind ==
                   KLEPExecutableKind.Action,
            "Observer trace keeps eligible root Goals and Actions as distinct target kinds");
    }

    private static void VerifyGoalIntrinsicAttractionPrecedesObserverPolish()
    {
        var evaluator = new ProbeGoalAttractionEvaluator(
            "observer.goal-attraction.policy",
            "v2",
            context => new KLEPGoalAttractionEvaluation(
                3f,
                "project need"));
        var goal = new KLEPGoal(
            Definition(
                "observer.goal-attraction.goal",
                1f,
                kind: KLEPExecutableKind.Goal),
            null,
            null,
            evaluator);
        var action = new ProbeExecutable(
            Definition("observer.goal-attraction.action", 2f),
            KLEPExecutableTickStatus.Succeeded);
        var source = new StaticEvidenceSource(
            "observer.goal-attraction.evidence",
            new KLEPObserverEvidence(
                goal.StableId,
                1f,
                "polish the supported goal"));
        var observer = new KLEPObserver(
            "observer.goal-attraction",
            "1",
            new[] { source },
            new KLEPObserverConfiguration(polishAmount: 2f));
        var neuron = new KLEPNeuron("observer.goal-attraction.neuron");
        neuron.RegisterExecutable(action);
        neuron.RegisterExecutable(goal);
        var agent = new KLEPAgent(
            neuron,
            configuration: null,
            guidanceObserver: observer);

        KLEPAgentTickTrace requesting = agent.Tick();
        CandidateEvaluation requestingGoal = FindCandidate(
            requesting.Decision,
            goal.StableId);
        Expect(requestingGoal.ScoreEvaluation.Total == 4f &&
               requestingGoal.ScoreEvaluation.Components.Count == 2 &&
               requestingGoal.ScoreEvaluation.Components[1].Kind ==
                   KLEPExecutableScoreComponentKind.GoalIntrinsicAttraction &&
               !HasObserverComponent(
                   requesting.Decision,
                   goal.StableId,
                   observer.StableId,
                   2f),
            "the requesting Tick contains intrinsic Goal attraction but no premature Observer component");
        Expect(evaluator.EvaluationCount == 1 &&
               agent.PendingGuidanceAdvice != null &&
               agent.PendingGuidanceAdvice.TargetExecutableId == goal.StableId,
            "Observer consultation does not reevaluate Goal attraction while preparing next-Tick advice");

        KLEPAgentTickTrace influenced = agent.Tick();
        CandidateEvaluation influencedGoal = FindCandidate(
            influenced.Decision,
            goal.StableId);
        int intrinsicCount = 0;
        int observerCount = 0;
        foreach (KLEPExecutableScoreComponent component in
                 influencedGoal.ScoreEvaluation.Components)
        {
            if (component.Kind ==
                KLEPExecutableScoreComponentKind.GoalIntrinsicAttraction)
            {
                intrinsicCount++;
            }
            else if (component.Kind ==
                     KLEPExecutableScoreComponentKind.ObserverInfluence)
            {
                observerCount++;
            }
        }

        Expect(influenced.Decision.GuidanceAdvice != null &&
               influenced.Decision.GuidanceAdvice.WasApplied &&
               influenced.Decision.GuidanceAdvice.PreObserverScore == 4f &&
               influenced.Decision.GuidanceAdvice.AuthoredScore == 4f &&
               influenced.Decision.GuidanceAdvice.EffectiveScore == 6f,
            "Observer application reports the complete pre-Observer Goal score and the polished total");
        Expect(evaluator.EvaluationCount == 2 &&
               influencedGoal.ScoreEvaluation.Total == 6f &&
               influencedGoal.ScoreEvaluation.Components.Count == 3 &&
               intrinsicCount == 1 &&
               observerCount == 1 &&
               influencedGoal.ScoreEvaluation.Components[2].Kind ==
                   KLEPExecutableScoreComponentKind.ObserverInfluence,
            "Observer influence is appended exactly once after exactly one intrinsic evaluation for that Tick");

        KLEPKeyDefinition missing = Key(
            "observer.goal-attraction.blocked-key");
        var blockedEvaluator = new ProbeGoalAttractionEvaluator(
            "observer.goal-attraction.blocked-policy",
            "1",
            context => new KLEPGoalAttractionEvaluation(100f));
        var blockedGoal = new KLEPGoal(
            Definition(
                "observer.goal-attraction.blocked",
                1f,
                kind: KLEPExecutableKind.Goal,
                validationLocks: new[]
                {
                    Lock(
                        "observer.goal-attraction.blocked-lock",
                        missing)
                }),
            null,
            null,
            blockedEvaluator);
        var available = new ProbeExecutable(
            Definition("observer.goal-attraction.available", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var eligibleSource = new StaticEvidenceSource(
            "observer.goal-attraction.available-evidence",
            new KLEPObserverEvidence(available.StableId, 1f));
        var blockedObserver = new KLEPObserver(
            "observer.goal-attraction.blocked-observer",
            "1",
            new[] { eligibleSource });
        var blockedNeuron = new KLEPNeuron(
            "observer.goal-attraction.blocked-neuron");
        blockedNeuron.RegisterExecutable(blockedGoal);
        blockedNeuron.RegisterExecutable(available);
        var blockedAgent = new KLEPAgent(
            blockedNeuron,
            configuration: null,
            guidanceObserver: blockedObserver);

        KLEPAgentTickTrace blockedTrace = blockedAgent.Tick();
        Expect(blockedEvaluator.EvaluationCount == 0 &&
               FindCandidate(
                   blockedTrace.Decision,
                   blockedGoal.StableId).ScoreEvaluation == null &&
               blockedObserver.LastTrace.Targets.Count == 1 &&
               blockedObserver.LastTrace.Targets[0].ExecutableStableId ==
                   available.StableId,
            "an ineligible Goal invokes neither intrinsic policy nor Observer evidence targeting");
    }

    private static void VerifyBlockedTargetIsHiddenAndCannotBeTargeted()
    {
        KLEPKeyDefinition missing = Key("observer.hidden.missing");
        var available = new ProbeExecutable(
            Definition("observer.hidden.available", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var blocked = new ProbeExecutable(
            Definition(
                "observer.hidden.blocked",
                10f,
                validationLocks: new[]
                {
                    Lock("observer.hidden.blocked.lock", missing)
                }),
            KLEPExecutableTickStatus.Succeeded);
        var source = new StaticEvidenceSource(
            "observer.hidden.source",
            new KLEPObserverEvidence(blocked.StableId, 100f));
        var observer = new KLEPObserver(
            "observer.hidden",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.hidden.neuron");
        neuron.RegisterExecutable(blocked);
        neuron.RegisterExecutable(available);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        Exception caught = Catch(() => agent.Tick());

        Expect(source.LastEligibleTargetIds.Count == 1 &&
               source.LastEligibleTargetIds[0] == available.StableId,
            "evidence sources receive only eligible root Goal and Action targets");
        Expect(caught is InvalidOperationException &&
               caught.Message.Contains("targeted unavailable Executable") &&
               agent.PendingGuidanceAdvice == null,
            "evidence cannot target a blocked Executable that was excluded from its context");
    }

    private static void VerifyZeroAndNegativeDirectionsAbstain()
    {
        var zero = new ProbeExecutable(
            Definition("observer.nonpositive.zero", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var negative = new ProbeExecutable(
            Definition("observer.nonpositive.negative", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var source = new StaticEvidenceSource(
            "observer.nonpositive.source",
            new KLEPObserverEvidence(zero.StableId, 0f),
            new KLEPObserverEvidence(negative.StableId, -1f));
        var observer = new KLEPObserver(
            "observer.nonpositive",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.nonpositive.neuron");
        neuron.RegisterExecutable(zero);
        neuron.RegisterExecutable(negative);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        KLEPAgentTickTrace trace = agent.Tick();

        Expect(trace.WasObserverConsulted &&
               trace.PreparedGuidanceAdvice == null &&
               agent.PendingGuidanceAdvice == null &&
               observer.LastTrace.AbstentionReason ==
                   KLEPObserverAbstentionReason.NoBeneficialDirection,
            "zero and negative holistic totals produce an explicit abstention");
        Expect(FindTarget(observer.LastTrace, zero.StableId).HolisticValue == 0f &&
               FindTarget(observer.LastTrace, negative.StableId).HolisticValue == -1f,
            "abstention trace preserves both zero and negative totals");
    }

    private static void VerifySourceRegistrationOrderIsDeterministic()
    {
        string forward = RunWithSourceRegistrationOrder(reverse: false);
        string reverse = RunWithSourceRegistrationOrder(reverse: true);

        Expect(forward == reverse,
            "reversing evidence source registration produces the same selected direction and trace order");
    }

    private static string RunWithSourceRegistrationOrder(bool reverse)
    {
        var a = new ProbeExecutable(
            Definition("observer.sourceorder.a", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var b = new ProbeExecutable(
            Definition("observer.sourceorder.b", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var early = new StaticEvidenceSource(
            "observer.sourceorder.a.source",
            new KLEPObserverEvidence(a.StableId, 1f),
            new KLEPObserverEvidence(b.StableId, -1f));
        var late = new StaticEvidenceSource(
            "observer.sourceorder.z.source",
            new KLEPObserverEvidence(a.StableId, 1f),
            new KLEPObserverEvidence(b.StableId, 4f));
        IKLEPObserverEvidenceSource[] sources = reverse
            ? new IKLEPObserverEvidenceSource[] { late, early }
            : new IKLEPObserverEvidenceSource[] { early, late };
        var observer = new KLEPObserver(
            "observer.sourceorder",
            "1",
            sources);
        var neuron = new KLEPNeuron(
            reverse
                ? "observer.sourceorder.reverse.neuron"
                : "observer.sourceorder.forward.neuron");
        neuron.RegisterExecutable(b);
        neuron.RegisterExecutable(a);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        agent.Tick();

        var parts = new List<string>
        {
            observer.LastTrace.SelectedExecutableId,
            agent.PendingGuidanceAdvice.TargetExecutableId
        };
        foreach (KLEPObserverTargetTrace target in observer.LastTrace.Targets)
        {
            parts.Add(target.ExecutableStableId + "=" + target.HolisticValue);
            foreach (KLEPObserverEvidenceTrace evidence in target.Evidence)
            {
                parts.Add(evidence.SourceId + ":" + evidence.Value);
            }
        }

        return string.Join("|", parts);
    }

    private static void VerifyUnchangedLowConfidenceEpisodeConsultsOnce()
    {
        var running = new ProbeExecutable(
            Definition("observer.episode.running", 1f),
            KLEPExecutableTickStatus.Running);
        var source = new StaticEvidenceSource(
            "observer.episode.source",
            new KLEPObserverEvidence(running.StableId, 1f));
        var observer = new KLEPObserver(
            "observer.episode",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.episode.neuron");
        neuron.RegisterExecutable(running);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        KLEPAgentTickTrace first = agent.Tick();
        KLEPAgentTickTrace second = agent.Tick();
        KLEPAgentTickTrace third = agent.Tick();

        Expect(first.NeedsGuidance && second.NeedsGuidance && third.NeedsGuidance,
            "an unchanged Running action continues to emit low-confidence guidance requests");
        Expect(first.WasObserverConsulted &&
               !second.WasObserverConsulted &&
               !third.WasObserverConsulted &&
               source.EvaluationCount == 1,
            "an unchanged low-confidence episode consults higher reasoning only once");
        Expect(second.Decision.GuidanceAdvice != null &&
               second.Decision.GuidanceAdvice.WasApplied &&
               third.Decision.GuidanceAdvice == null,
            "the episode's one prepared polish is offered once and then tarnishes");
    }

    private static void VerifyMissingAdviceTargetIsRejected()
    {
        var available = new ProbeExecutable(
            Definition("observer.missing.available", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var neuron = new KLEPNeuron("observer.missing.neuron");
        neuron.RegisterExecutable(available);
        var advice = new KLEPGuidanceAdvice(
            "observer.missing",
            "1",
            1,
            KLEPKeyEnvironmentSignature.Empty,
            "observer.missing.absent",
            Array.Empty<string>(),
            10f);

        var agent = new KLEPAgent(neuron);
        KLEPDecisionTrace trace =
            agent.TickWithPreparedGuidance(advice).Decision;

        Expect(trace.GuidanceAdvice != null &&
               trace.GuidanceAdvice.Kind ==
                   KLEPGuidanceAdviceApplicationKind.TargetMissing &&
               !trace.GuidanceAdvice.WasApplied,
            "advice for an absent target is rejected explicitly");
        Expect(trace.SelectedExecutableId == available.StableId &&
               !HasObserverComponent(
                   trace,
                   available.StableId,
                   advice.ObserverStableId,
                   advice.ScoreDelta),
            "missing-target advice does not alter ordinary arbitration");
    }

    private static void VerifyDuplicateEvidenceFromOneSourceIsRejected()
    {
        var action = new ProbeExecutable(
            Definition("observer.duplicate.action", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var source = new StaticEvidenceSource(
            "observer.duplicate.source",
            new KLEPObserverEvidence(action.StableId, 1f),
            new KLEPObserverEvidence(action.StableId, 2f));
        var observer = new KLEPObserver(
            "observer.duplicate",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.duplicate.neuron");
        neuron.RegisterExecutable(action);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        Exception caught = Catch(() => agent.Tick());

        Expect(caught is InvalidOperationException &&
               caught.Message.Contains("more than one contribution") &&
               source.EvaluationCount == 1,
            "one evidence source must aggregate to one net contribution per target");
        Expect(agent.PendingGuidanceAdvice == null &&
               observer.LastTrace == null,
            "duplicate evidence publishes neither partial advice nor a partial Observer trace");
    }

    private static void VerifyReplacementRegistrationRejectsOldAdviceAndReconsults()
    {
        const string targetId = "observer.replacement.action";
        var original = new ProbeExecutable(
            Definition(targetId, 1f),
            KLEPExecutableTickStatus.Running);
        var replacement = new ProbeExecutable(
            Definition(targetId, 1f),
            KLEPExecutableTickStatus.Running);
        var source = new StaticEvidenceSource(
            "observer.replacement.source",
            new KLEPObserverEvidence(targetId, 1f));
        var observer = new KLEPObserver(
            "observer.replacement",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.replacement.neuron");
        neuron.RegisterExecutable(original);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        agent.Tick();
        neuron.RemoveExecutable(targetId);
        neuron.RegisterExecutable(replacement);
        KLEPAgentTickTrace changed = agent.Tick();

        Expect(changed.Decision.GuidanceAdvice != null &&
               changed.Decision.GuidanceAdvice.Kind ==
                   KLEPGuidanceAdviceApplicationKind.TargetRegistrationChanged &&
               !HasObserverComponent(
                   changed.Decision, targetId, observer.StableId, 1f),
            "advice bound to a retired registration cannot polish its replacement");
        Expect(changed.WasObserverConsulted &&
               source.EvaluationCount == 2 &&
               agent.PendingGuidanceAdvice != null,
            "a same-ID registration change opens one fresh consultation");
    }

    private static void VerifyAuthoredScoreAndPolishOverflowBeforeQueueing()
    {
        var action = new ProbeExecutable(
            Definition("observer.scoreoverflow.action", float.MaxValue),
            KLEPExecutableTickStatus.Succeeded);
        var source = new StaticEvidenceSource(
            "observer.scoreoverflow.source",
            new KLEPObserverEvidence(action.StableId, 1f));
        var observer = new KLEPObserver(
            "observer.scoreoverflow",
            "1",
            new[] { source },
            new KLEPObserverConfiguration(polishAmount: float.MaxValue));
        var neuron = new KLEPNeuron("observer.scoreoverflow.neuron");
        neuron.RegisterExecutable(action);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        Exception caught = Catch(() => agent.Tick());

        Expect(caught is InvalidOperationException &&
               caught.Message.Contains("finite executable score range"),
            "authored score plus polish overflow is rejected during consultation");
        Expect(agent.PendingGuidanceAdvice == null &&
               agent.LastTrace.WasObserverConsulted &&
               agent.LastTrace.PreparedGuidanceAdvice == null &&
               observer.LastTrace.Advice != null,
            "overflowing advice is rejected before it can enter the Agent's pending queue");
    }

    private static void VerifyAdviceIsConsumedWhenTandemFaultsBeforeSolo()
    {
        var solo = new ProbeExecutable(
            Definition("observer.prefault.solo", 1f),
            KLEPExecutableTickStatus.Running);
        var source = new StaticEvidenceSource(
            "observer.prefault.source",
            new KLEPObserverEvidence(solo.StableId, 1f));
        var observer = new KLEPObserver(
            "observer.prefault",
            "1",
            new[] { source });
        var neuron = new KLEPNeuron("observer.prefault.neuron");
        neuron.RegisterExecutable(solo);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        agent.Tick();
        KLEPGuidanceAdvice prepared = agent.PendingGuidanceAdvice;
        neuron.RegisterExecutable(new FaultingExecutable(
            Definition(
                "observer.prefault.tandem",
                1f,
                executionMode: KLEPExecutionMode.Tandem),
            "observer pre-Solo Tandem fault"));

        Exception caught = Catch(() => agent.Tick());

        Expect(caught is InvalidOperationException &&
               caught.Message.Contains("observer pre-Solo Tandem fault"),
            "faulting Tandem stops the Tick before Solo arbitration");
        Expect(agent.PendingGuidanceAdvice == null &&
               agent.LastTrace.Decision.GuidanceAdvice != null &&
               ReferenceEquals(
                   agent.LastTrace.Decision.GuidanceAdvice.Advice,
                   prepared) &&
               agent.LastTrace.Decision.GuidanceAdvice.Kind ==
                   KLEPGuidanceAdviceApplicationKind.TickFaultedBeforeApplication,
            "offered advice is consumed and traced when a pre-Solo Tandem faults");
        Expect(agent.LastTrace.Decision.Fault != null &&
               agent.LastTrace.Decision.Fault.ExecutableStableId ==
                   "observer.prefault.tandem",
            "the same fault trace identifies the Tandem that prevented advice application");
    }

    private static void VerifyLegacyNullConfigurationConstructorRemainsUnambiguous()
    {
        var agent = new KLEPAgent(
            new KLEPNeuron("observer.compatibility.neuron"), null);

        Expect(agent.GuidanceObserver == null && agent.Configuration != null,
            "legacy null-configuration construction remains source-compatible");
    }

    private static void VerifyEvidenceBoundSelfModelUsesAcceptedAssessment()
    {
        const string primaryId = "observer.self.primary";
        var observer = new KLEPObserver("observer.self", "1");
        var primary = new ProbeExecutable(
            Definition(primaryId, 0f),
            KLEPExecutableTickStatus.Running);
        var neuron = new KLEPNeuron("observer.self.neuron");
        neuron.RegisterExecutable(primary);
        var agent = new KLEPAgent(
            neuron,
            new KLEPAgentConfiguration(actionCertaintyThreshold: 100f),
            observer);

        KLEPAgentTickTrace acceptedTrace = agent.Tick();
        KLEPObserverSelfModel acceptedModel = observer.ObserveSelf(acceptedTrace);
        KLEPExecutableStructuralMap acceptedMap =
            acceptedTrace.Decision.StructuralMap.ActiveAssessment;

        Expect(ReferenceEquals(acceptedModel.AgentTrace, acceptedTrace) &&
               ReferenceEquals(acceptedModel.StructuralMap, acceptedMap) &&
               ReferenceEquals(observer.LastSelfModel, acceptedModel),
            "ObserveSelf retains the exact immutable Agent trace and accepted structural map");
        Expect(acceptedModel.ModelerStableId == observer.StableId &&
               acceptedModel.ModelerVersion == observer.Version &&
               acceptedModel.StructuralObserverStableId == observer.StableId &&
               acceptedModel.StructuralObserverVersion == observer.Version,
            "a self-model records both modeler and accepted structural-Observer provenance");
        Expect(acceptedModel.CycleIndex == acceptedTrace.Decision.CycleIndex &&
               acceptedModel.WaveIndex == acceptedTrace.Decision.KeySnapshot.WaveIndex &&
               acceptedModel.CatalogRevision ==
                   acceptedTrace.Decision.StructuralMap.ActiveRevision &&
               acceptedModel.CatalogFingerprint.Equals(
                   acceptedTrace.Decision.StructuralMap.ActiveFingerprint) &&
               acceptedModel.EvidenceFingerprint.Equals(
                   acceptedTrace.EvidenceFingerprint),
            "a self-model binds the completed cycle, snapshot, accepted revision, graph, and payload evidence");
        Expect(observer.LastReasoningTrace == null,
            "capturing a self-model does not pretend that a reasoning query occurred");

        var duplicateDescendant = new ProbeExecutable(
            Definition(primaryId, 0f),
            KLEPExecutableTickStatus.Running);
        var rejectedGoal = new KLEPGoal(
            Definition(
                "observer.self.rejected-goal",
                0f,
                kind: KLEPExecutableKind.Goal),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AnyCanFire,
                    new KLEPExecutableBase[] { duplicateDescendant })
            });
        neuron.RegisterExecutable(rejectedGoal);

        KLEPAgentTickTrace rejectedTrace = agent.Tick();
        KLEPObserverSelfModel afterRejection = observer.ObserveSelf(rejectedTrace);

        Expect(rejectedTrace.Decision.StructuralMap.RejectedCatalogProposal &&
               rejectedTrace.Decision.StructuralMap.AttemptedAssessment != null &&
               !rejectedTrace.Decision.StructuralMap.AttemptedAssessment.IsValid,
            "the fixture actually supplies an invalid attempted catalog beside retained accepted evidence");
        Expect(ReferenceEquals(
                   afterRejection.StructuralMap,
                   rejectedTrace.Decision.StructuralMap.ActiveAssessment) &&
               !ReferenceEquals(
                   afterRejection.StructuralMap,
                   rejectedTrace.Decision.StructuralMap.AttemptedAssessment) &&
               ReferenceEquals(afterRejection.StructuralMap, acceptedMap),
            "ObserveSelf models the Agent-accepted catalog rather than a rejected attempted map");
        Expect(!afterRejection.StructuralMap.TryGetExecutable(
                   rejectedGoal.StableId,
                   out _),
            "a rejected catalog definition cannot leak into the active self-model");
        Expect(ReferenceEquals(acceptedModel.StructuralMap, acceptedMap) &&
               acceptedModel.CatalogRevision != null,
            "a later capture cannot rewrite an older immutable self-model");
    }

    private static void VerifyStructuralDependencyProposalPreservesAuthoredAlternatives()
    {
        KLEPKeyDefinition source = Key("observer.plan.source");
        KLEPKeyDefinition toolA = Key("observer.plan.tool-a");
        KLEPKeyDefinition toolB = Key("observer.plan.tool-b");
        KLEPKeyDefinition danger = Key("observer.plan.danger");
        KLEPKeyDefinition prepared = Key("observer.plan.prepared");
        KLEPKeyDefinition target = Key("observer.plan.target");

        var prepare = new ProbeExecutable(
            Definition(
                "observer.plan.prepare",
                0f,
                validationLocks: new[]
                {
                    Lock("observer.plan.prepare.source", source)
                },
                declaredOutputs: new[] { prepared }),
            KLEPExecutableTickStatus.Running);
        var finish = new ProbeExecutable(
            Definition(
                "observer.plan.finish",
                0f,
                validationLocks: new[]
                {
                    new KLEPLock(
                        "observer.plan.finish.requirements",
                        "prepared and equipped without danger",
                        new KLEPAll(
                            new KLEPKeyPresent(prepared.Id.Value),
                            new KLEPAny(
                                new KLEPKeyPresent(toolA.Id.Value),
                                new KLEPKeyPresent(toolB.Id.Value)),
                            new KLEPNot(
                                new KLEPKeyPresent(danger.Id.Value))))
                },
                declaredOutputs: new[] { target }),
            KLEPExecutableTickStatus.Running);
        var observer = new KLEPObserver("observer.plan", "1");
        var neuron = new KLEPNeuron("observer.plan.neuron");
        neuron.InitializeKey(source);
        neuron.RegisterExecutable(finish);
        neuron.RegisterExecutable(prepare);
        var agent = new KLEPAgent(
            neuron,
            new KLEPAgentConfiguration(actionCertaintyThreshold: 100f),
            observer);
        KLEPObserverSelfModel model = observer.ObserveSelf(agent.Tick());

        KLEPObserverStructuralDependencyProposal first =
            observer.ProposeStructuralDependencies(model, target.Id);
        string firstSignature = DependencySignature(first);
        KLEPObserverStructuralDependencyProposal second =
            observer.ProposeStructuralDependencies(model, target.Id);

        Expect(firstSignature == DependencySignature(second),
            "identical structural questions over one model produce identical ordered graphs and diagnostics");
        Expect(ReferenceEquals(first.SelfModel, model) &&
               first.TargetKeyId == target.Id &&
               !first.TargetPresentInCurrentEvidence &&
               first.CatalogRevision == model.CatalogRevision &&
               first.CatalogFingerprint.Equals(model.CatalogFingerprint) &&
               first.EvidenceFingerprint.Equals(model.EvidenceFingerprint),
            "a dependency proposal retains its exact target, catalog, and evidence model binding");

        KLEPObserverDependencyNode sourceNode = FindDependencyKeyNode(
            first.Graph, source.Id);
        KLEPObserverDependencyNode targetNode = FindDependencyKeyNode(
            first.Graph, target.Id);
        KLEPObserverDependencyNode finishNode = FindDependencyProducerNode(
            first.Graph, finish.StableId);
        KLEPObserverDependencyNode prepareNode = FindDependencyProducerNode(
            first.Graph, prepare.StableId);
        Expect(sourceNode.IsPresentInCurrentEvidence &&
               !targetNode.IsPresentInCurrentEvidence &&
               finishNode.IsIndependentlySchedulable &&
               prepareNode.IsIndependentlySchedulable,
            "dependency nodes distinguish current evidence from structural producer possibility");
        Expect(HasDependencyEdgeKind(
                   first.Graph, KLEPObserverDependencyEdgeKind.ProducedBy) &&
               HasDependencyEdgeKind(
                   first.Graph, KLEPObserverDependencyEdgeKind.ProducerRequiresLock) &&
               HasDependencyEdgeKind(
                   first.Graph, KLEPObserverDependencyEdgeKind.AllChild) &&
               HasDependencyEdgeKind(
                   first.Graph, KLEPObserverDependencyEdgeKind.AnyChild) &&
               HasDependencyEdgeKind(
                   first.Graph, KLEPObserverDependencyEdgeKind.NotChild) &&
               HasDependencyEdgeKind(
                   first.Graph, KLEPObserverDependencyEdgeKind.RequiresPresentKey) &&
               HasDependencyEdgeKind(
                   first.Graph, KLEPObserverDependencyEdgeKind.RequiresAbsentKey),
            "the proposal preserves producer, Lock, All, Any, Not, presence, and absence relationships");
        Expect(HasReasoningDiagnostic(
                   first.Diagnostics,
                   KLEPObserverReasoningDiagnosticCode.MissingProducer,
                   toolA.Id.Value) &&
               HasReasoningDiagnostic(
                   first.Diagnostics,
                   KLEPObserverReasoningDiagnosticCode.MissingProducer,
                   toolB.Id.Value) &&
               !HasReasoningDiagnostic(
                   first.Diagnostics,
                   KLEPObserverReasoningDiagnosticCode.MissingProducer,
                   source.Id.Value),
            "unmet positive leaves report missing producers while a currently present leaf is evidence-satisfied");
        Expect(HasReasoningDiagnostic(
                   first.Diagnostics,
                   KLEPObserverReasoningDiagnosticCode.NegativeRequirement,
                   danger.Id.Value) &&
               CountDependencyProducerNodesForKey(first.Graph, danger.Id) == 0,
            "a negative prerequisite remains an explicit absence requirement and is not inverted into a production route");
        Expect(ReferenceEquals(observer.LastReasoningTrace.Proposal, second) &&
               observer.LastReasoningTrace.NodeCount == second.Graph.Nodes.Count &&
               observer.LastReasoningTrace.EdgeCount == second.Graph.Edges.Count,
            "the Observer retains an evidence-bound trace of the latest explicit reasoning query");
    }

    private static void VerifyStructuralDependencyDiagnosticsAndGoalOwnership()
    {
        KLEPKeyDefinition a = Key("observer.plan.cycle.a");
        KLEPKeyDefinition b = Key("observer.plan.cycle.b");
        KLEPKeyDefinition childOutput = Key("observer.plan.child-output");
        var produceA = new ProbeExecutable(
            Definition(
                "observer.plan.produce-a",
                0f,
                validationLocks: new[]
                {
                    Lock("observer.plan.require-b", b)
                },
                declaredOutputs: new[] { a }),
            KLEPExecutableTickStatus.Running);
        var produceB = new ProbeExecutable(
            Definition(
                "observer.plan.produce-b",
                0f,
                validationLocks: new[]
                {
                    Lock("observer.plan.require-a", a)
                },
                declaredOutputs: new[] { b }),
            KLEPExecutableTickStatus.Running);
        var child = new ProbeExecutable(
            Definition(
                "observer.plan.goal-child",
                0f,
                declaredOutputs: new[] { childOutput }),
            KLEPExecutableTickStatus.Running);
        var goal = new KLEPGoal(
            Definition(
                "observer.plan.owner-goal",
                0f,
                kind: KLEPExecutableKind.Goal),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { child })
            });
        var observer = new KLEPObserver("observer.plan.diagnostics", "1");
        var neuron = new KLEPNeuron("observer.plan.diagnostics.neuron");
        neuron.RegisterExecutable(produceB);
        neuron.RegisterExecutable(goal);
        neuron.RegisterExecutable(produceA);
        var agent = new KLEPAgent(
            neuron,
            new KLEPAgentConfiguration(actionCertaintyThreshold: 100f),
            observer);
        KLEPObserverSelfModel model = observer.ObserveSelf(agent.Tick());

        KLEPObserverStructuralDependencyProposal cyclic =
            observer.ProposeStructuralDependencies(model, a.Id);
        Expect(HasReasoningDiagnostic(
                   cyclic.Diagnostics,
                   KLEPObserverReasoningDiagnosticCode.DependencyCycle,
                   a.Id.Value) &&
               FindDependencyProducerNode(
                   cyclic.Graph, produceA.StableId) != null &&
               FindDependencyProducerNode(
                   cyclic.Graph, produceB.StableId) != null,
            "cyclic dependencies terminate with an explicit diagnostic while retaining both producer definitions");

        KLEPObserverStructuralDependencyProposal owned =
            observer.ProposeStructuralDependencies(model, childOutput.Id);
        KLEPObserverDependencyNode ownedProducer = FindDependencyProducerNode(
            owned.Graph, child.StableId);
        Expect(!ownedProducer.IsIndependentlySchedulable &&
               ownedProducer.Producer.ParentExecutableId == goal.StableId &&
               HasReasoningDiagnostic(
                   owned.Diagnostics,
                   KLEPObserverReasoningDiagnosticCode
                       .GoalOwnedProducerNotIndependentlySchedulable,
                   child.StableId),
            "a Goal-owned producer remains explanatory evidence and is never promoted to an independent root action");
    }

    private static void VerifyStructuralReasoningIsPureImmutableAndIdentityBound()
    {
        KLEPKeyDefinition permit = Key("observer.plan.purity.permit");
        KLEPKeyDefinition target = Key("observer.plan.purity.target");
        KLEPLock blockedLock = Lock("observer.plan.purity.lock", permit);
        var blocked = new ProbeExecutable(
            Definition(
                "observer.plan.purity.blocked",
                10f,
                validationLocks: new[] { blockedLock },
                declaredOutputs: new[] { target }),
            KLEPExecutableTickStatus.Succeeded);
        var observer = new KLEPObserver("observer.plan.purity", "1");
        var neuron = new KLEPNeuron("observer.plan.purity.neuron");
        neuron.RegisterExecutable(blocked);
        var agent = new KLEPAgent(neuron, null, observer);
        KLEPAgentTickTrace before = agent.Tick();
        KLEPObserverSelfModel model = observer.ObserveSelf(before);
        long cycleBefore = neuron.CycleIndex;
        int factsBefore = before.Decision.KeySnapshot.Facts.Count;

        var emptyObserver = new KLEPObserver("observer.plan.empty", "1");
        Expect(Catch(() => emptyObserver.ProposeStructuralDependencies(target.Id))
                   is InvalidOperationException,
            "a convenience query requires an explicitly captured accepted self-model");
        Expect(Catch(() => emptyObserver.ProposeStructuralDependencies(
                       model, target.Id)) is ArgumentException,
            "an Observer cannot relabel another Observer's self-model as its own reasoning evidence");

        KLEPObserverStructuralDependencyProposal proposal =
            observer.ProposeStructuralDependencies(target.Id);
        Expect(neuron.CycleIndex == cycleBefore &&
               ReferenceEquals(agent.LastTrace, before) &&
               before.Decision.KeySnapshot.Facts.Count == factsBefore &&
               blocked.TickCount == 0 &&
               blockedLock.Attractiveness == 0f,
            "an explicit dependency query advances no Tick and mutates no Key, runtime, or Lock");
        Expect(HasNoPublicSetters(model) &&
               HasNoPublicSetters(proposal) &&
               HasNoPublicSetters(proposal.Graph) &&
               HasNoPublicSetters(proposal.Graph.Nodes[0]) &&
               HasNoPublicSetters(proposal.Graph.Edges[0]) &&
               HasNoPublicSetters(observer.LastReasoningTrace),
            "self-model, proposal, graph, node, edge, and reasoning trace expose no public setters");
        Expect(((IList<KLEPObserverDependencyNode>)proposal.Graph.Nodes).IsReadOnly &&
               ((IList<KLEPObserverDependencyEdge>)proposal.Graph.Edges).IsReadOnly &&
               ((IList<KLEPObserverReasoningDiagnostic>)proposal.Diagnostics).IsReadOnly,
            "dependency proposal collections reject caller mutation");

        KLEPAgentTickTrace after = agent.Tick();
        Expect(after.Decision.SelectedExecutableId == null &&
               blocked.TickCount == 0 &&
               !after.Decision.KeySnapshot.Contains(permit.Id),
            "a structural proposal cannot open a Lock or make its blocked producer eligible on the next Tick");
    }

    private static void VerifyExpectationLedgerOwnershipAndOwnerBinding()
    {
        var defaultObserver = new KLEPObserver(
            "observer.expectation.default", "1");
        var defaultLearned = new KLEPLearnedExpectations(
            "observer.expectation.bound", "1");
        var boundObserver = new KLEPObserver(
            "observer.expectation.bound",
            "1",
            learnedExpectations: defaultLearned);
        var expectations = new KLEPLearnedExpectations(
            "observer.expectation.owner", "2", 2d);
        var observer = new KLEPObserver(
            "observer.expectation.owner",
            "2",
            learnedExpectations: expectations);
        Expect(defaultObserver.LearnedExpectations == null &&
               ReferenceEquals(
                   boundObserver.LearnedExpectations, defaultLearned) &&
               defaultLearned.ConfidenceScale == 4d,
            "an Observer without injection owns no learned state and an injected default-scale authority is read-only visible");
        Expect(ReferenceEquals(observer.LearnedExpectations, expectations) &&
               expectations.OwnerStableId == observer.StableId &&
               expectations.OwnerVersion == observer.Version &&
               expectations.ConfidenceScale == 2d &&
               typeof(IKLEPLearnedExpectationsView).GetMethod("Record") == null,
            "the independent authority owns scale and mutation while the Observer sees only its query view");
        Expect(Catch(() => new KLEPObserver(
                   "observer.expectation.mismatch",
                   "1",
                   learnedExpectations: expectations)) is ArgumentException,
            "an Observer rejects a learned view bound to another modeler identity");

        KLEPKeyDefinition outcome = Key(
            "observer.expectation.owner.outcome");
        var source = new ProbeExecutable(
            Definition("observer.expectation.owner.source", 1f),
            KLEPExecutableTickStatus.Running);
        var neuron = new KLEPNeuron("observer.expectation.owner.neuron");
        neuron.RegisterExecutable(source);
        var agent = new KLEPAgent(neuron, null, observer);
        KLEPObserverSelfModel model = observer.ObserveSelf(agent.Tick());
        var context = ExpectationContext("owner");
        var horizon = ExpectationHorizon("next-tick");
        KLEPObserverExpectationQueryResult unknown =
            expectations.Query(
                model, source.StableId, outcome.Id,
                KLEPObserverExpectationObservationMeaning.PresentAfter,
                context, horizon);
        Expect(unknown.LedgerRevision == 0 &&
               unknown.Knowledge == KLEPObserverExpectationKnowledge.Unknown &&
               !unknown.IsKnown && !unknown.Likelihood.HasValue &&
               unknown.Confidence == 0d && unknown.CompletedCount == 0,
            "zero completed evidence is explicitly unknown");

        var otherObserver = new KLEPObserver(
            "observer.expectation.other", "1");
        var otherSource = new ProbeExecutable(
            Definition(source.StableId, 1f),
            KLEPExecutableTickStatus.Running);
        var otherNeuron = new KLEPNeuron(
            "observer.expectation.other.neuron");
        otherNeuron.RegisterExecutable(otherSource);
        var otherAgent = new KLEPAgent(
            otherNeuron, null, otherObserver);
        KLEPObserverSelfModel otherModel =
            otherObserver.ObserveSelf(otherAgent.Tick());
        KLEPKeySnapshot otherConsequence =
            otherAgent.Tick().Decision.KeySnapshot;
        Exception trialOwnerMismatch = Catch(() =>
            ExpectationTrial(
                observer, 1, otherModel, otherSource, outcome,
                context, horizon, otherConsequence,
                KLEPObserverExpectationTrialOutcome.NotObserved));
        Exception queryOwnerMismatch = Catch(() =>
            expectations.Query(
                otherModel, otherSource.StableId, outcome.Id,
                KLEPObserverExpectationObservationMeaning.PresentAfter,
                context, horizon));
        Expect(trialOwnerMismatch is ArgumentException &&
               queryOwnerMismatch is ArgumentException &&
               expectations.Revision == 0 &&
               expectations.LastEvidenceSequence == 0,
            "cross-Observer self-models cannot be relabeled as owned evidence");

        var epsilonExpectations = new KLEPLearnedExpectations(
            "observer.expectation.epsilon",
            "1",
            double.Epsilon);
        var epsilonObserver = new KLEPObserver(
            "observer.expectation.epsilon",
            "1",
            learnedExpectations: epsilonExpectations);
        var epsilonSource = new ProbeExecutable(
            Definition("observer.expectation.epsilon.source", 1f),
            KLEPExecutableTickStatus.Running);
        var epsilonNeuron = new KLEPNeuron(
            "observer.expectation.epsilon.neuron");
        epsilonNeuron.RegisterExecutable(epsilonSource);
        var epsilonAgent = new KLEPAgent(
            epsilonNeuron, null, epsilonObserver);
        KLEPObserverSelfModel epsilonModel =
            epsilonObserver.ObserveSelf(epsilonAgent.Tick());
        KLEPKeySnapshot epsilonConsequence =
            epsilonAgent.Tick().Decision.KeySnapshot;
        epsilonExpectations.Record(ExpectationTrial(
            epsilonObserver, 1, epsilonModel, epsilonSource, outcome,
            context, horizon, epsilonConsequence,
            KLEPObserverExpectationTrialOutcome.NotObserved));
        KLEPObserverExpectationQueryResult epsilonResult =
            ExpectationQuery(epsilonObserver, epsilonModel, epsilonSource,
                outcome, context, horizon);
        Expect(epsilonExpectations.ConfidenceScale ==
                   double.Epsilon &&
               epsilonResult.Confidence > 0d &&
               epsilonResult.Confidence < 1d,
            "finite evidence remains below certainty at an epsilon scale");
    }

    private static void
        VerifyExpectationEvidenceMathSeparationReplayAndImmutability()
    {
        var expectations = new KLEPLearnedExpectations(
            "observer.expectation.math", "1");
        var observer = new KLEPObserver(
            "observer.expectation.math",
            "1",
            learnedExpectations: expectations);
        KLEPKeyDefinition outcome = Key(
            "observer.expectation.math.outcome");
        var source = new ProbeExecutable(
            Definition("observer.expectation.math.source", 1f),
            KLEPExecutableTickStatus.Running);
        var neuron = new KLEPNeuron("observer.expectation.math.neuron");
        neuron.RegisterExecutable(source);
        var agent = new KLEPAgent(neuron, null, observer);
        KLEPObserverSelfModel model = observer.ObserveSelf(agent.Tick());
        var context = ExpectationContext("math");
        var horizon = ExpectationHorizon("next-tick");
        KLEPKeySnapshot missSnapshot =
            agent.Tick().Decision.KeySnapshot;
        neuron.AddKey(outcome, sourceId: "expectation-test");
        KLEPAgentTickTrace observedTrace = agent.Tick();
        KLEPKeySnapshot observedSnapshot =
            observedTrace.Decision.KeySnapshot;
        var trials = new List<KLEPObserverExpectationTrial>
        {
            ExpectationTrial(
                observer, 1, model, source, outcome, context, horizon,
                missSnapshot,
                KLEPObserverExpectationTrialOutcome.NotObserved),
            ExpectationTrial(
                observer, 2, model, source, outcome, context, horizon,
                observedSnapshot,
                KLEPObserverExpectationTrialOutcome.Observed),
            ExpectationTrial(
                observer, 3, model, source, outcome, context, horizon,
                observedSnapshot,
                KLEPObserverExpectationTrialOutcome.Observed),
            ExpectationTrial(
                observer, 4, model, source, outcome, context, horizon,
                observedSnapshot,
                KLEPObserverExpectationTrialOutcome.Censored)
        };
        expectations.Record(trials[0]);
        KLEPObserverExpectationQueryResult afterMiss =
            ExpectationQuery(observer, model, source, outcome, context, horizon);
        KLEPObserverExpectationSnapshot afterMissSnapshot =
            expectations.CaptureSnapshot();
        Expect(afterMiss.IsKnown && afterMiss.ObservedCount == 0 &&
               afterMiss.NotObservedCount == 1 &&
               afterMiss.Likelihood == 0d &&
               Math.Abs(afterMiss.Confidence - 0.2d) < 0.000000000001d,
            "one miss yields likelihood zero and confidence one fifth");

        expectations.Record(trials[1]);
        KLEPObserverExpectationQueryResult afterHit =
            ExpectationQuery(observer, model, source, outcome, context, horizon);
        Expect(afterHit.ObservedCount == 1 && afterHit.CompletedCount == 2 &&
               afterHit.Likelihood == 0.5d &&
               Math.Abs(afterHit.Confidence - (2d / 6d)) <
                   0.000000000001d,
            "hit and miss likelihood is one half while confidence uses N over N plus scale");
        expectations.Record(trials[2]);
        KLEPObserverExpectationQueryResult repeated =
            ExpectationQuery(observer, model, source, outcome, context, horizon);
        expectations.Record(trials[3]);
        KLEPObserverExpectationQueryResult censored =
            ExpectationQuery(observer, model, source, outcome, context, horizon);
        Expect(repeated.ObservedCount == 2 && repeated.CompletedCount == 3 &&
               repeated.Likelihood == (2d / 3d) &&
               Math.Abs(repeated.Confidence - (3d / 7d)) <
                   0.000000000001d,
            "repeated completed evidence raises confidence without hiding its ratio");
        Expect(censored.CensoredCount == 1 &&
               censored.CompletedCount == repeated.CompletedCount &&
               censored.Likelihood == repeated.Likelihood &&
               censored.Confidence == repeated.Confidence &&
               censored.LedgerRevision == 4 &&
               expectations.LastEvidenceSequence == 4,
            "censored evidence advances the ledger but not completed math");
        KLEPObserverExpectationAggregate frozenAggregate;
        bool retained = afterMissSnapshot.TryGetAggregate(
            afterMiss.Bucket, out frozenAggregate);
        Expect(afterMiss.LedgerRevision == 1 &&
               afterMiss.NotObservedCount == 1 &&
               retained && frozenAggregate.NotObservedCount == 1 &&
               afterMissSnapshot.Revision == 1 &&
               afterMissSnapshot.LastEvidenceSequence == 1 &&
               afterMissSnapshot.LastUpdate.RevisionAfter == 1,
            "later trials cannot rewrite an earlier query or ledger snapshot");
        Expect(HasNoPublicSetters(afterMiss) &&
               HasNoPublicSetters(afterMiss.Aggregate) &&
               HasNoPublicSetters(afterMissSnapshot) &&
               ((IList<KLEPObserverExpectationAggregate>)
                    afterMissSnapshot.Aggregates).IsReadOnly &&
               afterMissSnapshot.GetType().GetProperty("Updates") == null &&
               !HasUnboundedExpectationHistory(expectations),
            "expectation results are immutable and retain no unbounded update registry");
        KLEPObserverSelfModel priorWithKey =
            observer.ObserveSelf(observedTrace);
        KLEPKeySnapshot stillPresent =
            agent.Tick().Decision.KeySnapshot;
        KLEPObserverExpectationTrial presentAfter = ExpectationTrial(
            observer, 5, priorWithKey, source, outcome, context, horizon,
            stillPresent, KLEPObserverExpectationTrialOutcome.Observed,
            KLEPObserverExpectationObservationMeaning.PresentAfter);
        KLEPObserverExpectationTrial acquired = ExpectationTrial(
            observer, 6, priorWithKey, source, outcome, context, horizon,
            stillPresent, KLEPObserverExpectationTrialOutcome.NotObserved,
            KLEPObserverExpectationObservationMeaning.Acquired);
        expectations.Record(presentAfter);
        expectations.Record(acquired);
        KLEPObserverExpectationQueryResult presentResult =
            ExpectationQuery(
                observer, priorWithKey, source, outcome, context, horizon);
        KLEPObserverExpectationQueryResult acquiredResult =
            ExpectationQuery(observer, priorWithKey, source, outcome,
                context, horizon,
                KLEPObserverExpectationObservationMeaning.Acquired);
        Expect(presentResult.ObservedCount == 3 &&
               presentResult.NotObservedCount == 1 &&
               acquiredResult.ObservedCount == 0 &&
               acquiredResult.NotObservedCount == 1 &&
               !presentResult.Bucket.Equals(acquiredResult.Bucket),
            "PresentAfter and Acquired remain distinct exact evidence meanings");

        KLEPObserverExpectationQueryResult otherContext =
            ExpectationQuery(observer, model, source, outcome,
                ExpectationContext("other"), horizon);
        KLEPObserverExpectationQueryResult otherHorizon =
            ExpectationQuery(observer, model, source, outcome,
                context, ExpectationHorizon("later"));
        Expect(!otherContext.IsKnown && !otherHorizon.IsKnown &&
               !otherContext.Bucket.Equals(censored.Bucket) &&
               !otherHorizon.Bucket.Equals(censored.Bucket),
            "context and horizon identities never bleed evidence across buckets");
        neuron.RemoveExecutable(source.StableId);
        var replacement = new ProbeExecutable(
            Definition(source.StableId, 1f),
            KLEPExecutableTickStatus.Running);
        neuron.RegisterExecutable(replacement);
        KLEPObserverSelfModel replacementModel =
            observer.ObserveSelf(agent.Tick());
        KLEPObserverExpectationQueryResult replacementResult =
            ExpectationQuery(observer, replacementModel, replacement, outcome,
                context, horizon);
        Expect(!replacementResult.IsKnown &&
               replacementResult.Bucket.SourceRootTenureId !=
                   censored.Bucket.SourceRootTenureId &&
               !replacementResult.Bucket.CatalogFingerprint.Equals(
                   censored.Bucket.CatalogFingerprint),
            "replacement catalog fingerprints and root tenures isolate old evidence");
        long revisionBeforeRejection = expectations.Revision;
        long sequenceBeforeRejection =
            expectations.LastEvidenceSequence;
        KLEPObserverExpectationSnapshot snapshotBeforeRejection =
            expectations.CaptureSnapshot();
        Exception replayedSequence = Catch(() =>
            expectations.Record(trials[1]));
        Exception falseOutcome = Catch(() => ExpectationTrial(
            observer, 7, model, source, outcome, context, horizon,
            missSnapshot, KLEPObserverExpectationTrialOutcome.Observed));
        Expect(replayedSequence is InvalidOperationException &&
               falseOutcome is ArgumentException &&
               expectations.Revision == revisionBeforeRejection &&
               expectations.LastEvidenceSequence ==
                   sequenceBeforeRejection &&
               ReferenceEquals(
                   expectations.CaptureSnapshot().LastUpdate,
                   snapshotBeforeRejection.LastUpdate),
            "replayed sequence and false factual outcome reject atomically");
        KLEPLearnedExpectations replayA =
            KLEPLearnedExpectations.Replay(
                observer.StableId, observer.Version, trials);
        KLEPLearnedExpectations replayB =
            KLEPLearnedExpectations.Replay(
                observer.StableId, observer.Version, trials);
        KLEPObserverExpectationQueryResult replayResultA =
            ExpectationQuery(
                replayA, model, source, outcome, context, horizon);
        KLEPObserverExpectationQueryResult replayResultB =
            ExpectationQuery(
                replayB, model, source, outcome, context, horizon);
        Exception outOfOrderReplay = Catch(() =>
            KLEPLearnedExpectations.Replay(
                observer.StableId, observer.Version,
                new[] { trials[1], trials[0] }));
        Expect(replayResultA.ObservedCount == replayResultB.ObservedCount &&
               replayResultA.NotObservedCount == replayResultB.NotObservedCount &&
               replayResultA.CensoredCount == replayResultB.CensoredCount &&
               replayResultA.Likelihood == replayResultB.Likelihood &&
               replayResultA.Confidence == replayResultB.Confidence &&
               outOfOrderReplay is InvalidOperationException,
            "canonical replay is deterministic and rejects out-of-order evidence");
    }

    private static void VerifyExpectationReasoningIsNonAuthoritative()
    {
        var expectations = new KLEPLearnedExpectations(
            "observer.expectation.authority", "1");
        KLEPKeyDefinition outcome = Key(
            "observer.expectation.authority.outcome");
        KLEPLock blockedLock = Lock(
            "observer.expectation.authority.lock", outcome);
        var source = new ProbeExecutable(
            Definition("observer.expectation.authority.source", 1f,
                declaredOutputs: new[] { outcome }),
            KLEPExecutableTickStatus.Running);
        var blocked = new ProbeExecutable(
            Definition("observer.expectation.authority.blocked", 100f,
                validationLocks: new[] { blockedLock }),
            KLEPExecutableTickStatus.Succeeded);
        var observer = new KLEPObserver(
            "observer.expectation.authority",
            "1",
            learnedExpectations: expectations);
        var neuron = new KLEPNeuron(
            "observer.expectation.authority.neuron");
        neuron.RegisterExecutable(source);
        neuron.RegisterExecutable(blocked);
        var agent = new KLEPAgent(neuron, null, observer);
        KLEPObserverSelfModel model = observer.ObserveSelf(agent.Tick());
        KLEPAgentTickTrace before = agent.Tick();
        var context = ExpectationContext("authority");
        var horizon = ExpectationHorizon("next-tick");
        KLEPObserverExpectationTrial trial = ExpectationTrial(
            observer, 1, model, source, outcome, context, horizon,
            before.Decision.KeySnapshot,
            KLEPObserverExpectationTrialOutcome.NotObserved);
        long cycleBefore = neuron.CycleIndex;
        int sourceTicksBefore = source.TickCount;
        int factsBefore = before.Decision.KeySnapshot.Facts.Count;
        expectations.Record(trial);
        KLEPObserverExpectationQueryResult result =
            ExpectationQuery(observer, model, source, outcome, context, horizon);

        Expect(neuron.CycleIndex == cycleBefore &&
               ReferenceEquals(agent.LastTrace, before) &&
               source.TickCount == sourceTicksBefore && blocked.TickCount == 0 &&
               before.Decision.KeySnapshot.Facts.Count == factsBefore &&
               !before.Decision.KeySnapshot.Contains(outcome.Id) &&
               blockedLock.Attractiveness == 0f,
            "recording and querying expectation evidence mutates no live authority");
        Expect(result.NotObservedCount == 1 &&
               source.DeclaredOutputs.Count == 1 &&
               source.DeclaredOutputs[0].Id == outcome.Id,
            "empirical evidence does not rewrite declared completion outputs");
        KLEPAgentTickTrace after = agent.Tick();
        Expect(after.Decision.SelectedExecutableId == source.StableId &&
               blocked.TickCount == 0 &&
               !after.Decision.KeySnapshot.Contains(outcome.Id),
            "expectation evidence cannot open a Lock or change Agent selection");
    }

    private static void
        VerifyEmpiricalWanderExpectationBesideAuthoredStructure()
    {
        var expectations = new KLEPLearnedExpectations(
            "observer.expectation.zombie", "1");
        KLEPKeyDefinition nearbyHuman = Key(
            "observer.expectation.zombie.nearby-human");
        KLEPKeyDefinition ateHuman = Key(
            "observer.expectation.zombie.ate-human");
        KLEPKeyDefinition sensorPermit = Key(
            "observer.expectation.zombie.sensor-permit");
        KLEPLock sensorLock = Lock(
            "observer.expectation.zombie.sensor-lock", sensorPermit);
        KLEPLock eatLock = Lock(
            "observer.expectation.zombie.eat-lock", nearbyHuman);
        var wander = new ProbeExecutable(
            Definition("observer.expectation.zombie.wander", 10f),
            KLEPExecutableTickStatus.Running);
        var sensor = new ProbeExecutable(
            Definition(
                "observer.expectation.zombie.sensor",
                0f,
                kind: KLEPExecutableKind.Sensor,
                validationLocks: new[] { sensorLock },
                executionMode: KLEPExecutionMode.Tandem,
                declaredOutputs: new[] { nearbyHuman }),
            KLEPExecutableTickStatus.Succeeded);
        var eatHuman = new ProbeExecutable(
            Definition(
                "observer.expectation.zombie.eat-human",
                100f,
                validationLocks: new[] { eatLock },
                executionLocks: new[] { eatLock },
                declaredOutputs: new[] { ateHuman }),
            KLEPExecutableTickStatus.Running);

        var observer = new KLEPObserver(
            "observer.expectation.zombie",
            "1",
            learnedExpectations: expectations);
        var neuron = new KLEPNeuron(
            "observer.expectation.zombie.neuron");
        neuron.RegisterExecutable(sensor);
        neuron.RegisterExecutable(eatHuman);
        neuron.RegisterExecutable(wander);
        var agent = new KLEPAgent(neuron, null, observer);
        KLEPObserverSelfModel prior =
            observer.ObserveSelf(agent.Tick());
        Expect(prior.AgentTrace.Decision.SelectedExecutableId ==
                   wander.StableId &&
               wander.DeclaredOutputs.Count == 0 &&
               sensor.DeclaredOutputs.Count == 1 &&
               sensor.DeclaredOutputs[0].Id == nearbyHuman.Id &&
               eatHuman.DeclaredOutputs.Count == 1 &&
               eatHuman.DeclaredOutputs[0].Id == ateHuman.Id &&
               !prior.KeySnapshot.Contains(nearbyHuman.Id),
            "Wander does not produce NearbyHuman; Sensor and Eat Human own the authored chain");

        neuron.AddKey(
            nearbyHuman, sourceId: "observer.expectation.zombie.world");
        KLEPAgentTickTrace consequence = agent.Tick();
        Expect(consequence.Decision.SelectedExecutableId ==
                   eatHuman.StableId &&
               consequence.Decision.KeySnapshot.Contains(nearbyHuman.Id) &&
               !consequence.Decision.KeySnapshot.Contains(ateHuman.Id) &&
               sensor.TickCount == 0 && eatHuman.TickCount == 1,
            "a factual later NearbyHuman observation can gate Eat Human without running its blocked Sensor");
        KLEPKeyFact nearbyFact;
        bool foundNearby = consequence.Decision.KeySnapshot.TryGetFirst(
            nearbyHuman.Id, out nearbyFact);
        Expect(foundNearby && neuron.RemoveKey(nearbyFact),
            "the factual world observation can be removed before expectation reasoning");
        KLEPAgentTickTrace restored = agent.Tick();
        Expect(restored.Decision.SelectedExecutableId == wander.StableId &&
               !restored.Decision.KeySnapshot.Contains(nearbyHuman.Id) &&
               !restored.Decision.KeySnapshot.Contains(ateHuman.Id) &&
               sensor.TickCount == 0 && eatHuman.TickCount == 1,
            "removing NearbyHuman closes Eat Human and restores Wander before the query");

        var context = ExpectationContext("zombie-world");
        var horizon = ExpectationHorizon("later-observation");
        KLEPObserverExpectationTrial trial = ExpectationTrial(
            observer, 1, prior, wander, nearbyHuman, context, horizon,
            consequence.Decision.KeySnapshot,
            KLEPObserverExpectationTrialOutcome.Observed);
        long cycleBeforeReasoning = neuron.CycleIndex;
        int wanderTicksBeforeReasoning = wander.TickCount;
        expectations.Record(trial);
        KLEPObserverExpectationQueryResult expectation =
            ExpectationQuery(observer, prior, wander, nearbyHuman,
                context, horizon);
        KLEPObserverStructuralDependencyProposal structure =
            observer.ProposeStructuralDependencies(prior, ateHuman.Id);
        KLEPObserverDependencyNode ateKeyNode =
            FindDependencyKeyNode(structure.Graph, ateHuman.Id);
        KLEPObserverDependencyNode nearbyKeyNode =
            FindDependencyKeyNode(structure.Graph, nearbyHuman.Id);
        KLEPObserverDependencyNode eatNode =
            FindDependencyProducerNode(structure.Graph, eatHuman.StableId);
        KLEPObserverDependencyNode sensorNode =
            FindDependencyProducerNode(structure.Graph, sensor.StableId);
        Expect(expectation.ObservedCount == 1 &&
               expectation.NotObservedCount == 0 &&
               expectation.Likelihood == 1d &&
               expectation.Bucket.SourceExecutableId == wander.StableId &&
               wander.DeclaredOutputs.Count == 0,
            "the Observer can expect NearbyHuman after Wander without claiming Wander produces it");
        Expect(HasDependencyEdgeBetween(
                   structure.Graph, ateKeyNode.NodeId, eatNode.NodeId,
                   KLEPObserverDependencyEdgeKind.ProducedBy) &&
               HasDependencyEdgeBetween(
                   structure.Graph, nearbyKeyNode.NodeId, sensorNode.NodeId,
                   KLEPObserverDependencyEdgeKind.ProducedBy) &&
               HasDependencyEdgeKind(
                   structure.Graph,
                   KLEPObserverDependencyEdgeKind.ProducerRequiresLock) &&
               HasDependencyEdgeKind(
                   structure.Graph,
                   KLEPObserverDependencyEdgeKind.RequiresPresentKey),
            "authored structure separately maps Sensor to NearbyHuman to Eat Human to AteHuman");
        Expect(neuron.CycleIndex == cycleBeforeReasoning &&
               ReferenceEquals(agent.LastTrace, restored) &&
               wander.TickCount == wanderTicksBeforeReasoning &&
               sensor.TickCount == 0 && eatHuman.TickCount == 1 &&
               !restored.Decision.KeySnapshot.Contains(nearbyHuman.Id),
            "expectation and structural reasoning leave live execution untouched");

        KLEPAgentTickTrace after = agent.Tick();
        Expect(after.Decision.SelectedExecutableId == wander.StableId &&
               sensor.TickCount == 0 && eatHuman.TickCount == 1 &&
               !after.Decision.KeySnapshot.Contains(nearbyHuman.Id) &&
               !after.Decision.KeySnapshot.Contains(ateHuman.Id),
            "an empirical expectation neither opens Eat Human nor emits its structural outputs");

    }

    private static string RunDeterministicObserver(string neuronId)
    {
        var a = new ProbeExecutable(
            Definition("observer.repeat.a", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var b = new ProbeExecutable(
            Definition("observer.repeat.b", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var sourceB = new StaticEvidenceSource(
            "observer.repeat.z",
            new KLEPObserverEvidence(b.StableId, 2f, "memory"));
        var sourceA = new StaticEvidenceSource(
            "observer.repeat.a",
            new KLEPObserverEvidence(a.StableId, 1f, "need"));
        var observer = new KLEPObserver(
            "observer.repeat",
            "1",
            new IKLEPObserverEvidenceSource[] { sourceB, sourceA });
        var neuron = new KLEPNeuron(neuronId);
        neuron.RegisterExecutable(b);
        neuron.RegisterExecutable(a);
        var agent = new KLEPAgent(
            neuron, configuration: null, guidanceObserver: observer);

        agent.Tick();

        var parts = new List<string>
        {
            observer.LastTrace.SelectedExecutableId,
            agent.PendingGuidanceAdvice.TargetExecutableId
        };
        foreach (KLEPObserverTargetTrace target in observer.LastTrace.Targets)
        {
            parts.Add(target.ExecutableStableId + "=" + target.HolisticValue);
            foreach (KLEPObserverEvidenceTrace evidence in target.Evidence)
            {
                parts.Add(evidence.SourceId + ":" + evidence.Value);
            }
        }

        return string.Join("|", parts);
    }

    private static KLEPObserverExpectationContextIdentity ExpectationContext(
        string fingerprint)
    {
        return new KLEPObserverExpectationContextIdentity(
            "global", "observer-smoke", "1", fingerprint);
    }

    private static KLEPObserverExpectationHorizonIdentity ExpectationHorizon(
        string horizonId)
    {
        return new KLEPObserverExpectationHorizonIdentity(horizonId, "1");
    }

    private static KLEPObserverExpectationTrial ExpectationTrial(
        KLEPObserver owner,
        long sequence,
        KLEPObserverSelfModel model,
        KLEPExecutableBase source,
        KLEPKeyDefinition outcome,
        KLEPObserverExpectationContextIdentity context,
        KLEPObserverExpectationHorizonIdentity horizon,
        KLEPKeySnapshot consequence,
        KLEPObserverExpectationTrialOutcome disposition,
        KLEPObserverExpectationObservationMeaning meaning =
            KLEPObserverExpectationObservationMeaning.PresentAfter)
    {
        string suffix = sequence.ToString();
        return new KLEPObserverExpectationTrial(
            owner.StableId, owner.Version, sequence,
            "trial." + suffix,
            new[] { "evidence." + suffix },
            model, source.StableId, "run." + suffix, outcome.Id, meaning,
            context, horizon, consequence, disposition);
    }

    private static KLEPObserverExpectationQueryResult ExpectationQuery(
        KLEPObserver owner, KLEPObserverSelfModel model,
        KLEPExecutableBase source, KLEPKeyDefinition outcome,
        KLEPObserverExpectationContextIdentity context,
        KLEPObserverExpectationHorizonIdentity horizon,
        KLEPObserverExpectationObservationMeaning meaning =
            KLEPObserverExpectationObservationMeaning.PresentAfter)
    {
        return owner.QueryLearnedExpectation(
            model,
            source.StableId,
            outcome.Id,
            meaning,
            context,
            horizon);
    }

    private static KLEPObserverExpectationQueryResult ExpectationQuery(
        IKLEPLearnedExpectationsView ledger, KLEPObserverSelfModel model,
        KLEPExecutableBase source, KLEPKeyDefinition outcome,
        KLEPObserverExpectationContextIdentity context,
        KLEPObserverExpectationHorizonIdentity horizon,
        KLEPObserverExpectationObservationMeaning meaning =
            KLEPObserverExpectationObservationMeaning.PresentAfter)
    {
        return ledger.Query(
            model, source.StableId, outcome.Id, meaning, context, horizon);
    }

    private static KLEPExecutableDefinition Definition(
        string stableId,
        float score,
        KLEPExecutableKind kind = KLEPExecutableKind.Action,
        IEnumerable<KLEPLock> validationLocks = null,
        KLEPExecutionMode executionMode = KLEPExecutionMode.Solo,
        IEnumerable<KLEPLock> executionLocks = null,
        IEnumerable<KLEPKeyDefinition> declaredOutputs = null)
    {
        return new KLEPExecutableDefinition(
            stableId,
            stableId,
            kind,
            validationLocks: validationLocks,
            executionLocks: executionLocks,
            baseAttractiveness: score,
            executionMode: executionMode,
            declaredOutputs: declaredOutputs);
    }

    private static KLEPKeyDefinition Key(string stableId)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(stableId),
            stableId,
            defaultLifetime: KLEPKeyLifetime.Persistent);
    }

    private static KLEPKeyPayload Payload(
        string field,
        KLEPKeyValue value)
    {
        return new KLEPKeyPayload(
            new[] { new KeyValuePair<string, KLEPKeyValue>(field, value) });
    }

    private static KLEPKeyPayload EvidencePayload(
        bool boolean,
        long integer,
        double number,
        string text)
    {
        return new KLEPKeyPayload(new[]
        {
            new KeyValuePair<string, KLEPKeyValue>("text", text),
            new KeyValuePair<string, KLEPKeyValue>("boolean", boolean),
            new KeyValuePair<string, KLEPKeyValue>("number", number),
            new KeyValuePair<string, KLEPKeyValue>("integer", integer)
        });
    }

    private static KLEPKeyPayload EvidencePayloadReversed(
        bool boolean,
        long integer,
        double number,
        string text)
    {
        return new KLEPKeyPayload(new[]
        {
            new KeyValuePair<string, KLEPKeyValue>("integer", integer),
            new KeyValuePair<string, KLEPKeyValue>("number", number),
            new KeyValuePair<string, KLEPKeyValue>("boolean", boolean),
            new KeyValuePair<string, KLEPKeyValue>("text", text)
        });
    }

    private static KLEPLock Lock(
        string stableId,
        KLEPKeyDefinition required)
    {
        return new KLEPLock(
            stableId,
            stableId,
            new KLEPKeyPresent(required.Id.Value));
    }

    private static KLEPObserverDependencyNode FindDependencyKeyNode(
        KLEPObserverDependencyGraph graph,
        KLEPKeyId keyId)
    {
        foreach (KLEPObserverDependencyNode node in graph.Nodes)
        {
            if (node.Kind == KLEPObserverDependencyNodeKind.Key &&
                node.KeyId.HasValue &&
                node.KeyId.Value == keyId)
            {
                return node;
            }
        }

        throw new InvalidOperationException(
            $"Dependency graph did not retain Key '{keyId}'.");
    }

    private static KLEPObserverDependencyNode FindDependencyProducerNode(
        KLEPObserverDependencyGraph graph,
        string stableExecutableId)
    {
        foreach (KLEPObserverDependencyNode node in graph.Nodes)
        {
            if (node.Kind == KLEPObserverDependencyNodeKind.Producer &&
                node.Producer != null &&
                node.Producer.StableExecutableId == stableExecutableId)
            {
                return node;
            }
        }

        throw new InvalidOperationException(
            $"Dependency graph did not retain producer '{stableExecutableId}'.");
    }

    private static bool HasDependencyEdgeKind(
        KLEPObserverDependencyGraph graph,
        KLEPObserverDependencyEdgeKind kind)
    {
        foreach (KLEPObserverDependencyEdge edge in graph.Edges)
        {
            if (edge.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDependencyEdgeBetween(
        KLEPObserverDependencyGraph graph,
        string firstNodeId,
        string secondNodeId,
        KLEPObserverDependencyEdgeKind kind)
    {
        foreach (KLEPObserverDependencyEdge edge in graph.Edges)
        {
            bool sameEndpoints =
                (edge.FromNodeId == firstNodeId &&
                 edge.ToNodeId == secondNodeId) ||
                (edge.FromNodeId == secondNodeId &&
                 edge.ToNodeId == firstNodeId);
            if (sameEndpoints && edge.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasReasoningDiagnostic(
        IReadOnlyList<KLEPObserverReasoningDiagnostic> diagnostics,
        KLEPObserverReasoningDiagnosticCode code,
        string evidence)
    {
        foreach (KLEPObserverReasoningDiagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Code == code &&
                ((diagnostic.Path != null &&
                  diagnostic.Path.Contains(evidence, StringComparison.Ordinal)) ||
                 (diagnostic.Message != null &&
                  diagnostic.Message.Contains(evidence, StringComparison.Ordinal))))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountDependencyProducerNodesForKey(
        KLEPObserverDependencyGraph graph,
        KLEPKeyId keyId)
    {
        int count = 0;
        foreach (KLEPObserverDependencyNode node in graph.Nodes)
        {
            if (node.Kind == KLEPObserverDependencyNodeKind.Producer &&
                node.KeyId.HasValue &&
                node.KeyId.Value == keyId)
            {
                count++;
            }
        }

        return count;
    }

    private static string DependencySignature(
        KLEPObserverStructuralDependencyProposal proposal)
    {
        var parts = new List<string>
        {
            proposal.TargetKeyId.Value,
            proposal.CatalogRevision,
            proposal.CatalogFingerprint.Value,
            proposal.EvidenceFingerprint.CanonicalId
        };
        foreach (KLEPObserverDependencyNode node in proposal.Graph.Nodes)
        {
            parts.Add(
                "N|" + node.NodeId + "|" + node.Kind + "|" +
                (node.KeyId.HasValue ? node.KeyId.Value.Value : string.Empty) +
                "|" + node.IsPresentInCurrentEvidence + "|" +
                (node.Producer?.StableExecutableId ?? string.Empty) + "|" +
                (node.SourceLock?.StableId ?? string.Empty) + "|" +
                (node.SourceExpression?.Kind.ToString() ?? string.Empty) + "|" +
                node.IsNegativeContext + "|" +
                node.IsIndependentlySchedulable);
        }

        foreach (KLEPObserverDependencyEdge edge in proposal.Graph.Edges)
        {
            parts.Add(
                "E|" + edge.FromNodeId + "|" + edge.Kind + "|" +
                edge.ToNodeId);
        }

        foreach (KLEPObserverReasoningDiagnostic diagnostic in
                 proposal.Diagnostics)
        {
            parts.Add(
                "D|" + diagnostic.Code + "|" + diagnostic.Path + "|" +
                diagnostic.Message);
        }

        return string.Join("\n", parts);
    }

    private static KLEPObserverTargetTrace FindTarget(
        KLEPObserverTrace trace,
        string stableId)
    {
        foreach (KLEPObserverTargetTrace target in trace.Targets)
        {
            if (target.ExecutableStableId == stableId)
            {
                return target;
            }
        }

        throw new InvalidOperationException("Observer target was not traced.");
    }

    private static CandidateEvaluation FindCandidate(
        KLEPDecisionTrace trace,
        string stableId)
    {
        foreach (CandidateEvaluation candidate in trace.Candidates)
        {
            if (candidate.StableId == stableId)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Candidate was not traced.");
    }

    private static bool HasObserverComponent(
        KLEPDecisionTrace trace,
        string executableId,
        string observerId,
        float expectedValue)
    {
        CandidateEvaluation candidate = FindCandidate(trace, executableId);
        if (candidate.ScoreEvaluation == null)
        {
            return false;
        }

        foreach (KLEPExecutableScoreComponent component in
                 candidate.ScoreEvaluation.Components)
        {
            if (component.Kind ==
                    KLEPExecutableScoreComponentKind.ObserverInfluence &&
                component.SourceId == observerId &&
                component.Value == expectedValue)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCancellation(
        KLEPDecisionTrace trace,
        string executableId,
        KLEPExecutableExitReason reason)
    {
        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            if (step.Kind == KLEPExecutableStepKind.Cancellation &&
                step.ExecutableStableId == executableId &&
                step.ExitReason == reason)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNoPublicSetters(object value)
    {
        foreach (PropertyInfo property in value.GetType().GetProperties(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetSetMethod(nonPublic: false) != null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasUnboundedExpectationHistory(
        KLEPLearnedExpectations ledger)
    {
        foreach (FieldInfo field in ledger.GetType().GetFields(
                     BindingFlags.Instance | BindingFlags.NonPublic))
        {
            Type type = field.FieldType;
            if (!type.IsGenericType)
            {
                continue;
            }

            Type generic = type.GetGenericTypeDefinition();
            Type[] arguments = type.GetGenericArguments();
            if ((generic == typeof(List<>) && arguments[0] ==
                    typeof(KLEPObserverExpectationUpdateTrace)) ||
                generic == typeof(HashSet<>))
            {
                return true;
            }
        }

        return false;
    }

    private static Exception Catch(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected an exception, but none was thrown.");
    }

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException($"Assertion failed: {message}");
        }
    }

    private sealed class StaticEvidenceSource : IKLEPObserverEvidenceSource
    {
        private readonly IReadOnlyList<KLEPObserverEvidence> evidence;

        internal StaticEvidenceSource(
            string stableId,
            params KLEPObserverEvidence[] evidence)
        {
            StableId = stableId;
            Version = "1";
            this.evidence = evidence;
        }

        public string StableId { get; }
        public string Version { get; }
        public int EvaluationCount { get; private set; }
        public IReadOnlyList<string> LastEligibleTargetIds { get; private set; } =
            Array.Empty<string>();

        public IReadOnlyList<KLEPObserverEvidence> Evaluate(
            KLEPObserverEvidenceContext context)
        {
            EvaluationCount++;
            var ids = new List<string>(context.EligibleTargets.Count);
            foreach (KLEPExecutableDefinition target in context.EligibleTargets)
            {
                ids.Add(target.StableId);
            }

            LastEligibleTargetIds = ids.ToArray();
            return evidence;
        }
    }

    private sealed class ProbeGoalAttractionEvaluator :
        IKLEPGoalAttractionEvaluator
    {
        private readonly Func<KLEPGoalAttractionContext,
            KLEPGoalAttractionEvaluation> evaluate;

        internal ProbeGoalAttractionEvaluator(
            string stableId,
            string version,
            Func<KLEPGoalAttractionContext,
                KLEPGoalAttractionEvaluation> evaluate)
        {
            StableId = stableId;
            Version = version;
            this.evaluate = evaluate ??
                throw new ArgumentNullException(nameof(evaluate));
        }

        public string StableId { get; }
        public string Version { get; }
        public int EvaluationCount { get; private set; }

        public KLEPGoalAttractionEvaluation Evaluate(
            KLEPGoalAttractionContext context)
        {
            EvaluationCount++;
            return evaluate(context);
        }
    }

    private sealed class FaultingExecutable : KLEPExecutableBase
    {
        private readonly string message;

        internal FaultingExecutable(
            KLEPExecutableDefinition definition,
            string message)
            : base(definition)
        {
            this.message = message;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ProbeExecutable : KLEPExecutableBase
    {
        private readonly KLEPExecutableTickStatus status;

        internal ProbeExecutable(
            KLEPExecutableDefinition definition,
            KLEPExecutableTickStatus status)
            : base(definition)
        {
            this.status = status;
        }

        internal int TickCount { get; private set; }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            TickCount++;
            return status;
        }
    }
}
