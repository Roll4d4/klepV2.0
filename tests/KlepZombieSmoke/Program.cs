using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roll4d4.Klep.Behaviors;
using Roll4d4.Klep.Core;

internal static class Program
{
    private const string GroundKeyId = "key.ground";
    private const string EnemyDetectedKeyId = "key.enemy-detected";
    private const string MoveTargetKeyId = "key.move-target";
    private const string AttackTargetKeyId = "key.attack-target";

    private const string GroundSensorId = "sensor.ground";
    private const string EnemySensorId = "sensor.enemy";
    private const string RouterId = "router.enemy-target";
    private const string AttackRouterId = RouterId + ".attack";
    private const string MoveId = "action.zombie-move";
    private const string AttackId = "action.zombie-attack";

    private const double AttackRange = 1.75d;

    private static int assertions;

    private static void Main()
    {
        VerifyNoEnemyLeavesAgentPatient();
        VerifyFarEnemyMovesInSameTick();
        VerifyNearEnemySwitchesToAttackInSameTick();
        VerifyGroundLossBlocksRoutingAndMovement();
        VerifyOneCycleObservationsExpire();
        VerifyNearestAndOrdinalTargetSelection();
        VerifyInputAndRegistrationOrderDeterminism();
        VerifyObservationBoundaryValidation();
        VerifyRouterRejectsCorruptSnapshots();
        VerifyRouterFailsWithoutEnemyEvidence();
        VerifyConstructorShapeValidation();

        Console.WriteLine($"KLEP Zombie smoke passed: {assertions} assertions.");
    }

    private static void VerifyNoEnemyLeavesAgentPatient()
    {
        var fixture = new ZombieFixture("neuron.zombie.patient");

        fixture.SetObservation(false);
        KLEPAgentTickTrace empty = fixture.Agent.Tick();
        KLEPDecisionTrace decision = empty.Decision;

        Expect(decision.IsPatient,
            "No Ground and no enemy leaves the Agent patient");
        Expect(decision.SelectedExecutableId == null &&
               decision.CurrentSoloExecutableId == null,
            "A patient decision selects and retains no Solo");
        Expect(decision.InitialKeySnapshot.Facts.Count == 0 &&
               decision.KeySnapshot.Facts.Count == 0,
            "An empty sample begins and ends with an empty Key environment");
        Expect(FindStep(decision, GroundSensorId, KLEPExecutableStepKind.Tandem) != null &&
               FindStep(decision, EnemySensorId, KLEPExecutableStepKind.Tandem) != null,
            "Both observation Tandems still advance for an empty sample");
        Expect(FindStep(decision, RouterId, KLEPExecutableStepKind.Tandem) == null,
            "The Ground-and-enemy Move Router does not advance without observations");
        Expect(FindStep(
                   decision,
                   AttackRouterId,
                   KLEPExecutableStepKind.Tandem) == null,
            "The Ground-and-enemy Attack Router does not advance without observations");
        Expect(!FindCandidate(decision, MoveId).IsEligible &&
               !FindCandidate(decision, AttackId).IsEligible,
            "Move and Attack are filtered as ineligible before scoring");

        fixture.SetObservation(true);
        KLEPDecisionTrace groundedWithoutEnemy = fixture.Agent.Tick().Decision;
        Expect(groundedWithoutEnemy.IsPatient &&
               groundedWithoutEnemy.KeySnapshot.Contains(GroundKeyId),
            "Ground alone remains a patient state while preserving the sensed fact");
        Expect(!groundedWithoutEnemy.KeySnapshot.Contains(EnemyDetectedKeyId) &&
               !groundedWithoutEnemy.KeySnapshot.Contains(MoveTargetKeyId) &&
               !groundedWithoutEnemy.KeySnapshot.Contains(AttackTargetKeyId),
            "Ground alone cannot fabricate an enemy or target intent");
        Expect(fixture.Move.IntentCycleIndex == -1 &&
               fixture.Attack.IntentCycleIndex == -1,
            "Neither action exposes an intent when no enemy was observed");
    }

    private static void VerifyFarEnemyMovesInSameTick()
    {
        var fixture = new ZombieFixture("neuron.zombie.far");
        KLEPEnemyObservation enemy = Observation(
            "enemy.far", 8d, 8d, 0d, 2d);
        fixture.SetObservation(true, enemy);

        KLEPAgentTickTrace agentTrace = fixture.Agent.Tick();
        KLEPDecisionTrace decision = agentTrace.Decision;

        Expect(decision.InitialKeySnapshot.Facts.Count == 0,
            "The far-enemy Tick starts before either sensor has published");
        Expect(decision.KeySnapshot.Contains(GroundKeyId) &&
               decision.KeySnapshot.Contains(EnemyDetectedKeyId) &&
               decision.KeySnapshot.Contains(MoveTargetKeyId),
            "Ground, EnemyDetected, and MoveTarget settle in one Tick");
        Expect(!decision.KeySnapshot.Contains(AttackTargetKeyId),
            "A far enemy does not also produce AttackTarget");
        Expect(decision.SelectedExecutableId == MoveId &&
               decision.CurrentSoloExecutableId == MoveId &&
               !decision.IsPatient,
            "The far-enemy Tick selects and retains the Running Move Solo");

        KLEPExecutableStepTrace routerStep = FindStep(
            decision, RouterId, KLEPExecutableStepKind.Tandem);
        KLEPExecutableStepTrace attackRouterStep = FindStep(
            decision, AttackRouterId, KLEPExecutableStepKind.Tandem);
        KLEPExecutableStepTrace moveStep = FindStep(
            decision, MoveId, KLEPExecutableStepKind.Solo);
        Expect(routerStep != null &&
               routerStep.State == KLEPExecutableState.Succeeded &&
               routerStep.Outputs.Count == 1 &&
               routerStep.Outputs[0].KeyId.Value == MoveTargetKeyId,
            "The Router emits exactly one MoveTarget for a far enemy");
        Expect(attackRouterStep != null &&
               attackRouterStep.State == KLEPExecutableState.Failed &&
               attackRouterStep.Outputs.Count == 0,
            "The non-matching Attack Router fails without emitting a target");
        Expect(moveStep != null && moveStep.State == KLEPExecutableState.Running,
            "Move enters Running during the same Tick as its routed target");
        Expect(fixture.MoveRouter.LastRoute == KLEPEnemyTargetRoute.Move &&
               fixture.AttackRouter.LastRoute == KLEPEnemyTargetRoute.Move &&
               fixture.MoveRouter.LastTarget.EntityId == enemy.EntityId &&
               fixture.AttackRouter.LastTarget.EntityId == enemy.EntityId,
            "Both branch Routers expose the same selected Move target for inspection");

        Expect(fixture.Move.TryGetIntent(
                   decision.CycleIndex,
                   out KLEPEnemyObservation moveIntent) &&
               moveIntent.EntityId == enemy.EntityId,
            "Move exposes only the current Tick's immutable target intent");
        Expect(!fixture.Move.TryGetIntent(
                   decision.CycleIndex - 1,
                   out _),
            "Move rejects an intent query for a different Tick");
        Expect(!fixture.Attack.TryGetIntent(decision.CycleIndex, out _),
            "Attack has no intent during the Move Tick");

        KLEPKeyFact ground = GetOnlyFact(decision.KeySnapshot, fixture.Ground.Id);
        KLEPKeyFact detected = GetOnlyFact(
            decision.KeySnapshot, fixture.EnemyDetected.Id);
        KLEPKeyFact target = GetOnlyFact(
            decision.KeySnapshot, fixture.MoveTarget.Id);
        Expect(ground.SourceId == GroundSensorId &&
               detected.SourceId == EnemySensorId &&
               target.SourceId == RouterId,
            "Every settled fact retains the correct behavior provenance");
        Expect(ground.ActivatedTick == decision.CycleIndex &&
               detected.ActivatedTick == decision.CycleIndex &&
               target.ActivatedTick == decision.CycleIndex,
            "Every observation and routed intent activates during this Tick");
        Expect(ReadObservation(target).EntityId == enemy.EntityId,
            "MoveTarget preserves the complete selected enemy payload");
        Expect(decision.TandemWaves.Count >= 2,
            "The trace exposes a sensor wave followed by the newly unlocked Router wave");
    }

