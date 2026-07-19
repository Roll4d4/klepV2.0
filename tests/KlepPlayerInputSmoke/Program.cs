using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roll4d4.Klep.Behaviors;
using Roll4d4.Klep.Core;

internal static class Program
{
    private const string GroundId = "key.ground";
    private const string WId = "key.input.w";
    private const string AId = "key.input.a";
    private const string SId = "key.input.s";
    private const string DId = "key.input.d";
    private const string MouseAimId = "key.input.mouse-aim";

    private const string GroundSensorId = "sensor.ground";
    private const string WSensorId = "sensor.keyboard-movement.w";
    private const string ASensorId = "sensor.keyboard-movement.a";
    private const string SSensorId = "sensor.keyboard-movement.s";
    private const string DSensorId = "sensor.keyboard-movement.d";
    private const string MouseSensorId = "sensor.mouse-aim";
    private const string LocomotionId = "action.player-locomotion";

    private const double Epsilon = 0.000000000001d;
    private static int assertions;

    private static void Main()
    {
        VerifyKeyboardMovementMath();
        VerifyMouseAimPayload();
        VerifyKeyboardBranchesShareOneSample();
        VerifyNoObservationAndLocksLeavePatient();
        VerifyWProducesSameTickForwardIntent();
        VerifyCombinationsCancelAndNormalize();
        VerifyAimOnlyStillProducesRotationIntent();
        VerifyOneCycleInputsDoNotLeak();
        VerifyRegistrationOrderDeterminism();
        VerifyConstructorBoundaries();

        Console.WriteLine(
            $"KLEP player input smoke passed: {assertions} assertions.");
    }

    private static void VerifyKeyboardMovementMath()
    {
        KLEPKeyboardMovementInput none = KLEPKeyboardMovementInput.None;
        Expect(!none.W && !none.A && !none.S && !none.D,
            "None contains no pressed direction");
        Expect(!none.HasMovement, "None has no movement");
        ExpectNear(none.LocalX, 0d, "None has zero local X");
        ExpectNear(none.LocalZ, 0d, "None has zero local Z");

        VerifyAxes(true, false, false, false, 0d, 1d, "W");
        VerifyAxes(false, true, false, false, -1d, 0d, "A");
        VerifyAxes(false, false, true, false, 0d, -1d, "S");
        VerifyAxes(false, false, false, true, 1d, 0d, "D");

        double diagonal = 1d / Math.Sqrt(2d);
        VerifyAxes(true, false, false, true,
            diagonal, diagonal, "W+D");
        VerifyAxes(true, true, false, false,
            -diagonal, diagonal, "W+A");
        VerifyAxes(false, false, true, true,
            diagonal, -diagonal, "S+D");
        VerifyAxes(false, true, true, false,
            -diagonal, -diagonal, "S+A");

        VerifyAxes(true, false, true, false, 0d, 0d, "W+S");
        VerifyAxes(false, true, false, true, 0d, 0d, "A+D");
        VerifyAxes(true, true, true, true, 0d, 0d, "W+A+S+D");
        VerifyAxes(true, false, true, true, 1d, 0d, "W+S+D");
    }

    private static void VerifyMouseAimPayload()
    {
        var aim = new KLEPMouseAimObservation(12.5d, -2d, 40d);
        KLEPKeyPayload payload = aim.ToPayload();

        Expect(payload.Count == 3,
            "MouseAim payload contains exactly three coordinates");
        Expect(payload.Fields[0].Name == KLEPMouseAimObservation.WorldXField &&
               payload.Fields[1].Name == KLEPMouseAimObservation.WorldYField &&
               payload.Fields[2].Name == KLEPMouseAimObservation.WorldZField,
            "MouseAim payload fields are ordinally stable");
        Expect(payload.TryGetNumber(
                   KLEPMouseAimObservation.WorldXField,
                   out double worldX) && worldX == 12.5d,
            "MouseAim serializes world X");
        Expect(payload.TryGetNumber(
                   KLEPMouseAimObservation.WorldYField,
                   out double worldY) && worldY == -2d,
            "MouseAim serializes world Y");
        Expect(payload.TryGetNumber(
                   KLEPMouseAimObservation.WorldZField,
                   out double worldZ) && worldZ == 40d,
            "MouseAim serializes world Z");

        Expect(KLEPMouseAimObservation.TryRead(
                   payload,
                   out KLEPMouseAimObservation roundTrip),
            "MouseAim round trips through a Key payload");
        Expect(roundTrip.WorldX == aim.WorldX &&
               roundTrip.WorldY == aim.WorldY &&
               roundTrip.WorldZ == aim.WorldZ,
            "MouseAim round trip preserves every coordinate");
        Expect(ReferenceEquals(
                   KLEPMouseAimObservation.Read(payload).GetType(),
                   typeof(KLEPMouseAimObservation)),
            "MouseAim Read returns the immutable observation type");
        Expect(!KLEPMouseAimObservation.TryRead(
                   KLEPKeyPayload.Empty,
                   out _),
            "MouseAim rejects an empty payload");
        ExpectThrows<InvalidOperationException>(
            () => KLEPMouseAimObservation.Read(KLEPKeyPayload.Empty),
            "worldX",
            "MouseAim Read explains missing coordinates");
        ExpectThrows<ArgumentOutOfRangeException>(
            () => new KLEPMouseAimObservation(double.NaN, 0d, 0d),
            "finite",
            "MouseAim rejects NaN");
        ExpectThrows<ArgumentOutOfRangeException>(
            () => new KLEPMouseAimObservation(0d, double.PositiveInfinity, 0d),
            "finite",
            "MouseAim rejects infinity");
    }

