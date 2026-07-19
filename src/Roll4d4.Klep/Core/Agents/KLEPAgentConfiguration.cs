using System;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// Immutable parameters for the first tabular Agent-confidence model.
    /// Confidence and the guidance request are diagnostic. Optional Observer
    /// advice is a separate one-use influence boundary. ActionCertaintyThreshold
    /// is ordinary authored Neuron arbitration configuration, not learned bias.
    /// </summary>
    public sealed class KLEPAgentConfiguration
    {
        public KLEPAgentConfiguration(
            float actionCertaintyThreshold = 0f,
            float guidanceConfidenceThreshold = 0f,
            float learningRate = 0.2f,
            float discountFactor = 0.9f,
            float successReward = 1f,
            float failureReward = -1f,
            float interruptionReward = -0.25f,
            float familiarityScale = 4f)
        {
            RequireFinite(actionCertaintyThreshold, nameof(actionCertaintyThreshold));
            RequireRange(
                guidanceConfidenceThreshold,
                0f,
                1f,
                nameof(guidanceConfidenceThreshold));
            RequireRange(learningRate, float.Epsilon, 1f, nameof(learningRate));
            RequireRange(discountFactor, 0f, 1f, nameof(discountFactor), false);
            RequireFinite(successReward, nameof(successReward));
            RequireFinite(failureReward, nameof(failureReward));
            RequireFinite(interruptionReward, nameof(interruptionReward));
            RequireFinite(familiarityScale, nameof(familiarityScale));

            if (successReward <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(successReward));
            }

            if (failureReward > 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(failureReward));
            }

            if (interruptionReward > 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(interruptionReward));
            }

            if (familiarityScale <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(familiarityScale));
            }

            double positiveQBound =
                (double)successReward / (1d - discountFactor);
            if (double.IsNaN(positiveQBound) ||
                double.IsInfinity(positiveQBound) ||
                positiveQBound > float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(successReward),
                    "The configured positive Q bound must fit in a finite Single.");
            }

            float lowestReward = Math.Min(failureReward, interruptionReward);
            double negativeQBound =
                (double)lowestReward / (1d - discountFactor);
            if (double.IsNaN(negativeQBound) ||
                double.IsInfinity(negativeQBound) ||
                negativeQBound < -float.MaxValue)
            {
                string parameterName = failureReward <= interruptionReward
                    ? nameof(failureReward)
                    : nameof(interruptionReward);
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "The configured negative Q bound must fit in a finite Single.");
            }

            ActionCertaintyThreshold = actionCertaintyThreshold;
            GuidanceConfidenceThreshold = guidanceConfidenceThreshold;
            LearningRate = learningRate;
            DiscountFactor = discountFactor;
            SuccessReward = successReward;
            FailureReward = failureReward;
            InterruptionReward = interruptionReward;
            FamiliarityScale = familiarityScale;
            PositiveQBound = (float)positiveQBound;
        }

        public static KLEPAgentConfiguration Default { get; } =
            new KLEPAgentConfiguration();

        public float ActionCertaintyThreshold { get; }
        public float GuidanceConfidenceThreshold { get; }
        public float LearningRate { get; }
        public float DiscountFactor { get; }
        public float SuccessReward { get; }
        public float FailureReward { get; }
        public float InterruptionReward { get; }
        public float FamiliarityScale { get; }

        internal float PositiveQBound { get; }

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void RequireRange(
            float value,
            float minimum,
            float maximum,
            string parameterName,
            bool includeMaximum = true)
        {
            RequireFinite(value, parameterName);
            if (value < minimum ||
                (includeMaximum ? value > maximum : value >= maximum))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
