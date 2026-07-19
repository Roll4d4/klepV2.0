using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Emotion;
using Roll4d4.Klep.Ethics;
using Roll4d4.Klep.Memory;

internal static class Program
{
    private static int assertions;
    private static long keySequence;

    private static void Main()
    {
        VerifyModelCaptureCopiesFactsAndFadesOnlyDetail();
        VerifyExperienceRequiresAValidCausalTimeline();
        VerifySameTickWaveChronologyIsCausal();
        VerifyEthicsEvidenceCannotPostdateProducedEmotion();
        VerifyMomentsRejectImpossiblePerceptionState();
        VerifyActionOutcomeRequiresConsistentTerminalTruth();
        VerifyEmotionalConsequenceRetainsMotionAndPreferenceUsesIt();
#if KLEP_MEMORY_RUNTIME
        VerifyRecordAndReinforceOneProjectorCluster();
        VerifyCausalPhaseDirectionSeparatesProjectorClusters();
        VerifyColdMemoryForgetsOrArchivesByQualification();
        VerifySubUlpCoolingStillReachesExactCold();
        VerifyTraumaConsolidatesInBothEmotionalDirections();
        VerifyEpisodeDetailFadesWithoutErasingConsequence();
        VerifyRecallIsPureFilteredAndPreferenceAware();
        VerifyVelocityCancellationDoesNotManufactureRest();
        VerifyPositionCancellationDoesNotManufactureDesiredState();
        VerifyMissingEmotionDoesNotInventPreferenceEvidence();
        VerifySameTickExperiencesFollowConsequenceOrder();
        VerifyArchivedPatternsCanReturnToWorkingMemory();
        VerifyRehydrationRejectsContradictoryState();
        VerifyStateRoundTripContinuesDeterministically();
        VerifyUntickedStateCanRoundTrip();
        VerifySnapshotHistoryIsBoundedAndReadOnly();
        VerifyEquivalentRunsAreDeterministic();

        Console.WriteLine($"KLEP Memory smoke passed: {assertions} assertions.");
#else
        Console.WriteLine(
            $"KLEP Memory model smoke passed: {assertions} assertions; runtime smoke pending KLEPMemory.cs.");
#endif
    }

    private static void VerifyModelCaptureCopiesFactsAndFadesOnlyDetail()
    {
        var payload = new KLEPKeyPayload(new[]
        {
            new KeyValuePair<string, KLEPKeyValue>(
                "observedWorldTick",
                KLEPKeyValue.FromInteger(41)),
            new KeyValuePair<string, KLEPKeyValue>(
                "location",
                KLEPKeyValue.FromText("locker-a"))
        });
        var ammo = new KLEPKeyDefinition(
            new KLEPKeyId("knowledge.ammo"),
            "Ammo knowledge",
            scope: KLEPKeyScope.Local,
            defaultLifetime: KLEPKeyLifetime.Persistent,
            defaultPayload: payload);
        var neuron = new KLEPNeuron("memory.capture.fixture");
        neuron.InitializeKey(ammo, sourceId: "observer.scout");
        neuron.InitializeKey(ammo, sourceId: "observer.partner");

        KLEPKeySnapshot snapshot = neuron.TickViaAgent().KeySnapshot;
        KLEPMemoryMoment captured = KLEPMemoryMoment.Capture(
            "capture.prior",
            KLEPMemoryMomentRole.Prior,
            snapshot);

        Expect(captured.CapturedTick == 1 && captured.WaveIndex == 0,
            "A remembered moment keeps its observed Core Tick and wave");
        Expect(captured.Keys.Count == 2 && captured.KeyCells.Count == 1,
            "Memory copies every perceived occurrence while projector cells collapse stable identity");
        Expect(captured.Keys[0].DetailLevel == KLEPMemoryDetailLevel.Full &&
               captured.Keys[0].OccurrenceSequence > 0 &&
               captured.Keys[0].ActivatedTick == snapshot.Tick &&
               captured.Keys[0].PayloadFields.Count == 2,
            "Full Memory capture retains occurrence provenance and payload evidence");
        Expect(captured.Keys[0].SourceId.StartsWith("observer.", StringComparison.Ordinal),
            "Full Memory capture retains source provenance");

        KLEPMemoryMoment gist = captured.ToGist();
        Expect(gist.Keys.Count == 1 &&
               gist.Keys[0].DetailLevel == KLEPMemoryDetailLevel.KeyIdentityGist,
            "Fading collapses repeated occurrences to one stable Key identity");
        Expect(gist.Keys[0].KeyId == "knowledge.ammo" &&
               gist.Keys[0].PayloadFields.Count == 0 &&
               gist.Keys[0].OccurrenceSequence == 0 &&
               gist.Keys[0].IssuedTick == -1 &&
               !gist.Keys[0].Lifetime.HasValue,
            "A gist keeps Key identity but does not pretend discarded payload, timestamps, or lifetime remain");
        Expect(captured.Keys[0].DetailLevel == KLEPMemoryDetailLevel.Full &&
               captured.Keys[0].PayloadFields.Count == 2,
            "Producing a gist cannot mutate the captured full-detail moment");
    }

    private static void VerifyExperienceRequiresAValidCausalTimeline()
    {
        KLEPMemoryMoment prior = Moment(
            "timeline.prior", KLEPMemoryMomentRole.Prior, 2, "state.ground");
        KLEPMemoryMoment during = Moment(
            "timeline.during", KLEPMemoryMomentRole.During, 3, "state.air");
        KLEPMemoryMoment consequence = Moment(
            "timeline.consequence", KLEPMemoryMomentRole.Consequence, 4, "state.ground");

        var valid = new KLEPMemoryExperience(
            "experience.timeline.valid",
            4,
            new[] { prior, during, consequence });
        Expect(valid.Moments.Count == 3 &&
               valid.Moments[0].Role == KLEPMemoryMomentRole.Prior &&
               valid.Moments[2].Role == KLEPMemoryMomentRole.Consequence,
            "An experience accepts ordered Prior, During, and Consequence moments");
        Expect(valid.PriorGistId != valid.DuringGistId &&
               valid.CanonicalGistId.Contains("Prior", StringComparison.Ordinal) &&
               valid.CanonicalGistId.Contains("Consequence", StringComparison.Ordinal),
            "Canonical projector identity exposes causal phase rather than only a Key union");

        Expect(Catch(() => new KLEPMemoryExperience(
                   "experience.timeline.no-prior",
                   4,
                   new[] { during, consequence })) is ArgumentException,
            "An experience rejects a timeline without a Prior beginning");
        Expect(Catch(() => new KLEPMemoryExperience(
                   "experience.timeline.no-consequence",
                   4,
                   new[] { prior, during })) is ArgumentException,
            "An experience rejects a timeline without a Consequence ending");
        Expect(Catch(() => new KLEPMemoryExperience(
                   "experience.timeline.bad-middle",
                   4,
                   new[]
                   {
                       prior,
                       Moment("timeline.second-prior", KLEPMemoryMomentRole.Prior, 3, "state.air"),
                       consequence
                   })) is ArgumentException,
            "Only During moments may occur inside an experience timeline");
        Expect(Catch(() => new KLEPMemoryExperience(
                   "experience.timeline.reverse-time",
                   4,
                   new[]
                   {
                       Moment("timeline.late-prior", KLEPMemoryMomentRole.Prior, 4, "state.ground"),
                       Moment("timeline.early-consequence", KLEPMemoryMomentRole.Consequence, 3, "state.air")
                   })) is ArgumentException,
            "An experience rejects moments that run backward in Tick time");
        Expect(Catch(() => new KLEPMemoryExperience(
                   "experience.timeline.recorded-too-early",
                   3,
                   new[] { prior, consequence })) is ArgumentException,
            "An experience cannot be recorded before its consequence");

        var outsideAction = new KLEPMemoryActionOutcome(
            "action.jump", 1, 1, 4,
            KLEPExecutableState.Succeeded,
            KLEPExecutableExitReason.Succeeded);
        Expect(Catch(() => new KLEPMemoryExperience(
                   "experience.timeline.action-outside",
                   4,
                   new[] { prior, consequence },
                   outsideAction)) is ArgumentException,
            "A remembered action run must fit inside its remembered moments");
    }

