using System;
using System.Collections.Generic;
using System.Text;
using Roll4d4.Klep.Behaviors;
using Roll4d4.Klep.Core;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        VerifyClosedPayloadContracts();
        VerifyObservationAndRoutes();
        VerifyFireIntentAllowsFriendlyEvidence();
        VerifyReloadIntentTruth();
        VerifyDefinitionValidation();
        VerifyRegistrationOrderDeterminism();

        Console.WriteLine(
            $"KLEP Weapon smoke passed: {assertions} assertions.");
    }

    private static void VerifyClosedPayloadContracts()
    {
        KLEPWeaponObservation hostile = Observation(
            rounds: 3,
            hitKind: KLEPWeaponLineHitKind.Entity,
            hitEntityId: "entity.hostile",
            hitTeamId: "team.zombie",
            hitDistance: 7d);
        KLEPKeyPayload payload = hostile.ToPayload();
        KLEPWeaponObservation copy = KLEPWeaponObservation.Read(payload);
        Expect(payload.Count == KLEPWeaponObservation.PayloadFieldCount &&
               copy.WeaponId == hostile.WeaponId &&
               copy.ObservedWorldTick == hostile.ObservedWorldTick &&
               copy.HitKind == KLEPWeaponLineHitKind.Entity &&
               copy.HitEntityId == "entity.hostile" &&
               copy.HasSafeLine &&
               !copy.HasFriendlyEntityHit,
            "WeaponObservation round-trips its exact closed hostile-line schema");

        KLEPWeaponFireIntent fire = new KLEPWeaponFireIntent(hostile);
        KLEPWeaponFireIntent fireCopy =
            KLEPWeaponFireIntent.Read(fire.ToPayload());
        Expect(fire.ToPayload().Count ==
                   KLEPWeaponObservation.PayloadFieldCount &&
               fireCopy.WeaponId == hostile.WeaponId &&
               fireCopy.Observation.HitDistance == 7d,
            "WeaponFireIntent freezes the complete deciding observation");

        KLEPWeaponReloadIntent reload =
            new KLEPWeaponReloadIntent(hostile);
        KLEPWeaponReloadIntent reloadCopy =
            KLEPWeaponReloadIntent.Read(reload.ToPayload());
        Expect(reload.ToPayload().Count ==
                   KLEPWeaponReloadIntent.PayloadFieldCount &&
               reloadCopy.MagazineRounds == 3 &&
               reloadCopy.ReserveRounds == 12 &&
               reloadCopy.CanReload,
            "WeaponReloadIntent round-trips its exact ammunition snapshot");

        KLEPKeyPayload extraField = payload.Merge(new KLEPKeyPayload(new[]
        {
            new KeyValuePair<string, KLEPKeyValue>("undeclared", true)
        }));
        Expect(!KLEPWeaponObservation.TryRead(extraField, out _),
            "WeaponObservation rejects an extra field instead of accepting an open payload");
        Expect(!KLEPWeaponFireIntent.TryRead(payload, out _),
            "FireIntent rejects an Observation schema even when all data fields match");
        ExpectThrows<ArgumentException>(
            () => new KLEPWeaponObservation(
                "weapon.test",
                "entity.shooter",
                "team.human",
                1,
                6,
                2,
                0,
                0d,
                0d,
                0d,
                0d,
                0d,
                1d,
                20d,
                KLEPWeaponLineHitKind.None,
                "entity.impossible",
                "team.zombie",
                20d),
            "No-hit observations reject manufactured entity identity");
        ExpectThrows<ArgumentException>(
            () => new KLEPWeaponObservation(
                "weapon.test",
                "entity.shooter",
                "team.human",
                1,
                6,
                2,
                0,
                0d,
                0d,
                0d,
                0d,
                0d,
                0.5d,
                20d,
                KLEPWeaponLineHitKind.None,
                string.Empty,
                string.Empty,
                20d),
            "WeaponObservation rejects a non-normalized aim direction");
    }

    private static void VerifyObservationAndRoutes()
    {
        var fixture = new Fixture("route", reverseRegistration: false);

        fixture.Set(
            Observation(
                3,
                KLEPWeaponLineHitKind.Entity,
                "entity.friend",
                "team.human",
                4d),
            fire: false,
            reload: false);
        KLEPDecisionTrace friendly = fixture.Tick();
        Expect(HasKey(friendly, fixture.Loaded) &&
               HasKey(friendly, fixture.Friendly) &&
               !HasKey(friendly, fixture.Safe),
            "A loaded friendly first-hit routes Loaded + Friendly and never Safe");
        Expect(friendly.IsPatient,
            "Weapon evidence alone remains patient without a requested Solo");

        fixture.Set(
            Observation(
                3,
                KLEPWeaponLineHitKind.Entity,
                "entity.hostile",
                "team.zombie",
                5d),
            fire: false,
            reload: false);
        KLEPDecisionTrace hostile = fixture.Tick();
        Expect(HasKey(hostile, fixture.Loaded) &&
               HasKey(hostile, fixture.Safe) &&
               !HasKey(hostile, fixture.Friendly),
            "A loaded hostile first-hit routes Loaded + Safe and never Friendly");

        fixture.Set(
            Observation(
                3,
                KLEPWeaponLineHitKind.Obstacle,
                string.Empty,
                string.Empty,
                2d),
            fire: false,
            reload: false);
        KLEPDecisionTrace obstacle = fixture.Tick();
        Expect(HasKey(obstacle, fixture.Loaded) &&
               !HasKey(obstacle, fixture.Safe) &&
               !HasKey(obstacle, fixture.Friendly),
            "Obstacle evidence is neither affirmative hostile safety nor friendly obstruction");

        fixture.Set(null, fire: false, reload: false);
        KLEPDecisionTrace absent = fixture.Tick();
        KLEPExecutableStepTrace sensor = FindStep(
            absent,
            fixture.ObservationSensor.StableId,
            KLEPExecutableStepKind.Tandem);
        Expect(sensor != null &&
               sensor.State == KLEPExecutableState.Failed &&
               sensor.Outputs.Count == 0 &&
               !HasKey(absent, fixture.Observation),
            "An absent weapon sample fails without claiming observation truth");
    }

    private static void VerifyFireIntentAllowsFriendlyEvidence()
    {
        var fixture = new Fixture("fire", reverseRegistration: true);
        KLEPWeaponObservation friendlyObservation = Observation(
            2,
            KLEPWeaponLineHitKind.Entity,
            "entity.friend",
            "team.human",
            3d);
        fixture.Set(friendlyObservation, fire: true, reload: false);
        KLEPDecisionTrace trace = fixture.Tick();

        KLEPExecutableStepTrace fire = FindStep(
            trace,
            fixture.Fire.StableId,
            KLEPExecutableStepKind.Solo);
        Expect(fire != null &&
               fire.State == KLEPExecutableState.Succeeded &&
               fire.Outputs.Count == 1 &&
               fire.Outputs[0].Kind == KLEPExecutableOutputKind.Add &&
               fire.Outputs[0].SourceExecutableId == fixture.Fire.StableId &&
               ReferenceEquals(fire.Outputs[0].Definition, fixture.FireIntent),
            "A requested loaded Fire succeeds with one exact trace-bound intent output");
        KLEPWeaponFireIntent intent =
            KLEPWeaponFireIntent.Read(fire.Outputs[0].Payload);
        Expect(intent.Observation.HasFriendlyEntityHit &&
               intent.Observation.ObservedWorldTick == 41 &&
               intent.Observation.MagazineRounds == 2 &&
               HasKey(trace, fixture.Friendly),
            "Friendly evidence remains visible while manual Fire is intentionally permitted");

        fixture.Set(
            Observation(
                0,
                KLEPWeaponLineHitKind.None,
                string.Empty,
                string.Empty,
                20d),
            fire: true,
            reload: false);
        KLEPDecisionTrace empty = fixture.Tick();
        KLEPExecutableStepTrace emptyFire = FindStep(
            empty,
            fixture.Fire.StableId,
            KLEPExecutableStepKind.Solo);
        Expect(emptyFire == null,
            "An empty magazine never unlocks Fire or emits a new FireIntent");
    }

    private static void VerifyReloadIntentTruth()
    {
        var fixture = new Fixture("reload", reverseRegistration: false);
        fixture.Set(
            Observation(
                1,
                KLEPWeaponLineHitKind.None,
                string.Empty,
                string.Empty,
                20d),
            fire: false,
            reload: true);
        KLEPDecisionTrace trace = fixture.Tick();
        KLEPExecutableStepTrace reload = FindStep(
            trace,
            fixture.Reload.StableId,
            KLEPExecutableStepKind.Solo);
        Expect(reload != null &&
               reload.State == KLEPExecutableState.Succeeded &&
               reload.Outputs.Count == 1 &&
               reload.Outputs[0].SourceExecutableId == fixture.Reload.StableId &&
               ReferenceEquals(
                   reload.Outputs[0].Definition,
                   fixture.ReloadIntent),
            "Reload succeeds with one exact trace-bound intent output");
        KLEPWeaponReloadIntent intent =
            KLEPWeaponReloadIntent.Read(reload.Outputs[0].Payload);
        Expect(intent.MagazineCapacity == 6 &&
               intent.MagazineRounds == 1 &&
               intent.ReserveRounds == 12 &&
               intent.ObservedWorldTick == 41,
            "ReloadIntent freezes the exact deciding ammunition and world Tick");

        fixture.Set(
            Observation(
                6,
                KLEPWeaponLineHitKind.None,
                string.Empty,
                string.Empty,
                20d),
            fire: false,
            reload: true);
        KLEPDecisionTrace full = fixture.Tick();
        KLEPExecutableStepTrace failed = FindStep(
            full,
            fixture.Reload.StableId,
            KLEPExecutableStepKind.Solo);
        Expect(failed != null &&
               failed.State == KLEPExecutableState.Failed &&
               failed.Outputs.Count == 0,
            "A full magazine makes Reload fail without violating its output guarantee");
    }

    private static void VerifyDefinitionValidation()
    {
        KLEPKeyDefinition observation = Key("validate.observation");
        KLEPKeyDefinition output = Key("validate.output");
        KLEPExecutableDefinition valid = new KLEPExecutableDefinition(
            "sensor.validate.weapon",
            "Validate",
            KLEPExecutableKind.Sensor,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { observation });
        var sensor = new KLEPWeaponObservationSensorExecutable(
            valid,
            observation);
        Expect(ReferenceEquals(sensor.ObservationDefinition, observation),
            "Weapon sensor accepts its exact closed output definition");

        KLEPKeyDefinition mergedDefault = new KLEPKeyDefinition(
            new KLEPKeyId("key.weapon.defaulted"),
            "Defaulted",
            defaultLifetime: KLEPKeyLifetime.OneCycle,
            defaultPayload: new KLEPKeyPayload(new[]
            {
                new KeyValuePair<string, KLEPKeyValue>("surprise", true)
            }));
        ExpectThrows<ArgumentException>(
            () => new KLEPWeaponObservationSensorExecutable(
                new KLEPExecutableDefinition(
                    "sensor.weapon.defaulted",
                    "Defaulted",
                    KLEPExecutableKind.Sensor,
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { mergedDefault }),
                mergedDefault),
            "Typed weapon channels reject defaults that would open their schema");
        ExpectThrows<ArgumentException>(
            () => new KLEPWeaponFireExecutable(
                new KLEPExecutableDefinition(
                    "action.weapon.wrong-output",
                    "Wrong output",
                    KLEPExecutableKind.Action,
                    executionMode: KLEPExecutionMode.Solo,
                    declaredOutputs: new[] { output }),
                observation,
                Key("validate.not-exact")),
            "Fire rejects a declared output that is not its exact intent definition");
    }

    private static void VerifyRegistrationOrderDeterminism()
    {
        string forward = RunDeterministicScenario(false);
        string reverse = RunDeterministicScenario(true);
        Expect(forward == reverse,
            "Weapon decision truth is independent of root registration order");
        for (int repeat = 0; repeat < 20; repeat++)
        {
            Expect(RunDeterministicScenario((repeat & 1) != 0) == forward,
                $"Weapon trace remains deterministic on repeat {repeat}");
        }
    }

    private static string RunDeterministicScenario(bool reverse)
    {
        var fixture = new Fixture("deterministic", reverse);
        fixture.Set(
            Observation(
                4,
                KLEPWeaponLineHitKind.Entity,
                "entity.target",
                "team.zombie",
                8d),
            fire: true,
            reload: false);
        KLEPDecisionTrace trace = fixture.Tick();
        var result = new StringBuilder();
        result.Append(trace.CycleIndex)
            .Append('|').Append(trace.SelectedExecutableId)
            .Append('|').Append(trace.IsPatient);
        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            result.Append('|').Append(step.Kind)
                .Append(':').Append(step.ExecutableStableId)
                .Append(':').Append(step.State)
                .Append(':').Append(step.Result.RunIndex);
            foreach (KLEPExecutableOutput output in step.Outputs)
            {
                result.Append('[').Append(output.Kind)
                    .Append(':').Append(output.KeyId.Value)
                    .Append(':').Append(output.SourceExecutableId);
                foreach (KLEPKeyField field in output.Payload?.Fields ??
                         KLEPKeyPayload.Empty.Fields)
                {
                    result.Append(';').Append(field.Name)
                        .Append('=').Append(field.Value);
                }

                result.Append(']');
            }
        }

        return result.ToString();
    }

    private static KLEPWeaponObservation Observation(
        int rounds,
        KLEPWeaponLineHitKind hitKind,
        string hitEntityId,
        string hitTeamId,
        double hitDistance)
    {
        return new KLEPWeaponObservation(
            "weapon.test",
            "entity.shooter",
            "team.human",
            41,
            6,
            rounds,
            12,
            1d,
            2d,
            3d,
            0d,
            0d,
            1d,
            20d,
            hitKind,
            hitEntityId,
            hitTeamId,
            hitDistance);
    }

    private static KLEPKeyDefinition Key(string role)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId("key.weapon." + role),
            role,
            defaultLifetime: KLEPKeyLifetime.OneCycle);
    }

    private static bool HasKey(
        KLEPDecisionTrace trace,
        KLEPKeyDefinition definition)
    {
        return trace.KeySnapshot.Contains(definition.Id);
    }

    private static KLEPExecutableStepTrace FindStep(
        KLEPDecisionTrace trace,
        string executableId,
        KLEPExecutableStepKind kind)
    {
        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            if (step.Kind == kind &&
                StringComparer.Ordinal.Equals(
                    step.ExecutableStableId,
                    executableId))
            {
                return step;
            }
        }

        return null;
    }

    private static void Expect(bool condition, string description)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException(
                $"Assertion {assertions} failed: {description}");
        }
    }

    private static void ExpectThrows<TException>(
        Action action,
        string description)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            Expect(true, description);
            return;
        }

        Expect(false, description);
    }

    private sealed class Fixture
    {
        private readonly KLEPNeuron neuron;
        private readonly KLEPObservedKeySensorExecutable fireTriggerSensor;
        private readonly KLEPObservedKeySensorExecutable reloadTriggerSensor;

        internal Fixture(string suffix, bool reverseRegistration)
        {
            Observation = Key(suffix + ".observation");
            Loaded = Key(suffix + ".loaded");
            Safe = Key(suffix + ".safe");
            Friendly = Key(suffix + ".friendly");
            FireTrigger = Key(suffix + ".fire-trigger");
            ReloadTrigger = Key(suffix + ".reload-trigger");
            FireIntent = Key(suffix + ".fire-intent");
            ReloadIntent = Key(suffix + ".reload-intent");

            ObservationSensor = new KLEPWeaponObservationSensorExecutable(
                SensorDefinition(
                    "sensor.weapon." + suffix,
                    Observation),
                Observation);
            fireTriggerSensor = new KLEPObservedKeySensorExecutable(
                SensorDefinition(
                    "sensor.fire-trigger." + suffix,
                    FireTrigger));
            reloadTriggerSensor = new KLEPObservedKeySensorExecutable(
                SensorDefinition(
                    "sensor.reload-trigger." + suffix,
                    ReloadTrigger));
            KLEPWeaponObservationRouterExecutable loaded = Router(
                "router.loaded." + suffix,
                Loaded);
            KLEPWeaponObservationRouterExecutable safe = Router(
                "router.safe." + suffix,
                Safe);
            KLEPWeaponObservationRouterExecutable friendly = Router(
                "router.friendly." + suffix,
                Friendly);
            Fire = new KLEPWeaponFireExecutable(
                ActionDefinition(
                    "action.fire." + suffix,
                    FireIntent,
                    new KLEPAll(
                        Present(Observation),
                        Present(Loaded),
                        Present(FireTrigger)),
                    100f),
                Observation,
                FireIntent);
            Reload = new KLEPWeaponReloadExecutable(
                ActionDefinition(
                    "action.reload." + suffix,
                    ReloadIntent,
                    new KLEPAll(
                        Present(Observation),
                        Present(ReloadTrigger)),
                    90f),
                Observation,
                ReloadIntent);

            var executables = new List<KLEPExecutableBase>
            {
                ObservationSensor,
                fireTriggerSensor,
                reloadTriggerSensor,
                loaded,
                safe,
                friendly,
                Fire,
                Reload
            };
            if (reverseRegistration)
            {
                executables.Reverse();
            }

            neuron = new KLEPNeuron("neuron.weapon." + suffix);
            foreach (KLEPExecutableBase executable in executables)
            {
                neuron.RegisterExecutable(executable);
            }
        }

        internal KLEPKeyDefinition Observation { get; }
        internal KLEPKeyDefinition Loaded { get; }
        internal KLEPKeyDefinition Safe { get; }
        internal KLEPKeyDefinition Friendly { get; }
        internal KLEPKeyDefinition FireTrigger { get; }
        internal KLEPKeyDefinition ReloadTrigger { get; }
        internal KLEPKeyDefinition FireIntent { get; }
        internal KLEPKeyDefinition ReloadIntent { get; }
        internal KLEPWeaponObservationSensorExecutable ObservationSensor
        {
            get;
        }
        internal KLEPWeaponFireExecutable Fire { get; }
        internal KLEPWeaponReloadExecutable Reload { get; }

        internal void Set(
            KLEPWeaponObservation observation,
            bool fire,
            bool reload)
        {
            ObservationSensor.SetObservation(observation);
            fireTriggerSensor.SetObservation(fire);
            reloadTriggerSensor.SetObservation(reload);
        }

        internal KLEPDecisionTrace Tick()
        {
            return neuron.TickViaAgent();
        }

        private KLEPWeaponObservationRouterExecutable Router(
            string id,
            KLEPKeyDefinition output)
        {
            return new KLEPWeaponObservationRouterExecutable(
                new KLEPExecutableDefinition(
                    id,
                    id,
                    KLEPExecutableKind.Router,
                    executionLocks: new[]
                    {
                        new KLEPLock(
                            id + ".ready",
                            "Weapon observation present",
                            Present(Observation))
                    },
                    executionMode: KLEPExecutionMode.Tandem,
                    declaredOutputs: new[] { output }),
                Observation,
                Loaded,
                Safe,
                Friendly);
        }

        private static KLEPExecutableDefinition SensorDefinition(
            string id,
            KLEPKeyDefinition output)
        {
            return new KLEPExecutableDefinition(
                id,
                id,
                KLEPExecutableKind.Sensor,
                executionMode: KLEPExecutionMode.Tandem,
                declaredOutputs: new[] { output });
        }

        private static KLEPExecutableDefinition ActionDefinition(
            string id,
            KLEPKeyDefinition output,
            KLEPLockExpression expression,
            float score)
        {
            return new KLEPExecutableDefinition(
                id,
                id,
                KLEPExecutableKind.Action,
                executionLocks: new[]
                {
                    new KLEPLock(
                        id + ".ready",
                        id + " ready",
                        expression)
                },
                baseAttractiveness: score,
                executionMode: KLEPExecutionMode.Solo,
                declaredOutputs: new[] { output });
        }

        private static KLEPKeyPresent Present(KLEPKeyDefinition definition)
        {
            return new KLEPKeyPresent(definition.Id.Value);
        }
    }
}
