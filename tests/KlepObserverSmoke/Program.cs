using System;
using System.Collections.Generic;
using System.Reflection;
using Roll4d4.Klep.Core;
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

        KLEPDecisionTrace trace = neuron.Tick(0f, advice);

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

        KLEPDecisionTrace trace = neuron.Tick(0f, advice);

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

    private static KLEPExecutableDefinition Definition(
        string stableId,
        float score,
        KLEPExecutableKind kind = KLEPExecutableKind.Action,
        IEnumerable<KLEPLock> validationLocks = null,
        KLEPExecutionMode executionMode = KLEPExecutionMode.Solo)
    {
        return new KLEPExecutableDefinition(
            stableId,
            stableId,
            kind,
            validationLocks: validationLocks,
            baseAttractiveness: score,
            executionMode: executionMode);
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

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            return status;
        }
    }
}