    private static void VerifySameTickWaveChronologyIsCausal()
    {
        KLEPMemoryMoment basePrior = Moment(
            "wave.base-prior", KLEPMemoryMomentRole.Prior, 5, "state.before");
        KLEPMemoryMoment baseConsequence = Moment(
            "wave.base-consequence", KLEPMemoryMomentRole.Consequence, 5,
            "state.after");
        var latePrior = new KLEPMemoryMoment(
            "wave.late-prior",
            KLEPMemoryMomentRole.Prior,
            5,
            2,
            basePrior.Keys);
        var earlyConsequence = new KLEPMemoryMoment(
            "wave.early-consequence",
            KLEPMemoryMomentRole.Consequence,
            5,
            1,
            baseConsequence.Keys);

        Expect(Catch(() => new KLEPMemoryExperience(
                   "experience.wave-reversal",
                   5,
                   new[] { latePrior, earlyConsequence })) is ArgumentException,
            "A same-Tick experience cannot run backward through Tandem waves");

        var completionAfterConsequence = new KLEPMemoryActionOutcome(
            "action.wave-order",
            1,
            5,
            5,
            KLEPExecutableState.Succeeded,
            KLEPExecutableExitReason.Succeeded,
            waveIndex: 3,
            startedWaveIndex: 0);
        var prior = new KLEPMemoryMoment(
            "wave.prior",
            KLEPMemoryMomentRole.Prior,
            5,
            0,
            basePrior.Keys);
        var consequence = new KLEPMemoryMoment(
            "wave.consequence",
            KLEPMemoryMomentRole.Consequence,
            5,
            2,
            baseConsequence.Keys);
        Expect(Catch(() => new KLEPMemoryExperience(
                   "experience.action-after-consequence",
                   5,
                   new[] { prior, consequence },
                   completionAfterConsequence)) is ArgumentException,
            "An action cannot complete in a wave after its remembered consequence");
    }

    private static void VerifyEthicsEvidenceCannotPostdateProducedEmotion()
    {
        var prior = Moment(
            "ethics-time.prior",
            KLEPMemoryMomentRole.Prior,
            2,
            "state.before");
        var consequence = Moment(
            "ethics-time.consequence",
            KLEPMemoryMomentRole.Consequence,
            5,
            "state.after");
        var emotion = new KLEPMemoryEmotionalConsequence(
            "Valence",
            "Activation",
            2,
            3,
            KLEPEmotionVector.Zero,
            new KLEPEmotionVector(0.2f, 0f),
            KLEPEmotionVector.Zero,
            new KLEPEmotionVector(0.2f, 0f),
            new KLEPEmotionVector(0.1f, 0f),
            new KLEPEmotionVector(0.2f, 0f));
        var trace = new[]
        {
            new KLEPMemoryEthicsTraceRecord(
                "bias",
                applied: true,
                weight: 1f,
                proposedImpulse: new KLEPEmotionVector(0.2f, 0f),
                reasonCode: "fixture.ethics")
        };
        KLEPMemoryEthicsRecord EthicsAt(long tick, string id) =>
            new KLEPMemoryEthicsRecord(
                id,
                tick,
                KLEPEmotionInfluenceOrigin.Internal,
                "fixture.evaluator",
                "1",
                "Valence",
                "Activation",
                "fixture.context",
                "fixture.schema",
                "1",
                (double)0.2f,
                0d,
                wasClamped: false,
                impulse: new KLEPEmotionVector(0.2f, 0f),
                trace: trace);

        Expect(Catch(() => new KLEPMemoryExperience(
                   "experience.ethics-after-emotion",
                   5,
                   new[] { prior, consequence },
                   ethics: new[] { EthicsAt(4, "evaluation.late") },
                   emotion: emotion)) is ArgumentException,
            "Ethics evidence cannot claim it produced an Emotion state that already existed");
        var valid = new KLEPMemoryExperience(
            "experience.ethics-before-emotion",
            5,
            new[] { prior, consequence },
            ethics: new[] { EthicsAt(3, "evaluation.on-time") },
            emotion: emotion);
        Expect(valid.Ethics.Count == 1 &&
               valid.Ethics[0].EvaluationTick == emotion.ProducedTick,
            "Ethics evidence at or before the produced Emotion Tick remains causal");
    }

    private static void VerifyMomentsRejectImpossiblePerceptionState()
    {
        KLEPMemoryKeyRecord FutureActivated() => new KLEPMemoryKeyRecord(
            KLEPKeyScope.Local,
            "key.future",
            KLEPMemoryDetailLevel.Full,
            "store.local",
            1,
            KLEPKeyLifetime.Persistent,
            issuedTick: 5,
            activatedTick: 6);
        Expect(Catch(() => new KLEPMemoryMoment(
                   "moment.future",
                   KLEPMemoryMomentRole.Prior,
                   5,
                   0,
                   new[] { FutureActivated() })) is ArgumentException,
            "A moment rejects a Key that was not visible until a future Tick");

        var stalePulse = new KLEPMemoryKeyRecord(
            KLEPKeyScope.Local,
            "key.pulse",
            KLEPMemoryDetailLevel.Full,
            "store.local",
            2,
            KLEPKeyLifetime.OneCycle,
            issuedTick: 4,
            activatedTick: 4);
        Expect(Catch(() => new KLEPMemoryMoment(
                   "moment.stale-pulse",
                   KLEPMemoryMomentRole.Prior,
                   5,
                   0,
                   new[] { stalePulse })) is ArgumentException,
            "A moment rejects a stale OneCycle fact that no snapshot could expose");

        var exact = new KLEPMemoryKeyRecord(
            KLEPKeyScope.Local,
            "key.duplicate",
            KLEPMemoryDetailLevel.Full,
            "store.local",
            3,
            KLEPKeyLifetime.Persistent,
            issuedTick: 5,
            activatedTick: 5);
        Expect(Catch(() => new KLEPMemoryMoment(
                   "moment.duplicate-occurrence",
                   KLEPMemoryMomentRole.Prior,
                   5,
                   0,
                   new[] { exact, exact })) is ArgumentException,
            "A moment rejects one exact Key occurrence appearing twice");

        var globalTwin = new KLEPMemoryKeyRecord(
            KLEPKeyScope.Global,
            "key.duplicate",
            KLEPMemoryDetailLevel.Full,
            "store.global",
            1,
            KLEPKeyLifetime.Persistent,
            issuedTick: 5,
            activatedTick: 5);
        Expect(Catch(() => new KLEPMemoryMoment(
                   "moment.cross-scope",
                   KLEPMemoryMomentRole.Prior,
                   5,
                   0,
                   new[] { exact, globalTwin })) is ArgumentException,
            "A moment rejects one stable Key identity perceived in both scopes");
    }

    private static void VerifyActionOutcomeRequiresConsistentTerminalTruth()
    {
        var success = new KLEPMemoryActionOutcome(
            "action.climb", 1, 3, 4,
            KLEPExecutableState.Succeeded,
            KLEPExecutableExitReason.Succeeded,
            waveIndex: 2);
        Expect(success.WasSuccessful &&
               success.TerminalState == KLEPExecutableState.Succeeded &&
               success.ExitReason == KLEPExecutableExitReason.Succeeded &&
               success.WaveIndex == 2,
            "Memory preserves a successful terminal lifecycle result as factual outcome");
        Expect(Catch(() => new KLEPMemoryActionOutcome(
                   "action.running", 1, 3, 4,
                   KLEPExecutableState.Running,
                   KLEPExecutableExitReason.Succeeded)) is ArgumentException,
            "Memory rejects a nonterminal action as an outcome");
        Expect(Catch(() => new KLEPMemoryActionOutcome(
                   "action.inconsistent", 1, 3, 4,
                   KLEPExecutableState.Succeeded,
                   KLEPExecutableExitReason.Failed)) is ArgumentException,
            "Memory rejects disagreement between terminal state and exit reason");
        Expect(Catch(() => new KLEPMemoryActionOutcome(
                   "action.cancelled", 1, 3, 4,
                   KLEPExecutableState.Cancelled,
                   KLEPExecutableExitReason.Succeeded)) is ArgumentException,
            "Memory rejects an impossible cancellation reason");
        Expect(Catch(() => new KLEPMemoryActionOutcome(
                   "action.reverse-time", 1, 4, 3,
                   KLEPExecutableState.Failed,
                   KLEPExecutableExitReason.Failed)) is ArgumentOutOfRangeException,
            "Memory rejects an action outcome completed before it started");
    }

