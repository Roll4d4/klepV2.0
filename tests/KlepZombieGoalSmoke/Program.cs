using System;
using System.Collections.Generic;
using Roll4d4.Klep.Behaviors;
using Roll4d4.Klep.Core;

internal static class Program
{
    private const string GroundId = "key.ground";
    private const string EnemyId = "key.enemy-detected";
    private const string MoveTargetId = "key.move-target";
    private const string AttackTargetId = "key.attack-target";
    private const string EdgeDangerId = "key.edge-danger";
    private const string WanderDirectionId = "key.wander-direction";

    private const string GroundSensorId = "sensor.ground";
    private const string EnemySensorId = "sensor.enemy";
    private const string EdgeSensorId = "sensor.edge";
    private const string TargetRouterId = "router.target";
    private const string WanderRouterId = "router.wander";
    private const string MoveId = "action.move";
    private const string AttackId = "action.attack";
    private const string WanderId = "action.wander";
    private const string AvoidId = "action.avoid-edge";
    private const string EatGoalId = "goal.eat-human";
    private const string WanderGoalId = "goal.wander";
    private const string AvoidGoalId = "goal.avoid-edge";

    private const double AttackRange = 1.5d;
    private static int assertions;

    private static void Main()
    {
        VerifyShowcaseGoalHierarchy();
        VerifyEveryEdgeSupportMask();
        VerifyDeterministicWanderEquivalence();

        Console.WriteLine(
            $"KLEP Zombie Goal smoke passed: {assertions} assertions.");
    }

