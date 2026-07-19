using System;
using System.Collections.Generic;

namespace Roll4d4.Klep.Core
{
    public enum KLEPExecutableScoreComponentKind
    {
        BaseAttractiveness,
        ValidationLock,
        ExecutionLock,
        ObserverInfluence,
        GoalIntrinsicAttraction,
        ProjectedSatisfaction
    }

    public readonly struct KLEPExecutableScoreComponent
    {
        internal KLEPExecutableScoreComponent(
            KLEPExecutableScoreComponentKind kind,
            string sourceId,
            float value,
            string sourceVersion = null,
            string explanation = "",
            KLEPProjectedSatisfactionEvaluation projectedSatisfaction = null)
        {
            Kind = kind;
            SourceId = sourceId ?? throw new ArgumentNullException(nameof(sourceId));
            Value = value;
            SourceVersion = sourceVersion;
            Explanation = explanation ?? string.Empty;
            ProjectedSatisfaction = projectedSatisfaction;
        }

        public KLEPExecutableScoreComponentKind Kind { get; }
        public string SourceId { get; }
        public float Value { get; }
        public string SourceVersion { get; }
        public string Explanation { get; }
        public KLEPProjectedSatisfactionEvaluation ProjectedSatisfaction { get; }
    }

    // This first score model is intentionally small and fully inspectable. Key
    // attractiveness is not included because its meaning for Any and Not Lock
    // expressions is still unresolved.
    public sealed class KLEPExecutableScoreEvaluation
    {
        internal KLEPExecutableScoreEvaluation(
            string executableId,
            IReadOnlyList<KLEPExecutableScoreComponent> components,
            float total)
        {
            ExecutableId = executableId ?? throw new ArgumentNullException(nameof(executableId));
            Components = components ?? throw new ArgumentNullException(nameof(components));
            Total = total;
        }

        public string ExecutableId { get; }
        public IReadOnlyList<KLEPExecutableScoreComponent> Components { get; }
        public float Total { get; }

        internal KLEPExecutableScoreEvaluation WithGoalIntrinsicAttraction(
            string evaluatorStableId,
            string evaluatorVersion,
            float contribution,
            string explanation)
        {
            if (string.IsNullOrWhiteSpace(evaluatorStableId))
            {
                throw new ArgumentException(
                    "A non-empty Goal-attraction evaluator stable ID is required.",
                    nameof(evaluatorStableId));
            }

            if (string.IsNullOrWhiteSpace(evaluatorVersion))
            {
                throw new ArgumentException(
                    "A non-empty Goal-attraction evaluator version is required.",
                    nameof(evaluatorVersion));
            }

            if (float.IsNaN(contribution) || float.IsInfinity(contribution))
            {
                throw new ArgumentOutOfRangeException(nameof(contribution));
            }

            double effective = (double)Total + contribution;
            if (double.IsNaN(effective) ||
                double.IsInfinity(effective) ||
                effective > float.MaxValue ||
                effective < -float.MaxValue)
            {
                throw new InvalidOperationException(
                    "Goal intrinsic attraction exceeded the finite score range.");
            }

            var copy = new List<KLEPExecutableScoreComponent>(Components.Count + 1);
            for (int index = 0; index < Components.Count; index++)
            {
                copy.Add(Components[index]);
            }

            copy.Add(new KLEPExecutableScoreComponent(
                KLEPExecutableScoreComponentKind.GoalIntrinsicAttraction,
                evaluatorStableId,
                contribution,
                evaluatorVersion,
                explanation));
            return new KLEPExecutableScoreEvaluation(
                ExecutableId,
                copy.AsReadOnly(),
                (float)effective);
        }

        internal KLEPExecutableScoreEvaluation WithObserverInfluence(
            string observerStableId,
            float scoreDelta)
        {
            if (string.IsNullOrWhiteSpace(observerStableId))
            {
                throw new ArgumentException(
                    "A non-empty Observer stable ID is required.",
                    nameof(observerStableId));
            }

            if (float.IsNaN(scoreDelta) ||
                float.IsInfinity(scoreDelta) ||
                scoreDelta <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(scoreDelta));
            }

            double effective = (double)Total + scoreDelta;
            if (double.IsNaN(effective) ||
                double.IsInfinity(effective) ||
                effective > float.MaxValue ||
                effective < -float.MaxValue)
            {
                throw new InvalidOperationException(
                    "Observer polish exceeded the finite score range.");
            }

            var copy = new List<KLEPExecutableScoreComponent>(Components.Count + 1);
            for (int index = 0; index < Components.Count; index++)
            {
                copy.Add(Components[index]);
            }

            copy.Add(new KLEPExecutableScoreComponent(
                KLEPExecutableScoreComponentKind.ObserverInfluence,
                observerStableId,
                scoreDelta));
            return new KLEPExecutableScoreEvaluation(
                ExecutableId,
                copy.AsReadOnly(),
                (float)effective);
        }

        internal KLEPExecutableScoreEvaluation WithProjectedSatisfaction(
            KLEPProjectedSatisfactionEvaluation evaluation)
        {
            if (evaluation == null)
            {
                throw new ArgumentNullException(nameof(evaluation));
            }

            if (!StringComparer.Ordinal.Equals(
                    ExecutableId, evaluation.TargetExecutableId))
            {
                throw new ArgumentException(
                    "Projected satisfaction must target the scored Executable.",
                    nameof(evaluation));
            }

            float contribution = evaluation.ScoreContribution;
            if (float.IsNaN(contribution) || float.IsInfinity(contribution))
            {
                throw new ArgumentOutOfRangeException(nameof(evaluation));
            }

            double effective = (double)Total + contribution;
            if (double.IsNaN(effective) ||
                double.IsInfinity(effective) ||
                effective > float.MaxValue ||
                effective < -float.MaxValue)
            {
                throw new InvalidOperationException(
                    "Projected satisfaction exceeded the finite score range.");
            }

            var copy = new List<KLEPExecutableScoreComponent>(Components.Count + 1);
            for (int index = 0; index < Components.Count; index++)
            {
                copy.Add(Components[index]);
            }

            copy.Add(new KLEPExecutableScoreComponent(
                KLEPExecutableScoreComponentKind.ProjectedSatisfaction,
                evaluation.PolicyStableId,
                contribution,
                evaluation.PolicyVersion,
                $"Projected satisfaction from {evaluation.Desires.Count} " +
                "authored desires.",
                evaluation));
            return new KLEPExecutableScoreEvaluation(
                ExecutableId,
                copy.AsReadOnly(),
                (float)effective);
        }
    }
}