    private static void VerifyEmotionalConsequenceRetainsMotionAndPreferenceUsesIt()
    {
        Expect(Catch(() => new KLEPMemoryEmotionalConsequence(
                   "Valence",
                   "Activation",
                   1,
                   1,
                   KLEPEmotionVector.Zero,
                   new KLEPEmotionVector(1f, 0f),
                   KLEPEmotionVector.Zero,
                   new KLEPEmotionVector(1f, 0f),
                   new KLEPEmotionVector(0.9f, 0f),
                   new KLEPEmotionVector(1f, 0f)))
               is ArgumentOutOfRangeException,
            "A zero-duration emotional swing cannot manufacture trauma evidence");

        var emotion = new KLEPEmotion(
            new KLEPEmotionConfiguration(
                axisXName: "Valence",
                axisYName: "Activation",
                frictionPerTick: 0.1f),
            KLEPEmotionVector.Zero,
            new KLEPEmotionVector(0.2f, 0f));
        KLEPEmotionSnapshot produced = emotion.Advance(1, new[]
        {
            new KLEPEmotionInfluence(
                "ethics.helped",
                KLEPEmotionInfluenceOrigin.External,
                new KLEPEmotionVector(0.3f, 0f))
        });
        KLEPMemoryEmotionalConsequence consequence =
            KLEPMemoryEmotionalConsequence.Capture(
                0,
                KLEPEmotionVector.Zero,
                produced);

        Expect(consequence.ProducedState == produced.Position &&
               consequence.StartingVelocity == produced.VelocityBefore &&
               consequence.ProducedIntegratedVelocity == produced.IntegratedVelocity &&
               consequence.ProducedVelocity == produced.Velocity &&
               consequence.ProducedNetInfluence == produced.NetInfluence,
            "An emotional consequence copies both position and causal motion evidence");

        var preference = new KLEPEmotionalPreference(
            "Valence",
            "Activation",
            consequence.ProducedState,
            stabilityRadius: 0.01f,
            maximumStableSpeed: 0.05f);
        float positionAffinity = preference.EvaluateAffinity(consequence.ProducedState);
        float movingAffinity = preference.EvaluateStabilityAffinity(
            consequence.ProducedState,
            consequence.ProducedVelocity);
        float restingAffinity = preference.EvaluateStabilityAffinity(
            consequence.ProducedState,
            KLEPEmotionVector.Zero);
        Expect(positionAffinity == 1f && restingAffinity == 1f &&
               movingAffinity < restingAffinity,
            "An emotional preference can distinguish arriving at a desired state from being stable there");
    }

#if KLEP_MEMORY_RUNTIME
    private static void VerifyRecordAndReinforceOneProjectorCluster()
    {
        var configuration = Configuration(
            initialHeat: 1f,
            repetitionHeat: 0.75f,
            coolingPerTick: 0.1f,
            repetitionSimilarityThreshold: 0.6f,
            archiveRepetitionThreshold: 4);
        var memory = new KLEPMemory("owner.reinforce", configuration);
        KLEPMemoryExperience first = Experience(
            "reinforce.1", 1,
            new[] { "state.ground", "target.wall" },
            new[] { "state.air", "target.wall" },
            "action.jump");
        KLEPMemoryExperience second = Experience(
            "reinforce.2", 2,
            new[] { "target.wall", "state.ground" },
            new[] { "target.wall", "state.air" },
            "action.jump");

        KLEPMemorySnapshot recorded = memory.Tick(1, new[] { first });
        Expect(recorded.Clusters.Count == 1 &&
               recorded.Clusters[0].EncounterCount == 1 &&
               HasTransition(recorded, KLEPMemoryTransitionKind.Recorded),
            "A first experience records one inspectable working projector cluster");
        float heatAfterRecord = recorded.Clusters[0].Heat;

        KLEPMemorySnapshot reinforced = memory.Tick(2, new[] { second });
        Expect(reinforced.Clusters.Count == 1 &&
               reinforced.Clusters[0].EncounterCount == 2 &&
               HasTransition(reinforced, KLEPMemoryTransitionKind.Reinforced),
            "A phase-equivalent experience reinforces its existing cluster");
        Expect(reinforced.Clusters[0].Heat > heatAfterRecord &&
               reinforced.Clusters[0].RecentEpisodes.Count == 2,
            "Repetition adds heat and preserves bounded concrete exemplars");
        Expect(reinforced.Clusters[0].PhaseKeyFrequencies.Count == 4 &&
               reinforced.Clusters[0].CorePhaseKeyCells.Count == 4,
            "The projector records exact phase-aware Key frequencies and stable core cells");
    }

    private static void VerifyCausalPhaseDirectionSeparatesProjectorClusters()
    {
        var memory = new KLEPMemory(
            "owner.phase-direction",
            Configuration(
                coolingPerTick: 0.01f,
                repetitionSimilarityThreshold: 0.3f,
                archiveRepetitionThreshold: 4));
        KLEPMemoryExperience groundToAir = Experience(
            "phase.ground-to-air", 1,
            new[] { "context.shared", "state.ground" },
            new[] { "context.shared", "state.air" },
            "action.jump");
        KLEPMemoryExperience airToGround = Experience(
            "phase.air-to-ground", 2,
            new[] { "context.shared", "state.air" },
            new[] { "context.shared", "state.ground" },
            "action.jump");

        Expect(groundToAir.KeyCells.Count == 3 &&
               airToGround.KeyCells.Count == 3 &&
               groundToAir.CanonicalGistId != airToGround.CanonicalGistId,
            "Opposite causal directions may share a Key union but not a phase-aware gist");
        memory.Tick(1, new[] { groundToAir });
        KLEPMemorySnapshot snapshot = memory.Tick(2, new[] { airToGround });
        Expect(snapshot.Clusters.Count == 2 &&
               snapshot.Clusters[0].EncounterCount == 1 &&
               snapshot.Clusters[1].EncounterCount == 1,
            "Ground-to-air and air-to-ground experiences form separate projector clusters");
    }

    private static void VerifyColdMemoryForgetsOrArchivesByQualification()
    {
        var forgetful = new KLEPMemory(
            "owner.cold-forget",
            Configuration(
                initialHeat: 0.5f,
                repetitionHeat: 0.25f,
                coolingPerTick: 0.5f,
                archiveRepetitionThreshold: 2));
        forgetful.Tick(1, new[]
        {
            Experience(
                "cold.single", 1,
                new[] { "state.hungry" },
                new[] { "state.hungry" },
                "action.wait")
        });
        KLEPMemorySnapshot forgotten = forgetful.Tick(2);
        Expect(forgotten.Clusters.Count == 0 &&
               HasTransition(forgotten, KLEPMemoryTransitionKind.Forgotten),
            "An unqualified working memory is forgotten when its freshness and repetition heat cool away");

        var consolidating = new KLEPMemory(
            "owner.cold-archive",
            Configuration(
                initialHeat: 0.5f,
                repetitionHeat: 0.25f,
                coolingPerTick: 0.5f,
                archiveRepetitionThreshold: 2));
        consolidating.Tick(1, new[]
        {
            Experience(
                "cold.repeated.1", 1,
                new[] { "state.hungry" },
                new[] { "state.fed" },
                "action.eat")
        });
        KLEPMemorySnapshot repeated = consolidating.Tick(2, new[]
        {
            Experience(
                "cold.repeated.2", 2,
                new[] { "state.hungry" },
                new[] { "state.fed" },
                "action.eat")
        });
        Expect(repeated.ArchivedClusterCount == 1 &&
               repeated.Clusters[0].EncounterCount == 2 &&
               HasTransition(repeated, KLEPMemoryTransitionKind.Archived),
            "A repeated pattern consolidates instead of being lost when it reaches archive qualification");
    }