    private static void VerifyShowcaseGoalHierarchy()
    {
        var fixture = new ShowcaseFixture(
            "neuron.goal-showcase",
            seed: 0x4B1D5EEDu,
            wanderSegmentTicks: 6);

        fixture.SetWorld(grounded: true, edge: null);
        KLEPDecisionTrace wandering = fixture.Agent.Tick().Decision;

        Expect(wandering.SelectedExecutableId == WanderGoalId &&
               wandering.CurrentSoloExecutableId == WanderGoalId &&
               !wandering.IsPatient,
            "Ground with no human and no edge selects the Wander Goal");
        Expect(FindCandidate(wandering, WanderGoalId).IsEligible &&
               !FindCandidate(wandering, EatGoalId).IsEligible &&
               !FindCandidate(wandering, AvoidGoalId).IsEligible,
            "Only Wander is eligible in the empty grounded world");
        Expect(FindStep(
                   wandering,
                   WanderGoalId,
                   KLEPExecutableStepKind.Solo)?.State ==
               KLEPExecutableState.Running,
            "The Wander Goal remains Running");

        KLEPExecutableRuntimeSnapshot wanderRoot = FindRoot(
            wandering,
            WanderGoalId);
        KLEPGoalChildRuntimeSnapshot wanderChild = FindChild(
            wanderRoot,
            WanderId);
        Expect(wanderRoot.Goal != null &&
               wanderRoot.State == KLEPExecutableState.Running &&
               wanderRoot.IsCurrentSolo,
            "The root runtime snapshot identifies Wander as current");
        Expect(wanderChild.Runtime.State == KLEPExecutableState.Running,
            "The recursive Goal snapshot exposes the Running Wander child");
        Expect(fixture.Wander.TryGetIntent(
                   wandering.CycleIndex,
                   out double wanderX,
                   out double wanderZ,
                   out int wanderHeading) &&
               IsFinite(wanderX) &&
               IsFinite(wanderZ) &&
               wanderHeading >= 0 &&
               wanderHeading < KLEPDeterministicWanderRouterExecutable.HeadingCount,
            "The Wander child exposes one finite deterministic intent");

        VerifyRootOwnershipBoundary(fixture, wandering);

        fixture.SetWorld(
            grounded: true,
            edge: null,
            Human("human.far", 8d));
        KLEPDecisionTrace eatingFar = fixture.Agent.Tick().Decision;

        Expect(eatingFar.SelectedExecutableId == EatGoalId &&
               eatingFar.CurrentSoloExecutableId == EatGoalId,
            "A far human selects and retains Eat Human");
        CandidateEvaluation eligibleWander = FindCandidate(
            eatingFar,
            WanderGoalId);
        CandidateEvaluation eligibleEat = FindCandidate(
            eatingFar,
            EatGoalId);
        Expect(eligibleWander.IsEligible && eligibleEat.IsEligible,
            "A far human leaves both Wander and Eat Human eligible");
        Expect(eligibleWander.Score == 10f &&
               eligibleEat.Score == 50f &&
               eligibleEat.Score > eligibleWander.Score,
            "Eat Human wins ordinary arbitration by authored score 50 over Wander 10");
        int wanderCancellationIndex = FindStepIndex(
            eatingFar,
            WanderGoalId,
            KLEPExecutableStepKind.Cancellation);
        int eatSelectionIndex = FindStepIndex(
            eatingFar,
            EatGoalId,
            KLEPExecutableStepKind.Solo);
        Expect(wanderCancellationIndex >= 0 &&
               eatSelectionIndex > wanderCancellationIndex,
            "Wander is strictly interrupted before Eat Human advances");
        KLEPExecutableStepTrace wanderCancellation =
            eatingFar.Executions[wanderCancellationIndex];
        Expect(wanderCancellation.State == KLEPExecutableState.Cancelled &&
               wanderCancellation.ExitReason ==
               KLEPExecutableExitReason.Interrupted,
            "Wander records score-driven replacement as Interrupted");
        KLEPExecutableRuntimeSnapshot farEatRoot = FindRoot(
            eatingFar,
            EatGoalId);
        Expect(farEatRoot.State == KLEPExecutableState.Running &&
               FindChild(farEatRoot, MoveId).Runtime.State ==
               KLEPExecutableState.Running,
            "Eat Human exposes its Running Move child for a far target");
        Expect(fixture.Move.TryGetIntent(
                   eatingFar.CycleIndex,
                   out KLEPEnemyObservation moveIntent) &&
               moveIntent.EntityId == "human.far",
            "The Move child retains the far human intent");

        fixture.SetWorld(
            grounded: true,
            edge: null,
            Human("human.near", 1d));
        KLEPDecisionTrace eatingNear = fixture.Agent.Tick().Decision;

        Expect(eatingNear.SelectedExecutableId == EatGoalId &&
               eatingNear.CurrentSoloExecutableId == null &&
               !eatingNear.IsPatient,
            "A near human completes Eat Human without retaining a Solo");
        KLEPExecutableRuntimeSnapshot nearEatRoot = FindRoot(
            eatingNear,
            EatGoalId);
        Expect(nearEatRoot.State == KLEPExecutableState.Succeeded &&
               nearEatRoot.Goal.IsComplete,
            "The near-target Eat Human Goal succeeds");
        Expect(FindChild(nearEatRoot, AttackId).Runtime.State ==
               KLEPExecutableState.Succeeded,
            "The recursive Goal snapshot exposes the Succeeded Attack child");
        Expect(fixture.Attack.TryGetIntent(
                   eatingNear.CycleIndex,
                   out KLEPEnemyObservation attackIntent) &&
               attackIntent.EntityId == "human.near",
            "Attack exposes the near human intent in the success Tick");

        Expect(KLEPEdgeObservation.TryCreate(
                   0xFE,
                   out KLEPEdgeObservation edge),
            "The showcase edge mask creates edge evidence");
        fixture.SetWorld(
            grounded: true,
            edge: edge,
            Human("human.edge", 7d));
        KLEPDecisionTrace avoiding = fixture.Agent.Tick().Decision;

        Expect(avoiding.SelectedExecutableId == AvoidGoalId &&
               avoiding.CurrentSoloExecutableId == AvoidGoalId,
            "Edge evidence selects Avoid Edge even while a human is present");
        Expect(FindCandidate(avoiding, AvoidGoalId).IsEligible &&
               !FindCandidate(avoiding, EatGoalId).IsEligible,
            "Edge evidence makes Avoid eligible and closes Eat Human");
        KLEPExecutableRuntimeSnapshot avoidRoot = FindRoot(
            avoiding,
            AvoidGoalId);
        Expect(avoidRoot.State == KLEPExecutableState.Running &&
               FindChild(avoidRoot, AvoidId).Runtime.State ==
               KLEPExecutableState.Running,
            "Avoid Edge and its recovery child remain Running");
        Expect(fixture.Avoid.TryGetIntent(
                   avoiding.CycleIndex,
                   out KLEPEdgeObservation avoidIntent) &&
               avoidIntent.SupportedProbeMask == 0xFE,
            "The Avoid child exposes the exact edge observation");

        fixture.SetWorld(
            grounded: true,
            edge: null,
            Human("human.edge", 7d));
        KLEPDecisionTrace resumedEat = fixture.Agent.Tick().Decision;

        Expect(resumedEat.SelectedExecutableId == EatGoalId &&
               resumedEat.CurrentSoloExecutableId == EatGoalId,
            "Clearing edge evidence resumes Eat Human");
        int avoidCancellationIndex = FindStepIndex(
            resumedEat,
            AvoidGoalId,
            KLEPExecutableStepKind.Cancellation);
        int resumedEatIndex = FindStepIndex(
            resumedEat,
            EatGoalId,
            KLEPExecutableStepKind.Solo);
        Expect(avoidCancellationIndex >= 0 &&
               resumedEatIndex > avoidCancellationIndex,
            "Avoid Edge is cancelled before resumed Eat Human advances");
        Expect(FindChild(FindRoot(resumedEat, EatGoalId), MoveId)
                   .Runtime.State == KLEPExecutableState.Running,
            "Resumed Eat Human again exposes its Running Move child");

        fixture.SetWorld(
            grounded: false,
            edge: null,
            Human("human.ungrounded", 6d));
        KLEPDecisionTrace ungrounded = fixture.Agent.Tick().Decision;

        Expect(ungrounded.IsPatient &&
               ungrounded.SelectedExecutableId == null &&
               ungrounded.CurrentSoloExecutableId == null,
            "No ground leaves the Agent Patient even with a human sample");
        Expect(!FindCandidate(ungrounded, WanderGoalId).IsEligible &&
               !FindCandidate(ungrounded, EatGoalId).IsEligible &&
               !FindCandidate(ungrounded, AvoidGoalId).IsEligible,
            "No ground closes every showcase Goal");
        Expect(FindStep(
                   ungrounded,
                   EatGoalId,
                   KLEPExecutableStepKind.Cancellation)?.ExitReason ==
               KLEPExecutableExitReason.LocksClosed,
            "Ground loss cancels the previously Running Eat Human Goal");
    }

