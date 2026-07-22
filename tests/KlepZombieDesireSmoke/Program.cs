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
            BaselineAndSeparatedEffectsAreTruthful();
            ExactActionIdentityIsRequired();
            ConfigurationAndWorldOrderAreGuarded();
            Console.WriteLine(
                $"KLEP zombie Desire smoke tests passed ({assertions} assertions).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void BaselineAndSeparatedEffectsAreTruthful()
    {
        var body = new KLEPZombieDesireBody(
            "desire-owner.zombie.001",
            "action.zombie.attack",
            initialHunger01: 0.60f,
            metabolismPerWorldTick: 0.10f,
            successfulBiteRelief: 0.40f,
            fedDesireWeight: 2f);

        KLEPDesireSnapshot baseline = body.BaselineSnapshot;
        Expect(baseline.DesireTick == 0, "baseline uses Desire Tick 0");
        Expect(baseline.Desires.Count == 1, "baseline contains the fed Desire");
        KLEPDesireStateSnapshot fed = baseline.Desires[0];
        Expect(fed.DesireStableId == KLEPZombieDesireBody.FedDesireStableId,
            "fed Desire has a stable project identity");
        ExpectNear(fed.Satisfaction, 0.40f, "baseline satisfaction");
        ExpectNear(fed.Deficit, 0.60f, "baseline deficit");
        ExpectNear(fed.Pressure, 0.60f, "baseline pressure");
        ExpectNear(fed.Weight, 2f, "authored weight remains evidence");

        KLEPZombieDesireStep metabolism = body.AdvanceMetabolism(1);
        Expect(metabolism.Snapshot.DesireTick == 1,
            "metabolism gets its own next Desire Tick");
        Expect(metabolism.Effects.Attribution.Kind ==
               KLEPDesireEffectAttribution.External,
            "passive metabolism is External");
        Expect(!metabolism.Effects.Attribution.ActionRunIndex.HasValue,
            "External metabolism claims no action run");
        ExpectNear(body.Hunger01, 0.70f, "metabolism raises factual hunger");
        ExpectNear(metabolism.Effects.Effects[0].Effect, -0.10f,
            "metabolism records the negative fed-satisfaction effect");

        KLEPZombieDesireStep bite = body.RecordSuccessfulBite(
            1,
            "action.zombie.attack",
            7,
            "entity.survivor.001");
        Expect(bite.Snapshot.DesireTick == 2,
            "bite has a distinct Desire Tick from metabolism");
        Expect(bite.Effects.PriorSnapshotId == metabolism.Snapshot.SnapshotId,
            "bite begins from the post-metabolism snapshot");
        Expect(bite.Effects.Attribution.Kind ==
               KLEPDesireEffectAttribution.ActionOwned,
            "successful bite is ActionOwned");
        Expect(bite.Effects.Attribution.ActionStableId ==
               "action.zombie.attack",
            "bite retains exact action stable ID");
        Expect(bite.Effects.Attribution.ActionRunIndex == 7,
            "bite retains exact action run index");
        Expect(bite.Effects.Attribution.IsEligibleForAutomaticExpectationLearning,
            "exact successful bite is memory-ready learning evidence");
        ExpectNear(body.Hunger01, 0.30f, "bite relieves factual hunger");
        ExpectNear(bite.Effects.Effects[0].Effect, 0.40f,
            "bite records positive fed-satisfaction effect");
        Expect(ReferenceEquals(body.LastExternalEffects, metabolism.Effects),
            "body retains the separate External vector");
        Expect(ReferenceEquals(body.LastSuccessfulBiteEffects, bite.Effects),
            "body retains the separate ActionOwned vector");
    }

    private static void ExactActionIdentityIsRequired()
    {
        var body = new KLEPZombieDesireBody(
            "desire-owner.zombie.002",
            "action.zombie.attack");
        body.AdvanceMetabolism(1);

        Expect(Catch(() => body.RecordSuccessfulBite(
                   1,
                   "action.zombie.move",
                   1,
                   "entity.survivor.001")) is ArgumentException,
            "another Executable cannot claim bite relief");
        Expect(Catch(() => body.RecordSuccessfulBite(
                   1,
                   "action.zombie.attack",
                   0,
                   "entity.survivor.001")) is ArgumentOutOfRangeException,
            "bite requires a positive exact run index");

        body.RecordSuccessfulBite(
            1,
            "action.zombie.attack",
            1,
            "entity.survivor.001");
        Expect(Catch(() => body.RecordSuccessfulBite(
                   1,
                   "action.zombie.attack",
                   2,
                   "entity.survivor.001")) is InvalidOperationException,
            "one world Tick cannot claim two bites");

        body.AdvanceMetabolism(2);
        Expect(Catch(() => body.RecordSuccessfulBite(
                   2,
                   "action.zombie.attack",
                   1,
                   "entity.survivor.001")) is ArgumentOutOfRangeException,
            "an old attack run cannot be replayed");
    }

    private static void ConfigurationAndWorldOrderAreGuarded()
    {
        Expect(Catch(() => new KLEPZombieDesireBody(
                   "owner",
                   "attack",
                   float.NaN)) is ArgumentOutOfRangeException,
            "NaN hunger is rejected");
        Expect(Catch(() => new KLEPZombieDesireBody(
                   "owner",
                   "attack",
                   metabolismPerWorldTick: float.PositiveInfinity))
               is ArgumentOutOfRangeException,
            "infinite metabolism is rejected");
        Expect(Catch(() => new KLEPZombieDesireBody(
                   "owner",
                   "attack",
                   successfulBiteRelief: -0.1f))
               is ArgumentOutOfRangeException,
            "negative bite relief is rejected");
        Expect(Catch(() => new KLEPZombieDesireBody(
                   "owner",
                   "attack",
                   metabolismPerWorldTick: float.Epsilon))
               is ArgumentOutOfRangeException,
            "a positive metabolism delta too small to change float state is rejected");
        Expect(Catch(() => new KLEPZombieDesireBody(
                   "owner",
                   "attack",
                   successfulBiteRelief: float.Epsilon))
               is ArgumentOutOfRangeException,
            "a positive bite delta too small to change float state is rejected");

        var body = new KLEPZombieDesireBody("owner", "attack");
        Expect(Catch(() => body.AdvanceMetabolism(0))
               is ArgumentOutOfRangeException,
            "world Tick 0 is reserved for the baseline");
        Expect(Catch(() => body.RecordSuccessfulBite(
                   1,
                   "attack",
                   1,
                   "target")) is ArgumentOutOfRangeException,
            "bite cannot precede same-Tick metabolism");
        body.AdvanceMetabolism(1);
        Expect(Catch(() => body.AdvanceMetabolism(1))
               is ArgumentOutOfRangeException,
            "metabolism cannot replay a world Tick");
        Expect(Catch(() => body.AdvanceMetabolism(3))
               is ArgumentOutOfRangeException,
            "metabolism cannot silently skip a world Tick");

        var laterSpawn = new KLEPZombieDesireBody(
            "later-owner",
            "attack",
            initialWorldTick: 40);
        Expect(laterSpawn.BaselineSnapshot.ObservedMomentId.Contains("world.40"),
            "a later-spawned body binds its baseline to the supplied world Tick");
        laterSpawn.AdvanceMetabolism(41);
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