    private static void VerifyNearEnemySwitchesToAttackInSameTick()
    {
        var fixture = new ZombieFixture("neuron.zombie.switch");
        fixture.SetObservation(true, Observation("enemy.target", 6d));
        KLEPDecisionTrace moving = fixture.Agent.Tick().Decision;
        long moveRun = fixture.Move.IntentRunIndex;
        Expect(moving.CurrentSoloExecutableId == MoveId && moveRun >= 0,
            "The switch scenario begins with one Running Move run");

        KLEPEnemyObservation near = Observation("enemy.target", 1d, 1d);
        fixture.SetObservation(true, near);
        KLEPDecisionTrace attacking = fixture.Agent.Tick().Decision;

        Expect(attacking.KeySnapshot.Contains(GroundKeyId) &&
               attacking.KeySnapshot.Contains(EnemyDetectedKeyId) &&
               attacking.KeySnapshot.Contains(AttackTargetKeyId),
            "A near observation settles Ground, EnemyDetected, and AttackTarget");
        Expect(!attacking.KeySnapshot.Contains(MoveTargetKeyId),
            "The Router's near branch is mutually exclusive with MoveTarget");
        Expect(attacking.SelectedExecutableId == AttackId &&
               attacking.CurrentSoloExecutableId == null &&
               !attacking.IsPatient,
            "Attack succeeds instantaneously in the same Tick and leaves no Running Solo");

        KLEPExecutableStepTrace moveCancellation = FindStep(
            attacking, MoveId, KLEPExecutableStepKind.Cancellation);
        KLEPExecutableStepTrace attackStep = FindStep(
            attacking, AttackId, KLEPExecutableStepKind.Solo);
        KLEPExecutableStepTrace moveRouterStep = FindStep(
            attacking, RouterId, KLEPExecutableStepKind.Tandem);
        KLEPExecutableStepTrace attackRouterStep = FindStep(
            attacking, AttackRouterId, KLEPExecutableStepKind.Tandem);
        Expect(moveCancellation != null &&
               moveCancellation.State == KLEPExecutableState.Cancelled &&
               moveCancellation.ExitReason == KLEPExecutableExitReason.LocksClosed,
            "Move exits exactly because its MoveTarget Lock closed");
        Expect(attackStep != null &&
               attackStep.State == KLEPExecutableState.Succeeded &&
               attackStep.ExitReason == KLEPExecutableExitReason.Succeeded,
            "Attack enters, advances, exits, and succeeds in the switch Tick");
        Expect(moveRouterStep != null &&
               moveRouterStep.State == KLEPExecutableState.Failed &&
               moveRouterStep.Outputs.Count == 0 &&
               attackRouterStep != null &&
               attackRouterStep.State == KLEPExecutableState.Succeeded &&
               attackRouterStep.Outputs.Count == 1 &&
               attackRouterStep.Outputs[0].KeyId.Value == AttackTargetKeyId,
            "Only the matching Attack Router completes and guarantees its target");
        Expect(fixture.MoveRouter.LastRoute == KLEPEnemyTargetRoute.Attack &&
               fixture.AttackRouter.LastRoute == KLEPEnemyTargetRoute.Attack &&
               fixture.MoveRouter.LastTarget.EntityId == near.EntityId &&
               fixture.AttackRouter.LastTarget.EntityId == near.EntityId,
            "Both branch Routers expose the same near Attack target");
        Expect(GetOnlyFact(
                   attacking.KeySnapshot,
                   fixture.AttackTarget.Id).SourceId == AttackRouterId,
            "AttackTarget provenance identifies the successful Attack Router");
        Expect(fixture.Attack.TryGetIntent(
                   attacking.CycleIndex,
                   out KLEPEnemyObservation attackIntent) &&
               attackIntent.EntityId == near.EntityId &&
               attackIntent.Distance == near.Distance,
            "Attack exposes the selected current-Tick target");
        Expect(!fixture.Move.TryGetIntent(attacking.CycleIndex, out _) &&
               fixture.Move.IntentRunIndex == moveRun,
            "The stale Move run cannot masquerade as current-Tick motion");
    }

    private static void VerifyGroundLossBlocksRoutingAndMovement()
    {
        var fixture = new ZombieFixture("neuron.zombie.ground-loss");
        KLEPEnemyObservation far = Observation("enemy.air-test", 5d);
        fixture.SetObservation(true, far);
        KLEPDecisionTrace grounded = fixture.Agent.Tick().Decision;
        Expect(grounded.CurrentSoloExecutableId == MoveId,
            "Ground-loss setup starts in Running Move");

        fixture.SetObservation(false, far);
        KLEPDecisionTrace airborne = fixture.Agent.Tick().Decision;

        Expect(!airborne.KeySnapshot.Contains(GroundKeyId),
            "A false Ground sample leaves Ground absent");
        Expect(airborne.KeySnapshot.Contains(EnemyDetectedKeyId),
            "Enemy sensing remains independent while Ground is absent");
        Expect(!airborne.KeySnapshot.Contains(MoveTargetKeyId) &&
               !airborne.KeySnapshot.Contains(AttackTargetKeyId),
            "The Ground-and-enemy Router emits no action target while airborne");
        Expect(FindStep(airborne, RouterId, KLEPExecutableStepKind.Tandem) == null,
            "Ground loss prevents the Move Router itself from advancing");
        Expect(FindStep(
                   airborne,
                   AttackRouterId,
                   KLEPExecutableStepKind.Tandem) == null,
            "Ground loss prevents the Attack Router itself from advancing");
        Expect(airborne.IsPatient &&
               airborne.SelectedExecutableId == null &&
               airborne.CurrentSoloExecutableId == null,
            "The airborne Agent returns to the patient state");

        KLEPExecutableStepTrace cancellation = FindStep(
            airborne, MoveId, KLEPExecutableStepKind.Cancellation);
        Expect(cancellation != null &&
               cancellation.ExitReason == KLEPExecutableExitReason.LocksClosed,
            "Ground loss cancels Running Move through its closed All Lock");
        Expect(!FindCandidate(airborne, MoveId).IsEligible &&
               !FindCandidate(airborne, AttackId).IsEligible,
            "Neither action can become eligible from EnemyDetected alone");
        Expect(!fixture.Move.TryGetIntent(airborne.CycleIndex, out _),
            "The Unity boundary cannot retrieve stale movement after Ground loss");
    }

