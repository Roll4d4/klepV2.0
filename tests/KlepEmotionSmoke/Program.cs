using System;
using System.Collections.Generic;
using System.Globalization;
using Roll4d4.Klep.Emotion;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        VerifyDesignerNamedAxesAndDefaults();
        VerifyVectorTextIsCultureInvariant();
        VerifyInvalidInputsAreRejected();
        VerifyContextEvaluationSuppliesMeaning();
        VerifyFrictionStopsMotionWithoutErasingPosition();
        VerifySubUlpFrictionStillStopsMotion();
        VerifyInternalAndExternalInfluencesAreRecorded();
        VerifyPositionSpeedAndNetInfluenceAreBounded();
        VerifyUnchangedPositionDurationIsInspectable();
        VerifyBoundarySaturationIsNotRest();
        VerifySnapshotsAreBoundedAndImmutable();
        VerifyTicksAreExplicitAndContinuous();
        VerifyEquivalentRunsAreDeterministic();

        Console.WriteLine($"KLEP Emotion smoke passed: {assertions} assertions.");
    }

    private static void VerifyDesignerNamedAxesAndDefaults()
    {
        KLEPEmotionConfiguration defaults = KLEPEmotionConfiguration.Default;
        Expect(defaults.AxisXName == "X" && defaults.AxisYName == "Y",
            "Default Emotion axes remain semantically unclaimed");
        Expect(defaults.FrictionPerTick == 0.1f &&
               defaults.MaximumSpeed == 1f &&
               defaults.SnapshotCapacity == 32,
            "Default Emotion physics are bounded and inspectable");

        var named = new KLEPEmotionConfiguration(
            axisXName: "Valence",
            axisYName: "Activation");
        Expect(named.AxisXName == "Valence" &&
               named.AxisYName == "Activation",
            "A designer may assign meanings to both graph axes");

        var emotion = new KLEPEmotion(named);
        Expect(emotion.Position == KLEPEmotionVector.Zero &&
               emotion.Velocity == KLEPEmotionVector.Zero &&
               emotion.Tick == 0 &&
               emotion.GetSnapshotHistory().Count == 0,
            "Emotion begins at rest without inventing an evaluation");
    }

    private static void VerifyInvalidInputsAreRejected()
    {
        Expect(Catch(() => new KLEPEmotionVector(float.NaN, 0f))
               is ArgumentOutOfRangeException,
            "Emotion rejects a non-finite axis value");
        Expect(Catch(() => new KLEPEmotionVector(1.01f, 0f))
               is ArgumentOutOfRangeException,
            "Emotion rejects a point outside the normalized graph");
        Expect(Catch(() => new KLEPEmotionConfiguration(axisXName: " "))
               is ArgumentException,
            "Emotion requires inspectable axis names");
        Expect(Catch(() => new KLEPEmotionConfiguration(
                   axisXName: "Same",
                   axisYName: "Same")) is ArgumentException,
            "Emotion requires distinct axis names for inspection");
        Expect(Catch(() => new KLEPEmotionConfiguration(frictionPerTick: 0f))
               is ArgumentOutOfRangeException,
            "Emotion requires positive friction so motion must stop");
        Expect(Catch(() => new KLEPEmotionConfiguration(snapshotCapacity: 0))
               is ArgumentOutOfRangeException,
            "Emotion requires a positive recent-snapshot capacity");
        Expect(Catch(() => new KLEPEmotionInfluence(
                   "",
                   KLEPEmotionInfluenceOrigin.Internal,
                   KLEPEmotionVector.Zero)) is ArgumentException,
            "Emotion influences require source provenance");
        Expect(Catch(() => new KLEPEmotionInfluence(
                   "invalid-origin",
                   (KLEPEmotionInfluenceOrigin)99,
                   KLEPEmotionVector.Zero)) is ArgumentOutOfRangeException,
            "Emotion rejects an unknown influence origin");

        var configuration = new KLEPEmotionConfiguration(maximumSpeed: 0.25f);
        Expect(Catch(() => new KLEPEmotion(
                   configuration,
                   KLEPEmotionVector.Zero,
                   new KLEPEmotionVector(0.5f, 0f)))
               is ArgumentOutOfRangeException,
            "Emotion rejects an initial velocity above its speed limit");
    }

    private static void VerifyVectorTextIsCultureInvariant()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Expect(new KLEPEmotionVector(0.5f, -0.25f).ToString() ==
                   "(0.5, -0.25)",
                "Emotion vector text is stable across host cultures");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static void VerifyContextEvaluationSuppliesMeaning()
    {
        var harmfulEvaluation = new KLEPEmotionInfluence(
            "ethics.same-action.harmful-context",
            KLEPEmotionInfluenceOrigin.External,
            new KLEPEmotionVector(-0.5f, 0f));
        var rescuingEvaluation = new KLEPEmotionInfluence(
            "ethics.same-action.rescuing-context",
            KLEPEmotionInfluenceOrigin.External,
            new KLEPEmotionVector(0.5f, 0f));

        var harmful = new KLEPEmotion();
        var rescuing = new KLEPEmotion();
        harmful.Advance(1, new[] { harmfulEvaluation });
        rescuing.Advance(1, new[] { rescuingEvaluation });

        Expect(harmful.Position.X == -0.5f && rescuing.Position.X == 0.5f,
            "The supplied contextual evaluation, not the action name, gives an event emotional meaning");
        Expect(harmful.Position.Y == rescuing.Position.Y,
            "One evaluated action may differ on only the axis selected by its Ethics designer");
    }

    private static void VerifyFrictionStopsMotionWithoutErasingPosition()
    {
        var configuration = new KLEPEmotionConfiguration(
            frictionPerTick: 0.25f,
            maximumSpeed: 1f);
        var emotion = new KLEPEmotion(
            configuration,
            KLEPEmotionVector.Zero,
            new KLEPEmotionVector(0.5f, 0f));

        KLEPEmotionSnapshot first = emotion.Advance(1);
        KLEPEmotionSnapshot second = emotion.Advance(2);
        KLEPEmotionSnapshot third = emotion.Advance(3);

        Expect(first.Position == new KLEPEmotionVector(0.5f, 0f) &&
               first.Velocity == new KLEPEmotionVector(0.25f, 0f),
            "An emotional body moves before friction reduces its carried velocity");
        Expect(second.Position == new KLEPEmotionVector(0.75f, 0f) &&
               second.IsAtRest,
            "Linear emotional friction reaches exact rest in finite Tick time");
        Expect(third.Position == second.Position && third.IsAtRest,
            "Friction stops motion without silently returning emotional position to neutral");
    }

    private static void VerifySubUlpFrictionStillStopsMotion()
    {
        var emotion = new KLEPEmotion(
            new KLEPEmotionConfiguration(frictionPerTick: float.Epsilon),
            KLEPEmotionVector.Zero,
            new KLEPEmotionVector(0.5f, 0f));

        KLEPEmotionSnapshot snapshot = emotion.Advance(1);
        Expect(snapshot.IsAtRest,
            "A positive friction too small to reduce a Single still reaches exact rest");

        var diagonal = new KLEPEmotion(
            new KLEPEmotionConfiguration(frictionPerTick: 0.1f),
            KLEPEmotionVector.Zero,
            new KLEPEmotionVector(0.6f, 0.8f));
        long tick = 0;
        while (diagonal.Velocity != KLEPEmotionVector.Zero && tick < 12)
        {
            tick++;
            diagonal.Advance(tick);
        }

        Expect(diagonal.Velocity == KLEPEmotionVector.Zero && tick <= 11,
            "Diagonal emotional motion also reaches exact rest in bounded Tick time");
    }

    private static void VerifyInternalAndExternalInfluencesAreRecorded()
    {
        var emotion = new KLEPEmotion();
        KLEPEmotionSnapshot snapshot = emotion.Advance(
            1,
            new[]
            {
                new KLEPEmotionInfluence(
                    "self.action-evaluation",
                    KLEPEmotionInfluenceOrigin.Internal,
                    new KLEPEmotionVector(0.2f, 0f)),
                new KLEPEmotionInfluence(
                    "observed.other-action-evaluation",
                    KLEPEmotionInfluenceOrigin.External,
                    new KLEPEmotionVector(0f, -0.3f))
            });

        Expect(snapshot.Influences.Count == 2 &&
               snapshot.Influences[0].Origin ==
                   KLEPEmotionInfluenceOrigin.Internal &&
               snapshot.Influences[1].Origin ==
                   KLEPEmotionInfluenceOrigin.External,
            "Snapshots preserve ordered internal and external evaluation provenance");
        Expect(snapshot.NetInfluence == new KLEPEmotionVector(0.2f, -0.3f),
            "A Tick exposes the bounded net emotional impulse it integrated");
    }

    private static void VerifyPositionSpeedAndNetInfluenceAreBounded()
    {
        var emotion = new KLEPEmotion(new KLEPEmotionConfiguration(
            frictionPerTick: 0.1f,
            maximumSpeed: 0.5f));
        KLEPEmotionSnapshot snapshot = emotion.Advance(
            1,
            new[]
            {
                Influence("strong.1", 1f, 1f),
                Influence("strong.2", 1f, 1f)
            });

        Expect(snapshot.NetInfluence == new KLEPEmotionVector(1f, 1f),
            "Stacked influence is bounded to the normalized graph");
        Expect(Approximately(snapshot.IntegratedVelocity.Magnitude, 0.5f),
            "Emotional speed is bounded independently of stacked influence");

        for (long tick = 2; tick <= 12; tick++)
        {
            emotion.Advance(tick, new[] { Influence($"push.{tick}", 1f, 1f) });
        }

        Expect(emotion.Position.X <= 1f && emotion.Position.Y <= 1f,
            "Emotional position remains inside the normalized graph");
    }

    private static void VerifyUnchangedPositionDurationIsInspectable()
    {
        var emotion = new KLEPEmotion(
            new KLEPEmotionConfiguration(frictionPerTick: 1f),
            new KLEPEmotionVector(-1f, 0f),
            KLEPEmotionVector.Zero);

        for (long tick = 1; tick <= 5; tick++)
        {
            emotion.Advance(tick);
        }

        Expect(emotion.Position == new KLEPEmotionVector(-1f, 0f) &&
               emotion.UnchangedPositionTickCount == 5,
            "Emotion exposes how long an Agent has remained at a negative position");
        Expect(emotion.LastSnapshot.UnchangedPositionTickCount == 5,
            "The latest snapshot carries unchanged-position duration for future Memory analysis");
    }

    private static void VerifyBoundarySaturationIsNotRest()
    {
        var emotion = new KLEPEmotion(
            new KLEPEmotionConfiguration(frictionPerTick: 0.1f),
            new KLEPEmotionVector(1f, 0f),
            new KLEPEmotionVector(0.5f, 0f));

        KLEPEmotionSnapshot snapshot = emotion.Advance(1);
        Expect(snapshot.UnchangedPositionTickCount == 1 && !snapshot.IsAtRest,
            "A graph-bound position may remain unchanged while emotional velocity is still being damped");
    }

    private static void VerifySnapshotsAreBoundedAndImmutable()
    {
        var emotion = new KLEPEmotion(new KLEPEmotionConfiguration(
            snapshotCapacity: 2));
        emotion.Advance(1);
        emotion.Advance(2);
        emotion.Advance(3);

        IReadOnlyList<KLEPEmotionSnapshot> history =
            emotion.GetSnapshotHistory();
        Expect(history.Count == 2 && history[0].Tick == 2 && history[1].Tick == 3,
            "Emotion retains only its configured recent snapshot window");

        var mutableHistory = history as IList<KLEPEmotionSnapshot>;
        Expect(mutableHistory != null && mutableHistory.IsReadOnly,
            "Published snapshot history cannot mutate Emotion state");

        var source = new List<KLEPEmotionInfluence>
        {
            Influence("copied", 0.1f, 0f)
        };
        var copiedEmotion = new KLEPEmotion();
        KLEPEmotionSnapshot copied = copiedEmotion.Advance(1, source);
        source.Clear();
        Expect(copied.Influences.Count == 1,
            "A snapshot owns an immutable copy of supplied influences");
        Expect(ReferenceEquals(copied.Configuration, copiedEmotion.Configuration) &&
               copied.Configuration.AxisXName == "X" &&
               copied.Configuration.AxisYName == "Y",
            "A snapshot carries its immutable graph schema for later Memory analysis");

        var duplicates = new List<KLEPEmotionInfluence>
        {
            Influence("duplicate", 0.1f, 0f),
            Influence("duplicate", 0.2f, 0f)
        };
        var guarded = new KLEPEmotion();
        Expect(Catch(() => guarded.Advance(1, duplicates)) is ArgumentException &&
               guarded.Tick == 0 &&
               guarded.GetSnapshotHistory().Count == 0,
            "Duplicate source IDs fail before Emotion state changes");
    }

    private static void VerifyTicksAreExplicitAndContinuous()
    {
        var emotion = new KLEPEmotion();
        Expect(Catch(() => emotion.Advance(2)) is ArgumentOutOfRangeException,
            "Emotion refuses to invent a missing Tick");
        emotion.Advance(1);
        Expect(Catch(() => emotion.Advance(1)) is ArgumentOutOfRangeException,
            "Emotion advances at most once for one supplied Tick");
        Expect(Catch(() => emotion.Advance(3)) is ArgumentOutOfRangeException,
            "Emotion requires a continuous caller-owned Tick path");
    }

    private static void VerifyEquivalentRunsAreDeterministic()
    {
        var configuration = new KLEPEmotionConfiguration(
            axisXName: "Good-Bad",
            axisYName: "Calm-Activated",
            frictionPerTick: 0.15f,
            maximumSpeed: 0.6f,
            snapshotCapacity: 8);
        var first = new KLEPEmotion(configuration);
        var second = new KLEPEmotion(configuration);

        for (long tick = 1; tick <= 6; tick++)
        {
            KLEPEmotionInfluence[] influences = tick == 1
                ? new[] { Influence("initial", -0.4f, 0.3f) }
                : tick == 4
                    ? new[] { Influence("correction", 0.6f, -0.2f) }
                    : Array.Empty<KLEPEmotionInfluence>();
            KLEPEmotionSnapshot left = first.Advance(tick, influences);
            KLEPEmotionSnapshot right = second.Advance(tick, influences);

            Expect(left.Position == right.Position &&
                   left.Velocity == right.Velocity &&
                   left.Tick == right.Tick &&
                   left.PositionBefore == right.PositionBefore &&
                   left.VelocityBefore == right.VelocityBefore &&
                   left.NetInfluence == right.NetInfluence &&
                   left.IntegratedVelocity == right.IntegratedVelocity &&
                   left.UnchangedPositionTickCount ==
                       right.UnchangedPositionTickCount &&
                   EquivalentInfluences(left.Influences, right.Influences),
                $"Identical Emotion inputs produce the same snapshot at Tick {tick}");
        }
    }

    private static bool EquivalentInfluences(
        IReadOnlyList<KLEPEmotionInfluence> left,
        IReadOnlyList<KLEPEmotionInfluence> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!string.Equals(
                    left[i].SourceId,
                    right[i].SourceId,
                    StringComparison.Ordinal) ||
                left[i].Origin != right[i].Origin ||
                left[i].Impulse != right[i].Impulse)
            {
                return false;
            }
        }

        return true;
    }

    private static KLEPEmotionInfluence Influence(
        string sourceId,
        float x,
        float y)
    {
        return new KLEPEmotionInfluence(
            sourceId,
            KLEPEmotionInfluenceOrigin.Internal,
            new KLEPEmotionVector(x, y));
    }

    private static bool Approximately(float left, float right)
    {
        return Math.Abs(left - right) <= 0.00001f;
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
            throw new InvalidOperationException(message);
        }
    }
}