    private static void VerifyKeyboardBranchesShareOneSample()
    {
        var fixture = new PlayerFixture("neuron.player.keyboard-branches");
        var sample = new KLEPKeyboardMovementInput(
            true,
            true,
            false,
            false);
        fixture.SetObservation(false, sample, null);

        Expect(ReferenceEquals(fixture.WSensor.Observation, sample) &&
               ReferenceEquals(fixture.ASensor.Observation, sample) &&
               ReferenceEquals(fixture.SSensor.Observation, sample) &&
               ReferenceEquals(fixture.DSensor.Observation, sample),
            "Four keyboard branches share the exact immutable host sample");
        Expect(fixture.WSensor.StableId == WSensorId &&
               fixture.ASensor.StableId == ASensorId &&
               fixture.SSensor.StableId == SSensorId &&
               fixture.DSensor.StableId == DSensorId,
            "Keyboard branches are four stable-ID-distinct Tandem roots");
        Expect(fixture.WSensor.Definition.DeclaredOutputs.Count == 1 &&
               fixture.ASensor.Definition.DeclaredOutputs.Count == 1 &&
               fixture.SSensor.Definition.DeclaredOutputs.Count == 1 &&
               fixture.DSensor.Definition.DeclaredOutputs.Count == 1 &&
               ReferenceEquals(fixture.WSensor.OutputDefinition, fixture.W) &&
               ReferenceEquals(fixture.ASensor.OutputDefinition, fixture.A) &&
               ReferenceEquals(fixture.SSensor.OutputDefinition, fixture.S) &&
               ReferenceEquals(fixture.DSensor.OutputDefinition, fixture.D),
            "Each keyboard branch declares and exposes exactly its inferred direction output");

        KLEPDecisionTrace decision = fixture.Agent.Tick().Decision;
        Expect(decision.KeySnapshot.Contains(WId) &&
               decision.KeySnapshot.Contains(AId) &&
               !decision.KeySnapshot.Contains(SId) &&
               !decision.KeySnapshot.Contains(DId),
            "Shared W+A sample publishes only the two pressed branch Keys");
        ExpectSensorTrace(decision, false, sample, hasAim: false);
    }

    private static void VerifyNoObservationAndLocksLeavePatient()
    {
        var fixture = new PlayerFixture("neuron.player.patient");
        fixture.SetObservation(
            grounded: false,
            KLEPKeyboardMovementInput.None,
            null);

        KLEPDecisionTrace empty = fixture.Agent.Tick().Decision;
        Expect(empty.IsPatient,
            "No external observation leaves the player patient");
        Expect(empty.SelectedExecutableId == null &&
               empty.CurrentSoloExecutableId == null,
            "Patient input selects and retains no Solo");
        Expect(empty.InitialKeySnapshot.Facts.Count == 0 &&
               empty.KeySnapshot.Facts.Count == 0,
            "Empty sensors publish no facts");
        ExpectSensorTrace(
            empty,
            grounded: false,
            KLEPKeyboardMovementInput.None,
            hasAim: false);
        Expect(FindCandidate(empty, LocomotionId).IsEligible == false,
            "Locomotion is filtered before scoring when both Locks are absent");
        Expect(FindStep(empty, LocomotionId, KLEPExecutableStepKind.Solo) == null,
            "Ineligible locomotion does not advance");

        fixture.SetObservation(
            grounded: true,
            new KLEPKeyboardMovementInput(true, false, false, false),
            null);
        KLEPDecisionTrace noAim = fixture.Agent.Tick().Decision;
        Expect(noAim.IsPatient,
            "Ground and W without MouseAim remain patient");
        Expect(noAim.KeySnapshot.Contains(GroundId) &&
               noAim.KeySnapshot.Contains(WId) &&
               !noAim.KeySnapshot.Contains(MouseAimId),
            "The blocked Tick still exposes its Ground and W observations");
        ExpectSensorTrace(
            noAim,
            grounded: true,
            new KLEPKeyboardMovementInput(true, false, false, false),
            hasAim: false);
        Expect(!FindCandidate(noAim, LocomotionId).IsEligible,
            "Missing MouseAim closes the combined locomotion Lock");

        var aim = new KLEPMouseAimObservation(5d, 0d, 2d);
        fixture.SetObservation(
            grounded: false,
            new KLEPKeyboardMovementInput(true, false, false, false),
            aim);
        KLEPDecisionTrace noGround = fixture.Agent.Tick().Decision;
        Expect(noGround.IsPatient,
            "W and MouseAim without Ground remain patient");
        Expect(noGround.KeySnapshot.Contains(WId) &&
               noGround.KeySnapshot.Contains(MouseAimId) &&
               !noGround.KeySnapshot.Contains(GroundId),
            "The blocked Tick preserves W and MouseAim facts");
        ExpectSensorTrace(
            noGround,
            grounded: false,
            new KLEPKeyboardMovementInput(true, false, false, false),
            hasAim: true);
        Expect(!FindCandidate(noGround, LocomotionId).IsEligible,
            "Missing Ground closes the combined locomotion Lock");
        Expect(!fixture.Locomotion.TryGetIntent(
                   noGround.CycleIndex,
                   out _,
                   out _),
            "A blocked Tick exposes no locomotion intent");
    }