    private static void VerifyOneCycleObservationsExpire()
    {
        var fixture = new ZombieFixture("neuron.zombie.expiry");
        fixture.SetObservation(true, Observation("enemy.expiring", 4d));
        KLEPDecisionTrace present = fixture.Agent.Tick().Decision;
        KLEPKeyFact oldGround = GetOnlyFact(present.KeySnapshot, fixture.Ground.Id);
        KLEPKeyFact oldEnemy = GetOnlyFact(
            present.KeySnapshot, fixture.EnemyDetected.Id);
        KLEPKeyFact oldMove = GetOnlyFact(
            present.KeySnapshot, fixture.MoveTarget.Id);

        fixture.SetObservation(false);
        KLEPDecisionTrace absent = fixture.Agent.Tick().Decision;
        Expect(absent.InitialKeySnapshot.Facts.Count == 0,
            "All prior OneCycle facts expire before the next initial snapshot");
        Expect(absent.KeySnapshot.Facts.Count == 0,
            "An empty follow-up sample emits no replacement observations or intents");
        Expect(!absent.KeySnapshot.Contains(GroundKeyId) &&
               !absent.KeySnapshot.Contains(EnemyDetectedKeyId) &&
               !absent.KeySnapshot.Contains(MoveTargetKeyId),
            "Ground, EnemyDetected, and MoveTarget all disappear together");
        Expect(oldGround.Lifetime == KLEPKeyLifetime.OneCycle &&
               oldEnemy.Lifetime == KLEPKeyLifetime.OneCycle &&
               oldMove.Lifetime == KLEPKeyLifetime.OneCycle,
            "The disappeared chain was authored entirely as OneCycle facts");
        Expect(absent.IsPatient &&
               FindStep(absent, MoveId, KLEPExecutableStepKind.Cancellation)
                   .ExitReason == KLEPExecutableExitReason.LocksClosed,
            "Expiry closes Move and returns the Agent to patient");

        fixture.SetObservation(true, Observation("enemy.expiring", 4d));
        KLEPDecisionTrace reobserved = fixture.Agent.Tick().Decision;
        KLEPKeyFact newGround = GetOnlyFact(
            reobserved.KeySnapshot, fixture.Ground.Id);
        KLEPKeyFact newEnemy = GetOnlyFact(
            reobserved.KeySnapshot, fixture.EnemyDetected.Id);
        Expect(newGround.OccurrenceId != oldGround.OccurrenceId &&
               newEnemy.OccurrenceId != oldEnemy.OccurrenceId,
            "A later observation creates fresh occurrences rather than reviving snapshots");
    }

    private static void VerifyNearestAndOrdinalTargetSelection()
    {
        var fixture = new ZombieFixture("neuron.zombie.selection");
        fixture.SetObservation(
            true,
            Observation("enemy.zeta", 9d),
            Observation("enemy.nearest", 3d, 3d),
            Observation("enemy.alpha", 6d));
        KLEPDecisionTrace nearest = fixture.Agent.Tick().Decision;

        Expect(fixture.MoveRouter.LastTarget.EntityId == "enemy.nearest" &&
               fixture.MoveRouter.LastTarget.Distance == 3d &&
               fixture.AttackRouter.LastTarget.EntityId == "enemy.nearest" &&
               fixture.AttackRouter.LastTarget.Distance == 3d,
            "Both branch Routers select the strictly nearest observed enemy");
        Expect(fixture.Move.LastTarget.EntityId == "enemy.nearest",
            "Move consumes the Router's nearest target");
        Expect(ReadObservation(GetOnlyFact(
                   nearest.KeySnapshot,
                   fixture.MoveTarget.Id)).EntityId == "enemy.nearest",
            "The published MoveTarget agrees with Router and Move diagnostics");

        IReadOnlyList<string> firstOrder = ReadEntityIds(
            nearest.KeySnapshot, fixture.EnemyDetected.Id);
        Expect(Join(firstOrder) ==
               "enemy.alpha,enemy.nearest,enemy.zeta",
            "EnemyDetected occurrences are emitted in ordinal entity-ID order");

        fixture.SetObservation(
            true,
            Observation("enemy.zeta", 4d),
            Observation("enemy.alpha", 4d),
            Observation("enemy.other", 7d));
        KLEPDecisionTrace tied = fixture.Agent.Tick().Decision;

        Expect(fixture.MoveRouter.LastTarget.EntityId == "enemy.alpha" &&
               fixture.MoveRouter.LastTarget.Distance == 4d &&
               fixture.AttackRouter.LastTarget.EntityId == "enemy.alpha" &&
               fixture.AttackRouter.LastTarget.Distance == 4d,
            "Both branch Routers break equal-distance ties by ordinal entity ID");
        Expect(fixture.Move.LastTarget.EntityId == "enemy.alpha" &&
               tied.CurrentSoloExecutableId == MoveId,
            "The Running Move consumes the deterministic tie winner");
        Expect(ReadObservation(GetOnlyFact(
                   tied.KeySnapshot,
                   fixture.MoveTarget.Id)).EntityId == "enemy.alpha",
            "The tied selection remains explicit in the immutable target payload");
    }

    private static void VerifyInputAndRegistrationOrderDeterminism()
    {
        string baseline = RunDeterministicScenario(false, false);
        Expect(RunDeterministicScenario(true, false) == baseline,
            "Reversing executable registration preserves the complete zombie trace");
        Expect(RunDeterministicScenario(false, true) == baseline,
            "Reversing enemy observation input preserves the complete zombie trace");
        Expect(RunDeterministicScenario(true, true) == baseline,
            "Reversing both registration and input preserves the complete zombie trace");

        for (int repeat = 0; repeat < 20; repeat++)
        {
            bool reverseRegistration = (repeat & 1) != 0;
            bool reverseInput = (repeat & 2) != 0;
            Expect(RunDeterministicScenario(
                       reverseRegistration,
                       reverseInput) == baseline,
                $"Zombie trace remains deterministic on repeat {repeat}");
        }
    }