    private static void VerifySubUlpCoolingStillReachesExactCold()
    {
        var memory = new KLEPMemory(
            "owner.sub-ulp-cooling",
            Configuration(
                initialHeat: 0.5f,
                coolingPerTick: float.Epsilon,
                archiveRepetitionThreshold: 2));
        memory.Tick(1, new[]
        {
            Experience(
                "sub-ulp.single", 1,
                new[] { "state.remembered" },
                new[] { "state.remembered" },
                "action.wait")
        });

        KLEPMemorySnapshot cooled = memory.Tick(2);
        KLEPMemoryTransition cooling = null;
        for (int i = 0; i < cooled.Transitions.Count; i++)
        {
            if (cooled.Transitions[i].Kind == KLEPMemoryTransitionKind.Cooled)
            {
                cooling = cooled.Transitions[i];
                break;
            }
        }

        Expect(cooling != null &&
               cooling.HeatBefore == 0.5f &&
               cooling.Cooling == 0.5f &&
               cooling.HeatAfter == 0f,
            "Positive sub-ULP cooling reaches exact cold and traces its actual heat delta");
        Expect(cooled.Clusters.Count == 0 &&
               HasTransition(cooled, KLEPMemoryTransitionKind.Forgotten),
            "Exact cold from sub-ULP cooling follows the ordinary same-Tick forgetting policy");
    }

    private static void VerifyTraumaConsolidatesInBothEmotionalDirections()
    {
        VerifyOneTraumaDirection("positive", 0.8f);
        VerifyOneTraumaDirection("negative", -0.8f);
    }

    private static void VerifyOneTraumaDirection(string suffix, float producedX)
    {
        var memory = new KLEPMemory(
            "owner.trauma." + suffix,
            Configuration(
                traumaSwingThreshold: 0.75f,
                archiveSwingThreshold: 0.5f,
                archiveRepetitionThreshold: 5));
        KLEPMemorySnapshot snapshot = memory.Tick(1, new[]
        {
            Experience(
                "trauma." + suffix,
                1,
                new[] { "event.pending" },
                new[] { "event.complete" },
                "action.experience",
                producedEmotionX: producedX)
        });

        Expect(snapshot.ArchivedClusterCount == 1 &&
               snapshot.Clusters[0].TraumaCount == 1 &&
               snapshot.Clusters[0].PeakEmotionalSwing >= 0.79f &&
               HasTransition(snapshot, KLEPMemoryTransitionKind.TraumaArchived),
            $"A large {suffix} emotional swing immediately consolidates as trauma without moralizing its direction");
    }

    private static void VerifyEpisodeDetailFadesWithoutErasingConsequence()
    {
        var memory = new KLEPMemory(
            "owner.detail-fade",
            Configuration(
                initialHeat: 2f,
                coolingPerTick: 0.01f,
                archiveRepetitionThreshold: 5,
                fullDetailRetentionTicks: 1));
        KLEPMemoryExperience detailed = Experience(
            "detail.full",
            1,
            new[] { "state.hungry" },
            new[] { "state.fed" },
            "action.eat",
            producedEmotionX: 0.25f);
        memory.Tick(1, new[] { detailed });
        memory.Tick(2);
        KLEPMemorySnapshot faded = memory.Tick(3);
        KLEPMemoryClusterSnapshot cluster = faded.Clusters[0];

        Expect(HasTransition(faded, KLEPMemoryTransitionKind.DetailFaded),
            "A retained episode emits an inspectable transition when full detail ages out");
        Expect(cluster.RecentEpisodes.Count == 1 &&
               !cluster.RecentEpisodes[0].HasFullDetail &&
               cluster.RecentEpisodes[0].Moments[0].Keys[0].KeyId == "state.hungry",
            "Detail fading retains the episode's Key identities");
        Expect(cluster.RecentEpisodes[0].ActionOutcome != null &&
               cluster.RecentEpisodes[0].ActionOutcome.WasSuccessful &&
               cluster.RecentEpisodes[0].Emotion != null &&
               cluster.RecentEpisodes[0].Emotion.ProducedState.X == 0.25f,
            "Detail fading does not erase factual action or emotional consequence");
    }

    private static void VerifyRecallIsPureFilteredAndPreferenceAware()
    {
        var memory = new KLEPMemory(
            "owner.recall",
            Configuration(
                initialHeat: 2f,
                coolingPerTick: 0.01f,
                repetitionSimilarityThreshold: 0.8f,
                archiveRepetitionThreshold: 5,
                recallSimilarityThreshold: 0.1f));
        memory.Tick(1, new[]
        {
            Experience(
                "recall.calm", 1,
                new[] { "context.shared", "route.calm" },
                new[] { "context.shared", "result.safe" },
                "action.calm",
                producedEmotionX: 0.5f,
                producedVelocityX: 0f)
        });
        memory.Tick(2, new[]
        {
            Experience(
                "recall.volatile", 2,
                new[] { "context.shared", "route.volatile" },
                new[] { "context.shared", "result.risky" },
                "action.volatile",
                producedEmotionX: 0.5f,
                producedVelocityX: 0.8f)
        });

        var preference = new KLEPEmotionalPreference(
            "Valence",
            "Activation",
            new KLEPEmotionVector(0.5f, 0f),
            stabilityRadius: 0.05f,
            maximumStableSpeed: 0.1f);
        var cue = new KLEPMemoryCue(
            new[] { new KLEPMemoryKeyCell(KLEPKeyScope.Local, "context.shared") },
            preference: preference);
        string before = Describe(memory.Snapshot);
        int historyBefore = memory.GetSnapshotHistory().Count;
        KLEPMemoryRecallResult first = memory.Recall(cue);
        KLEPMemoryRecallResult second = memory.Recall(cue);

        Expect(first.Matches.Count == 2 &&
               Describe(first) == Describe(second),
            "Repeated recall from unchanged Memory is stable and deterministic");
        Expect(Describe(memory.Snapshot) == before &&
               memory.GetSnapshotHistory().Count == historyBefore,
            "Recall is a pure read and cannot heat, cool, reorder, or snapshot Memory");
        KLEPMemoryRecall calm = FindRecall(first, "action.calm");
        KLEPMemoryRecall volatileRecall = FindRecall(first, "action.volatile");
        Expect(calm.PreferenceAffinity.HasValue &&
               volatileRecall.PreferenceAffinity.HasValue &&
               calm.PreferenceAffinity.Value > volatileRecall.PreferenceAffinity.Value,
            "Preference affinity considers produced emotional velocity as well as destination");

        var filteredCue = new KLEPMemoryCue(
            cue.KeyCells,
            actionStableId: "action.calm",
            preference: preference);
        KLEPMemoryRecallResult filtered = memory.Recall(filteredCue, 8);
        Expect(filtered.Matches.Count == 1 &&
               filtered.Matches[0].Cluster.ActionStableId == "action.calm",
            "An action-filtered cue cannot return another action's cluster");
    }

