using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Observer
{
    public sealed class KLEPObserverConfiguration
    {
        public KLEPObserverConfiguration(
            float polishAmount = 1f,
            float minimumDirectionValue = 0f)
        {
            if (float.IsNaN(polishAmount) ||
                float.IsInfinity(polishAmount) ||
                polishAmount <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(polishAmount));
            }

            if (float.IsNaN(minimumDirectionValue) ||
                float.IsInfinity(minimumDirectionValue) ||
                minimumDirectionValue < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumDirectionValue));
            }

            PolishAmount = polishAmount;
            MinimumDirectionValue = minimumDirectionValue;
        }

        public static KLEPObserverConfiguration Default { get; } =
            new KLEPObserverConfiguration();

        public float PolishAmount { get; }
        public float MinimumDirectionValue { get; }
    }

    /// <summary>
    /// One project-owned holistic consideration. The Observer supplies source
    /// provenance from the evidence provider that returned it.
    /// </summary>
    public sealed class KLEPObserverEvidence
    {
        public KLEPObserverEvidence(
            string targetExecutableId,
            float value,
            string explanation = "")
        {
            TargetExecutableId = KLEPObserverValidation.RequireId(
                targetExecutableId, nameof(targetExecutableId));
            Value = KLEPObserverValidation.RequireFinite(value, nameof(value));
            Explanation = explanation ?? string.Empty;
        }

        public string TargetExecutableId { get; }
        public float Value { get; }
        public string Explanation { get; }
    }

    public interface IKLEPObserverEvidenceSource
    {
        string StableId { get; }
        string Version { get; }
        IReadOnlyList<KLEPObserverEvidence> Evaluate(
            KLEPObserverEvidenceContext context);
    }

    /// <summary>
    /// Read-only view presented to project evidence providers. The target list
    /// contains only currently eligible root Solo Goals and actions.
    /// </summary>
    public sealed class KLEPObserverEvidenceContext
    {
        private readonly ReadOnlyCollection<KLEPExecutableDefinition> targets;

        internal KLEPObserverEvidenceContext(
            KLEPAgentGuidanceContext guidance,
            IEnumerable<KLEPExecutableDefinition> targets)
        {
            Guidance = guidance ?? throw new ArgumentNullException(nameof(guidance));
            var copy = new List<KLEPExecutableDefinition>();
            if (targets != null)
            {
                foreach (KLEPExecutableDefinition target in targets)
                {
                    copy.Add(target ?? throw new ArgumentException(
                        "Observer targets cannot contain null.", nameof(targets)));
                }
            }

            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.StableId, right.StableId));
            this.targets = new ReadOnlyCollection<KLEPExecutableDefinition>(copy);
        }

        public KLEPAgentGuidanceContext Guidance { get; }
        public KLEPAgentTickTrace AgentTrace => Guidance.Trace;
        public KLEPGuidanceRequest Request => Guidance.Request;
        public KLEPKeySnapshot KeySnapshot => Guidance.Trace.Decision.KeySnapshot;
        public IReadOnlyList<KLEPExecutableDefinition> EligibleTargets => targets;
        public IReadOnlyList<KLEPAgentExperience> AgentExperiences =>
            Guidance.Experiences;
    }

    public sealed class KLEPObserverEvidenceTrace
    {
        internal KLEPObserverEvidenceTrace(
            string sourceId,
            string sourceVersion,
            KLEPObserverEvidence evidence)
        {
            SourceId = sourceId;
            SourceVersion = sourceVersion;
            TargetExecutableId = evidence.TargetExecutableId;
            Value = evidence.Value;
            Explanation = evidence.Explanation;
        }

        public string SourceId { get; }
        public string SourceVersion { get; }
        public string TargetExecutableId { get; }
        public float Value { get; }
        public string Explanation { get; }
    }

    public sealed class KLEPObserverTargetTrace
    {
        private readonly ReadOnlyCollection<KLEPObserverEvidenceTrace> evidence;

        internal KLEPObserverTargetTrace(
            KLEPExecutableDefinition definition,
            IEnumerable<KLEPObserverEvidenceTrace> evidence,
            double holisticValue)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            var copy = new List<KLEPObserverEvidenceTrace>();
            if (evidence != null)
            {
                foreach (KLEPObserverEvidenceTrace item in evidence)
                {
                    copy.Add(item ?? throw new ArgumentException(
                        "Observer target evidence cannot contain null.",
                        nameof(evidence)));
                }
            }

            this.evidence = new ReadOnlyCollection<KLEPObserverEvidenceTrace>(copy);
            HolisticValue = KLEPObserverValidation.RequireFinite(
                holisticValue, nameof(holisticValue));
        }

        public KLEPExecutableDefinition Definition { get; }
        public string ExecutableStableId => Definition.StableId;
        public KLEPExecutableKind Kind => Definition.Kind;
        public IReadOnlyList<KLEPObserverEvidenceTrace> Evidence => evidence;
        public double HolisticValue { get; }
    }

    public enum KLEPObserverAbstentionReason
    {
        None,
        NoEligibleGoalOrAction,
        NoEvidence,
        NoBeneficialDirection
    }

    public sealed class KLEPObserverTrace
    {
        private readonly ReadOnlyCollection<KLEPObserverTargetTrace> targets;

        internal KLEPObserverTrace(
            string observerStableId,
            string observerVersion,
            KLEPGuidanceRequest request,
            IEnumerable<KLEPObserverTargetTrace> targets,
            string selectedExecutableId,
            KLEPObserverAbstentionReason abstentionReason,
            KLEPGuidanceAdvice advice)
        {
            ObserverStableId = observerStableId;
            ObserverVersion = observerVersion;
            Request = request ?? throw new ArgumentNullException(nameof(request));
            var copy = new List<KLEPObserverTargetTrace>();
            if (targets != null)
            {
                foreach (KLEPObserverTargetTrace target in targets)
                {
                    copy.Add(target ?? throw new ArgumentException(
                        "Observer traces cannot contain a null target.",
                        nameof(targets)));
                }
            }

            this.targets = new ReadOnlyCollection<KLEPObserverTargetTrace>(copy);
            SelectedExecutableId = selectedExecutableId;
            AbstentionReason = abstentionReason;
            Advice = advice;
        }

        public string ObserverStableId { get; }
        public string ObserverVersion { get; }
        public KLEPGuidanceRequest Request { get; }
        public IReadOnlyList<KLEPObserverTargetTrace> Targets => targets;
        public string SelectedExecutableId { get; }
        public KLEPObserverAbstentionReason AbstentionReason { get; }
        public KLEPGuidanceAdvice Advice { get; }
        public bool ProvidedDirection => Advice != null;
    }

    internal static class KLEPObserverValidation
    {
        internal static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable ID is required.", parameterName);
            }

            return value;
        }

        internal static float RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static double RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }
}