    private static void VerifyObservationBoundaryValidation()
    {
        KLEPEnemyObservation source = new KLEPEnemyObservation(
            "enemy.roundtrip", null, 2.5d, 1d, 2d, 3d);
        KLEPKeyPayload payload = source.ToPayload();
        Expect(KLEPEnemyObservation.TryRead(
                   payload,
                   out KLEPEnemyObservation roundTrip),
            "A valid enemy payload round-trips through the immutable Key format");
        Expect(roundTrip.EntityId == source.EntityId &&
               roundTrip.TeamId == string.Empty &&
               roundTrip.Distance == source.Distance &&
               roundTrip.PositionX == source.PositionX &&
               roundTrip.PositionY == source.PositionY &&
               roundTrip.PositionZ == source.PositionZ,
            "The round trip preserves identity, normalized team, distance, and position");
        Expect(payload.Count == 6,
            "The enemy payload has exactly the six documented scalar fields");

        ZombieFixture fixture = new ZombieFixture("neuron.zombie.copy-boundary");
        var mutable = new List<KLEPEnemyObservation>
        {
            Observation("enemy.zeta", 4d),
            Observation("enemy.alpha", 3d)
        };
        fixture.EnemySensor.SetObservations(mutable);
        mutable.Clear();
        Expect(fixture.EnemySensor.Observations.Count == 2 &&
               fixture.EnemySensor.Observations[0].EntityId == "enemy.alpha" &&
               fixture.EnemySensor.Observations[1].EntityId == "enemy.zeta",
            "SetObservations snapshots and ordinally sorts caller-owned input");

        ExpectThrows<ArgumentNullException>(
            () => fixture.EnemySensor.SetObservations(null),
            "A null observation collection is rejected");
        ExpectThrows<ArgumentException>(
            () => fixture.EnemySensor.SetObservations(
                new KLEPEnemyObservation[] { Observation("enemy.ok", 1d), null }),
            "A null observation item is rejected");
        ExpectThrows<ArgumentException>(
            () => fixture.EnemySensor.SetObservations(new[]
            {
                Observation("enemy.duplicate", 1d),
                Observation("enemy.duplicate", 2d)
            }),
            "Duplicate entity IDs in one sample are rejected before Tick");

        ExpectThrows<ArgumentException>(
            () => Observation(" ", 1d),
            "A blank entity ID is rejected");
        ExpectThrows<ArgumentOutOfRangeException>(
            () => Observation("enemy.negative", -0.01d),
            "A negative distance is rejected");
        ExpectThrows<ArgumentOutOfRangeException>(
            () => Observation("enemy.nan-distance", double.NaN),
            "A NaN distance is rejected");
        ExpectThrows<ArgumentOutOfRangeException>(
            () => Observation("enemy.inf-distance", double.PositiveInfinity),
            "An infinite distance is rejected");
        ExpectThrows<ArgumentOutOfRangeException>(
            () => Observation(
                "enemy.nan-position", 1d, double.NaN, 0d, 0d),
            "A non-finite sampled position is rejected");

        KLEPKeyPayload missingFields = new KLEPKeyPayload(new[]
        {
            new KeyValuePair<string, KLEPKeyValue>(
                KLEPEnemyObservation.EntityIdField,
                "enemy.incomplete")
        });
        Expect(!KLEPEnemyObservation.TryRead(missingFields, out _),
            "TryRead safely rejects an incomplete enemy payload");
        ExpectThrows<InvalidOperationException>(
            () => KLEPEnemyObservation.Read(missingFields),
            "Read rejects an incomplete enemy payload with an explicit failure");
    }

    private static void VerifyRouterRejectsCorruptSnapshots()
    {
        KLEPKeyDefinition enemy = MakeOneCycleKey(
            EnemyDetectedKeyId, "EnemyDetected");
        KLEPKeyDefinition move = MakeOneCycleKey(
            MoveTargetKeyId, "MoveTarget");
        KLEPKeyDefinition attack = MakeOneCycleKey(
            AttackTargetKeyId, "AttackTarget");

        KLEPKeyPayload malformed = new KLEPKeyPayload(new[]
        {
            new KeyValuePair<string, KLEPKeyValue>(
                KLEPEnemyObservation.EntityIdField,
                "enemy.malformed")
        });
        var malformedEmitter = new RawEnemyEmitter(
            MakeRawEmitterDefinition("sensor.raw-malformed", enemy),
            enemy,
            malformed);
        var malformedRouter = new KLEPEnemyTargetRouterExecutable(
            MakeRouterDefinition(
                "router.raw-malformed", null, enemy, move, false),
            enemy,
            move,
            attack,
            AttackRange);
        var malformedNeuron = new KLEPNeuron("neuron.zombie.malformed");
        malformedNeuron.RegisterExecutable(malformedRouter);
        malformedNeuron.RegisterExecutable(malformedEmitter);
        var malformedAgent = new KLEPAgent(malformedNeuron);
        ExpectThrowsMessage<InvalidOperationException>(
            () => malformedAgent.Tick(),
            "enemy observation payload requires",
            "The Router defensively rejects a malformed EnemyDetected fact");

        KLEPKeyPayload duplicate = Observation(
            "enemy.duplicate-snapshot", 3d).ToPayload();
        var duplicateEmitter = new RawEnemyEmitter(
            MakeRawEmitterDefinition("sensor.raw-duplicate", enemy),
            enemy,
            duplicate,
            duplicate);
        var duplicateRouter = new KLEPEnemyTargetRouterExecutable(
            MakeRouterDefinition(
                "router.raw-duplicate", null, enemy, move, false),
            enemy,
            move,
            attack,
            AttackRange);
        var duplicateNeuron = new KLEPNeuron("neuron.zombie.duplicate-snapshot");
        duplicateNeuron.RegisterExecutable(duplicateRouter);
        duplicateNeuron.RegisterExecutable(duplicateEmitter);
        var duplicateAgent = new KLEPAgent(duplicateNeuron);
        ExpectThrowsMessage<InvalidOperationException>(
            () => duplicateAgent.Tick(),
            "more than one EnemyDetected occurrence",
            "The Router defensively rejects duplicate entity occurrences in a snapshot");
    }

    private static void VerifyRouterFailsWithoutEnemyEvidence()
    {
        KLEPKeyDefinition enemy = MakeOneCycleKey(
            EnemyDetectedKeyId, "EnemyDetected");
        KLEPKeyDefinition move = MakeOneCycleKey(
            MoveTargetKeyId, "MoveTarget");
        KLEPKeyDefinition attack = MakeOneCycleKey(
            AttackTargetKeyId, "AttackTarget");
        var router = new KLEPEnemyTargetRouterExecutable(
            new KLEPExecutableDefinition(
                "router.no-enemy",
                "Always-sampled Move Router",
                KLEPExecutableKind.Router,
                executionMode: KLEPExecutionMode.Tandem,
                declaredOutputs: new[] { move }),
            enemy,
            move,
            attack,
            AttackRange);
        var neuron = new KLEPNeuron("neuron.zombie.no-enemy-router");
        neuron.RegisterExecutable(router);

        KLEPDecisionTrace trace = new KLEPAgent(neuron).Tick().Decision;
        KLEPExecutableStepTrace step = FindStep(
            trace,
            router.StableId,
            KLEPExecutableStepKind.Tandem);
        Expect(step != null &&
               step.State == KLEPExecutableState.Failed &&
               step.Outputs.Count == 0,
            "An advancing branch Router fails without EnemyDetected evidence");
        Expect(router.LastRoute == KLEPEnemyTargetRoute.None &&
               router.LastTarget == null,
            "A no-enemy failure clears the Router's route and target diagnostics");
    }