    private static void VerifyVelocityCancellationDoesNotManufactureRest()
    {
        var memory = new KLEPMemory(
            "owner.velocity-cancellation",
            Configuration(
                initialHeat: 2f,
                coolingPerTick: 0.01f,
                archiveSwingThreshold: 2f,
                archiveRepetitionThreshold: 5));
        memory.Tick(1, new[]
        {
            Experience(
                "velocity.positive", 1,
                new[] { "context.velocity" },
                new[] { "result.velocity" },
                "action.velocity",
                producedEmotionX: 0.5f,
                producedVelocityX: 0.8f)
        });
        memory.Tick(2, new[]
        {
            Experience(
                "velocity.negative", 2,
                new[] { "context.velocity" },
                new[] { "result.velocity" },
                "action.velocity",
                producedEmotionX: 0.5f,
                producedVelocityX: -0.8f)
        });

        KLEPMemoryClusterSnapshot cluster = memory.Snapshot.Clusters[0];
        Expect(cluster.AverageProducedVelocity == KLEPEmotionVector.Zero &&
               Math.Abs(cluster.AverageProducedSpeed - 0.8f) < 0.0001f,
            "Opposite emotional velocities may cancel direction but not remembered speed");
        var preference = new KLEPEmotionalPreference(
            "Valence",
            "Activation",
            new KLEPEmotionVector(0.5f, 0f),
            stabilityRadius: 0.05f,
            maximumStableSpeed: 0.1f);
        KLEPMemoryRecallResult recall = memory.Recall(new KLEPMemoryCue(
            new[]
            {
                new KLEPMemoryKeyCell(
                    KLEPKeyScope.Local,
                    "context.velocity")
            },
            preference: preference));
        Expect(recall.Matches.Count == 1 &&
               recall.Matches[0].PreferenceAffinity.HasValue &&
               recall.Matches[0].PreferenceAffinity.Value < 1f,
            "Signed velocity cancellation cannot manufacture remembered emotional rest");
        Expect(Math.Abs(recall.Matches[0].RepetitionStrength - (2f / 6f)) <
                   0.0001f,
            "Recall repetition uses N over N plus the configured scale");
    }

    private static void VerifyPositionCancellationDoesNotManufactureDesiredState()
    {
        var memory = new KLEPMemory(
            "owner.position-cancellation",
            Configuration(
                initialHeat: 2f,
                coolingPerTick: 0.01f,
                archiveSwingThreshold: 2f,
                archiveRepetitionThreshold: 5));
        memory.Tick(1, new[]
        {
            Experience(
                "position.positive", 1,
                new[] { "context.position" },
                new[] { "result.position" },
                "action.position",
                producedEmotionX: 0.5f)
        });
        memory.Tick(2, new[]
        {
            Experience(
                "position.negative", 2,
                new[] { "context.position" },
                new[] { "result.position" },
                "action.position",
                producedEmotionX: -0.5f)
        });

        KLEPMemoryClusterSnapshot cluster = memory.Snapshot.Clusters[0];
        Expect(cluster.AverageProducedEmotion == KLEPEmotionVector.Zero &&
               Math.Abs(cluster.RootMeanSquareDistanceTo(
                   KLEPEmotionVector.Zero) - 0.5f) < 0.0001f,
            "Opposite produced states may average to zero without claiming either experience occurred there");
        var preference = new KLEPEmotionalPreference(
            "Valence",
            "Activation",
            KLEPEmotionVector.Zero,
            stabilityRadius: 0.05f,
            maximumStableSpeed: 0.1f);
        KLEPMemoryRecallResult recall = memory.Recall(new KLEPMemoryCue(
            new[]
            {
                new KLEPMemoryKeyCell(
                    KLEPKeyScope.Local,
                    "context.position")
            },
            preference: preference));
        Expect(recall.Matches.Count == 1 &&
               recall.Matches[0].PreferenceAffinity.HasValue &&
               recall.Matches[0].PreferenceAffinity.Value < 1f,
            "A bimodal produced-state history cannot masquerade as a stable desired state");
    }

    private static void VerifyMissingEmotionDoesNotInventPreferenceEvidence()
    {
        var memory = new KLEPMemory(
            "owner.no-emotion",
            Configuration(initialHeat: 2f, coolingPerTick: 0.01f));
        memory.Tick(1, new[]
        {
            Experience(
                "no-emotion.1",
                1,
                new[] { "context.no-emotion" },
                new[] { "result.no-emotion" },
                "action.no-emotion")
        });
        var cells = new[]
        {
            new KLEPMemoryKeyCell(
                KLEPKeyScope.Local,
                "context.no-emotion")
        };
        KLEPMemoryRecall withoutPreference = memory.Recall(
            new KLEPMemoryCue(cells)).Matches[0];
        KLEPMemoryRecall withPreference = memory.Recall(
            new KLEPMemoryCue(
                cells,
                preference: new KLEPEmotionalPreference(
                    "Valence",
                    "Activation",
                    KLEPEmotionVector.Zero))).Matches[0];
        Expect(!withPreference.PreferenceAffinity.HasValue &&
               Math.Abs(withPreference.RecallStrength -
                   withoutPreference.RecallStrength) < 0.0001f,
            "A preference cue cannot add fictional neutral evidence when an experience recorded no Emotion result");
    }

    private static void VerifySameTickExperiencesFollowConsequenceOrder()
    {
        var memory = new KLEPMemory(
            "owner.same-tick-order",
            Configuration(
                initialHeat: 0.5f,
                repetitionHeat: 0.25f,
                coolingPerTick: 0.01f,
                traumaSwingThreshold: 0.8f,
                archiveSwingThreshold: 2f,
                archiveRepetitionThreshold: 10));
        KLEPMemoryExperience earlier = ExperienceAtConsequenceWave(
            "z.earlier",
            recordedTick: 6,
            consequenceTick: 5,
            consequenceWave: 1,
            producedEmotionX: 0.1f);
        KLEPMemoryExperience laterTrauma = ExperienceAtConsequenceWave(
            "a.later-trauma",
            recordedTick: 6,
            consequenceTick: 5,
            consequenceWave: 3,
            producedEmotionX: 1f);

        KLEPMemorySnapshot snapshot = memory.Tick(
            6,
            new[] { laterTrauma, earlier });
        KLEPMemoryClusterSnapshot cluster = snapshot.Clusters[0];
        Expect(snapshot.Transitions[0].ExperienceId == earlier.ExperienceId &&
               snapshot.Transitions[1].ExperienceId == laterTrauma.ExperienceId &&
               snapshot.Transitions[2].Kind ==
                   KLEPMemoryTransitionKind.TraumaArchived,
            "Same-Tick experiences are applied by consequence Tick and wave, not lexical ID");
        Expect(cluster.IsArchived && !cluster.IsWorking &&
               cluster.MostRecentProducedEmotion ==
                   laterTrauma.Emotion.ProducedState,
            "The causally latest same-Tick trauma remains archived and owns the most-recent Emotion result");
    }

    private static void VerifyArchivedPatternsCanReturnToWorkingMemory()
    {
        var memory = new KLEPMemory(
            "owner.reheated-archive",
            Configuration(
                initialHeat: 0.5f,
                repetitionHeat: 0.25f,
                coolingPerTick: 0.5f,
                archiveSwingThreshold: 2f,
                archiveRepetitionThreshold: 2));
        memory.Tick(1, new[]
        {
            Experience(
                "reheat.1", 1,
                new[] { "state.cue" },
                new[] { "state.result" },
                "action.repeat")
        });
        KLEPMemorySnapshot consolidated = memory.Tick(2, new[]
        {
            Experience(
                "reheat.2", 2,
                new[] { "state.cue" },
                new[] { "state.result" },
                "action.repeat")
        });
        Expect(consolidated.Clusters[0].IsArchived &&
               consolidated.Clusters[0].IsWorking,
            "A repeated pattern may be consolidated while its hot copy remains working");

        KLEPMemorySnapshot cold = memory.Tick(4);
        Expect(cold.Clusters[0].IsArchived && !cold.Clusters[0].IsWorking,
            "Cooling removes only the working presence of a consolidated pattern");
        KLEPMemorySnapshot reheated = memory.Tick(5, new[]
        {
            Experience(
                "reheat.3", 5,
                new[] { "state.cue" },
                new[] { "state.result" },
                "action.repeat")
        });
        Expect(reheated.Clusters[0].IsArchived &&
               reheated.Clusters[0].IsWorking &&
               reheated.Clusters[0].Heat > 0f,
            "Re-experiencing a deep pattern returns a hot copy to working Memory");
    }