    private static void VerifyWProducesSameTickForwardIntent()
    {
        var fixture = new PlayerFixture("neuron.player.forward");
        var aim = new KLEPMouseAimObservation(8d, 0.25d, -3d);
        fixture.SetObservation(
            grounded: true,
            new KLEPKeyboardMovementInput(true, false, false, false),
            aim);

        KLEPDecisionTrace decision = fixture.Agent.Tick().Decision;
        Expect(decision.InitialKeySnapshot.Facts.Count == 0,
            "The first input Tick begins before sensor publication");
        Expect(decision.KeySnapshot.Contains(GroundId) &&
               decision.KeySnapshot.Contains(WId) &&
               decision.KeySnapshot.Contains(MouseAimId),
            "Ground, W, and MouseAim settle within one Tick");
        Expect(!decision.KeySnapshot.Contains(AId) &&
               !decision.KeySnapshot.Contains(SId) &&
               !decision.KeySnapshot.Contains(DId),
            "W input does not fabricate other direction facts");
        Expect(decision.SelectedExecutableId == LocomotionId &&
               decision.CurrentSoloExecutableId == LocomotionId &&
               !decision.IsPatient,
            "W selects the Running locomotion Solo in the same Tick");
        Expect(FindCandidate(decision, LocomotionId).IsEligible,
            "Ground plus MouseAim makes locomotion eligible");

        KLEPExecutableStepTrace step = FindStep(
            decision,
            LocomotionId,
            KLEPExecutableStepKind.Solo);
        Expect(step != null && step.State == KLEPExecutableState.Running,
            "Locomotion enters Running exactly once");
        Expect(fixture.Locomotion.TryGetIntent(
                   decision.CycleIndex,
                   out KLEPKeyboardMovementInput input,
                   out KLEPMouseAimObservation readAim),
            "Locomotion exposes a cycle-bound combined intent");
        ExpectNear(input.LocalX, 0d,
            "W intent has no local-right movement");
        ExpectNear(input.LocalZ, 1d,
            "W intent moves actor-local forward");
        Expect(input.W && !input.A && !input.S && !input.D,
            "W intent preserves the individual direction presence");
        Expect(readAim.WorldX == aim.WorldX &&
               readAim.WorldY == aim.WorldY &&
               readAim.WorldZ == aim.WorldZ,
            "The combined intent preserves the mouse world point");
        Expect(!fixture.Locomotion.TryGetIntent(
                   decision.CycleIndex - 1,
                   out _,
                   out _),
            "A locomotion intent cannot be consumed for another Tick");

        Expect(GetOnlyFact(decision.KeySnapshot, fixture.W.Id).SourceId ==
               WSensorId,
            "W fact retains its dedicated keyboard-branch provenance");
        Expect(GetOnlyFact(decision.KeySnapshot, fixture.MouseAim.Id).SourceId ==
               MouseSensorId,
            "MouseAim fact retains mouse-sensor provenance");
        Expect(GetOnlyFact(decision.KeySnapshot, fixture.Ground.Id).SourceId ==
               GroundSensorId,
            "Ground fact retains ground-sensor provenance");
        Expect(decision.TandemWaves.Count >= 1 &&
               decision.TandemWaves[0].DidLocalStateChange,
            "The trace exposes the sensor publication barrier");
        ExpectSensorTrace(
            decision,
            grounded: true,
            new KLEPKeyboardMovementInput(true, false, false, false),
            hasAim: true);
    }