    private static void VerifyConstructorShapeValidation()
    {
        KLEPKeyDefinition ground = MakeOneCycleKey(GroundKeyId, "Ground");
        KLEPKeyDefinition enemy = MakeOneCycleKey(
            EnemyDetectedKeyId, "EnemyDetected");
        KLEPKeyDefinition move = MakeOneCycleKey(
            MoveTargetKeyId, "MoveTarget");
        KLEPKeyDefinition attack = MakeOneCycleKey(
            AttackTargetKeyId, "AttackTarget");

        KLEPExecutableDefinition sensorDefinition =
            MakeEnemySensorDefinition("sensor.shape", enemy);
        var validSensor = new KLEPEnemyObservationSensorExecutable(
            sensorDefinition, enemy);
        Expect(validSensor.Kind == KLEPExecutableKind.Sensor &&
               validSensor.ExecutionMode == KLEPExecutionMode.Tandem &&
               ReferenceEquals(validSensor.EnemyDetectedDefinition, enemy),
            "The valid Enemy Sensor shape is explicit and inspectable");
        ExpectThrows<ArgumentNullException>(
            () => new KLEPEnemyObservationSensorExecutable(null, enemy),
            "Enemy Sensor rejects a null definition");
        ExpectThrows<ArgumentNullException>(
            () => new KLEPEnemyObservationSensorExecutable(sensorDefinition, null),
            "Enemy Sensor rejects a null EnemyDetected definition");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyObservationSensorExecutable(
                new KLEPExecutableDefinition(
                    "sensor.wrong-kind",
                    "Wrong Kind",
                    KLEPExecutableKind.Action,
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { enemy }),
                enemy),
            "Enemy Sensor rejects the wrong Executable kind");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyObservationSensorExecutable(
                new KLEPExecutableDefinition(
                    "sensor.wrong-mode",
                    "Wrong Mode",
                    KLEPExecutableKind.Sensor,
                    executionMode: KLEPExecutionMode.Solo,
                    declaredOutputs: new[] { enemy }),
                enemy),
            "Enemy Sensor rejects Solo mode");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyObservationSensorExecutable(
                new KLEPExecutableDefinition(
                    "sensor.no-output",
                    "No Output",
                    KLEPExecutableKind.Sensor,
                    executionMode: KLEPExecutionMode.Tandem),
                enemy),
            "Enemy Sensor rejects an empty output declaration");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyObservationSensorExecutable(
                new KLEPExecutableDefinition(
                    "sensor.two-outputs",
                    "Two Outputs",
                    KLEPExecutableKind.Sensor,
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { enemy, move }),
                enemy),
            "Enemy Sensor rejects more than one declared output");

        KLEPKeyDefinition enemyTwin = MakeOneCycleKey(
            EnemyDetectedKeyId, "EnemyDetected Twin");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyObservationSensorExecutable(
                sensorDefinition, enemyTwin),
            "Enemy Sensor requires the exact declared output definition instance");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyObservationSensorExecutable(
                MakeEnemySensorDefinition(
                    "sensor.persistent",
                    MakeKey(
                        "key.enemy-persistent",
                        "Enemy Persistent",
                        KLEPKeyScope.Local,
                        KLEPKeyLifetime.Persistent)),
                MakeKey(
                    "key.enemy-persistent",
                    "Different exact object",
                    KLEPKeyScope.Local,
                    KLEPKeyLifetime.Persistent)),
            "Enemy Sensor rejects a Persistent EnemyDetected output");
        KLEPKeyDefinition globalEnemy = MakeKey(
            "key.enemy-global",
            "Enemy Global",
            KLEPKeyScope.Global,
            KLEPKeyLifetime.OneCycle);
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyObservationSensorExecutable(
                MakeEnemySensorDefinition("sensor.global", globalEnemy),
                globalEnemy),
            "Enemy Sensor rejects a Global EnemyDetected output");

        KLEPExecutableDefinition routerDefinition = MakeRouterDefinition(
            "router.shape", ground, enemy, move, true);
        var validRouter = new KLEPEnemyTargetRouterExecutable(
            routerDefinition, enemy, move, attack, AttackRange);
        Expect(validRouter.Kind == KLEPExecutableKind.Router &&
               validRouter.ExecutionMode == KLEPExecutionMode.Tandem &&
               validRouter.AttackRange == AttackRange &&
               validRouter.ConfiguredRoute == KLEPEnemyTargetRoute.Move,
            "The valid Move Router shape, range, and branch are inspectable");
        KLEPExecutableDefinition attackRouterDefinition = MakeRouterDefinition(
            "router.shape.attack", ground, enemy, attack, true);
        var validAttackRouter = new KLEPEnemyTargetRouterExecutable(
            attackRouterDefinition, enemy, move, attack, AttackRange);
        Expect(validAttackRouter.ConfiguredRoute == KLEPEnemyTargetRoute.Attack,
            "The exact AttackTarget declaration configures the Attack branch");
        ExpectThrows<ArgumentNullException>(
            () => new KLEPEnemyTargetRouterExecutable(
                null, enemy, move, attack, AttackRange),
            "Target Router rejects a null definition");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyTargetRouterExecutable(
                new KLEPExecutableDefinition(
                    "router.wrong-kind",
                    "Wrong Kind",
                    KLEPExecutableKind.Sensor,
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { move }),
                enemy, move, attack, AttackRange),
            "Target Router rejects the wrong Executable kind");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyTargetRouterExecutable(
                new KLEPExecutableDefinition(
                    "router.wrong-mode",
                    "Wrong Mode",
                    KLEPExecutableKind.Router,
                    executionMode: KLEPExecutionMode.Solo,
                    declaredOutputs: new[] { move }),
                enemy, move, attack, AttackRange),
            "Target Router rejects Solo mode");
        ExpectThrows<ArgumentNullException>(
            () => new KLEPEnemyTargetRouterExecutable(
                routerDefinition, null, move, attack, AttackRange),
            "Target Router rejects a null EnemyDetected definition");
        ExpectThrows<ArgumentNullException>(
            () => new KLEPEnemyTargetRouterExecutable(
                routerDefinition, enemy, null, attack, AttackRange),
            "Target Router rejects a null MoveTarget definition");
        ExpectThrows<ArgumentNullException>(
            () => new KLEPEnemyTargetRouterExecutable(
                routerDefinition, enemy, move, null, AttackRange),
            "Target Router rejects a null AttackTarget definition");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyTargetRouterExecutable(
                new KLEPExecutableDefinition(
                    "router.no-outputs",
                    "No Outputs",
                    KLEPExecutableKind.Router,
                    executionMode: KLEPExecutionMode.Tandem),
                enemy, move, attack, AttackRange),
            "Target Router rejects an empty output declaration");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyTargetRouterExecutable(
                new KLEPExecutableDefinition(
                    "router.two-outputs",
                    "Two Outputs",
                    KLEPExecutableKind.Router,
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { move, attack }),
                enemy, move, attack, AttackRange),
            "Target Router rejects more than one declared output");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyTargetRouterExecutable(
                new KLEPExecutableDefinition(
                    "router.unrelated-output",
                    "Unrelated Output",
                    KLEPExecutableKind.Router,
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { enemy }),
                enemy, move, attack, AttackRange),
            "Target Router rejects a declaration other than its exact branch output");

        KLEPKeyDefinition moveTwin = MakeOneCycleKey(
            MoveTargetKeyId, "MoveTarget Twin");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyTargetRouterExecutable(
                routerDefinition, enemy, moveTwin, attack, AttackRange),
            "Target Router requires the exact declared MoveTarget instance");
        KLEPKeyDefinition attackTwin = MakeOneCycleKey(
            AttackTargetKeyId, "AttackTarget Twin");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyTargetRouterExecutable(
                attackRouterDefinition,
                enemy,
                move,
                attackTwin,
                AttackRange),
            "Target Router requires the exact declared AttackTarget instance");
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyTargetRouterExecutable(
                new KLEPExecutableDefinition(
                    "router.same-targets",
                    "Same Targets",
                    KLEPExecutableKind.Router,
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { move }),
                enemy,
                move,
                MakeOneCycleKey(MoveTargetKeyId, "Attack Alias"),
                AttackRange),
            "Target Router requires distinct MoveTarget and AttackTarget IDs");
        ExpectThrows<ArgumentOutOfRangeException>(
            () => new KLEPEnemyTargetRouterExecutable(
                routerDefinition, enemy, move, attack, -1d),
            "Target Router rejects a negative Attack range");
        ExpectThrows<ArgumentOutOfRangeException>(
            () => new KLEPEnemyTargetRouterExecutable(
                routerDefinition, enemy, move, attack, double.NaN),
            "Target Router rejects a NaN Attack range");
        ExpectThrows<ArgumentOutOfRangeException>(
            () => new KLEPEnemyTargetRouterExecutable(
                routerDefinition, enemy, move, attack, double.PositiveInfinity),
            "Target Router rejects an infinite Attack range");
        KLEPKeyDefinition persistentMove = MakeKey(
            "key.move-persistent",
            "Persistent Move",
            KLEPKeyScope.Local,
            KLEPKeyLifetime.Persistent);
        ExpectThrows<ArgumentException>(
            () => new KLEPEnemyTargetRouterExecutable(
                new KLEPExecutableDefinition(
                    "router.persistent",
                    "Persistent",
                    KLEPExecutableKind.Router,
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { persistentMove }),
                enemy, persistentMove, attack, AttackRange),
            "Target Router rejects a Persistent target output");

        KLEPExecutableDefinition moveDefinition = MakeMoveDefinition(
            "move.shape", ground, move);
        var validMove = new KLEPZombieMoveExecutable(moveDefinition, move);
        Expect(validMove.Kind == KLEPExecutableKind.Action &&
               validMove.ExecutionMode == KLEPExecutionMode.Solo &&
               ReferenceEquals(validMove.MoveTargetDefinition, move),
            "The valid Move shape is explicit and inspectable");
        ExpectThrows<ArgumentNullException>(
            () => new KLEPZombieMoveExecutable(null, move),
            "Move rejects a null definition");
        ExpectThrows<ArgumentNullException>(
            () => new KLEPZombieMoveExecutable(moveDefinition, null),
            "Move rejects a null target definition");
        ExpectThrows<ArgumentException>(
            () => new KLEPZombieMoveExecutable(
                new KLEPExecutableDefinition(
                    "move.wrong-kind",
                    "Wrong Kind",
                    KLEPExecutableKind.Router,
                    executionMode: KLEPExecutionMode.Solo),
                move),
            "Move rejects the wrong Executable kind");
        ExpectThrows<ArgumentException>(
            () => new KLEPZombieMoveExecutable(
                new KLEPExecutableDefinition(
                    "move.wrong-mode",
                    "Wrong Mode",
                    KLEPExecutableKind.Action,
                    executionMode: KLEPExecutionMode.Tandem),
                move),
            "Move rejects Tandem mode");
        ExpectThrows<ArgumentException>(
            () => new KLEPZombieMoveExecutable(
                moveDefinition,
                MakeKey(
                    "key.move-global",
                    "Global Move",
                    KLEPKeyScope.Global,
                    KLEPKeyLifetime.OneCycle)),
            "Move rejects a Global target Key");

        KLEPExecutableDefinition attackDefinition = MakeAttackDefinition(
            "attack.shape", ground, attack);
        var validAttack = new KLEPZombieAttackExecutable(
            attackDefinition, attack);
        Expect(validAttack.Kind == KLEPExecutableKind.Action &&
               validAttack.ExecutionMode == KLEPExecutionMode.Solo &&
               ReferenceEquals(validAttack.AttackTargetDefinition, attack) &&
               validAttack.DeclaredOutputs.Count == 0,
            "The valid Attack shape is explicit and inspectable");
        ExpectThrows<ArgumentNullException>(
            () => new KLEPZombieAttackExecutable(null, attack),
            "Attack rejects a null definition");
        ExpectThrows<ArgumentNullException>(
            () => new KLEPZombieAttackExecutable(attackDefinition, null),
            "Attack rejects a null target definition");
        ExpectThrows<ArgumentException>(
            () => new KLEPZombieAttackExecutable(
                new KLEPExecutableDefinition(
                    "attack.wrong-kind",
                    "Wrong Kind",
                    KLEPExecutableKind.Sensor,
                    executionMode: KLEPExecutionMode.Solo),
                attack),
            "Attack rejects the wrong Executable kind");
        ExpectThrows<ArgumentException>(
            () => new KLEPZombieAttackExecutable(
                new KLEPExecutableDefinition(
                    "attack.wrong-mode",
                    "Wrong Mode",
                    KLEPExecutableKind.Action,
                    executionMode: KLEPExecutionMode.Tandem),
                attack),
            "Attack rejects Tandem mode");
        ExpectThrows<ArgumentException>(
            () => new KLEPZombieAttackExecutable(
                new KLEPExecutableDefinition(
                    "attack.declared-output",
                    "Attack With Impossible Key Promise",
                    KLEPExecutableKind.Action,
                    executionMode: KLEPExecutionMode.Solo,
                    declaredOutputs: new[] { move }),
                attack),
            "Attack rejects Key outputs that its host-effect success cannot emit");
        ExpectThrows<ArgumentException>(
            () => new KLEPZombieAttackExecutable(
                attackDefinition,
                MakeKey(
                    "key.attack-persistent",
                    "Persistent Attack",
                    KLEPKeyScope.Local,
                    KLEPKeyLifetime.Persistent)),
            "Attack rejects a Persistent target Key");
    }

    private static string RunDeterministicScenario(
        bool reverseRegistration,
        bool reverseInput)
    {
        var fixture = new ZombieFixture(
            "neuron.zombie.determinism",
            reverseRegistration);
        var observations = new List<KLEPEnemyObservation>
        {
            Observation("enemy.gamma", 7d),
            Observation("enemy.alpha", 4d),
            Observation("enemy.beta", 4d)
        };
        if (reverseInput)
        {
            observations.Reverse();
        }

        fixture.SetObservation(true, observations.ToArray());
        KLEPAgentTickTrace moving = fixture.Agent.Tick();
        fixture.SetObservation(true, Observation("enemy.alpha", 1d));
        KLEPAgentTickTrace attacking = fixture.Agent.Tick();
        return Serialize(moving.Decision) + "\n" + Serialize(attacking.Decision);
    }

    private static KLEPEnemyObservation Observation(
        string entityId,
        double distance,
        double positionX = 0d,
        double positionY = 0d,
        double positionZ = 0d,
        string teamId = "team.human")
    {
        return new KLEPEnemyObservation(
            entityId,
            teamId,
            distance,
            positionX,
            positionY,
            positionZ);
    }

    private static KLEPKeyDefinition MakeOneCycleKey(
        string stableId,
        string displayName)
    {
        return MakeKey(
            stableId,
            displayName,
            KLEPKeyScope.Local,
            KLEPKeyLifetime.OneCycle);
    }

    private static KLEPKeyDefinition MakeKey(
        string stableId,
        string displayName,
        KLEPKeyScope scope,
        KLEPKeyLifetime lifetime)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(stableId),
            displayName,
            scope: scope,
            defaultLifetime: lifetime);
    }

    private static KLEPExecutableDefinition MakeGroundSensorDefinition(
        KLEPKeyDefinition ground)
    {
        return new KLEPExecutableDefinition(
            GroundSensorId,
            "Ground Sensor",
            KLEPExecutableKind.Sensor,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { ground });
    }

    private static KLEPExecutableDefinition MakeEnemySensorDefinition(
        string stableId,
        KLEPKeyDefinition enemy)
    {
        return new KLEPExecutableDefinition(
            stableId,
            "Enemy Sensor",
            KLEPExecutableKind.Sensor,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { enemy });
    }

    private static KLEPExecutableDefinition MakeRouterDefinition(
        string stableId,
        KLEPKeyDefinition ground,
        KLEPKeyDefinition enemy,
        KLEPKeyDefinition declaredOutput,
        bool requireGround)
    {
        KLEPLockExpression expression = requireGround
            ? (KLEPLockExpression)new KLEPAll(
                new KLEPKeyPresent(ground.Id.Value),
                new KLEPKeyPresent(enemy.Id.Value))
            : new KLEPKeyPresent(enemy.Id.Value);
        return new KLEPExecutableDefinition(
            stableId,
            "Enemy Target Router",
            KLEPExecutableKind.Router,
            executionLocks: new[]
            {
                new KLEPLock(
                    stableId + ".lock.inputs",
                    "Required target-routing observations",
                    expression)
            },
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { declaredOutput });
    }

    private static KLEPExecutableDefinition MakeMoveDefinition(
        string stableId,
        KLEPKeyDefinition ground,
        KLEPKeyDefinition move)
    {
        return new KLEPExecutableDefinition(
            stableId,
            "Zombie Move",
            KLEPExecutableKind.Action,
            executionLocks: new[]
            {
                new KLEPLock(
                    stableId + ".lock",
                    "Ground and MoveTarget",
                    new KLEPAll(
                        new KLEPKeyPresent(ground.Id.Value),
                        new KLEPKeyPresent(move.Id.Value)))
            },
            baseAttractiveness: 1f,
            executionMode: KLEPExecutionMode.Solo);
    }

    private static KLEPExecutableDefinition MakeAttackDefinition(
        string stableId,
        KLEPKeyDefinition ground,
        KLEPKeyDefinition attack)
    {
        return new KLEPExecutableDefinition(
            stableId,
            "Zombie Attack",
            KLEPExecutableKind.Action,
            executionLocks: new[]
            {
                new KLEPLock(
                    stableId + ".lock",
                    "Ground and AttackTarget",
                    new KLEPAll(
                        new KLEPKeyPresent(ground.Id.Value),
                        new KLEPKeyPresent(attack.Id.Value)))
            },
            baseAttractiveness: 2f,
            executionMode: KLEPExecutionMode.Solo);
    }

    private static KLEPExecutableDefinition MakeRawEmitterDefinition(
        string stableId,
        KLEPKeyDefinition enemy)
    {
        return new KLEPExecutableDefinition(
            stableId,
            "Raw Enemy Emitter",
            KLEPExecutableKind.Sensor,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { enemy });
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

        throw new InvalidOperationException(
            $"Candidate '{stableId}' was not found in the decision trace.");
    }

    private static KLEPExecutableStepTrace FindStep(
        KLEPDecisionTrace trace,
        string stableId,
        KLEPExecutableStepKind kind)
    {
        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            if (step.ExecutableStableId == stableId && step.Kind == kind)
            {
                return step;
            }
        }

        return null;
    }

    private static KLEPKeyFact GetOnlyFact(
        KLEPKeySnapshot snapshot,
        KLEPKeyId keyId)
    {
        IReadOnlyList<KLEPKeyFact> facts = snapshot.FindAll(keyId);
        if (facts.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one '{keyId}' fact, but found {facts.Count}.");
        }

        return facts[0];
    }

    private static KLEPEnemyObservation ReadObservation(KLEPKeyFact fact)
    {
        return KLEPEnemyObservation.Read(fact.Payload);
    }

    private static IReadOnlyList<string> ReadEntityIds(
        KLEPKeySnapshot snapshot,
        KLEPKeyId keyId)
    {
        IReadOnlyList<KLEPKeyFact> facts = snapshot.FindAll(keyId);
        var ids = new List<string>(facts.Count);
        foreach (KLEPKeyFact fact in facts)
        {
            ids.Add(ReadObservation(fact).EntityId);
        }

        return ids;
    }

    private static string Join(IReadOnlyList<string> values)
    {
        var copy = new string[values.Count];
        for (int index = 0; index < values.Count; index++)
        {
            copy[index] = values[index];
        }

        return string.Join(",", copy);
    }

    private static string Serialize(KLEPDecisionTrace trace)
    {
        var text = new StringBuilder();
        text.Append("cycle=").Append(trace.CycleIndex)
            .Append("|selected=").Append(trace.SelectedExecutableId ?? "<none>")
            .Append("|current=").Append(trace.CurrentSoloExecutableId ?? "<none>")
            .Append("|patient=").Append(trace.IsPatient);
        AppendSnapshot(text, "initial", trace.InitialKeySnapshot);
        foreach (KLEPTandemWaveTrace wave in trace.TandemWaves)
        {
            text.Append("|wave=").Append(wave.WaveIndex)
                .Append(',').Append(wave.DidLocalStateChange)
                .Append(',').Append(wave.Termination);
            AppendCandidates(text, wave.Candidates);
            AppendExecutions(text, wave.Executions);
            AppendSnapshot(text, "wave-out", wave.OutputSnapshot);
        }

        AppendCandidates(text, trace.Candidates);
        AppendExecutions(text, trace.Executions);
        AppendSnapshot(text, "final", trace.KeySnapshot);
        return text.ToString();
    }

    private static void AppendSnapshot(
        StringBuilder text,
        string label,
        KLEPKeySnapshot snapshot)
    {
        text.Append('|').Append(label).Append('=')
            .Append(snapshot.Tick).Append(',').Append(snapshot.WaveIndex);
        foreach (KLEPKeyFact fact in snapshot.Facts)
        {
            text.Append(";fact:")
                .Append(fact.KeyId.Value).Append(',')
                .Append(fact.OccurrenceId.StoreId).Append(',')
                .Append(fact.OccurrenceId.Sequence).Append(',')
                .Append(fact.Lifetime).Append(',')
                .Append(fact.SourceId);
            AppendPayload(text, fact.Payload);
        }
    }

    private static void AppendPayload(
        StringBuilder text,
        KLEPKeyPayload payload)
    {
        if (payload == null)
        {
            text.Append(";payload:<none>");
            return;
        }

        foreach (KLEPKeyField field in payload.Fields)
        {
            text.Append(";field:")
                .Append(field.Name).Append(',')
                .Append(field.Value.Kind).Append(',')
                .Append(field.Value.ToString());
        }
    }

    private static void AppendCandidates(
        StringBuilder text,
        IReadOnlyList<CandidateEvaluation> candidates)
    {
        foreach (CandidateEvaluation candidate in candidates)
        {
            text.Append("|candidate:")
                .Append(candidate.StableId).Append(',')
                .Append(candidate.IsEligible).Append(',')
                .Append(candidate.Score.HasValue
                    ? candidate.Score.Value.ToString(
                        "R", CultureInfo.InvariantCulture)
                    : "<none>");
        }
    }

    private static void AppendExecutions(
        StringBuilder text,
        IReadOnlyList<KLEPExecutableStepTrace> steps)
    {
        foreach (KLEPExecutableStepTrace step in steps)
        {
            text.Append("|step:")
                .Append(step.Kind).Append(',')
                .Append(step.ExecutableStableId).Append(',')
                .Append(step.Result.RunIndex).Append(',')
                .Append(step.State).Append(',')
                .Append(step.ExitReason.HasValue
                    ? step.ExitReason.Value.ToString()
                    : "<none>");
            foreach (KLEPExecutableOutput output in step.Outputs)
            {
                text.Append(";output:")
                    .Append(output.Kind).Append(',')
                    .Append(output.KeyId.Value).Append(',')
                    .Append(output.SourceExecutableId);
                AppendPayload(text, output.Payload);
            }
        }
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

    private static void ExpectThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        assertions++;
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Assertion failed: {message}");
    }

    private static void ExpectThrowsMessage<TException>(
        Action action,
        string expectedFragment,
        string message)
        where TException : Exception
    {
        assertions++;
        try
        {
            action();
        }
        catch (TException exception)
        {
            if (exception.Message.IndexOf(
                    expectedFragment,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Assertion failed: {message}. Unexpected error: {exception.Message}");
        }

        throw new InvalidOperationException(
            $"Assertion failed: {message}");
    }

    private sealed class ZombieFixture
    {
        internal ZombieFixture(
            string neuronStableId,
            bool reverseRegistration = false)
        {
            Ground = MakeOneCycleKey(GroundKeyId, "Ground");
            EnemyDetected = MakeOneCycleKey(
                EnemyDetectedKeyId, "EnemyDetected");
            MoveTarget = MakeOneCycleKey(MoveTargetKeyId, "MoveTarget");
            AttackTarget = MakeOneCycleKey(
                AttackTargetKeyId, "AttackTarget");

            GroundSensor = new KLEPObservedKeySensorExecutable(
                MakeGroundSensorDefinition(Ground));
            EnemySensor = new KLEPEnemyObservationSensorExecutable(
                MakeEnemySensorDefinition(EnemySensorId, EnemyDetected),
                EnemyDetected);
            MoveRouter = new KLEPEnemyTargetRouterExecutable(
                MakeRouterDefinition(
                    RouterId,
                    Ground,
                    EnemyDetected,
                    MoveTarget,
                    true),
                EnemyDetected,
                MoveTarget,
                AttackTarget,
                AttackRange);
            AttackRouter = new KLEPEnemyTargetRouterExecutable(
                MakeRouterDefinition(
                    AttackRouterId,
                    Ground,
                    EnemyDetected,
                    AttackTarget,
                    true),
                EnemyDetected,
                MoveTarget,
                AttackTarget,
                AttackRange);
            Move = new KLEPZombieMoveExecutable(
                MakeMoveDefinition(MoveId, Ground, MoveTarget),
                MoveTarget);
            Attack = new KLEPZombieAttackExecutable(
                MakeAttackDefinition(AttackId, Ground, AttackTarget),
                AttackTarget);

            Neuron = new KLEPNeuron(neuronStableId);
            var executables = new List<KLEPExecutableBase>
            {
                GroundSensor,
                EnemySensor,
                MoveRouter,
                AttackRouter,
                Move,
                Attack
            };
            if (reverseRegistration)
            {
                executables.Reverse();
            }

            foreach (KLEPExecutableBase executable in executables)
            {
                Neuron.RegisterExecutable(executable);
            }

            Agent = new KLEPAgent(Neuron);
        }

        internal KLEPKeyDefinition Ground { get; }
        internal KLEPKeyDefinition EnemyDetected { get; }
        internal KLEPKeyDefinition MoveTarget { get; }
        internal KLEPKeyDefinition AttackTarget { get; }
        internal KLEPObservedKeySensorExecutable GroundSensor { get; }
        internal KLEPEnemyObservationSensorExecutable EnemySensor { get; }
        internal KLEPEnemyTargetRouterExecutable MoveRouter { get; }
        internal KLEPEnemyTargetRouterExecutable AttackRouter { get; }
        internal KLEPZombieMoveExecutable Move { get; }
        internal KLEPZombieAttackExecutable Attack { get; }
        internal KLEPNeuron Neuron { get; }
        internal KLEPAgent Agent { get; }

        internal void SetObservation(
            bool grounded,
            params KLEPEnemyObservation[] enemies)
        {
            GroundSensor.SetObservation(grounded);
            EnemySensor.SetObservations(
                enemies ?? Array.Empty<KLEPEnemyObservation>());
        }
    }

    private sealed class RawEnemyEmitter : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition definition;
        private readonly KLEPKeyPayload[] payloads;

        internal RawEnemyEmitter(
            KLEPExecutableDefinition executableDefinition,
            KLEPKeyDefinition definition,
            params KLEPKeyPayload[] payloads)
            : base(executableDefinition)
        {
            this.definition = definition;
            this.payloads = payloads;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            foreach (KLEPKeyPayload payload in payloads)
            {
                context.Add(definition, payload);
            }

            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