    private static void VerifyRehydrationRejectsContradictoryState()
    {
        var memory = new KLEPMemory(
            "owner.rehydrate-validation",
            Configuration(initialHeat: 2f, coolingPerTick: 0.01f));
        memory.Tick(1, new[]
        {
            Experience(
                "rehydrate.1",
                1,
                new[] { "context.rehydrate" },
                new[] { "result.rehydrate" },
                "action.rehydrate",
                producedEmotionX: 0.4f,
                producedVelocityX: 0.3f)
        });
        memory.Tick(2);
        KLEPMemoryState captured = memory.CaptureState();
        KLEPMemoryClusterSnapshot cluster = captured.Clusters[0];

        Expect(Catch(() => RehydrateCluster(
                   cluster,
                   producedPositionSquaredMagnitudeSum: 0d))
               is ArgumentException,
            "Rehydration rejects a produced position sum whose squared evidence says it never occurred");
        Expect(Catch(() => RehydrateCluster(
                   cluster,
                   producedSpeedSum: 0d)) is ArgumentException,
            "Rehydration rejects a signed velocity sum with fictional zero non-cancelling speed");
        Expect(Catch(() => RehydrateCluster(
                   cluster,
                   actionStableId: "   ")) is ArgumentException,
            "Rehydration rejects a whitespace action identity that no experience could reinforce");
        Expect(Catch(() => RehydrateCluster(
                   cluster,
                   succeededCount: long.MaxValue,
                   failedCount: long.MaxValue,
                   cancelledCount: 3)) is ArgumentException,
            "Rehydration checks terminal outcome counts without signed overflow");

        KLEPMemoryClusterSnapshot impossibleHistoricalCluster =
            RehydrateCluster(cluster, heat: 0f, isWorking: true);
        var impossibleHistory = new[]
        {
            new KLEPMemorySnapshot(
                captured.OwnerId,
                1,
                new[] { impossibleHistoricalCluster },
                Array.Empty<KLEPMemoryTransition>()),
            captured.SnapshotHistory[captured.SnapshotHistory.Count - 1]
        };
        var impossibleState = new KLEPMemoryState(
            captured.OwnerId,
            captured.Tick,
            captured.NextClusterSequence,
            captured.Configuration,
            captured.Clusters,
            captured.SeenExperienceIds,
            impossibleHistory,
            captured.LastTransitions);
        Expect(Catch(() => KLEPMemory.Restore(impossibleState))
               is ArgumentException,
            "Restore deep-validates earlier diagnostic snapshots instead of trusting only the current tail");
    }

    private static void VerifyStateRoundTripContinuesDeterministically()
    {
        KLEPMemoryConfiguration configuration = Configuration(
            initialHeat: 2f,
            coolingPerTick: 0.1f,
            archiveRepetitionThreshold: 4);
        var original = new KLEPMemory("owner.restore", configuration);
        original.Tick(1, new[]
        {
            Experience(
                "restore.1", 1,
                new[] { "state.ready" },
                new[] { "state.done" },
                "action.work",
                producedEmotionX: 0.25f,
                producedVelocityX: 0.1f)
        });
        original.Tick(2);

        KLEPMemoryState captured = original.CaptureState();
        var state = new KLEPMemoryState(
            captured.OwnerId,
            captured.Tick,
            captured.NextClusterSequence,
            captured.Configuration,
            captured.Clusters,
            captured.SeenExperienceIds,
            captured.SnapshotHistory,
            captured.LastTransitions,
            captured.SchemaVersion);
        KLEPMemory restored = KLEPMemory.Restore(state);
        Expect(Describe(original.Snapshot) == Describe(restored.Snapshot) &&
               restored.CaptureState().OwnerId == "owner.restore" &&
               restored.CaptureState().Tick == 2,
            "Restoring data-first state preserves owner, Tick, clusters, sums, and exemplars");
        Expect(DescribeHistory(original.GetSnapshotHistory()) ==
                   DescribeHistory(restored.GetSnapshotHistory()) &&
               ReferenceEquals(
                   restored.Snapshot,
                   restored.GetSnapshotHistory()[
                       restored.GetSnapshotHistory().Count - 1]),
            "Restore preserves the bounded public snapshot history and its canonical current tail");
        Expect(typeof(KLEPMemoryState).GetConstructors().Length > 0 &&
               typeof(KLEPMemorySnapshot).GetConstructors().Length > 0 &&
               typeof(KLEPMemoryClusterSnapshot).GetConstructors().Length > 0 &&
               typeof(KLEPMemoryTransition).GetConstructors().Length > 0 &&
               typeof(KLEPMemoryEthicsRecord).GetConstructors().Length > 0 &&
               typeof(KLEPMemoryEthicsTraceRecord).GetConstructors().Length > 0,
            "A project-owned persistence adapter can publicly reconstruct every non-Core Memory state layer");

        KLEPMemoryExperience next = Experience(
            "restore.2", 3,
            new[] { "state.ready" },
            new[] { "state.done" },
            "action.work",
            producedEmotionX: -0.25f,
            producedVelocityX: -0.1f);
        KLEPMemorySnapshot originalNext = original.Tick(3, new[] { next });
        KLEPMemorySnapshot restoredNext = restored.Tick(3, new[] { next });
        Expect(Describe(originalNext) == Describe(restoredNext) &&
               Describe(original.CaptureState()) == Describe(restored.CaptureState()),
            "Original and restored Memory continue identically from the same future input");
    }

    private static void VerifyUntickedStateCanRoundTrip()
    {
        var memory = new KLEPMemory("owner.unticked", Configuration());
        KLEPMemoryState state = memory.CaptureState();
        KLEPMemory restored = KLEPMemory.Restore(state);
        Expect(state.Tick == 0 && state.NextClusterSequence == 1 &&
               restored.CurrentTick == 0 && restored.Snapshot.Clusters.Count == 0 &&
               state.SnapshotHistory.Count == 0 &&
               restored.GetSnapshotHistory().Count == 0,
            "An Agent may persist and restore Memory before its first experienced Tick");
    }

    private static void VerifySnapshotHistoryIsBoundedAndReadOnly()
    {
        var memory = new KLEPMemory(
            "owner.snapshots",
            Configuration(
                initialHeat: 2f,
                coolingPerTick: 0.01f,
                snapshotCapacity: 2));
        memory.Tick(1);
        memory.Tick(2);
        memory.Tick(3);

        IReadOnlyList<KLEPMemorySnapshot> history = memory.GetSnapshotHistory();
        Expect(history.Count == 2 && history[0].Tick == 2 && history[1].Tick == 3,
            "Memory retains only its configured recent snapshot window in Tick order");
        var mutable = history as IList<KLEPMemorySnapshot>;
        Expect(mutable == null || Catch(() => mutable.Add(memory.Snapshot))
               is NotSupportedException,
            "Snapshot history does not expose mutable backing storage");
        var transitions = memory.Snapshot.Transitions as IList<KLEPMemoryTransition>;
        Expect(transitions == null || Catch(() => transitions.Clear())
               is NotSupportedException,
            "A Memory snapshot's transition trace is read-only");
    }

    private static void VerifyEquivalentRunsAreDeterministic()
    {
        string first = RunDeterministicScenario();
        string second = RunDeterministicScenario();
        Expect(first == second,
            "Equivalent Memory owners produce byte-for-byte equivalent state, trace, and recall descriptions");
    }

