using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        VerifyAdoptionContinuationInterruptionLocksAndResumption();
        VerifySuccessCompletesAndLaterRunCreatesNewIntention();
        VerifyGoalRecipeAuthorityFaultAndBelowThresholdAbandon();
        VerifyActiveAndSuspendedRemovalAbandon();
        VerifyReplacementTenureCreatesNewIntention();
        VerifyPatientActionTandemAndNestedGoalsAreIgnored();
        VerifyCommittedInterruptionSurvivesFaultingChallenger();
        VerifyTerminalSuccessOutputFaultAbandonsOneIntention();
        VerifyRemovalCancellationFaultOverridesCatalogRetirement();

        Console.WriteLine(
            $"KLEP Intention smoke passed: {assertions} assertions.");
    }

    private static void VerifyAdoptionContinuationInterruptionLocksAndResumption()
    {
        const string pursueId = "intention.flow.goal.pursue";
        const string avoidId = "intention.flow.goal.avoid";
        KLEPKeyDefinition permit = Key("intention.flow.key.permit");
        KLEPKeyDefinition danger = Key("intention.flow.key.danger");
        var neuron = new KLEPNeuron("intention.flow.neuron");
        neuron.InitializeKey(permit);
        KLEPKeyFact dangerFact = null;

        KLEPGoal pursue = RunningGoal(
            pursueId,
            10f,
            Locks("intention.flow.lock.pursue", permit));
        KLEPGoal avoid = RunningGoal(
            avoidId,
            20f,
            Locks("intention.flow.lock.avoid", danger));
        neuron.RegisterExecutable(pursue);
        neuron.RegisterExecutable(avoid);
        var agent = new KLEPAgent(neuron);

        KLEPAgentTickTrace adoptedTrace = agent.Tick();
        KLEPIntentionSnapshot adopted = adoptedTrace.IntentionSnapshot;
        KLEPIntentionRecordSnapshot firstPursue = RequireOpen(adopted, pursueId);
        KLEPIntentionTransition adoption = RequireTransition(
            adopted,
            pursueId,
            KLEPIntentionTransitionKind.Adopted);

        Expect(adopted.AgentTickOrdinal == 1 && adopted.CoreCycleIndex == 1,
            "The first intention snapshot binds the exact Agent and Core clocks");
        Expect(adopted.HasActiveIntention &&
               adopted.ActiveIntentionId == firstPursue.IntentionId &&
               firstPursue.Status == KLEPIntentionStatus.Active,
            "The first actual root Goal advance adopts one Active intention");
        Expect(adoption.PriorStatus == null &&
               adoption.Status == KLEPIntentionStatus.Active &&
               adoption.Reason == KLEPIntentionTransitionReason.GoalSelected &&
               adoption.GoalRunIndex == 1,
            "Adopted is an ordered transition into Active with the first Goal run");
        Expect(adopted.OpenIntentions.Count == 1 &&
               adopted.RetiredIntentions.Count == 0 &&
               adopted.Transitions.Count == 1,
            "Adoption publishes one open record and no invented terminal history");

        string pursueIntentionId = firstPursue.IntentionId;
        string pursueTenureId = firstPursue.RootTenureId;
        long pursueFirstRun = firstPursue.LatestGoalRunIndex;
        long frozenRevision = adopted.Revision;

        KLEPIntentionSnapshot continued = agent.Tick().IntentionSnapshot;
        KLEPIntentionRecordSnapshot continuedPursue = RequireOpen(
            continued,
            pursueId);
        Expect(continued.Transitions.Count == 0 &&
               continuedPursue.IntentionId == pursueIntentionId &&
               continuedPursue.LatestGoalRunIndex == pursueFirstRun,
            "A continued Running Goal produces no duplicate intention transition");

        dangerFact = neuron.AddKey(danger, sourceId: "test.danger-observed");
        KLEPIntentionSnapshot interrupted = agent.Tick().IntentionSnapshot;
        KLEPIntentionRecordSnapshot suspendedPursue = RequireOpen(
            interrupted,
            pursueId);
        KLEPIntentionRecordSnapshot activeAvoid = RequireOpen(interrupted, avoidId);
        KLEPIntentionTransition interruptedPursue = RequireTransition(
            interrupted,
            pursueId,
            KLEPIntentionTransitionKind.Suspended);
        KLEPIntentionTransition adoptedAvoid = RequireTransition(
            interrupted,
            avoidId,
            KLEPIntentionTransitionKind.Adopted);

        Expect(interrupted.Transitions.Count == 2 &&
               interruptedPursue.TransitionSequence <
                   adoptedAvoid.TransitionSequence,
            "Interruption is frozen before the higher-scoring Goal is adopted");
        Expect(suspendedPursue.Status == KLEPIntentionStatus.Suspended &&
               suspendedPursue.IntentionId == pursueIntentionId &&
               suspendedPursue.RootTenureId == pursueTenureId,
            "Interruption suspends rather than replaces the desired end");
        Expect(interruptedPursue.Reason ==
                   KLEPIntentionTransitionReason.GoalInterrupted &&
               interruptedPursue.ExecutableExitReason ==
                   KLEPExecutableExitReason.Interrupted &&
               interruptedPursue.RelatedExecutableStableId == avoidId &&
               interruptedPursue.RelatedRootTenureId == activeAvoid.RootTenureId,
            "The suspension retains exact lifecycle and replacement-root evidence");
        Expect(activeAvoid.Status == KLEPIntentionStatus.Active &&
               interrupted.ActiveIntentionId == activeAvoid.IntentionId,
            "The challenger becomes the sole Active root Solo intention");

        Expect(neuron.RemoveKey(dangerFact),
            "The danger occurrence is staged for exact removal");
        KLEPIntentionSnapshot resumed = agent.Tick().IntentionSnapshot;
        KLEPIntentionRecordSnapshot resumedPursue = RequireOpen(resumed, pursueId);
        KLEPIntentionRecordSnapshot suspendedAvoid = RequireOpen(resumed, avoidId);
        KLEPIntentionTransition lockedAvoid = RequireTransition(
            resumed,
            avoidId,
            KLEPIntentionTransitionKind.Suspended);
        KLEPIntentionTransition resumedPursueTransition = RequireTransition(
            resumed,
            pursueId,
            KLEPIntentionTransitionKind.Resumed);

        Expect(resumed.Transitions.Count == 2 &&
               lockedAvoid.TransitionSequence <
                   resumedPursueTransition.TransitionSequence,
            "LocksClosed suspension is frozen before the prior Goal resumes");
        Expect(lockedAvoid.Reason ==
                   KLEPIntentionTransitionReason.GoalLocksClosed &&
               lockedAvoid.ExecutableExitReason ==
                   KLEPExecutableExitReason.LocksClosed &&
               suspendedAvoid.Status == KLEPIntentionStatus.Suspended,
            "A closed Lock suspends the current intention without declaring failure");
        Expect(resumedPursue.IntentionId == pursueIntentionId &&
               resumedPursue.RootTenureId == pursueTenureId &&
               resumedPursue.Status == KLEPIntentionStatus.Active &&
               resumedPursue.LatestGoalRunIndex > pursueFirstRun,
            "The same Goal tenure resumes the same intention under a new runtime run");
        Expect(resumedPursueTransition.PriorStatus ==
                   KLEPIntentionStatus.Suspended &&
               resumedPursueTransition.Status == KLEPIntentionStatus.Active &&
               resumedPursueTransition.Reason ==
                   KLEPIntentionTransitionReason.GoalSelected,
            "Resumption is an explicit Suspended-to-Active transition");

        Expect(adopted.Revision == frozenRevision &&
               adopted.Transitions.Count == 1 &&
               RequireOpen(adopted, pursueId).Status ==
                   KLEPIntentionStatus.Active &&
               adopted.OpenIntentions.Count == 1,
            "A retained older intention snapshot remains frozen after later transitions");
        var frozenOpen = (IList<KLEPIntentionRecordSnapshot>)adopted.OpenIntentions;
        Expect(Catch(() => frozenOpen.Clear()) is NotSupportedException,
            "Frozen intention collections reject external mutation");
    }

    private static void VerifySuccessCompletesAndLaterRunCreatesNewIntention()
    {
        const string goalId = "intention.complete.goal";
        var neuron = new KLEPNeuron("intention.complete.neuron");
        neuron.RegisterExecutable(CompletingGoal(goalId, 5f));
        var agent = new KLEPAgent(neuron);

        KLEPIntentionSnapshot first = agent.Tick().IntentionSnapshot;
        KLEPIntentionTransition adopted = RequireTransition(
            first,
            goalId,
            KLEPIntentionTransitionKind.Adopted);
        KLEPIntentionTransition completed = RequireTransition(
            first,
            goalId,
            KLEPIntentionTransitionKind.Completed);
        KLEPIntentionRecordSnapshot retired = RequireRetired(first, goalId);

        Expect(first.OpenIntentions.Count == 0 &&
               first.RetiredIntentions.Count == 1 &&
               first.Transitions.Count == 2 &&
               !first.HasActiveIntention,
            "A single-Tick Goal freezes adoption and completion without leaving it open");
        Expect(adopted.TransitionSequence < completed.TransitionSequence &&
               completed.PriorStatus == KLEPIntentionStatus.Active &&
               completed.Status == KLEPIntentionStatus.Completed,
            "A successful single-Tick run orders Adopted before Completed");
        Expect(completed.Reason ==
                   KLEPIntentionTransitionReason.GoalSucceeded &&
               completed.ExecutableExitReason ==
                   KLEPExecutableExitReason.Succeeded &&
               retired.Status == KLEPIntentionStatus.Completed,
            "Succeeded is the exact lifecycle fact that completes an intention");

        string firstIntentionId = retired.IntentionId;
        KLEPIntentionSnapshot second = agent.Tick().IntentionSnapshot;
        KLEPIntentionRecordSnapshot secondRetired = RequireRetired(second, goalId);
        Expect(secondRetired.IntentionId != firstIntentionId &&
               secondRetired.IntentionSequence > retired.IntentionSequence &&
               CountTransitions(
                   second,
                   goalId,
                   KLEPIntentionTransitionKind.Resumed) == 0,
            "A later run after completion adopts a new intention rather than resuming one");
    }

    private static void VerifyGoalRecipeAuthorityFaultAndBelowThresholdAbandon()
    {
        const string diagnosticId = "intention.authority.goal-kind-only";
        var diagnosticNeuron = new KLEPNeuron("intention.authority.neuron");
        diagnosticNeuron.RegisterExecutable(new ScriptedExecutable(
            Definition(
                diagnosticId,
                KLEPExecutableKind.Goal,
                5f),
            KLEPExecutableTickStatus.Failed));
        var diagnosticAgent = new KLEPAgent(diagnosticNeuron);

        KLEPAgentTickTrace diagnosticTrace = diagnosticAgent.Tick();
        Expect(diagnosticTrace.Decision.SelectedExecutableId == diagnosticId &&
               diagnosticTrace.Decision.Executions.Count > 0 &&
               diagnosticTrace.Decision.Executions[
                   diagnosticTrace.Decision.Executions.Count - 1].State ==
                   KLEPExecutableState.Failed,
            "The diagnostic Goal-kind executable still exercises its ordinary runtime lifecycle");
        Expect(diagnosticTrace.IntentionSnapshot.OpenIntentions.Count == 0 &&
               diagnosticTrace.IntentionSnapshot.RetiredIntentions.Count == 0 &&
               diagnosticTrace.IntentionSnapshot.Transitions.Count == 0,
            "Kind=Goal metadata without an actual KLEPGoal recipe cannot create an intention");

        const string childFailureId = "intention.authority.real-goal";
        var failingChild = new ScriptedExecutable(
            Definition(
                childFailureId + ".failed-child",
                KLEPExecutableKind.Action,
                0f),
            KLEPExecutableTickStatus.Failed);
        var realGoal = new KLEPGoal(
            Definition(childFailureId, KLEPExecutableKind.Goal, 5f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { failingChild })
            });
        var realGoalNeuron = new KLEPNeuron(
            "intention.authority.real-goal-neuron");
        realGoalNeuron.RegisterExecutable(realGoal);
        var realGoalAgent = new KLEPAgent(realGoalNeuron);
        KLEPAgentTickTrace realGoalTrace = realGoalAgent.Tick();
        Expect(realGoalTrace.Decision.Executions.Count > 0 &&
               realGoalTrace.Decision.Executions[
                   realGoalTrace.Decision.Executions.Count - 1].State ==
                   KLEPExecutableState.Running &&
               RequireOpen(
                   realGoalTrace.IntentionSnapshot,
                   childFailureId).Status == KLEPIntentionStatus.Active,
            "An actual KLEPGoal recipe owns an intention while its failed child leaves the authored All layer incomplete");

        const string faultedId = "intention.fault.goal";
        var sentinel = new InvalidOperationException(
            "intention faulted Goal sentinel");
        var faultNeuron = new KLEPNeuron("intention.fault.neuron");
        faultNeuron.RegisterExecutable(FaultingGoal(faultedId, 5f, sentinel));
        var faultAgent = new KLEPAgent(faultNeuron);

        Exception caught = Catch(() => faultAgent.Tick());
        KLEPIntentionSnapshot faulted = faultAgent.LastTrace.IntentionSnapshot;
        KLEPIntentionTransition faultTransition = RequireTransition(
            faulted,
            faultedId,
            KLEPIntentionTransitionKind.Abandoned);
        Expect(ReferenceEquals(caught, sentinel) &&
               !faultAgent.LastTrace.DidCompleteObservation,
            "The real Goal fault is rethrown unchanged and remains a faulted Agent Tick");
        Expect(faulted.Transitions.Count == 2 &&
               faultTransition.Reason ==
                   KLEPIntentionTransitionReason.GoalFaulted &&
               faultTransition.ExecutableExitReason ==
                   KLEPExecutableExitReason.Faulted &&
               RequireRetired(faulted, faultedId).Status ==
                   KLEPIntentionStatus.Abandoned,
            "An advanced faulted Tick still adopts then abandons its exact Goal intention");

        const string thresholdId = "intention.threshold.goal";
        KLEPKeyDefinition wanted = Key("intention.threshold.key.wanted");
        var thresholdNeuron = new KLEPNeuron("intention.threshold.neuron");
        KLEPKeyFact wantedFact = thresholdNeuron.InitializeKey(wanted);
        thresholdNeuron.RegisterExecutable(RunningGoal(
            thresholdId,
            1f,
            attractionEvaluator: new PresenceAttractionEvaluator(
                "intention.threshold.evaluator",
                wanted.Id.Value,
                10f,
                -10f)));
        var thresholdAgent = new KLEPAgent(thresholdNeuron);

        KLEPIntentionRecordSnapshot beforeThreshold = RequireOpen(
            thresholdAgent.Tick().IntentionSnapshot,
            thresholdId);
        Expect(thresholdNeuron.RemoveKey(wantedFact),
            "The attraction-supporting Key is staged for removal");
        KLEPIntentionSnapshot belowThreshold =
            thresholdAgent.Tick().IntentionSnapshot;
        KLEPIntentionTransition thresholdTransition = RequireTransition(
            belowThreshold,
            thresholdId,
            KLEPIntentionTransitionKind.Abandoned);
        Expect(thresholdTransition.Reason ==
                   KLEPIntentionTransitionReason.GoalBelowThreshold &&
               thresholdTransition.ExecutableExitReason ==
                   KLEPExecutableExitReason.BelowThreshold &&
               RequireRetired(belowThreshold, thresholdId).IntentionId ==
                   beforeThreshold.IntentionId,
            "A score at or below certainty abandons the exact active intention");
    }

    private static void VerifyActiveAndSuspendedRemovalAbandon()
    {
        const string activeId = "intention.remove.active-goal";
        var activeNeuron = new KLEPNeuron("intention.remove.active-neuron");
        activeNeuron.RegisterExecutable(RunningGoal(activeId, 5f));
        var activeAgent = new KLEPAgent(activeNeuron);
        KLEPIntentionRecordSnapshot active = RequireOpen(
            activeAgent.Tick().IntentionSnapshot,
            activeId);

        activeNeuron.RemoveExecutable(activeId);
        KLEPIntentionSnapshot removedActive =
            activeAgent.Tick().IntentionSnapshot;
        KLEPIntentionTransition activeRemoval = RequireTransition(
            removedActive,
            activeId,
            KLEPIntentionTransitionKind.Abandoned);
        Expect(activeRemoval.Reason ==
                   KLEPIntentionTransitionReason.GoalRemoved &&
               activeRemoval.ExecutableExitReason ==
                   KLEPExecutableExitReason.Removed &&
               RequireRetired(removedActive, activeId).IntentionId ==
                   active.IntentionId,
            "Removing a Running Goal abandons through its actual cancellation result");

        const string suspendedId = "intention.remove.suspended-goal";
        const string blockerId = "intention.remove.blocker-goal";
        KLEPKeyDefinition blockerKey = Key("intention.remove.key.blocker");
        var suspendedNeuron = new KLEPNeuron(
            "intention.remove.suspended-neuron");
        suspendedNeuron.RegisterExecutable(RunningGoal(suspendedId, 5f));
        suspendedNeuron.RegisterExecutable(RunningGoal(
            blockerId,
            10f,
            Locks("intention.remove.lock.blocker", blockerKey)));
        var suspendedAgent = new KLEPAgent(suspendedNeuron);
        KLEPIntentionRecordSnapshot initiallyActive = RequireOpen(
            suspendedAgent.Tick().IntentionSnapshot,
            suspendedId);
        suspendedNeuron.AddKey(
            blockerKey,
            sourceId: "test.blocker-observed");
        KLEPIntentionSnapshot interrupted =
            suspendedAgent.Tick().IntentionSnapshot;
        Expect(RequireOpen(interrupted, suspendedId).Status ==
                   KLEPIntentionStatus.Suspended,
            "The removal fixture first creates a genuinely suspended intention");

        suspendedNeuron.RemoveExecutable(suspendedId);
        KLEPIntentionSnapshot removedSuspended =
            suspendedAgent.Tick().IntentionSnapshot;
        KLEPIntentionTransition suspendedRemoval = RequireTransition(
            removedSuspended,
            suspendedId,
            KLEPIntentionTransitionKind.Abandoned);
        Expect(suspendedRemoval.Reason ==
                   KLEPIntentionTransitionReason.CatalogRemoved &&
               suspendedRemoval.ExecutableExitReason == null &&
               RequireRetired(removedSuspended, suspendedId).IntentionId ==
                   initiallyActive.IntentionId,
            "Removing a suspended Goal is detected from catalog tenure without inventing a cancellation");
        Expect(RequireOpen(removedSuspended, blockerId).Status ==
                   KLEPIntentionStatus.Active,
            "Catalog retirement does not disturb the continuing active intention");
    }

    private static void VerifyReplacementTenureCreatesNewIntention()
    {
        const string goalId = "intention.replace.goal";
        const string blockerId = "intention.replace.blocker";
        KLEPKeyDefinition blockerKey = Key("intention.replace.key.blocker");
        var neuron = new KLEPNeuron("intention.replace.neuron");
        neuron.RegisterExecutable(RunningGoal(goalId, 5f));
        neuron.RegisterExecutable(RunningGoal(
            blockerId,
            10f,
            Locks("intention.replace.lock.blocker", blockerKey)));
        var agent = new KLEPAgent(neuron);

        KLEPIntentionRecordSnapshot original = RequireOpen(
            agent.Tick().IntentionSnapshot,
            goalId);
        KLEPKeyFact blockerFact = neuron.AddKey(
            blockerKey,
            sourceId: "test.blocker-observed");
        KLEPIntentionSnapshot suspended = agent.Tick().IntentionSnapshot;
        Expect(RequireOpen(suspended, goalId).Status ==
                   KLEPIntentionStatus.Suspended,
            "The original tenure is suspended before replacement");

        neuron.RemoveExecutable(goalId);
        neuron.RegisterExecutable(RunningGoal(goalId, 5f));
        KLEPIntentionSnapshot replaced = agent.Tick().IntentionSnapshot;
        KLEPIntentionTransition replacement = RequireTransition(
            replaced,
            goalId,
            KLEPIntentionTransitionKind.Abandoned);
        KLEPIntentionRecordSnapshot oldRetired = RequireRetired(replaced, goalId);
        Expect(replacement.Reason ==
                   KLEPIntentionTransitionReason.RegistrationReplaced &&
               oldRetired.IntentionId == original.IntentionId &&
               replacement.RelatedExecutableStableId == goalId &&
               replacement.RelatedRootTenureId != original.RootTenureId,
            "Replacing a stable Goal ID abandons the exact old registration tenure");
        Expect(FindOpen(replaced, goalId) == null,
            "An unselected replacement is not prematurely adopted");

        Expect(neuron.RemoveKey(blockerFact),
            "The replacement fixture clears the blocking Goal's Key");
        KLEPIntentionSnapshot replacementSelected =
            agent.Tick().IntentionSnapshot;
        KLEPIntentionRecordSnapshot newIntention = RequireOpen(
            replacementSelected,
            goalId);
        KLEPIntentionTransition newAdoption = RequireTransition(
            replacementSelected,
            goalId,
            KLEPIntentionTransitionKind.Adopted);
        Expect(newIntention.IntentionId != original.IntentionId &&
               newIntention.RootTenureId != original.RootTenureId &&
               newIntention.IntentionSequence > original.IntentionSequence,
            "The replacement tenure receives a new deterministic intention identity");
        Expect(newAdoption.Kind == KLEPIntentionTransitionKind.Adopted &&
               CountTransitions(
                   replacementSelected,
                   goalId,
                   KLEPIntentionTransitionKind.Resumed) == 0,
            "A replacement Goal is adopted, never resumed from the prior tenure");
    }

    private static void VerifyPatientActionTandemAndNestedGoalsAreIgnored()
    {
        var patientAgent = new KLEPAgent(
            new KLEPNeuron("intention.ignore.patient-neuron"));
        KLEPIntentionSnapshot patient = patientAgent.Tick().IntentionSnapshot;
        Expect(patient.OpenIntentions.Count == 0 &&
               patient.RetiredIntentions.Count == 0 &&
               patient.Transitions.Count == 0 &&
               patientAgent.LastTrace.Decision.IsPatient,
            "A Patient Agent invents no intention");

        const string actionId = "intention.ignore.action";
        var actionNeuron = new KLEPNeuron("intention.ignore.action-neuron");
        actionNeuron.RegisterExecutable(new ScriptedExecutable(
            Definition(actionId, KLEPExecutableKind.Action, 5f),
            KLEPExecutableTickStatus.Running));
        var actionAgent = new KLEPAgent(actionNeuron);
        KLEPAgentTickTrace actionTrace = actionAgent.Tick();
        Expect(actionTrace.Decision.SelectedExecutableId == actionId &&
               actionTrace.IntentionSnapshot.OpenIntentions.Count == 0 &&
               actionTrace.IntentionSnapshot.Transitions.Count == 0,
            "A selected non-Goal root remains behavior without becoming an intention");

        const string tandemId = "intention.ignore.tandem-goal";
        var tandemNeuron = new KLEPNeuron("intention.ignore.tandem-neuron");
        tandemNeuron.RegisterExecutable(CompletingGoal(
            tandemId,
            0f,
            KLEPExecutionMode.Tandem));
        var tandemAgent = new KLEPAgent(tandemNeuron);
        KLEPAgentTickTrace tandemTrace = tandemAgent.Tick();
        Expect(HasExecution(
                   tandemTrace.Decision,
                   tandemId,
                   KLEPExecutableStepKind.Tandem) &&
               tandemTrace.IntentionSnapshot.OpenIntentions.Count == 0 &&
               tandemTrace.IntentionSnapshot.RetiredIntentions.Count == 0 &&
               tandemTrace.IntentionSnapshot.Transitions.Count == 0,
            "An actual automatic Tandem Goal executes but remains outside V1 intention scope");

        const string rootId = "intention.ignore.root-goal";
        const string nestedId = "intention.ignore.nested-goal";
        var leaf = new ScriptedExecutable(
            Definition(
                "intention.ignore.nested-leaf",
                KLEPExecutableKind.Action,
                0f),
            KLEPExecutableTickStatus.Running);
        var nested = new KLEPGoal(
            Definition(nestedId, KLEPExecutableKind.Goal, 0f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { leaf })
            });
        var root = new KLEPGoal(
            Definition(rootId, KLEPExecutableKind.Goal, 5f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { nested })
            });
        var nestedNeuron = new KLEPNeuron("intention.ignore.nested-neuron");
        nestedNeuron.RegisterExecutable(root);
        var nestedAgent = new KLEPAgent(nestedNeuron);
        KLEPIntentionSnapshot nestedSnapshot =
            nestedAgent.Tick().IntentionSnapshot;
        Expect(nestedSnapshot.OpenIntentions.Count == 1 &&
               RequireOpen(nestedSnapshot, rootId).Status ==
                   KLEPIntentionStatus.Active,
            "The selected root Goal creates the one V1 intention");
        Expect(FindOpen(nestedSnapshot, nestedId) == null &&
               CountTransitions(
                   nestedSnapshot,
                   nestedId,
                   KLEPIntentionTransitionKind.Adopted) == 0,
            "A Goal-owned nested Goal remains recipe progress, not an independently adopted intention");
    }

    private static void VerifyCommittedInterruptionSurvivesFaultingChallenger()
    {
        const string currentId = "intention.fault-chain.current-goal";
        const string challengerId = "intention.fault-chain.challenger-goal";
        var neuron = new KLEPNeuron("intention.fault-chain.neuron");
        neuron.RegisterExecutable(RunningGoal(currentId, 5f));
        var agent = new KLEPAgent(neuron);
        KLEPIntentionRecordSnapshot initial = RequireOpen(
            agent.Tick().IntentionSnapshot,
            currentId);

        var sentinel = new InvalidOperationException(
            "intention faulting challenger sentinel");
        neuron.RegisterExecutable(FaultingGoal(challengerId, 10f, sentinel));
        Exception caught = Catch(() => agent.Tick());
        KLEPIntentionSnapshot faulted = agent.LastTrace.IntentionSnapshot;
        KLEPIntentionRecordSnapshot suspended = RequireOpen(faulted, currentId);
        KLEPIntentionRecordSnapshot retiredChallenger = RequireRetired(
            faulted,
            challengerId);

        Expect(ReferenceEquals(caught, sentinel) &&
               !agent.LastTrace.DidCompleteObservation,
            "The challenger fault remains the original faulted Agent result");
        Expect(suspended.IntentionId == initial.IntentionId &&
               suspended.Status == KLEPIntentionStatus.Suspended &&
               suspended.LastTransitionReason ==
                   KLEPIntentionTransitionReason.GoalInterrupted,
            "The current Goal's committed interruption survives the later challenger fault");
        Expect(retiredChallenger.Status == KLEPIntentionStatus.Abandoned &&
               retiredChallenger.LastTransitionReason ==
                   KLEPIntentionTransitionReason.GoalFaulted,
            "The challenger is adopted and abandoned in the same faulted Tick");
        Expect(faulted.Transitions.Count == 3 &&
               faulted.Transitions[0].GoalStableId == currentId &&
               faulted.Transitions[0].Kind ==
                   KLEPIntentionTransitionKind.Suspended &&
               faulted.Transitions[1].GoalStableId == challengerId &&
               faulted.Transitions[1].Kind ==
                   KLEPIntentionTransitionKind.Adopted &&
               faulted.Transitions[2].GoalStableId == challengerId &&
               faulted.Transitions[2].Kind ==
                   KLEPIntentionTransitionKind.Abandoned,
            "Fault evidence preserves the exact committed lifecycle ordering");

        neuron.RemoveExecutable(challengerId);
        KLEPIntentionSnapshot recovered = agent.Tick().IntentionSnapshot;
        KLEPIntentionRecordSnapshot resumed = RequireOpen(recovered, currentId);
        Expect(resumed.IntentionId == initial.IntentionId &&
               resumed.Status == KLEPIntentionStatus.Active &&
               RequireTransition(
                   recovered,
                   currentId,
                   KLEPIntentionTransitionKind.Resumed).PriorStatus ==
                   KLEPIntentionStatus.Suspended,
            "Recovery resumes the same intention after removing the faulting challenger");
    }

    private static void VerifyTerminalSuccessOutputFaultAbandonsOneIntention()
    {
        const string goalId = "intention.output-fault.goal";
        var globalOutput = new KLEPKeyDefinition(
            new KLEPKeyId("intention.output-fault.key.global"),
            "Intention output-fault Global Key",
            scope: KLEPKeyScope.Global,
            defaultLifetime: KLEPKeyLifetime.Persistent);
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                goalId,
                goalId,
                KLEPExecutableKind.Goal,
                declaredOutputs: new[] { globalOutput },
                baseAttractiveness: 5f),
            new[]
            {
                new KLEPGoalLayer(KLEPGoalLayerRequirement.NoneNeedToFire)
            },
            globalOutput);
        var neuron = new KLEPNeuron("intention.output-fault.neuron");
        neuron.RegisterExecutable(goal);
        var agent = new KLEPAgent(neuron);

        Exception caught = Catch(() => agent.Tick());
        KLEPAgentTickTrace trace = agent.LastTrace;
        KLEPExecutableStepTrace provisional = null;
        KLEPExecutableStepTrace faulted = null;
        foreach (KLEPExecutableStepTrace step in trace.Decision.Executions)
        {
            if (!StringComparer.Ordinal.Equals(
                    step.ExecutableStableId,
                    goalId) ||
                step.Kind != KLEPExecutableStepKind.Solo)
            {
                continue;
            }

            if (step.State == KLEPExecutableState.Succeeded)
            {
                provisional = step;
            }
            else if (step.State == KLEPExecutableState.Faulted)
            {
                faulted = step;
            }
        }

        Expect(caught is InvalidOperationException &&
               trace.Decision.Fault != null &&
               trace.Decision.Fault.ExecutableStableId == goalId &&
               trace.Decision.Fault.Stage ==
                   KLEPExecutableLifecycleStage.OutputApplication,
            "A terminal Goal output-application failure remains an exact faulted Agent Tick");
        Expect(provisional != null && faulted != null &&
               provisional.Result.RunIndex == faulted.Result.RunIndex &&
               provisional.Outputs.Count == 1 &&
               faulted.ExitReason == KLEPExecutableExitReason.Faulted,
            "The decision retains provisional success and Faulted results for the same Goal run");

        KLEPIntentionSnapshot snapshot = trace.IntentionSnapshot;
        KLEPIntentionTransition adopted = RequireTransition(
            snapshot,
            goalId,
            KLEPIntentionTransitionKind.Adopted);
        KLEPIntentionTransition abandoned = RequireTransition(
            snapshot,
            goalId,
            KLEPIntentionTransitionKind.Abandoned);
        KLEPIntentionRecordSnapshot retired = RequireRetired(snapshot, goalId);
        Expect(snapshot.OpenIntentions.Count == 0 &&
               snapshot.RetiredIntentions.Count == 1 &&
               snapshot.Transitions.Count == 2 &&
               CountTransitions(
                   snapshot,
                   goalId,
                   KLEPIntentionTransitionKind.Completed) == 0,
            "Provisional success cannot complete or duplicate an intention when output application faults");
        Expect(adopted.IntentionId == abandoned.IntentionId &&
               abandoned.IntentionId == retired.IntentionId &&
               abandoned.Reason ==
                   KLEPIntentionTransitionReason.GoalFaulted &&
               abandoned.ExecutableExitReason ==
                   KLEPExecutableExitReason.Faulted &&
               retired.Status == KLEPIntentionStatus.Abandoned,
            "One adopted identity is abandoned as GoalFaulted by the authoritative result");
    }

    private static void VerifyRemovalCancellationFaultOverridesCatalogRetirement()
    {
        const string goalId = "intention.removal-fault.goal";
        var sentinel = new InvalidOperationException(
            "intention removal cancellation sentinel");
        var child = new ScriptedExecutable(
            Definition(
                goalId + ".child",
                KLEPExecutableKind.Action,
                0f),
            KLEPExecutableTickStatus.Running,
            exitFault: sentinel);
        var goal = new KLEPGoal(
            Definition(goalId, KLEPExecutableKind.Goal, 5f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { child })
            });
        var neuron = new KLEPNeuron("intention.removal-fault.neuron");
        neuron.RegisterExecutable(goal);
        var agent = new KLEPAgent(neuron);
        KLEPIntentionRecordSnapshot active = RequireOpen(
            agent.Tick().IntentionSnapshot,
            goalId);

        neuron.RemoveExecutable(goalId);
        Exception caught = Catch(() => agent.Tick());
        KLEPAgentTickTrace trace = agent.LastTrace;
        KLEPIntentionSnapshot snapshot = trace.IntentionSnapshot;
        KLEPIntentionTransition abandoned = RequireTransition(
            snapshot,
            goalId,
            KLEPIntentionTransitionKind.Abandoned);
        KLEPIntentionRecordSnapshot retired = RequireRetired(snapshot, goalId);

        Expect(ReferenceEquals(caught, sentinel) &&
               trace.Decision.Fault != null &&
               trace.Decision.Fault.ExecutableStableId == goalId + ".child" &&
               trace.Decision.Fault.Stage ==
                   KLEPExecutableLifecycleStage.Exit,
            "A removed Goal's cancellation teardown fault is rethrown unchanged and traced to its exact child callback");
        Expect(snapshot.OpenIntentions.Count == 0 &&
               snapshot.RetiredIntentions.Count == 1 &&
               snapshot.Transitions.Count == 1 &&
               retired.IntentionId == active.IntentionId,
            "Faulting removal retires the one existing intention identity exactly once");
        Expect(abandoned.Reason ==
                   KLEPIntentionTransitionReason.GoalFaulted &&
               abandoned.ExecutableExitReason ==
                   KLEPExecutableExitReason.Faulted &&
               retired.LastTransitionReason ==
                   KLEPIntentionTransitionReason.GoalFaulted &&
               abandoned.Reason !=
                   KLEPIntentionTransitionReason.CatalogRemoved,
            "Exact faulted cancellation evidence overrides catalog-removal fallback semantics");
    }

    private static KLEPGoal RunningGoal(
        string stableId,
        float score,
        IEnumerable<KLEPLock> locks = null,
        IKLEPGoalAttractionEvaluator attractionEvaluator = null)
    {
        var child = new ScriptedExecutable(
            Definition(
                stableId + ".child",
                KLEPExecutableKind.Action,
                0f),
            KLEPExecutableTickStatus.Running);
        return new KLEPGoal(
            Definition(
                stableId,
                KLEPExecutableKind.Goal,
                score,
                validationLocks: locks),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { child })
            },
            null,
            attractionEvaluator);
    }

    private static KLEPGoal CompletingGoal(
        string stableId,
        float score,
        KLEPExecutionMode mode = KLEPExecutionMode.Solo)
    {
        return new KLEPGoal(
            Definition(
                stableId,
                KLEPExecutableKind.Goal,
                score,
                mode),
            new[]
            {
                new KLEPGoalLayer(KLEPGoalLayerRequirement.NoneNeedToFire)
            });
    }

    private static KLEPGoal FaultingGoal(
        string stableId,
        float score,
        Exception fault)
    {
        var child = new ScriptedExecutable(
            Definition(
                stableId + ".faulting-child",
                KLEPExecutableKind.Action,
                0f),
            KLEPExecutableTickStatus.Running,
            fault);
        return new KLEPGoal(
            Definition(stableId, KLEPExecutableKind.Goal, score),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { child })
            });
    }

    private static KLEPExecutableDefinition Definition(
        string stableId,
        KLEPExecutableKind kind,
        float score,
        KLEPExecutionMode mode = KLEPExecutionMode.Solo,
        IEnumerable<KLEPLock> validationLocks = null)
    {
        return new KLEPExecutableDefinition(
            stableId,
            stableId,
            kind,
            validationLocks: validationLocks,
            baseAttractiveness: score,
            executionMode: mode);
    }

    private static KLEPKeyDefinition Key(string stableId)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(stableId),
            stableId,
            defaultLifetime: KLEPKeyLifetime.Persistent);
    }

    private static IReadOnlyList<KLEPLock> Locks(
        string lockId,
        KLEPKeyDefinition required)
    {
        return new[]
        {
            new KLEPLock(
                lockId,
                lockId,
                new KLEPKeyPresent(required.Id.Value))
        };
    }

    private static KLEPIntentionRecordSnapshot RequireOpen(
        KLEPIntentionSnapshot snapshot,
        string goalStableId)
    {
        KLEPIntentionRecordSnapshot found = FindOpen(snapshot, goalStableId);
        return found ?? throw new InvalidOperationException(
            $"Open intention for Goal '{goalStableId}' was not present.");
    }

    private static KLEPIntentionRecordSnapshot FindOpen(
        KLEPIntentionSnapshot snapshot,
        string goalStableId)
    {
        foreach (KLEPIntentionRecordSnapshot intention in snapshot.OpenIntentions)
        {
            if (StringComparer.Ordinal.Equals(
                    intention.GoalStableId,
                    goalStableId))
            {
                return intention;
            }
        }

        return null;
    }

    private static KLEPIntentionRecordSnapshot RequireRetired(
        KLEPIntentionSnapshot snapshot,
        string goalStableId)
    {
        foreach (KLEPIntentionRecordSnapshot intention in snapshot.RetiredIntentions)
        {
            if (StringComparer.Ordinal.Equals(
                    intention.GoalStableId,
                    goalStableId))
            {
                return intention;
            }
        }

        throw new InvalidOperationException(
            $"Retired intention for Goal '{goalStableId}' was not present.");
    }

    private static KLEPIntentionTransition RequireTransition(
        KLEPIntentionSnapshot snapshot,
        string goalStableId,
        KLEPIntentionTransitionKind kind)
    {
        foreach (KLEPIntentionTransition transition in snapshot.Transitions)
        {
            if (StringComparer.Ordinal.Equals(
                    transition.GoalStableId,
                    goalStableId) &&
                transition.Kind == kind)
            {
                return transition;
            }
        }

        throw new InvalidOperationException(
            $"Transition '{kind}' for Goal '{goalStableId}' was not present.");
    }

    private static int CountTransitions(
        KLEPIntentionSnapshot snapshot,
        string goalStableId,
        KLEPIntentionTransitionKind kind)
    {
        int count = 0;
        foreach (KLEPIntentionTransition transition in snapshot.Transitions)
        {
            if (StringComparer.Ordinal.Equals(
                    transition.GoalStableId,
                    goalStableId) &&
                transition.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasExecution(
        KLEPDecisionTrace trace,
        string stableId,
        KLEPExecutableStepKind kind)
    {
        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            if (StringComparer.Ordinal.Equals(
                    step.ExecutableStableId,
                    stableId) &&
                step.Kind == kind)
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
            throw new InvalidOperationException(
                $"Assertion failed: {message}");
        }
    }

    private sealed class ScriptedExecutable : KLEPExecutableBase
    {
        private readonly KLEPExecutableTickStatus status;
        private readonly Exception fault;
        private readonly Exception exitFault;

        internal ScriptedExecutable(
            KLEPExecutableDefinition definition,
            KLEPExecutableTickStatus status,
            Exception fault = null,
            Exception exitFault = null)
            : base(definition)
        {
            this.status = status;
            this.fault = fault;
            this.exitFault = exitFault;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (fault != null)
            {
                throw fault;
            }

            return status;
        }

        protected override void OnExit(KLEPExecutableExitContext context)
        {
            if (exitFault != null)
            {
                throw exitFault;
            }
        }
    }

    private sealed class PresenceAttractionEvaluator :
        IKLEPGoalAttractionEvaluator
    {
        private readonly string keyId;
        private readonly float present;
        private readonly float absent;

        internal PresenceAttractionEvaluator(
            string stableId,
            string keyId,
            float present,
            float absent)
        {
            StableId = stableId;
            Version = "1";
            this.keyId = keyId;
            this.present = present;
            this.absent = absent;
        }

        public string StableId { get; }
        public string Version { get; }

        public KLEPGoalAttractionEvaluation Evaluate(
            KLEPGoalAttractionContext context)
        {
            bool isPresent = context.KeySnapshot.Contains(keyId);
            return new KLEPGoalAttractionEvaluation(
                isPresent ? present : absent,
                isPresent
                    ? "The authored attraction fact is present."
                    : "The authored attraction fact is absent.");
        }
    }
}