    private static void VerifyRootOwnershipBoundary(
        ShowcaseFixture fixture,
        KLEPDecisionTrace trace)
    {
        IReadOnlyList<KLEPExecutableDefinition> roots =
            fixture.Neuron.GetRootExecutableDefinitionsSnapshot();
        Expect(roots.Count == 8,
            "The showcase registers three Sensors, two Routers, and three Goals as roots");
        Expect(ContainsDefinition(roots, EatGoalId) &&
               ContainsDefinition(roots, WanderGoalId) &&
               ContainsDefinition(roots, AvoidGoalId),
            "All three Goals are present in the root definition inventory");
        Expect(!ContainsDefinition(roots, MoveId) &&
               !ContainsDefinition(roots, AttackId) &&
               !ContainsDefinition(roots, WanderId) &&
               !ContainsDefinition(roots, AvoidId),
            "Goal-owned children are absent from root definitions");

        Expect(FindChild(FindRoot(trace, EatGoalId), AttackId) != null &&
               FindChild(FindRoot(trace, EatGoalId), MoveId) != null &&
               FindChild(FindRoot(trace, WanderGoalId), WanderId) != null &&
               FindChild(FindRoot(trace, AvoidGoalId), AvoidId) != null,
            "Every Goal-owned child is visible recursively in frozen runtime snapshots");
    }