    private static string RunDeterministicScenario()
    {
        var memory = new KLEPMemory(
            "owner.deterministic",
            Configuration(
                initialHeat: 2f,
                coolingPerTick: 0.1f,
                repetitionSimilarityThreshold: 0.6f,
                archiveRepetitionThreshold: 3,
                fullDetailRetentionTicks: 2,
                snapshotCapacity: 4));
        memory.Tick(1, new[]
        {
            Experience(
                "deterministic.1", 1,
                new[] { "state.a", "context.shared" },
                new[] { "state.b", "context.shared" },
                "action.advance",
                producedEmotionX: 0.25f,
                producedVelocityX: 0.1f)
        });
        memory.Tick(2, new[]
        {
            Experience(
                "deterministic.2", 2,
                new[] { "context.shared", "state.a" },
                new[] { "context.shared", "state.b" },
                "action.advance",
                producedEmotionX: -0.25f,
                producedVelocityX: -0.1f)
        });
        memory.Tick(3);
        memory.Tick(4);
        var cue = new KLEPMemoryCue(new[]
        {
            new KLEPMemoryKeyCell(KLEPKeyScope.Local, "context.shared")
        });

        var text = new StringBuilder();
        text.Append(Describe(memory.CaptureState()));
        text.Append('|').Append(Describe(memory.Recall(cue)));
        IReadOnlyList<KLEPMemorySnapshot> history = memory.GetSnapshotHistory();
        for (int i = 0; i < history.Count; i++)
        {
            text.Append('|').Append(Describe(history[i]));
        }

        return text.ToString();
    }
#endif

    private static KLEPMemoryConfiguration Configuration(
        float initialHeat = 1f,
        float repetitionHeat = 0.75f,
        float coolingPerTick = 0.1f,
        float repetitionSimilarityThreshold = 0.6f,
        float traumaSwingThreshold = 1f,
        float archiveSwingThreshold = 0.5f,
        int archiveRepetitionThreshold = 2,
        long fullDetailRetentionTicks = 50,
        int snapshotCapacity = 32,
        float recallSimilarityThreshold = 0.2f)
    {
        return new KLEPMemoryConfiguration(
            axisXName: "Valence",
            axisYName: "Activation",
            initialHeat: initialHeat,
            repetitionHeat: repetitionHeat,
            emotionalSalienceScale: 1f,
            coolingPerTick: coolingPerTick,
            maximumHeat: 10f,
            repetitionSimilarityThreshold: repetitionSimilarityThreshold,
            coreKeyFrequencyThreshold: 0.5f,
            traumaSwingThreshold: traumaSwingThreshold,
            archiveSwingThreshold: archiveSwingThreshold,
            archiveRepetitionThreshold: archiveRepetitionThreshold,
            indelibleTraumaRepetitions: 3,
            workingCapacity: 16,
            archivedCapacity: 16,
            recentEpisodeCapacity: 3,
            memorableEpisodeCapacity: 2,
            fullDetailRetentionTicks: fullDetailRetentionTicks,
            snapshotCapacity: snapshotCapacity,
            recallSimilarityThreshold: recallSimilarityThreshold,
            recallRepetitionScale: 4f);
    }

    private static KLEPMemoryExperience Experience(
        string experienceId,
        long tick,
        IReadOnlyList<string> priorKeys,
        IReadOnlyList<string> consequenceKeys,
        string actionStableId,
        float? producedEmotionX = null,
        float producedVelocityX = 0f,
        IReadOnlyList<string> duringKeys = null)
    {
        if (tick <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                "The fixture requires a prior Tick before the recorded consequence.");
        }

        long priorTick = tick - 1;
        var moments = new List<KLEPMemoryMoment>
        {
            Moment(
                experienceId + ".prior",
                KLEPMemoryMomentRole.Prior,
                priorTick,
                priorKeys)
        };
        if (duringKeys != null)
        {
            moments.Add(Moment(
                experienceId + ".during",
                KLEPMemoryMomentRole.During,
                tick,
                duringKeys));
        }

        moments.Add(Moment(
            experienceId + ".consequence",
            KLEPMemoryMomentRole.Consequence,
            tick,
            consequenceKeys));

