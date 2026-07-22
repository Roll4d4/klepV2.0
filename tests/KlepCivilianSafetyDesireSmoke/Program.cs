using System;
using Roll4d4.Klep.Desire;
using Roll4d4.Klep.ZombieTest;

internal static class Program
{
    private static int assertions;

    private static int Main()
    {
        try
        {
            SafetyAndReadinessRemainDistinct();
            PassiveEffectsCannotTrainTheCritic();
            ConfigurationAndWorldOrderAreGuarded();
            Console.WriteLine(
                $"KLEP civilian safety Desire smoke passed ({assertions} assertions). ");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void SafetyAndReadinessRemainDistinct()
    {
        var body = new KLEPCivilianSafetyDesireBody(
            "desire-owner.civilian.001",
            threatRadius: 10f,
            initiallyAlive: true,
            initialNearestHostileDistance: 10f,
            initiallyHasWeapon: true,
            initiallyWeaponLoaded: true,
            safetyWeight: 2f,
            readyToDefendWeight: 0.5f);

        KLEPDesireSnapshot baseline = body.BaselineSnapshot;
        Expect(baseline.DesireTick == 0,
            "baseline uses Desire Tick zero");
        Expect(baseline.Desires.Count == 2,
            "the body owns separate safety and readiness definitions");
        KLEPDesireStateSnapshot safety = baseline.Desires[0];
        KLEPDesireStateSnapshot readiness = baseline.Desires[1];
        Expect(safety.DesireStableId ==
               KLEPCivilianSafetyDesireBody.SafetyDesireStableId,
            "actual safety has a stable identity");
        Expect(readiness.DesireStableId ==
               KLEPCivilianSafetyDesireBody.ReadyToDefendDesireStableId,
            "defensive readiness has a distinct stable identity");
        ExpectNear(safety.Satisfaction, 1f,
            "an alive civilian outside the threat radius is safe");
        ExpectNear(safety.Pressure, 0f,
            "no nearby threat leaves safety dormant");
        ExpectNear(readiness.Satisfaction, 1f,
            "a possessed loaded weapon satisfies readiness");
        ExpectNear(readiness.Pressure, 0f,
            "readiness is dormant without a nearby threat");
        ExpectNear(safety.Weight, 2f,
            "authored safety weight remains evidence");
        ExpectNear(readiness.Weight, 0.5f,
            "authored readiness weight remains evidence");

        KLEPCivilianSafetyDesireStep threatened = body.ObserveWorld(
            1,
            isAlive: true,
            nearestHostileDistance: 2f,
            hasWeapon: true,
            weaponLoaded: false);
        safety = threatened.Snapshot.Desires[0];
        readiness = threatened.Snapshot.Desires[1];
        ExpectNear(safety.Satisfaction, 0.2f,
            "actual safety follows normalized hostile proximity");
        ExpectNear(safety.Pressure, 0.8f,
            "nearby threat creates safety pressure");
        ExpectNear(readiness.Satisfaction, 0f,
            "an unloaded possessed weapon is not ready");
        ExpectNear(readiness.Pressure, 0.8f,
            "readiness urgency follows the same factual threat");

        KLEPCivilianSafetyDesireStep loaded = body.ObserveWorld(
            2,
            isAlive: true,
            nearestHostileDistance: 2f,
            hasWeapon: true,
            weaponLoaded: true);
        ExpectNear(loaded.Effects.Effects[0].Effect, 0f,
            "loading a weapon does not manufacture actual safety");
        ExpectNear(loaded.Effects.Effects[1].Effect, 1f,
            "loading changes only defensive readiness");

        KLEPCivilianSafetyDesireStep dead = body.ObserveWorld(
            3,
            isAlive: false,
            nearestHostileDistance: 0f,
            hasWeapon: true,
            weaponLoaded: true);
        ExpectNear(dead.Snapshot.Desires[0].Satisfaction, 0f,
            "a dead civilian is not reported safe");
        ExpectNear(dead.Snapshot.Desires[0].Pressure, 0f,
            "a removed mind has no continuing safety pressure");
        ExpectNear(dead.Snapshot.Desires[1].Satisfaction, 0f,
            "a dead civilian is not reported ready to defend");
        ExpectNear(dead.Snapshot.Desires[1].Pressure, 0f,
            "a removed mind has no continuing readiness pressure");
    }

    private static void PassiveEffectsCannotTrainTheCritic()
    {
        var body = new KLEPCivilianSafetyDesireBody(
            "desire-owner.civilian.002",
            8f,
            true,
            8f,
            false,
            false);
        KLEPCivilianSafetyDesireStep step = body.ObserveWorld(
            1,
            true,
            4f,
            false,
            false);

        Expect(step.Effects.Attribution.Kind ==
               KLEPDesireEffectAttribution.Unknown,
            "periodic world sampling claims no unsupported causal owner");
        Expect(!step.Effects.Attribution.ActionRunIndex.HasValue,
            "passive safety evidence claims no action run");
        Expect(!step.Effects.Attribution.IsEligibleForAutomaticExpectationLearning,
            "unknown safety evidence cannot train the ActionOwned critic");
        Expect(step.Effects.Effects.Count == 2,
            "the full ordered Desire effect vector is preserved");
        Expect(ReferenceEquals(body.LastEffects, step.Effects),
            "the body retains its latest immutable vector for inspection");
    }

    private static void ConfigurationAndWorldOrderAreGuarded()
    {
        Expect(Catch(() => new KLEPCivilianSafetyDesireBody(
                   "owner",
                   float.NaN,
                   true,
                   1f,
                   false,
                   false)) is ArgumentOutOfRangeException,
            "NaN threat radius is rejected");
        Expect(Catch(() => new KLEPCivilianSafetyDesireBody(
                   "owner",
                   0f,
                   true,
                   1f,
                   false,
                   false)) is ArgumentOutOfRangeException,
            "threat radius must be positive");
        Expect(Catch(() => new KLEPCivilianSafetyDesireBody(
                   "owner",
                   8f,
                   true,
                   float.PositiveInfinity,
                   false,
                   false)) is ArgumentOutOfRangeException,
            "nearest-hostile distance must be finite");
        Expect(Catch(() => new KLEPCivilianSafetyDesireBody(
                   "owner",
                   8f,
                   true,
                   -1f,
                   false,
                   false)) is ArgumentOutOfRangeException,
            "nearest-hostile distance cannot be negative");
        Expect(Catch(() => new KLEPCivilianSafetyDesireBody(
                   "owner",
                   8f,
                   true,
                   8f,
                   false,
                   true)) is ArgumentException,
            "loaded cannot be reported without possession");
        Expect(Catch(() => new KLEPCivilianSafetyDesireBody(
                   "owner",
                   8f,
                   true,
                   8f,
                   false,
                   false,
                   safetyWeight: -1f)) is ArgumentOutOfRangeException,
            "authored Desire weights cannot be negative");

        var body = new KLEPCivilianSafetyDesireBody(
            "owner.order",
            8f,
            true,
            8f,
            false,
            false);
        Expect(Catch(() => body.ObserveWorld(
                   0,
                   true,
                   8f,
                   false,
                   false)) is ArgumentOutOfRangeException,
            "baseline world Tick cannot be replayed");
        Expect(Catch(() => body.ObserveWorld(
                   2,
                   true,
                   8f,
                   false,
                   false)) is ArgumentOutOfRangeException,
            "world observations cannot skip a Tick");
        body.ObserveWorld(1, true, 8f, false, false);
        Expect(Catch(() => body.ObserveWorld(
                   1,
                   true,
                   8f,
                   false,
                   false)) is ArgumentOutOfRangeException,
            "world observations cannot replay a Tick");
        Expect(Catch(() => body.ObserveWorld(
                   2,
                   true,
                   8f,
                   false,
                   true)) is ArgumentException,
            "later observations also enforce weapon-state consistency");

        var later = new KLEPCivilianSafetyDesireBody(
            "owner.later",
            8f,
            true,
            8f,
            false,
            false,
            initialWorldTick: 40);
        Expect(later.BaselineSnapshot.ObservedMomentId.Contains("world.40"),
            "a later-spawned body binds its baseline to its world Tick");
        later.ObserveWorld(41, true, 8f, false, false);
    }

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
            throw new InvalidOperationException("Assertion failed: " + message);
        }
    }

    private static void ExpectNear(float actual, float expected, string message)
    {
        Expect(Math.Abs(actual - expected) <= 0.00001f,
            message + $" (expected {expected}, actual {actual})");
    }
}