    private static void VerifyEveryEdgeSupportMask()
    {
        for (int supportedMask = 0;
             supportedMask <= KLEPEdgeObservation.AllSupportedProbeMask;
             supportedMask++)
        {
            bool firstCreated = KLEPEdgeObservation.TryCreate(
                supportedMask,
                out KLEPEdgeObservation first);
            bool secondCreated = KLEPEdgeObservation.TryCreate(
                supportedMask,
                out KLEPEdgeObservation second);

            if (supportedMask == KLEPEdgeObservation.AllSupportedProbeMask)
            {
                Expect(!firstCreated && first == null,
                    "All-supported mask produces no first edge observation");
                Expect(!secondCreated && second == null,
                    "All-supported mask deterministically produces no observation");
                continue;
            }

            int missingMask =
                KLEPEdgeObservation.AllSupportedProbeMask ^ supportedMask;
            Expect(firstCreated && secondCreated && first != null && second != null,
                $"Support mask {supportedMask} produces edge evidence");
            Expect(first.SupportedProbeMask == supportedMask &&
                   first.MissingProbeMask == missingMask,
                $"Support mask {supportedMask} preserves exact mask evidence");
            Expect(first.MissingCount == CountBits(missingMask),
                $"Support mask {supportedMask} records the exact missing count");
            Expect(IsFinite(first.AvoidanceX) && IsFinite(first.AvoidanceZ),
                $"Support mask {supportedMask} produces finite avoidance");
            double lengthSquared =
                first.AvoidanceX * first.AvoidanceX +
                first.AvoidanceZ * first.AvoidanceZ;
            Expect(Math.Abs(lengthSquared - 1d) <= 1e-12d,
                $"Support mask {supportedMask} produces a unit direction");
            Expect(first.SupportedProbeMask == second.SupportedProbeMask &&
                   first.MissingProbeMask == second.MissingProbeMask &&
                   first.MissingCount == second.MissingCount &&
                   first.AvoidanceX == second.AvoidanceX &&
                   first.AvoidanceZ == second.AvoidanceZ,
                $"Support mask {supportedMask} reduces deterministically");

            KLEPEdgeObservation roundTrip = KLEPEdgeObservation.Read(
                first.ToPayload());
            Expect(roundTrip.SupportedProbeMask == first.SupportedProbeMask &&
                   roundTrip.MissingProbeMask == first.MissingProbeMask &&
                   roundTrip.MissingCount == first.MissingCount &&
                   roundTrip.AvoidanceX == first.AvoidanceX &&
                   roundTrip.AvoidanceZ == first.AvoidanceZ,
                $"Support mask {supportedMask} payload round-trips exactly");
        }
    }

    private static void VerifyDeterministicWanderEquivalence()
    {
        const uint seed = 0xD37E51A9u;
        const int segmentTicks = 5;
        var left = new ShowcaseFixture(
            "neuron.wander-equivalence",
            seed,
            segmentTicks);
        var right = new ShowcaseFixture(
            "neuron.wander-equivalence",
            seed,
            segmentTicks);

        for (int iteration = 0; iteration < 64; iteration++)
        {
            left.SetWorld(grounded: true, edge: null);
            right.SetWorld(grounded: true, edge: null);
            KLEPDecisionTrace leftTrace = left.Agent.Tick().Decision;
            KLEPDecisionTrace rightTrace = right.Agent.Tick().Decision;

            Expect(leftTrace.CycleIndex == rightTrace.CycleIndex &&
                   leftTrace.SelectedExecutableId == rightTrace.SelectedExecutableId &&
                   leftTrace.CurrentSoloExecutableId == rightTrace.CurrentSoloExecutableId,
                $"Equivalent wander fixtures choose the same Goal at iteration {iteration}");
            Expect(left.WanderRouter.LastSegmentIndex ==
                   right.WanderRouter.LastSegmentIndex,
                $"Equivalent wander fixtures retain the same segment at iteration {iteration}");
            Expect(left.WanderRouter.LastHeadingIndex ==
                   right.WanderRouter.LastHeadingIndex,
                $"Equivalent wander fixtures choose the same heading at iteration {iteration}");
            Expect(left.WanderRouter.LastDirectionX ==
                   right.WanderRouter.LastDirectionX &&
                   left.WanderRouter.LastDirectionZ ==
                   right.WanderRouter.LastDirectionZ,
                $"Equivalent wander fixtures produce the same direction at iteration {iteration}");

            bool leftHasIntent = left.Wander.TryGetIntent(
                leftTrace.CycleIndex,
                out double leftX,
                out double leftZ,
                out int leftHeading);
            bool rightHasIntent = right.Wander.TryGetIntent(
                rightTrace.CycleIndex,
                out double rightX,
                out double rightZ,
                out int rightHeading);
            Expect(leftHasIntent && rightHasIntent,
                $"Equivalent wander children expose current intent at iteration {iteration}");
            Expect(leftX == rightX &&
                   leftZ == rightZ &&
                   leftHeading == rightHeading,
                $"Equivalent wander child intents are identical at iteration {iteration}");
        }
    }