        KLEPMemoryActionOutcome outcome = string.IsNullOrWhiteSpace(actionStableId)
            ? null
            : new KLEPMemoryActionOutcome(
                actionStableId,
                tick + 1,
                priorTick,
                tick,
                KLEPExecutableState.Succeeded,
                KLEPExecutableExitReason.Succeeded);
        KLEPMemoryEmotionalConsequence emotion = !producedEmotionX.HasValue
            ? null
            : new KLEPMemoryEmotionalConsequence(
                "Valence",
                "Activation",
                priorTick,
                tick,
                KLEPEmotionVector.Zero,
                new KLEPEmotionVector(producedEmotionX.Value, 0f),
                startingVelocity: KLEPEmotionVector.Zero,
                producedIntegratedVelocity:
                    new KLEPEmotionVector(producedVelocityX, 0f),
                producedVelocity: new KLEPEmotionVector(producedVelocityX, 0f),
                producedNetInfluence: KLEPEmotionVector.Zero);
        return new KLEPMemoryExperience(
            experienceId,
            tick,
            moments,
            outcome,
            emotion: emotion);
    }

    private static KLEPMemoryExperience ExperienceAtConsequenceWave(
        string experienceId,
        long recordedTick,
        long consequenceTick,
        int consequenceWave,
        float producedEmotionX)
    {
        if (consequenceTick <= 0 || recordedTick < consequenceTick ||
            consequenceWave <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(consequenceTick));
        }

        long priorTick = consequenceTick - 1;
        KLEPMemoryMoment prior = Moment(
            experienceId + ".prior",
            KLEPMemoryMomentRole.Prior,
            priorTick,
            "context.same-tick");
        KLEPMemoryMoment baseConsequence = Moment(
            experienceId + ".consequence",
            KLEPMemoryMomentRole.Consequence,
            consequenceTick,
            "result.same-tick");
        var consequence = new KLEPMemoryMoment(
            baseConsequence.MomentId,
            baseConsequence.Role,
            baseConsequence.CapturedTick,
            consequenceWave,
            baseConsequence.Keys);
        var outcome = new KLEPMemoryActionOutcome(
            "action.same-tick",
            consequenceWave,
            priorTick,
            consequenceTick,
            KLEPExecutableState.Succeeded,
            KLEPExecutableExitReason.Succeeded,
            waveIndex: consequenceWave,
            startedWaveIndex: 0);
        var emotion = new KLEPMemoryEmotionalConsequence(
            "Valence",
            "Activation",
            priorTick,
            consequenceTick,
            KLEPEmotionVector.Zero,
            new KLEPEmotionVector(producedEmotionX, 0f),
            KLEPEmotionVector.Zero,
            new KLEPEmotionVector(producedEmotionX, 0f),
            KLEPEmotionVector.Zero,
            new KLEPEmotionVector(producedEmotionX, 0f));
        return new KLEPMemoryExperience(
            experienceId,
            recordedTick,
            new[] { prior, consequence },
            outcome,
            emotion: emotion);
    }

    private static KLEPMemoryMoment Moment(
        string momentId,
        KLEPMemoryMomentRole role,
        long tick,
        params string[] keys)
    {
        return Moment(momentId, role, tick, (IReadOnlyList<string>)keys);
    }

    private static KLEPMemoryMoment Moment(
        string momentId,
        KLEPMemoryMomentRole role,
        long tick,
        IReadOnlyList<string> keys)
    {
        var records = new List<KLEPMemoryKeyRecord>(keys.Count);
        for (int i = 0; i < keys.Count; i++)
        {
            long sequence = ++keySequence;
            records.Add(new KLEPMemoryKeyRecord(
                KLEPKeyScope.Local,
                keys[i],
                KLEPMemoryDetailLevel.Full,
                occurrenceStoreId: "memory.fixture.keys",
                occurrenceSequence: sequence,
                lifetime: KLEPKeyLifetime.Persistent,
                issuedTick: tick,
                activatedTick: tick,
                sourceId: "memory.fixture",
                payloadFields: new[]
                {
                    new KLEPKeyField(
                        "observedWorldTick",
                        KLEPKeyValue.FromInteger(tick))
                }));
        }

        return new KLEPMemoryMoment(momentId, role, tick, 0, records);
    }

    private static bool HasTransition(
        KLEPMemorySnapshot snapshot,
        KLEPMemoryTransitionKind kind)
    {
        for (int i = 0; i < snapshot.Transitions.Count; i++)
        {
            if (snapshot.Transitions[i].Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static KLEPMemoryRecall FindRecall(
        KLEPMemoryRecallResult result,
        string actionStableId)
    {
        for (int i = 0; i < result.Matches.Count; i++)
        {
            if (result.Matches[i].Cluster.ActionStableId == actionStableId)
            {
                return result.Matches[i];
            }
        }

        throw new InvalidOperationException(
            $"Expected recalled action '{actionStableId}'.");
    }

    private static string Describe(KLEPMemoryRecallResult result)
    {
        var text = new StringBuilder();
        text.Append(result.Tick.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < result.Matches.Count; i++)
        {
            KLEPMemoryRecall recall = result.Matches[i];
            text.Append('|').Append(recall.Cluster.ClusterId)
                .Append(':').Append(Float(recall.CueSimilarity))
                .Append(':').Append(Float(recall.RepetitionStrength))
                .Append(':').Append(Float(recall.FreshnessStrength))
                .Append(':').Append(Float(recall.EmotionalStrength))
                .Append(':').Append(Float(recall.RecallStrength))
                .Append(':').Append(recall.PreferenceAffinity.HasValue
                    ? Float(recall.PreferenceAffinity.Value)
                    : "none");
        }

        return text.ToString();
    }

    private static string Describe(KLEPMemoryState state)
    {
        var text = new StringBuilder();
        text.Append(state.SchemaVersion.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(state.OwnerId)
            .Append(':').Append(state.Tick.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(state.NextClusterSequence.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < state.SeenExperienceIds.Count; i++)
        {
            text.Append(":seen=").Append(state.SeenExperienceIds[i]);
        }
        for (int i = 0; i < state.Clusters.Count; i++)
        {
            AppendCluster(text, state.Clusters[i]);
        }
        for (int i = 0; i < state.LastTransitions.Count; i++)
        {
            KLEPMemoryTransition transition = state.LastTransitions[i];
            text.Append(":last=").Append(transition.Kind)
                .Append('/').Append(transition.ClusterId)
                .Append('/').Append(transition.ExperienceId)
                .Append('/').Append(Float(transition.HeatAfter));
        }
        for (int i = 0; i < state.SnapshotHistory.Count; i++)
        {
            text.Append(":history=")
                .Append(Describe(state.SnapshotHistory[i]));
        }

        return text.ToString();
    }

    private static string DescribeHistory(
        IReadOnlyList<KLEPMemorySnapshot> history)
    {
        var text = new StringBuilder();
        for (int i = 0; i < history.Count; i++)
        {
            text.Append('[').Append(Describe(history[i])).Append(']');
        }

        return text.ToString();
    }

    private static string Describe(KLEPMemorySnapshot snapshot)
    {
        var text = new StringBuilder();
        text.Append(snapshot.OwnerId)
            .Append(':').Append(snapshot.Tick.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(snapshot.WorkingClusterCount.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(snapshot.ArchivedClusterCount.ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < snapshot.Clusters.Count; i++)
        {
            AppendCluster(text, snapshot.Clusters[i]);
        }
        for (int i = 0; i < snapshot.Transitions.Count; i++)
        {
            KLEPMemoryTransition transition = snapshot.Transitions[i];
            text.Append("|t=")
                .Append(transition.Tick.ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(transition.Kind)
                .Append(',').Append(transition.ClusterId)
                .Append(',').Append(transition.ExperienceId)
                .Append(',').Append(Float(transition.HeatBefore))
                .Append(',').Append(Float(transition.FreshnessHeat))
                .Append(',').Append(Float(transition.RepetitionHeat))
                .Append(',').Append(Float(transition.EmotionalSalience))
                .Append(',').Append(Float(transition.Cooling))
                .Append(',').Append(Float(transition.HeatAfter))
                .Append(',').Append(transition.ReasonCode);
        }

        return text.ToString();
    }

    private static KLEPMemoryClusterSnapshot RehydrateCluster(
        KLEPMemoryClusterSnapshot source,
        string actionStableId = null,
        float? heat = null,
        bool? isWorking = null,
        double? producedPositionSquaredMagnitudeSum = null,
        double? producedSpeedSum = null,
        long? succeededCount = null,
        long? failedCount = null,
        long? cancelledCount = null)
    {
        return new KLEPMemoryClusterSnapshot(
            source.ClusterId,
            actionStableId ?? source.ActionStableId,
            source.EncounterCount,
            source.FirstEncounterTick,
            source.LastEncounterTick,
            heat ?? source.Heat,
            isWorking ?? source.IsWorking,
            source.IsArchived,
            source.IsIndelible,
            source.TraumaCount,
            source.PeakEmotionalSwing,
            succeededCount ?? source.SucceededCount,
            failedCount ?? source.FailedCount,
            cancelledCount ?? source.CancelledCount,
            source.FaultedCount,
            source.ProducedEmotionCount,
            source.ProducedEmotionSumX,
            source.ProducedEmotionSumY,
            producedPositionSquaredMagnitudeSum ??
                source.ProducedPositionSquaredMagnitudeSum,
            source.ProducedVelocitySumX,
            source.ProducedVelocitySumY,
            producedSpeedSum ?? source.ProducedSpeedSum,
            source.MostRecentProducedEmotion,
            source.MostRecentProducedVelocity,
            source.PhaseKeyFrequencies,
            source.CorePhaseKeyCells,
            source.RecentEpisodes,
            source.MemorableEpisodes);
    }

    private static void AppendCluster(
        StringBuilder text,
        KLEPMemoryClusterSnapshot cluster)
    {
        text.Append("|c=").Append(cluster.ClusterId)
            .Append(',').Append(cluster.ActionStableId)
            .Append(',').Append(cluster.EncounterCount.ToString(CultureInfo.InvariantCulture))
            .Append(',').Append(cluster.FirstEncounterTick.ToString(CultureInfo.InvariantCulture))
            .Append(',').Append(cluster.LastEncounterTick.ToString(CultureInfo.InvariantCulture))
            .Append(',').Append(Float(cluster.Heat))
            .Append(',').Append(cluster.IsWorking)
            .Append(',').Append(cluster.IsArchived)
            .Append(',').Append(cluster.IsIndelible)
            .Append(',').Append(cluster.TraumaCount.ToString(CultureInfo.InvariantCulture))
            .Append(',').Append(Float(cluster.PeakEmotionalSwing))
            .Append(',').Append(cluster.ProducedEmotionCount.ToString(CultureInfo.InvariantCulture))
            .Append(',').Append(Double(cluster.ProducedEmotionSumX))
            .Append(',').Append(Double(cluster.ProducedEmotionSumY))
            .Append(',').Append(Double(
                cluster.ProducedPositionSquaredMagnitudeSum))
            .Append(',').Append(Double(cluster.ProducedVelocitySumX))
            .Append(',').Append(Double(cluster.ProducedVelocitySumY))
            .Append(',').Append(Double(cluster.ProducedSpeedSum));
        for (int i = 0; i < cluster.PhaseKeyFrequencies.Count; i++)
        {
            KLEPMemoryPhaseKeyFrequency frequency = cluster.PhaseKeyFrequencies[i];
            text.Append(",f=").Append(frequency.Cell)
                .Append('/').Append(frequency.HitCount.ToString(CultureInfo.InvariantCulture))
                .Append('/').Append(frequency.EncounterCount.ToString(CultureInfo.InvariantCulture));
        }
        for (int i = 0; i < cluster.RecentEpisodes.Count; i++)
        {
            text.Append(",r=").Append(cluster.RecentEpisodes[i].ExperienceId)
                .Append('/').Append(cluster.RecentEpisodes[i].HasFullDetail);
        }
        for (int i = 0; i < cluster.MemorableEpisodes.Count; i++)
        {
            text.Append(",m=").Append(cluster.MemorableEpisodes[i].ExperienceId)
                .Append('/').Append(cluster.MemorableEpisodes[i].HasFullDetail);
        }
    }

    private static string Float(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string Double(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static Exception Catch(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException(
                $"Assertion {assertions} failed: {message}");
        }
    }
}