    private static void VerifyCombinationsCancelAndNormalize()
    {
        var fixture = new PlayerFixture("neuron.player.combinations");
        var aim = new KLEPMouseAimObservation(3d, 0d, 9d);

        KLEPKeyboardMovementInput diagonal = TickForInput(
            fixture,
            new KLEPKeyboardMovementInput(true, false, false, true),
            aim,
            out KLEPDecisionTrace diagonalTrace);
        double expected = 1d / Math.Sqrt(2d);
        ExpectNear(diagonal.LocalX, expected,
            "W+D normalizes local X");
        ExpectNear(diagonal.LocalZ, expected,
            "W+D normalizes local Z");
        ExpectNear(
            Math.Sqrt(
                diagonal.LocalX * diagonal.LocalX +
                diagonal.LocalZ * diagonal.LocalZ),
            1d,
            "W+D has unit movement magnitude");
        Expect(diagonalTrace.KeySnapshot.Contains(WId) &&
               diagonalTrace.KeySnapshot.Contains(DId) &&
               !diagonalTrace.KeySnapshot.Contains(AId) &&
               !diagonalTrace.KeySnapshot.Contains(SId),
            "W+D publishes exactly those two presence Keys");

        KLEPKeyboardMovementInput opposed = TickForInput(
            fixture,
            new KLEPKeyboardMovementInput(true, false, true, false),
            aim,
            out KLEPDecisionTrace opposedTrace);
        ExpectNear(opposed.LocalX, 0d,
            "W+S has zero local X");
        ExpectNear(opposed.LocalZ, 0d,
            "W+S cancels local forward movement");
        Expect(!opposed.HasMovement,
            "Opposed W+S produces a rotation-only intent");
        Expect(opposedTrace.KeySnapshot.Contains(WId) &&
               opposedTrace.KeySnapshot.Contains(SId),
            "Cancellation is calculated from two truthful input Keys");

        KLEPKeyboardMovementInput horizontalOpposed = TickForInput(
            fixture,
            new KLEPKeyboardMovementInput(false, true, false, true),
            aim,
            out KLEPDecisionTrace horizontalTrace);
        ExpectNear(horizontalOpposed.LocalX, 0d,
            "A+D cancels local-right movement");
        ExpectNear(horizontalOpposed.LocalZ, 0d,
            "A+D has zero local forward movement");
        Expect(horizontalTrace.KeySnapshot.Contains(AId) &&
               horizontalTrace.KeySnapshot.Contains(DId),
            "A+D remains represented by individual presence Keys");

        KLEPKeyboardMovementInput residual = TickForInput(
            fixture,
            new KLEPKeyboardMovementInput(true, false, true, true),
            aim,
            out _);
        ExpectNear(residual.LocalX, 1d,
            "W+S+D cancels forward and retains right");
        ExpectNear(residual.LocalZ, 0d,
            "W+S+D has no residual forward component");
    }

    private static void VerifyAimOnlyStillProducesRotationIntent()
    {
        var fixture = new PlayerFixture("neuron.player.aim-only");
        var aim = new KLEPMouseAimObservation(-7d, 1d, 14d);
        fixture.SetObservation(true, KLEPKeyboardMovementInput.None, aim);

        KLEPDecisionTrace decision = fixture.Agent.Tick().Decision;
        Expect(!decision.IsPatient &&
               decision.CurrentSoloExecutableId == LocomotionId,
            "Ground plus MouseAim runs locomotion without keyboard movement");
        Expect(decision.KeySnapshot.Contains(GroundId) &&
               decision.KeySnapshot.Contains(MouseAimId),
            "Aim-only Tick contains its two required Lock facts");
        Expect(!decision.KeySnapshot.Contains(WId) &&
               !decision.KeySnapshot.Contains(AId) &&
               !decision.KeySnapshot.Contains(SId) &&
               !decision.KeySnapshot.Contains(DId),
            "Aim-only Tick contains no direction facts");
        Expect(fixture.Locomotion.TryGetIntent(
                   decision.CycleIndex,
                   out KLEPKeyboardMovementInput input,
                   out KLEPMouseAimObservation readAim),
            "Aim-only Tick exposes a combined effect intent");
        Expect(!input.HasMovement &&
               input.LocalX == 0d &&
               input.LocalZ == 0d,
            "Aim-only intent requests no translation");
        Expect(readAim.WorldX == -7d &&
               readAim.WorldY == 1d &&
               readAim.WorldZ == 14d,
            "Aim-only intent still requests the exact rotation target");
        ExpectSensorTrace(
            decision,
            grounded: true,
            KLEPKeyboardMovementInput.None,
            hasAim: true);
    }

