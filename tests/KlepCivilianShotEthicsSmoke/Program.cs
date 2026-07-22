using System;
using Roll4d4.Klep.Behaviors;
using Roll4d4.Klep.Cognition;
using Roll4d4.Klep.Emotion;
using Roll4d4.Klep.Ethics;
using Roll4d4.Klep.Unity;
using Roll4d4.Klep.ZombieTest;

internal static class Program
{
    private const string ActorNeuronId = "neuron.civilian.ethics-smoke";
    private const string ShooterEntityId = "entity.human.shooter";
    private const string ActionId = "action.civilian.fire";
    private const string WeaponId = "weapon.rifle.ethics-smoke";

    private static int assertions;
    private static long evaluationIndex;

    private static int Main()
    {
        try
        {
            ZombieDamageIsBeneficial();
            ZombieKillAddsTheKillBonus();
            HumanDamageIsInjurious();
            HumanKillAddsTheKillPenalty();
            MissAndObstacleAreNeutralButTraced();
            Console.WriteLine(
                $"KLEP civilian shot Ethics smoke passed ({assertions} assertions)." );
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ZombieDamageIsBeneficial()
    {
        KLEPEthicsEvaluation<KLEPCivilianShotContext> evaluation = Evaluate(
            KLEPWeaponLineHitKind.Entity,
            "entity.zombie.target",
            KLEPCivilianCognitionDefaults.ZombieTeamId,
            healthBefore: 1f,
            healthAfter: 0.75f,
            targetKilled: false,
            friendlyFire: false);

        ExpectJudgment(evaluation, 0.25, 0.10, appliedRules: 1,
            "damaging a zombie uses the approved positive Ethics impulse");
    }

    private static void ZombieKillAddsTheKillBonus()
    {
        KLEPEthicsEvaluation<KLEPCivilianShotContext> evaluation = Evaluate(
            KLEPWeaponLineHitKind.Entity,
            "entity.zombie.target",
            KLEPCivilianCognitionDefaults.ZombieTeamId,
            healthBefore: 0.25f,
            healthAfter: 0f,
            targetKilled: true,
            friendlyFire: false);

        ExpectJudgment(evaluation, 0.35, 0.05, appliedRules: 2,
            "killing a zombie adds the distinct kill bonus");
    }

    private static void HumanDamageIsInjurious()
    {
        KLEPEthicsEvaluation<KLEPCivilianShotContext> evaluation = Evaluate(
            KLEPWeaponLineHitKind.Entity,
            "entity.human.target",
            KLEPCivilianCognitionDefaults.HumanTeamId,
            healthBefore: 1f,
            healthAfter: 0.75f,
            targetKilled: false,
            friendlyFire: true);

        ExpectJudgment(evaluation, -0.50, 0.40, appliedRules: 1,
            "damaging a human uses the approved friendly-fire penalty");
    }

    private static void HumanKillAddsTheKillPenalty()
    {
        KLEPEthicsEvaluation<KLEPCivilianShotContext> evaluation = Evaluate(
            KLEPWeaponLineHitKind.Entity,
            "entity.human.target",
            KLEPCivilianCognitionDefaults.HumanTeamId,
            healthBefore: 0.25f,
            healthAfter: 0f,
            targetKilled: true,
            friendlyFire: true);

        ExpectJudgment(evaluation, -0.85, 0.70, appliedRules: 2,
            "killing a human adds the distinct kill penalty");
    }

    private static void MissAndObstacleAreNeutralButTraced()
    {
        KLEPEthicsEvaluation<KLEPCivilianShotContext> miss = Evaluate(
            KLEPWeaponLineHitKind.None,
            string.Empty,
            string.Empty,
            healthBefore: 0f,
            healthAfter: 0f,
            targetKilled: false,
            friendlyFire: false);
        KLEPEthicsEvaluation<KLEPCivilianShotContext> obstacle = Evaluate(
            KLEPWeaponLineHitKind.Obstacle,
            string.Empty,
            string.Empty,
            healthBefore: 0f,
            healthAfter: 0f,
            targetKilled: false,
            friendlyFire: false);

        ExpectJudgment(miss, 0, 0, appliedRules: 0,
            "a miss remains ethically neutral");
        ExpectJudgment(obstacle, 0, 0, appliedRules: 0,
            "an obstacle impact remains ethically neutral");
        Expect(miss.Judgment.Trace.Count == 5 &&
               obstacle.Judgment.Trace.Count == 5,
            "neutral shots retain the bias and all four inspectable rule traces");
        Expect(ContainsEvidence(miss, "hit-kind.None") &&
               ContainsEvidence(obstacle, "hit-kind.Obstacle"),
            "neutral traces preserve the factual shot kind");
    }

    private static KLEPEthicsEvaluation<KLEPCivilianShotContext> Evaluate(
        KLEPWeaponLineHitKind hitKind,
        string hitEntityId,
        string hitTeamId,
        float healthBefore,
        float healthAfter,
        bool targetKilled,
        bool friendlyFire)
    {
        evaluationIndex++;
        var receipt = new KLEPWeaponAppliedShotReceipt(
            agentCycle: evaluationIndex,
            actionStableId: ActionId,
            actionRunIndex: evaluationIndex,
            weaponId: WeaponId,
            shooterEntityId: ShooterEntityId,
            shooterTeamId: KLEPCivilianCognitionDefaults.HumanTeamId,
            observedWorldTick: evaluationIndex,
            magazineBefore: 6,
            magazineAfter: 5,
            hitKind: hitKind,
            hitEntityId: hitEntityId,
            hitTeamId: hitTeamId,
            hitDistance: hitKind == KLEPWeaponLineHitKind.None ? 0f : 3f,
            healthBefore: healthBefore,
            healthAfter: healthAfter,
            targetKilled: targetKilled,
            friendlyFire: friendlyFire);
        var context = new KLEPCivilianShotContext(
            ActorNeuronId,
            actionWorldTick: evaluationIndex,
            consequenceWorldTick: evaluationIndex + 1,
            receipt: receipt);
        KLEPCognitionComposition<KLEPCivilianShotContext> cognition =
            KLEPCivilianCognitionDefaults.Create(
                "memory.civilian.ethics-smoke." + evaluationIndex,
                "observer.civilian.ethics-smoke." + evaluationIndex,
                "valence",
                "activation");

        return cognition.Ethics.Evaluate(
            new KLEPEthicsRequest<KLEPCivilianShotContext>(
                "evaluation.civilian.ethics-smoke." + evaluationIndex,
                evaluationTick: evaluationIndex + 1,
                causeOrigin: KLEPEmotionInfluenceOrigin.Internal,
                emotionConfiguration: cognition.Emotion.Configuration,
                contextIdentity: new KLEPEthicsContextIdentity(
                    "context.civilian.ethics-smoke." + evaluationIndex,
                    "context.civilian.applied-shot",
                    "1"),
                context: context));
    }

    private static void ExpectJudgment(
        KLEPEthicsEvaluation<KLEPCivilianShotContext> evaluation,
        double expectedX,
        double expectedY,
        int appliedRules,
        string message)
    {
        ExpectNear(evaluation.Judgment.RawX, expectedX, message + " X");
        ExpectNear(evaluation.Judgment.RawY, expectedY, message + " Y");

        int actualApplied = 0;
        for (int index = 0; index < evaluation.Judgment.Trace.Count; index++)
        {
            if (evaluation.Judgment.Trace[index].Applied &&
                evaluation.Judgment.Trace[index].SourceId.StartsWith(
                    "rule:",
                    StringComparison.Ordinal))
            {
                actualApplied++;
            }
        }

        Expect(actualApplied == appliedRules,
            message + " applies the expected number of ordered rules");
    }

    private static bool ContainsEvidence(
        KLEPEthicsEvaluation<KLEPCivilianShotContext> evaluation,
        string expected)
    {
        for (int traceIndex = 0;
             traceIndex < evaluation.Judgment.Trace.Count;
             traceIndex++)
        {
            var evidence = evaluation.Judgment.Trace[traceIndex].EvidenceIds;
            for (int evidenceIndex = 0;
                 evidenceIndex < evidence.Count;
                 evidenceIndex++)
            {
                if (StringComparer.Ordinal.Equals(
                        evidence[evidenceIndex],
                        expected))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException("Assertion failed: " + message);
        }
    }

    private static void ExpectNear(
        double actual,
        double expected,
        string message)
    {
        Expect(Math.Abs(actual - expected) <= 0.00001,
            message + $" (expected {expected}, actual {actual})");
    }
}
