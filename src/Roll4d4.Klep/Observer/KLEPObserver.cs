using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Observer
{
    /// <summary>
    /// Deterministic higher-reasoning boundary. It combines project evidence,
    /// chooses one currently possible holistic direction, and returns one-use
    /// polish. It never executes behavior or mutates KLEP state.
    /// </summary>
    public sealed class KLEPObserver : IKLEPGuidanceObserver
    {
        private static readonly StringComparer IdComparer = StringComparer.Ordinal;
        private readonly ReadOnlyCollection<IKLEPObserverEvidenceSource> sources;
        private readonly ReadOnlyCollection<EvidenceSourceRegistration>
            sourceRegistrations;
        private bool isObserving;

        public KLEPObserver(
            string stableId,
            string version,
            IEnumerable<IKLEPObserverEvidenceSource> evidenceSources = null,
            KLEPObserverConfiguration configuration = null)
        {
            StableId = KLEPObserverValidation.RequireId(stableId, nameof(stableId));
            Version = KLEPObserverValidation.RequireId(version, nameof(version));
            Configuration = configuration ?? KLEPObserverConfiguration.Default;
            sourceRegistrations = CopySources(evidenceSources);
            var visibleSources = new List<IKLEPObserverEvidenceSource>(
                sourceRegistrations.Count);
            for (int index = 0; index < sourceRegistrations.Count; index++)
            {
                visibleSources.Add(sourceRegistrations[index].Source);
            }

            sources = new ReadOnlyCollection<IKLEPObserverEvidenceSource>(
                visibleSources);
        }

        public string StableId { get; }
        public string Version { get; }
        public KLEPObserverConfiguration Configuration { get; }
        public IReadOnlyList<IKLEPObserverEvidenceSource> EvidenceSources => sources;
        public KLEPObserverTrace LastTrace { get; private set; }

        public KLEPGuidanceAdvice Observe(KLEPAgentGuidanceContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (isObserving)
            {
                throw new InvalidOperationException(
                    $"Observer '{StableId}' cannot Observe recursively.");
            }

            LastTrace = null;
            isObserving = true;
            try
            {
                List<KLEPExecutableDefinition> targets =
                    CollectEligibleTargets(context);
                if (targets.Count == 0)
                {
                    LastTrace = new KLEPObserverTrace(
                        StableId,
                        Version,
                        context.Request,
                        Array.Empty<KLEPObserverTargetTrace>(),
                        null,
                        KLEPObserverAbstentionReason.NoEligibleGoalOrAction,
                        null);
                    return null;
                }

                var evidenceContext = new KLEPObserverEvidenceContext(
                    context, targets);
                var evidenceByTarget = new Dictionary<
                    string, List<KLEPObserverEvidenceTrace>>(IdComparer);
                var totals = new Dictionary<string, double>(IdComparer);
                foreach (KLEPExecutableDefinition target in targets)
                {
                    evidenceByTarget.Add(
                        target.StableId,
                        new List<KLEPObserverEvidenceTrace>());
                    totals.Add(target.StableId, 0d);
                }

                int evidenceCount = 0;
                foreach (EvidenceSourceRegistration registration in
                         sourceRegistrations)
                {
                    IKLEPObserverEvidenceSource source = registration.Source;
                    ValidateSourceIdentity(registration);
                    var suppliedTargetIds = new HashSet<string>(IdComparer);
                    IReadOnlyList<KLEPObserverEvidence> supplied =
                        source.Evaluate(evidenceContext);
                    ValidateSourceIdentity(registration);
                    if (supplied == null)
                    {
                        throw new InvalidOperationException(
                            $"Evidence source '{source.StableId}' returned null.");
                    }

                    for (int index = 0; index < supplied.Count; index++)
                    {
                        KLEPObserverEvidence item = supplied[index] ??
                            throw new InvalidOperationException(
                                $"Evidence source '{source.StableId}' returned a null item.");
                        if (!suppliedTargetIds.Add(item.TargetExecutableId))
                        {
                            throw new InvalidOperationException(
                                $"Evidence source '{source.StableId}' returned more " +
                                $"than one contribution for '{item.TargetExecutableId}'. " +
                                "Each source must aggregate one net value per target.");
                        }

                        if (!evidenceByTarget.TryGetValue(
                                item.TargetExecutableId,
                                out List<KLEPObserverEvidenceTrace> targetEvidence))
                        {
                            throw new InvalidOperationException(
                                $"Evidence source '{source.StableId}' targeted unavailable " +
                                $"Executable '{item.TargetExecutableId}'.");
                        }

                        double total = totals[item.TargetExecutableId] + item.Value;
                        if (double.IsNaN(total) ||
                            double.IsInfinity(total) ||
                            total > float.MaxValue ||
                            total < -float.MaxValue)
                        {
                            throw new InvalidOperationException(
                                $"Holistic evidence for '{item.TargetExecutableId}' " +
                                "exceeded the finite score range.");
                        }

                        targetEvidence.Add(new KLEPObserverEvidenceTrace(
                            registration.StableId,
                            registration.Version,
                            item));
                        totals[item.TargetExecutableId] = total;
                        evidenceCount++;
                    }
                }

                var traces = new List<KLEPObserverTargetTrace>(targets.Count);
                KLEPExecutableDefinition selected = null;
                double selectedValue = double.NegativeInfinity;
                foreach (KLEPExecutableDefinition target in targets)
                {
                    double holisticValue = totals[target.StableId];
                    traces.Add(new KLEPObserverTargetTrace(
                        target,
                        evidenceByTarget[target.StableId],
                        holisticValue));

                    if (holisticValue > Configuration.MinimumDirectionValue &&
                        (selected == null || holisticValue > selectedValue))
                    {
                        selected = target;
                        selectedValue = holisticValue;
                    }
                }

                if (selected == null)
                {
                    KLEPObserverAbstentionReason reason = evidenceCount == 0
                        ? KLEPObserverAbstentionReason.NoEvidence
                        : KLEPObserverAbstentionReason.NoBeneficialDirection;
                    LastTrace = new KLEPObserverTrace(
                        StableId,
                        Version,
                        context.Request,
                        traces,
                        null,
                        reason,
                        null);
                    return null;
                }

                var advice = new KLEPGuidanceAdvice(
                    StableId,
                    Version,
                    context.Request.CycleIndex,
                    context.Request.Environment,
                    selected.StableId,
                    CollectLockIds(selected),
                    Configuration.PolishAmount,
                    context.Request.EvidenceFingerprint);
                LastTrace = new KLEPObserverTrace(
                    StableId,
                    Version,
                    context.Request,
                    traces,
                    selected.StableId,
                    KLEPObserverAbstentionReason.None,
                    advice);
                return advice;
            }
            finally
            {
                isObserving = false;
            }
        }

        private static List<KLEPExecutableDefinition> CollectEligibleTargets(
            KLEPAgentGuidanceContext context)
        {
            var eligibleIds = new HashSet<string>(
                context.Request.EligibleExecutableIds,
                IdComparer);
            var targets = new List<KLEPExecutableDefinition>();
            foreach (KLEPExecutableDefinition definition in context.RootExecutables)
            {
                bool supportedKind = definition.Kind == KLEPExecutableKind.Action ||
                    definition.Kind == KLEPExecutableKind.Goal;
                if (supportedKind &&
                    definition.ExecutionMode == KLEPExecutionMode.Solo &&
                    eligibleIds.Contains(definition.StableId))
                {
                    targets.Add(definition);
                }
            }

            targets.Sort((left, right) => IdComparer.Compare(
                left.StableId, right.StableId));
            return targets;
        }

        private static IReadOnlyList<string> CollectLockIds(
            KLEPExecutableDefinition target)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(IdComparer);
            AppendLockIds(target.ValidationLocks, ids, seen);
            AppendLockIds(target.ExecutionLocks, ids, seen);
            return new ReadOnlyCollection<string>(ids);
        }

        private static void AppendLockIds(
            IReadOnlyList<KLEPLock> locks,
            List<string> ids,
            HashSet<string> seen)
        {
            for (int index = 0; index < locks.Count; index++)
            {
                if (seen.Add(locks[index].StableId))
                {
                    ids.Add(locks[index].StableId);
                }
            }
        }

        private static ReadOnlyCollection<EvidenceSourceRegistration> CopySources(
            IEnumerable<IKLEPObserverEvidenceSource> source)
        {
            var copy = new List<EvidenceSourceRegistration>();
            var ids = new HashSet<string>(IdComparer);
            if (source != null)
            {
                foreach (IKLEPObserverEvidenceSource item in source)
                {
                    if (item == null)
                    {
                        throw new ArgumentException(
                            "Observer evidence sources cannot contain null.",
                            nameof(source));
                    }

                    string stableId = KLEPObserverValidation.RequireId(
                        item.StableId, nameof(source));
                    string version = KLEPObserverValidation.RequireId(
                        item.Version, nameof(source));
                    if (!ids.Add(stableId))
                    {
                        throw new ArgumentException(
                            $"Observer evidence source ID '{stableId}' occurs more than once.",
                            nameof(source));
                    }

                    copy.Add(new EvidenceSourceRegistration(
                        item, stableId, version));
                }
            }

            copy.Sort((left, right) => IdComparer.Compare(
                left.StableId, right.StableId));
            return new ReadOnlyCollection<EvidenceSourceRegistration>(copy);
        }

        private static void ValidateSourceIdentity(
            EvidenceSourceRegistration registration)
        {
            if (!IdComparer.Equals(
                    registration.StableId,
                    registration.Source.StableId) ||
                !IdComparer.Equals(
                    registration.Version,
                    registration.Source.Version))
            {
                throw new InvalidOperationException(
                    $"Evidence source '{registration.StableId}' changed its " +
                    "stable ID or version after Observer registration.");
            }
        }

        private sealed class EvidenceSourceRegistration
        {
            internal EvidenceSourceRegistration(
                IKLEPObserverEvidenceSource source,
                string stableId,
                string version)
            {
                Source = source;
                StableId = stableId;
                Version = version;
            }

            internal IKLEPObserverEvidenceSource Source { get; }
            internal string StableId { get; }
            internal string Version { get; }
        }
    }
}