    private static void VerifyOneCycleInputsDoNotLeak()
    {
        var fixture = new PlayerFixture("neuron.player.expiry");
        var firstAim = new KLEPMouseAimObservation(1d, 0d, 2d);
        fixture.SetObservation(
            true,
            new KLEPKeyboardMovementInput(true, false, false, false),
            firstAim);
        KLEPDecisionTrace first = fixture.Agent.Tick().Decision;
        ExpectSensorTrace(
            first,
            grounded: true,
            new KLEPKeyboardMovementInput(true, false, false, false),
            hasAim: true);
        KLEPKeyFact firstW = GetOnlyFact(first.KeySnapshot, fixture.W.Id);
        KLEPKeyFact firstAimFact =
            GetOnlyFact(first.KeySnapshot, fixture.MouseAim.Id);

        var secondAim = new KLEPMouseAimObservation(9d, 0d, -4d);
        fixture.SetObservation(
            true,
            new KLEPKeyboardMovementInput(false, true, false, false),
            secondAim);
        KLEPDecisionTrace second = fixture.Agent.Tick().Decision;
        ExpectSensorTrace(
            second,
            grounded: true,
            new KLEPKeyboardMovementInput(false, true, false, false),
            hasAim: true);
        Expect(!second.KeySnapshot.Contains(WId) &&
               second.KeySnapshot.Contains(AId),
            "A later A sample does not leak the prior W fact");
        Expect(GetOnlyFact(second.KeySnapshot, fixture.MouseAim.Id)
                   .OccurrenceId != firstAimFact.OccurrenceId,
            "A repeated MouseAim observation is a fresh OneCycle occurrence");
        Expect(second.KeySnapshot.FindAll(fixture.W.Id).Count == 0 &&
               firstW.OccurrenceId != default,
            "The prior W occurrence is absent from the later snapshot");
        Expect(fixture.Locomotion.TryGetIntent(
                   second.CycleIndex,
                   out KLEPKeyboardMovementInput secondInput,
                   out KLEPMouseAimObservation readSecondAim) &&
               secondInput.A && !secondInput.W &&
               readSecondAim.WorldX == secondAim.WorldX,
            "The later intent reads only the latest immutable observations");

        fixture.SetObservation(
            true,
            new KLEPKeyboardMovementInput(false, false, false, true),
            null);
        KLEPDecisionTrace lostAim = fixture.Agent.Tick().Decision;
        ExpectSensorTrace(
            lostAim,
            grounded: true,
            new KLEPKeyboardMovementInput(false, false, false, true),
            hasAim: false);
        Expect(lostAim.IsPatient &&
               lostAim.CurrentSoloExecutableId == null,
            "Losing MouseAim cancels locomotion and returns to patient");
        Expect(!lostAim.KeySnapshot.Contains(MouseAimId) &&
               lostAim.KeySnapshot.Contains(DId),
            "Expired MouseAim cannot leak while the current D fact remains truthful");
        KLEPExecutableStepTrace cancellation = FindStep(
            lostAim,
            LocomotionId,
            KLEPExecutableStepKind.Cancellation);
        Expect(cancellation != null &&
               cancellation.ExitReason == KLEPExecutableExitReason.LocksClosed,
            "MouseAim loss cancels the Solo specifically through its Lock");
        Expect(!fixture.Locomotion.TryGetIntent(
                   lostAim.CycleIndex,
                   out _,
                   out _),
            "A previous locomotion intent is not valid in the cancelled Tick");
    }

    private static void VerifyRegistrationOrderDeterminism()
    {
        var normal = new PlayerFixture(
            "neuron.player.order",
            reverseRegistration: false);
        var reversed = new PlayerFixture(
            "neuron.player.order",
            reverseRegistration: true);
        var input = new KLEPKeyboardMovementInput(true, true, false, false);
        var aim = new KLEPMouseAimObservation(13d, 0d, 17d);
        normal.SetObservation(true, input, aim);
        reversed.SetObservation(true, input, aim);

        KLEPDecisionTrace left = normal.Agent.Tick().Decision;
        KLEPDecisionTrace right = reversed.Agent.Tick().Decision;
        ExpectSensorTrace(left, true, input, hasAim: true);
        ExpectSensorTrace(right, true, input, hasAim: true);
        Expect(SerializeDecision(left) == SerializeDecision(right),
            "Registration order cannot change the input decision trace");
        bool hasLeftIntent = normal.Locomotion.TryGetIntent(
            left.CycleIndex,
            out KLEPKeyboardMovementInput leftInput,
            out KLEPMouseAimObservation leftAim);
        bool hasRightIntent = reversed.Locomotion.TryGetIntent(
            right.CycleIndex,
            out KLEPKeyboardMovementInput rightInput,
            out KLEPMouseAimObservation rightAim);
        Expect(hasLeftIntent && hasRightIntent,
            "Both registration orders expose their current intent");
        ExpectNear(leftInput.LocalX, rightInput.LocalX,
            "Registration order preserves local X");
        ExpectNear(leftInput.LocalZ, rightInput.LocalZ,
            "Registration order preserves local Z");
        Expect(leftAim.WorldX == rightAim.WorldX &&
               leftAim.WorldY == rightAim.WorldY &&
               leftAim.WorldZ == rightAim.WorldZ,
            "Registration order preserves MouseAim data");
    }