    private static KLEPEnemyObservation Human(
        string entityId,
        double distance)
    {
        return new KLEPEnemyObservation(
            entityId,
            "team.human",
            distance,
            distance,
            0d,
            0d);
    }

    private static KLEPKeyDefinition OneCycleKey(
        string stableId,
        string displayName)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(stableId),
            displayName,
            scope: KLEPKeyScope.Local,
            defaultLifetime: KLEPKeyLifetime.OneCycle);
    }

    private static KLEPExecutableDefinition TandemDefinition(
        string stableId,
        string displayName,
        KLEPExecutableKind kind,
        KLEPLockExpression expression,
        params KLEPKeyDefinition[] outputs)
    {
        return new KLEPExecutableDefinition(
            stableId,
            displayName,
            kind,
            executionLocks: expression == null
                ? null
                : new[]
                {
                    new KLEPLock(
                        stableId + ".lock",
                        displayName + " requirements",
                        expression)
                },
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: outputs);
    }

    private static KLEPExecutableDefinition ActionDefinition(
        string stableId,
        string displayName,
        KLEPLockExpression expression)
    {
        return new KLEPExecutableDefinition(
            stableId,
            displayName,
            KLEPExecutableKind.Action,
            executionLocks: new[]
            {
                new KLEPLock(
                    stableId + ".lock",
                    displayName + " requirements",
                    expression)
            },
            executionMode: KLEPExecutionMode.Solo);
    }

    private static KLEPExecutableDefinition GoalDefinition(
        string stableId,
        string displayName,
        float score,
        KLEPLockExpression expression)
    {
        return new KLEPExecutableDefinition(
            stableId,
            displayName,
            KLEPExecutableKind.Goal,
            executionLocks: new[]
            {
                new KLEPLock(
                    stableId + ".lock",
                    displayName + " requirements",
                    expression)
            },
            baseAttractiveness: score,
            executionMode: KLEPExecutionMode.Solo);
    }

    private static KLEPLockExpression Present(KLEPKeyDefinition key) =>
        new KLEPKeyPresent(key.Id.Value);

    private static KLEPLockExpression Missing(KLEPKeyDefinition key) =>
        new KLEPNot(Present(key));

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

        throw new InvalidOperationException(
            $"Candidate '{stableId}' was not found.");
    }

    private static KLEPExecutableStepTrace FindStep(
        KLEPDecisionTrace trace,
        string stableId,
        KLEPExecutableStepKind kind)
    {
        int index = FindStepIndex(trace, stableId, kind);
        return index < 0 ? null : trace.Executions[index];
    }

    private static int FindStepIndex(
        KLEPDecisionTrace trace,
        string stableId,
        KLEPExecutableStepKind kind)
    {
        for (int index = 0; index < trace.Executions.Count; index++)
        {
            KLEPExecutableStepTrace step = trace.Executions[index];
            if (step.ExecutableStableId == stableId && step.Kind == kind)
            {
                return index;
            }
        }

        return -1;
    }

    private static KLEPExecutableRuntimeSnapshot FindRoot(
        KLEPDecisionTrace trace,
        string stableId)
    {
        foreach (KLEPExecutableRuntimeSnapshot root in trace.ExecutableStates)
        {
            if (root.ExecutableStableId == stableId)
            {
                return root;
            }
        }

        throw new InvalidOperationException(
            $"Root runtime '{stableId}' was not found.");
    }

    private static KLEPGoalChildRuntimeSnapshot FindChild(
        KLEPExecutableRuntimeSnapshot root,
        string stableId)
    {
        if (root.Goal != null)
        {
            foreach (KLEPGoalLayerRuntimeSnapshot layer in root.Goal.Layers)
            {
                foreach (KLEPGoalChildRuntimeSnapshot child in layer.Children)
                {
                    if (child.ExecutableStableId == stableId)
                    {
                        return child;
                    }

                    KLEPGoalChildRuntimeSnapshot nested = FindChild(
                        child.Runtime,
                        stableId,
                        throwWhenMissing: false);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            $"Goal child runtime '{stableId}' was not found under '{root.ExecutableStableId}'.");
    }

    private static KLEPGoalChildRuntimeSnapshot FindChild(
        KLEPExecutableRuntimeSnapshot root,
        string stableId,
        bool throwWhenMissing)
    {
        if (root.Goal != null)
        {
            foreach (KLEPGoalLayerRuntimeSnapshot layer in root.Goal.Layers)
            {
                foreach (KLEPGoalChildRuntimeSnapshot child in layer.Children)
                {
                    if (child.ExecutableStableId == stableId)
                    {
                        return child;
                    }

                    KLEPGoalChildRuntimeSnapshot nested = FindChild(
                        child.Runtime,
                        stableId,
                        throwWhenMissing: false);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
            }
        }

        if (throwWhenMissing)
        {
            throw new InvalidOperationException(
                $"Goal child runtime '{stableId}' was not found under '{root.ExecutableStableId}'.");
        }

        return null;
    }

    private static bool ContainsDefinition(
        IReadOnlyList<KLEPExecutableDefinition> definitions,
        string stableId)
    {
        foreach (KLEPExecutableDefinition definition in definitions)
        {
            if (definition.StableId == stableId)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException(
                $"Assertion failed: {message}");
        }
    }

    private sealed class ShowcaseFixture
    {
        internal ShowcaseFixture(
            string neuronStableId,
            uint seed,
            int wanderSegmentTicks)
        {
            Ground = OneCycleKey(GroundId, "Ground");
            Enemy = OneCycleKey(EnemyId, "Enemy Detected");
            MoveTarget = OneCycleKey(MoveTargetId, "Move Target");
            AttackTarget = OneCycleKey(AttackTargetId, "Attack Target");
            EdgeDanger = OneCycleKey(EdgeDangerId, "Edge Danger");
            WanderDirection = OneCycleKey(
                WanderDirectionId,
                "Wander Direction");

            GroundSensor = new KLEPObservedKeySensorExecutable(
                TandemDefinition(
                    GroundSensorId,
                    "Ground Sensor",
                    KLEPExecutableKind.Sensor,
                    null,
                    Ground));
            EnemySensor = new KLEPEnemyObservationSensorExecutable(
                TandemDefinition(
                    EnemySensorId,
                    "Enemy Sensor",
                    KLEPExecutableKind.Sensor,
                    null,
                    Enemy),
                Enemy);
            EdgeSensor = new KLEPEdgeObservationSensorExecutable(
                TandemDefinition(
                    EdgeSensorId,
                    "Edge Sensor",
                    KLEPExecutableKind.Sensor,
                    null,
                    EdgeDanger),
                EdgeDanger);
            TargetRouter = new KLEPEnemyTargetRouterExecutable(
                TandemDefinition(
                    TargetRouterId,
                    "Target Router",
                    KLEPExecutableKind.Router,
                    new KLEPAll(Present(Ground), Present(Enemy)),
                    MoveTarget,
                    AttackTarget),
                Enemy,
                MoveTarget,
                AttackTarget,
                AttackRange);
            WanderRouter = new KLEPDeterministicWanderRouterExecutable(
                TandemDefinition(
                    WanderRouterId,
                    "Wander Router",
                    KLEPExecutableKind.Router,
                    new KLEPAll(
                        Present(Ground),
                        Missing(EdgeDanger)),
                    WanderDirection),
                WanderDirection,
                seed,
                wanderSegmentTicks);

            Move = new KLEPZombieMoveExecutable(
                ActionDefinition(
                    MoveId,
                    "Move Toward Human",
                    new KLEPAll(Present(Ground), Present(MoveTarget))),
                MoveTarget);
            Attack = new KLEPZombieAttackExecutable(
                ActionDefinition(
                    AttackId,
                    "Attack Human",
                    new KLEPAll(Present(Ground), Present(AttackTarget))),
                AttackTarget);
            Wander = new KLEPZombieWanderExecutable(
                ActionDefinition(
                    WanderId,
                    "Walk Wander Heading",
                    new KLEPAll(Present(Ground), Present(WanderDirection))),
                WanderDirection);
            Avoid = new KLEPZombieAvoidEdgeExecutable(
                ActionDefinition(
                    AvoidId,
                    "Step Toward Supported Ground",
                    new KLEPAll(Present(Ground), Present(EdgeDanger))),
                EdgeDanger);

            EatGoal = new KLEPGoal(
                GoalDefinition(
                    EatGoalId,
                    "Eat Human",
                    50f,
                    new KLEPAll(
                        Present(Ground),
                        Present(Enemy),
                        Missing(EdgeDanger))),
                new[]
                {
                    new KLEPGoalLayer(
                        KLEPGoalLayerRequirement.AnyCanFire,
                        new KLEPExecutableBase[] { Attack, Move })
                });
            WanderGoal = new KLEPGoal(
                GoalDefinition(
                    WanderGoalId,
                    "Wander",
                    10f,
                    new KLEPAll(
                        Present(Ground),
                        Missing(EdgeDanger),
                        Present(WanderDirection))),
                new[]
                {
                    new KLEPGoalLayer(
                        KLEPGoalLayerRequirement.AnyCanFire,
                        new KLEPExecutableBase[] { Wander })
                });
            AvoidGoal = new KLEPGoal(
                GoalDefinition(
                    AvoidGoalId,
                    "Avoid Edge",
                    100f,
                    new KLEPAll(Present(Ground), Present(EdgeDanger))),
                new[]
                {
                    new KLEPGoalLayer(
                        KLEPGoalLayerRequirement.AnyCanFire,
                        new KLEPExecutableBase[] { Avoid })
                });

            Neuron = new KLEPNeuron(neuronStableId);
            foreach (KLEPExecutableBase root in new KLEPExecutableBase[]
                     {
                         WanderGoal,
                         EdgeSensor,
                         TargetRouter,
                         EatGoal,
                         GroundSensor,
                         AvoidGoal,
                         WanderRouter,
                         EnemySensor
                     })
            {
                Neuron.RegisterExecutable(root);
            }

            Agent = new KLEPAgent(Neuron);
        }

        internal KLEPKeyDefinition Ground { get; }
        internal KLEPKeyDefinition Enemy { get; }
        internal KLEPKeyDefinition MoveTarget { get; }
        internal KLEPKeyDefinition AttackTarget { get; }
        internal KLEPKeyDefinition EdgeDanger { get; }
        internal KLEPKeyDefinition WanderDirection { get; }
        internal KLEPObservedKeySensorExecutable GroundSensor { get; }
        internal KLEPEnemyObservationSensorExecutable EnemySensor { get; }
        internal KLEPEdgeObservationSensorExecutable EdgeSensor { get; }
        internal KLEPEnemyTargetRouterExecutable TargetRouter { get; }
        internal KLEPDeterministicWanderRouterExecutable WanderRouter { get; }
        internal KLEPZombieMoveExecutable Move { get; }
        internal KLEPZombieAttackExecutable Attack { get; }
        internal KLEPZombieWanderExecutable Wander { get; }
        internal KLEPZombieAvoidEdgeExecutable Avoid { get; }
        internal KLEPGoal EatGoal { get; }
        internal KLEPGoal WanderGoal { get; }
        internal KLEPGoal AvoidGoal { get; }
        internal KLEPNeuron Neuron { get; }
        internal KLEPAgent Agent { get; }

        internal void SetWorld(
            bool grounded,
            KLEPEdgeObservation edge,
            params KLEPEnemyObservation[] humans)
        {
            GroundSensor.SetObservation(grounded);
            EdgeSensor.SetObservation(edge);
            EnemySensor.SetObservations(
                humans ?? Array.Empty<KLEPEnemyObservation>());
        }
    }
}
