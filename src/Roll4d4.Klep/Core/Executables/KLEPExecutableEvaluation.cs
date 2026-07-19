using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Roll4d4.Klep.Core
{
    public enum KLEPExecutableLockGroup
    {
        Validation,
        Execution
    }

    public sealed class KLEPExecutableLockGroupEvaluation
    {
        private readonly ReadOnlyCollection<string> blockedLockIds;

        internal KLEPExecutableLockGroupEvaluation(
            KLEPExecutableLockGroup group,
            IReadOnlyList<KLEPLockEvaluation> locks)
        {
            Group = group;
            Locks = locks ?? throw new ArgumentNullException(nameof(locks));

            var blocked = new List<string>();
            foreach (KLEPLockEvaluation item in Locks)
            {
                if (!item.IsSatisfied)
                {
                    blocked.Add(item.LockId);
                }
            }

            blockedLockIds = new ReadOnlyCollection<string>(blocked);
            IsSatisfied = blocked.Count == 0;
        }

        public KLEPExecutableLockGroup Group { get; }
        public bool IsSatisfied { get; }
        public IReadOnlyList<KLEPLockEvaluation> Locks { get; }
        public IReadOnlyList<string> BlockedLockIds => blockedLockIds;
    }

    public sealed class KLEPExecutableEvaluation
    {
        internal KLEPExecutableEvaluation(
            string executableId,
            KLEPExecutableLockGroupEvaluation validation,
            KLEPExecutableLockGroupEvaluation execution)
        {
            ExecutableId = executableId ?? throw new ArgumentNullException(nameof(executableId));
            Validation = validation ?? throw new ArgumentNullException(nameof(validation));
            Execution = execution ?? throw new ArgumentNullException(nameof(execution));
            IsEligible = Validation.IsSatisfied && Execution.IsSatisfied;
            Explanation = BuildExplanation();
        }

        public string ExecutableId { get; }
        public bool IsEligible { get; }
        public KLEPExecutableLockGroupEvaluation Validation { get; }
        public KLEPExecutableLockGroupEvaluation Execution { get; }
        public string Explanation { get; }

        private string BuildExplanation()
        {
            if (IsEligible)
            {
                return "Validation and Execution Lock groups are satisfied.";
            }

            var blockedGroups = new List<string>(2);
            if (!Validation.IsSatisfied)
            {
                blockedGroups.Add(
                    $"Validation blocked by [{string.Join(", ", Validation.BlockedLockIds)}]");
            }

            if (!Execution.IsSatisfied)
            {
                blockedGroups.Add(
                    $"Execution blocked by [{string.Join(", ", Execution.BlockedLockIds)}]");
            }

            return string.Join("; ", blockedGroups) + ".";
        }
    }
}