    private static void VerifyConstructorBoundaries()
    {
        KLEPKeyDefinition w = MakeOneCycleKey(WId, "W");
        KLEPKeyDefinition a = MakeOneCycleKey(AId, "A");
        KLEPKeyDefinition s = MakeOneCycleKey(SId, "S");
        KLEPKeyDefinition d = MakeOneCycleKey(DId, "D");
        KLEPKeyDefinition aim = MakeOneCycleKey(MouseAimId, "MouseAim");

        ExpectThrows<ArgumentException>(
            () => new KLEPKeyboardMovementSensorExecutable(
                new KLEPExecutableDefinition(
                    "bad.keyboard.mode",
                    "Bad keyboard mode",
                    KLEPExecutableKind.Sensor,
                    executionMode: KLEPExecutionMode.Solo,
                    declaredOutputs: new[] { w }),
                w, a, s, d),
            "Tandem",
            "Keyboard sensor rejects Solo mode");
        ExpectThrows<ArgumentException>(
            () => new KLEPKeyboardMovementSensorExecutable(
                new KLEPExecutableDefinition(
                    "bad.keyboard.count",
                    "Bad keyboard count",
                    KLEPExecutableKind.Sensor,
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { w, a }),
                w, a, s, d),
            "exactly one",
            "A keyboard sensor branch requires one guaranteed output");

        KLEPKeyDefinition persistentW = new KLEPKeyDefinition(
            new KLEPKeyId("key.input.persistent-w"),
            "Persistent W",
            defaultLifetime: KLEPKeyLifetime.Persistent);
        ExpectThrows<ArgumentException>(
            () => new KLEPKeyboardMovementSensorExecutable(
                new KLEPExecutableDefinition(
                    "bad.keyboard.lifetime",
                    "Bad keyboard lifetime",
                    KLEPExecutableKind.Sensor,
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { persistentW }),
                persistentW, a, s, d),
            "OneCycle",
            "Keyboard sensor rejects a Persistent direction Key");

        KLEPKeyDefinition wAlias = MakeOneCycleKey(WId, "W alias");
        ExpectThrows<ArgumentException>(
            () => new KLEPKeyboardMovementSensorExecutable(
                MakeKeyboardDefinition(
                    "bad.keyboard.alias",
                    "Bad keyboard alias",
                    w),
                wAlias, a, s, d),
            "supplied W, A, S, or D",
            "Keyboard sensor rejects a same-ID but different definition object");

        ExpectThrows<ArgumentException>(
            () => new KLEPMouseAimSensorExecutable(
                new KLEPExecutableDefinition(
                    "bad.mouse.kind",
                    "Bad mouse kind",
                    KLEPExecutableKind.Action,
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { aim }),
                aim),
            "Sensor",
            "MouseAim sensor rejects Action kind");

        ExpectThrows<ArgumentException>(
            () => new KLEPPlayerLocomotionExecutable(
                new KLEPExecutableDefinition(
                    "bad.locomotion.mode",
                    "Bad locomotion mode",
                    KLEPExecutableKind.Action,
                    executionMode: KLEPExecutionMode.Tandem),
                w, a, s, d, aim),
            "Solo",
            "Player locomotion rejects Tandem mode");

        KLEPKeyDefinition globalAim = new KLEPKeyDefinition(
            new KLEPKeyId("key.input.global-aim"),
            "Global aim",
            scope: KLEPKeyScope.Global,
            defaultLifetime: KLEPKeyLifetime.OneCycle);
        ExpectThrows<ArgumentException>(
            () => new KLEPPlayerLocomotionExecutable(
                MakeLocomotionDefinition(
                    MakeOneCycleKey(GroundId, "Ground"),
                    globalAim),
                w, a, s, d, globalAim),
            "Local",
            "Player locomotion rejects a Global MouseAim Key");

        ExpectThrows<ArgumentNullException>(
            () => new KLEPKeyboardMovementSensorExecutable(
                    MakeKeyboardDefinition(
                        "keyboard.null-observation",
                        "Keyboard null observation",
                        w),
                    w, a, s, d)
                .SetObservation(null),
            "observation",
            "Keyboard sensor rejects a null immutable sample");
    }

    private static KLEPKeyboardMovementInput TickForInput(
        PlayerFixture fixture,
        KLEPKeyboardMovementInput input,
        KLEPMouseAimObservation aim,
        out KLEPDecisionTrace decision)
    {
        fixture.SetObservation(true, input, aim);
        decision = fixture.Agent.Tick().Decision;
        ExpectSensorTrace(decision, true, input, aim != null);
        Expect(fixture.Locomotion.TryGetIntent(
                   decision.CycleIndex,
                   out KLEPKeyboardMovementInput intent,
                   out _),
            "A grounded aimed input produces a locomotion intent");
        return intent;
    }

