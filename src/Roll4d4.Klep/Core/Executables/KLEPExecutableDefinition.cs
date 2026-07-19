using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Roll4d4.Klep.Core
{
    // This is authored definition data. Per-run state belongs to a Neuron- or
    // Goal-owned lifecycle record, never to this object.
    public sealed class KLEPExecutableDefinition
    {
        private readonly ReadOnlyCollection<KLEPLock> validationLocks;
        private readonly ReadOnlyCollection<KLEPLock> executionLocks;
        private readonly ReadOnlyCollection<KLEPKeyDefinition> declaredOutputs;
        private readonly Dictionary<KLEPKeyId, KLEPKeyDefinition> declaredOutputsById;
        private readonly KLEPExecutableScoreEvaluation score;

        public KLEPExecutableDefinition(
            string stableId,
            string displayName,
            KLEPExecutableKind kind,
            IEnumerable<KLEPLock> validationLocks = null,
            IEnumerable<KLEPLock> executionLocks = null,
            float baseAttractiveness = 0f,
            KLEPExecutionMode executionMode = KLEPExecutionMode.Solo,
            IEnumerable<KLEPKeyDefinition> declaredOutputs = null)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException(
                    "A non-empty stable Executable ID is required.", nameof(stableId));
            }

            if (!Enum.IsDefined(typeof(KLEPExecutableKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (float.IsNaN(baseAttractiveness) || float.IsInfinity(baseAttractiveness))
            {
                throw new ArgumentOutOfRangeException(nameof(baseAttractiveness));
            }

            if (!Enum.IsDefined(typeof(KLEPExecutionMode), executionMode))
            {
                throw new ArgumentOutOfRangeException(nameof(executionMode));
            }

            StableId = stableId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? stableId : displayName;
            Kind = kind;
            BaseAttractiveness = baseAttractiveness;
            ExecutionMode = executionMode;
            this.validationLocks = CopyLocks(validationLocks, nameof(validationLocks));
            this.executionLocks = CopyLocks(executionLocks, nameof(executionLocks));
            declaredOutputsById = new Dictionary<KLEPKeyId, KLEPKeyDefinition>();
            this.declaredOutputs = CopyDeclaredOutputs(
                declaredOutputs, declaredOutputsById, nameof(declaredOutputs));
            score = BuildScore();
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public KLEPExecutableKind Kind { get; }
        public float BaseAttractiveness { get; }
        public KLEPExecutionMode ExecutionMode { get; }
        public IReadOnlyList<KLEPLock> ValidationLocks => validationLocks;
        public IReadOnlyList<KLEPLock> ExecutionLocks => executionLocks;
        public IReadOnlyList<KLEPKeyDefinition> DeclaredOutputs => declaredOutputs;

        public bool DeclaresOutput(KLEPKeyId keyId)
        {
            return declaredOutputsById.ContainsKey(keyId);
        }

        public bool TryGetDeclaredOutput(
            KLEPKeyId keyId,
            out KLEPKeyDefinition definition)
        {
            return declaredOutputsById.TryGetValue(keyId, out definition);
        }

        public KLEPExecutableEvaluation Evaluate(KLEPKeySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            // Both groups deliberately receive the same frozen snapshot. Every
            // Lock is evaluated so the diagnostic result is complete.
            KLEPExecutableLockGroupEvaluation validation = EvaluateGroup(
                KLEPExecutableLockGroup.Validation, validationLocks, snapshot);
            KLEPExecutableLockGroupEvaluation execution = EvaluateGroup(
                KLEPExecutableLockGroup.Execution, executionLocks, snapshot);

            return new KLEPExecutableEvaluation(StableId, validation, execution);
        }

        internal KLEPExecutableScoreEvaluation EvaluateScore(KLEPKeySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return score;
        }

        private static ReadOnlyCollection<KLEPLock> CopyLocks(
            IEnumerable<KLEPLock> source,
            string parameterName)
        {
            var copy = new List<KLEPLock>();
            if (source != null)
            {
                foreach (KLEPLock item in source)
                {
                    copy.Add(item ?? throw new ArgumentException(
                        "An Executable Lock group cannot contain null.", parameterName));
                }
            }

            return new ReadOnlyCollection<KLEPLock>(copy);
        }

        private static ReadOnlyCollection<KLEPKeyDefinition> CopyDeclaredOutputs(
            IEnumerable<KLEPKeyDefinition> source,
            Dictionary<KLEPKeyId, KLEPKeyDefinition> byId,
            string parameterName)
        {
            var copy = new List<KLEPKeyDefinition>();
            if (source != null)
            {
                foreach (KLEPKeyDefinition item in source)
                {
                    if (item == null)
                    {
                        throw new ArgumentException(
                            "Declared Executable outputs cannot contain null.",
                            parameterName);
                    }

                    if (byId.ContainsKey(item.Id))
                    {
                        throw new ArgumentException(
                            $"Key '{item.Id}' is declared more than once by the Executable.",
                            parameterName);
                    }

                    byId.Add(item.Id, item);
                    copy.Add(item);
                }
            }

            return new ReadOnlyCollection<KLEPKeyDefinition>(copy);
        }

        private static KLEPExecutableLockGroupEvaluation EvaluateGroup(
            KLEPExecutableLockGroup group,
            IReadOnlyList<KLEPLock> locks,
            KLEPKeySnapshot snapshot)
        {
            var evaluations = new List<KLEPLockEvaluation>(locks.Count);
            foreach (KLEPLock item in locks)
            {
                evaluations.Add(item.Evaluate(snapshot));
            }

            return new KLEPExecutableLockGroupEvaluation(
                group, new ReadOnlyCollection<KLEPLockEvaluation>(evaluations));
        }

        private KLEPExecutableScoreEvaluation BuildScore()
        {
            var components = new List<KLEPExecutableScoreComponent>(
                1 + validationLocks.Count + executionLocks.Count)
            {
                new KLEPExecutableScoreComponent(
                    KLEPExecutableScoreComponentKind.BaseAttractiveness,
                    StableId,
                    BaseAttractiveness)
            };
            double total = BaseAttractiveness;

            AppendLockScores(
                components,
                validationLocks,
                KLEPExecutableScoreComponentKind.ValidationLock,
                ref total);
            AppendLockScores(
                components,
                executionLocks,
                KLEPExecutableScoreComponentKind.ExecutionLock,
                ref total);

            if (double.IsNaN(total) || double.IsInfinity(total) ||
                total > float.MaxValue || total < -float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(BaseAttractiveness),
                    "The combined Executable and Lock attractiveness must be a finite Single.");
            }

            return new KLEPExecutableScoreEvaluation(
                StableId,
                new ReadOnlyCollection<KLEPExecutableScoreComponent>(components),
                (float)total);
        }

        private static void AppendLockScores(
            List<KLEPExecutableScoreComponent> components,
            IReadOnlyList<KLEPLock> locks,
            KLEPExecutableScoreComponentKind kind,
            ref double total)
        {
            foreach (KLEPLock item in locks)
            {
                components.Add(new KLEPExecutableScoreComponent(
                    kind, item.StableId, item.Attractiveness));
                total += item.Attractiveness;
            }
        }
    }

    // The old name was KlapType. This corrected name remains diagnostic only;
    // it does not grant scheduling or lifecycle behavior.
    public enum KLEPExecutableKind
    {
        Action,
        Goal,
        Router,
        Sensor
    }

    public enum KLEPExecutionMode
    {
        Solo,
        Tandem
    }
}