    private static void VerifyAxes(
        bool w,
        bool a,
        bool s,
        bool d,
        double expectedX,
        double expectedZ,
        string label)
    {
        var input = new KLEPKeyboardMovementInput(w, a, s, d);
        Expect(input.W == w && input.A == a && input.S == s && input.D == d,
            label + " preserves button state");
        ExpectNear(input.LocalX, expectedX, label + " local X");
        ExpectNear(input.LocalZ, expectedZ, label + " local Z");
        Expect(input.HasMovement == (expectedX != 0d || expectedZ != 0d),
            label + " movement presence matches its normalized vector");
    }

    private static KLEPKeyDefinition MakeOneCycleKey(
        string stableId,
        string displayName)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(stableId),
            displayName,
            scope: KLEPKeyScope.Local,
            defaultLifetime: KLEPKeyLifetime.OneCycle);
    }

    private static KLEPExecutableDefinition MakeGroundDefinition(
        KLEPKeyDefinition ground)
    {
        return new KLEPExecutableDefinition(
            GroundSensorId,
            "Ground Sensor",
            KLEPExecutableKind.Sensor,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { ground });
    }

    private static KLEPExecutableDefinition MakeKeyboardDefinition(
        string stableId,
        string displayName,
        KLEPKeyDefinition output)
    {
        return new KLEPExecutableDefinition(
            stableId,
            displayName,
            KLEPExecutableKind.Sensor,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { output });
    }

    private static KLEPExecutableDefinition MakeMouseDefinition(
        KLEPKeyDefinition mouseAim)
    {
        return new KLEPExecutableDefinition(
            MouseSensorId,
            "Mouse Aim Sensor",
            KLEPExecutableKind.Sensor,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { mouseAim });
    }

    private static KLEPExecutableDefinition MakeLocomotionDefinition(
        KLEPKeyDefinition ground,
        KLEPKeyDefinition mouseAim)
    {
        return new KLEPExecutableDefinition(
            LocomotionId,
            "Player Locomotion",
            KLEPExecutableKind.Action,
            executionLocks: new[]
            {
                new KLEPLock(
                    LocomotionId + ".lock",
                    "Ground and MouseAim",
                    new KLEPAll(
                        new KLEPKeyPresent(ground.Id.Value),
                        new KLEPKeyPresent(mouseAim.Id.Value)))
            },
            baseAttractiveness: 1f,
            executionMode: KLEPExecutionMode.Solo);
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
            $"Candidate '{stableId}' was not found.");
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

    private static void ExpectSensorTrace(
        KLEPDecisionTrace trace,
        bool grounded,
        KLEPKeyboardMovementInput keyboard,
        bool hasAim)
    {
        if (keyboard == null)
        {
            throw new ArgumentNullException(nameof(keyboard));
        }

        int sensorStepCount = 0;
        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            if (step.Kind == KLEPExecutableStepKind.Tandem &&
                IsInputSensorId(step.ExecutableStableId))
            {
                sensorStepCount++;
            }
        }

        Expect(sensorStepCount == 6,
            "Each input Tick traces Ground, four keyboard branches, and MouseAim exactly once");
        ExpectSensorStep(trace, GroundSensorId, grounded);
        ExpectSensorStep(trace, WSensorId, keyboard.W);
        ExpectSensorStep(trace, ASensorId, keyboard.A);
        ExpectSensorStep(trace, SSensorId, keyboard.S);
        ExpectSensorStep(trace, DSensorId, keyboard.D);
        ExpectSensorStep(trace, MouseSensorId, hasAim);
    }

    private static void ExpectSensorStep(
        KLEPDecisionTrace trace,
        string stableId,
        bool shouldSucceed)
    {
        KLEPExecutableStepTrace step = FindStep(
            trace,
            stableId,
            KLEPExecutableStepKind.Tandem);
        KLEPExecutableState expectedState = shouldSucceed
            ? KLEPExecutableState.Succeeded
            : KLEPExecutableState.Failed;
        int expectedOutputs = shouldSucceed ? 1 : 0;
        Expect(step != null &&
               step.State == expectedState &&
               step.Outputs.Count == expectedOutputs,
            shouldSucceed
                ? $"Input sensor '{stableId}' succeeds with its one guaranteed output"
                : $"Input sensor '{stableId}' fails without output when its observation is absent");
    }

    private static bool IsInputSensorId(string stableId)
    {
        return stableId == GroundSensorId ||
               stableId == WSensorId ||
               stableId == ASensorId ||
               stableId == SSensorId ||
               stableId == DSensorId ||
               stableId == MouseSensorId;
    }

    private static KLEPKeyFact GetOnlyFact(
        KLEPKeySnapshot snapshot,
        KLEPKeyId keyId)
    {
        IReadOnlyList<KLEPKeyFact> facts = snapshot.FindAll(keyId);
        if (facts.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one '{keyId}' fact, found {facts.Count}.");
        }

        return facts[0];
    }

    private static string SerializeDecision(KLEPDecisionTrace trace)
    {
        var builder = new StringBuilder();
        builder.Append(trace.CycleIndex)
            .Append('|').Append(trace.SelectedExecutableId)
            .Append('|').Append(trace.CurrentSoloExecutableId)
            .Append('|').Append(trace.IsPatient);
        foreach (KLEPKeyFact fact in trace.KeySnapshot.Facts)
        {
            builder.Append("|K:").Append(fact.KeyId.Value)
                .Append(':').Append(fact.SourceId);
            foreach (KLEPKeyField field in fact.Payload.Fields)
            {
                builder.Append(':').Append(field.Name)
                    .Append('=').Append(field.Value.ToString());
            }
        }

        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            builder.Append("|E:").Append(step.ExecutableStableId)
                .Append(':').Append(step.Kind)
                .Append(':').Append(step.State)
                .Append(':').Append(step.ExitReason);
        }

        return builder.ToString();
    }

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException(
                "Assertion failed: " + message);
        }
    }

    private static void ExpectNear(
        double actual,
        double expected,
        string message)
    {
        assertions++;
        if (Math.Abs(actual - expected) > Epsilon)
        {
            throw new InvalidOperationException(
                $"Assertion failed: {message}. Expected " +
                $"{expected.ToString("R", CultureInfo.InvariantCulture)}, got " +
                $"{actual.ToString("R", CultureInfo.InvariantCulture)}.");
        }
    }

    private static void ExpectThrows<TException>(
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
                $"Assertion failed: {message}. Unexpected error: " +
                exception.Message);
        }

        throw new InvalidOperationException("Assertion failed: " + message);
    }

    private sealed class PlayerFixture
    {
        internal PlayerFixture(
            string neuronStableId,
            bool reverseRegistration = false)
        {
            Ground = MakeOneCycleKey(GroundId, "Ground");
            W = MakeOneCycleKey(WId, "W");
            A = MakeOneCycleKey(AId, "A");
            S = MakeOneCycleKey(SId, "S");
            D = MakeOneCycleKey(DId, "D");
            MouseAim = MakeOneCycleKey(MouseAimId, "MouseAim");

            GroundSensor = new KLEPObservedKeySensorExecutable(
                MakeGroundDefinition(Ground));
            WSensor = new KLEPKeyboardMovementSensorExecutable(
                MakeKeyboardDefinition(WSensorId, "W Keyboard Sensor", W),
                W, A, S, D);
            ASensor = new KLEPKeyboardMovementSensorExecutable(
                MakeKeyboardDefinition(ASensorId, "A Keyboard Sensor", A),
                W, A, S, D);
            SSensor = new KLEPKeyboardMovementSensorExecutable(
                MakeKeyboardDefinition(SSensorId, "S Keyboard Sensor", S),
                W, A, S, D);
            DSensor = new KLEPKeyboardMovementSensorExecutable(
                MakeKeyboardDefinition(DSensorId, "D Keyboard Sensor", D),
                W, A, S, D);
            MouseSensor = new KLEPMouseAimSensorExecutable(
                MakeMouseDefinition(MouseAim),
                MouseAim);
            Locomotion = new KLEPPlayerLocomotionExecutable(
                MakeLocomotionDefinition(Ground, MouseAim),
                W, A, S, D, MouseAim);

            Neuron = new KLEPNeuron(neuronStableId);
            var executables = new List<KLEPExecutableBase>
            {
                GroundSensor,
                WSensor,
                ASensor,
                SSensor,
                DSensor,
                MouseSensor,
                Locomotion
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
        internal KLEPKeyDefinition W { get; }
        internal KLEPKeyDefinition A { get; }
        internal KLEPKeyDefinition S { get; }
        internal KLEPKeyDefinition D { get; }
        internal KLEPKeyDefinition MouseAim { get; }
        internal KLEPObservedKeySensorExecutable GroundSensor { get; }
        internal KLEPKeyboardMovementSensorExecutable WSensor { get; }
        internal KLEPKeyboardMovementSensorExecutable ASensor { get; }
        internal KLEPKeyboardMovementSensorExecutable SSensor { get; }
        internal KLEPKeyboardMovementSensorExecutable DSensor { get; }
        internal KLEPMouseAimSensorExecutable MouseSensor { get; }
        internal KLEPPlayerLocomotionExecutable Locomotion { get; }
        internal KLEPNeuron Neuron { get; }
        internal KLEPAgent Agent { get; }

        internal void SetObservation(
            bool grounded,
            KLEPKeyboardMovementInput keyboard,
            KLEPMouseAimObservation aim)
        {
            GroundSensor.SetObservation(grounded);
            WSensor.SetObservation(keyboard);
            ASensor.SetObservation(keyboard);
            SSensor.SetObservation(keyboard);
            DSensor.SetObservation(keyboard);
            MouseSensor.SetObservation(aim);
        }
    }
}
