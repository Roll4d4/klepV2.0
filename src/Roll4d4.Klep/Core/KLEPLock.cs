using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// The read-only boundary a Lock needs from the future KeySnapshot.
    /// Lock evaluation neither owns nor mutates Keys.
    /// </summary>
    public interface IKLEPLockKeySource
    {
        bool Contains(string stableKeyId);
    }

    public sealed class KLEPLock
    {
        public KLEPLock(
            string stableId,
            string displayName,
            KLEPLockExpression expression,
            float attractiveness = 0f)
        {
            StableId = RequireId(stableId, nameof(stableId));
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? StableId : displayName;
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));

            if (float.IsNaN(attractiveness) || float.IsInfinity(attractiveness))
            {
                throw new ArgumentOutOfRangeException(nameof(attractiveness));
            }

            Attractiveness = attractiveness;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public float Attractiveness { get; }
        public KLEPLockExpression Expression { get; }

        public KLEPLockEvaluation Evaluate(IKLEPLockKeySource keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            var results = new List<KLEPLockExpressionResult>();
            bool satisfied = Expression.Evaluate(keys, "root", results);
            string explanation = satisfied
                ? $"Lock '{DisplayName}' is satisfied."
                : $"Lock '{DisplayName}' is blocked by its {Expression.Kind} expression.";

            return new KLEPLockEvaluation(
                StableId,
                satisfied,
                new ReadOnlyCollection<KLEPLockExpressionResult>(results),
                explanation);
        }

        // Convenience overload for the Neuron's immutable cycle snapshot.
        public KLEPLockEvaluation Evaluate(KLEPKeySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return Evaluate((IKLEPLockKeySource)snapshot);
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty stable ID is required.", parameterName);
            }

            return value;
        }

    }

    public enum KLEPLockExpressionKind
    {
        KeyPresent,
        All,
        Any,
        Not
    }

    public abstract class KLEPLockExpression
    {
        public abstract KLEPLockExpressionKind Kind { get; }

        internal abstract bool Evaluate(
            IKLEPLockKeySource keys,
            string path,
            List<KLEPLockExpressionResult> results);

        protected static ReadOnlyCollection<KLEPLockExpression> CopyChildren(
            KLEPLockExpression[] children)
        {
            if (children == null)
            {
                throw new ArgumentNullException(nameof(children));
            }

            var copy = new List<KLEPLockExpression>(children.Length);
            foreach (KLEPLockExpression child in children)
            {
                copy.Add(child ?? throw new ArgumentException(
                    "A Lock expression cannot contain a null child.", nameof(children)));
            }

            return new ReadOnlyCollection<KLEPLockExpression>(copy);
        }
    }

    public sealed class KLEPKeyPresent : KLEPLockExpression
    {
        public KLEPKeyPresent(string stableKeyId)
        {
            if (string.IsNullOrWhiteSpace(stableKeyId))
            {
                throw new ArgumentException(
                    "A non-empty stable Key ID is required.", nameof(stableKeyId));
            }

            StableKeyId = stableKeyId;
        }

        public string StableKeyId { get; }
        public override KLEPLockExpressionKind Kind => KLEPLockExpressionKind.KeyPresent;

        internal override bool Evaluate(
            IKLEPLockKeySource keys,
            string path,
            List<KLEPLockExpressionResult> results)
        {
            bool satisfied = keys.Contains(StableKeyId);
            results.Add(new KLEPLockExpressionResult(path, Kind, StableKeyId, satisfied));
            return satisfied;
        }
    }

    public sealed class KLEPAll : KLEPLockExpression
    {
        private readonly ReadOnlyCollection<KLEPLockExpression> children;

        public KLEPAll(params KLEPLockExpression[] children)
        {
            this.children = CopyChildren(children);
        }

        public IReadOnlyList<KLEPLockExpression> Children => children;
        public override KLEPLockExpressionKind Kind => KLEPLockExpressionKind.All;

        internal override bool Evaluate(
            IKLEPLockKeySource keys,
            string path,
            List<KLEPLockExpressionResult> results)
        {
            bool satisfied = true;
            for (int index = 0; index < children.Count; index++)
            {
                satisfied &= children[index].Evaluate(
                    keys, $"{path}.all[{index}]", results);
            }

            results.Add(new KLEPLockExpressionResult(path, Kind, null, satisfied));
            return satisfied;
        }
    }

    public sealed class KLEPAny : KLEPLockExpression
    {
        private readonly ReadOnlyCollection<KLEPLockExpression> children;

        public KLEPAny(params KLEPLockExpression[] children)
        {
            this.children = CopyChildren(children);
        }

        public IReadOnlyList<KLEPLockExpression> Children => children;
        public override KLEPLockExpressionKind Kind => KLEPLockExpressionKind.Any;

        internal override bool Evaluate(
            IKLEPLockKeySource keys,
            string path,
            List<KLEPLockExpressionResult> results)
        {
            bool satisfied = false;
            for (int index = 0; index < children.Count; index++)
            {
                satisfied |= children[index].Evaluate(
                    keys, $"{path}.any[{index}]", results);
            }

            results.Add(new KLEPLockExpressionResult(path, Kind, null, satisfied));
            return satisfied;
        }
    }

    public sealed class KLEPNot : KLEPLockExpression
    {
        public KLEPNot(KLEPLockExpression child)
        {
            Child = child ?? throw new ArgumentNullException(nameof(child));
        }

        public KLEPLockExpression Child { get; }
        public override KLEPLockExpressionKind Kind => KLEPLockExpressionKind.Not;

        internal override bool Evaluate(
            IKLEPLockKeySource keys,
            string path,
            List<KLEPLockExpressionResult> results)
        {
            bool satisfied = !Child.Evaluate(keys, $"{path}.not", results);
            results.Add(new KLEPLockExpressionResult(path, Kind, null, satisfied));
            return satisfied;
        }
    }

    public readonly struct KLEPLockExpressionResult
    {
        public KLEPLockExpressionResult(
            string path,
            KLEPLockExpressionKind kind,
            string stableKeyId,
            bool isSatisfied)
        {
            Path = path;
            Kind = kind;
            StableKeyId = stableKeyId;
            IsSatisfied = isSatisfied;
        }

        public string Path { get; }
        public KLEPLockExpressionKind Kind { get; }
        public string StableKeyId { get; }
        public bool IsSatisfied { get; }
    }

    public sealed class KLEPLockEvaluation
    {
        internal KLEPLockEvaluation(
            string lockId,
            bool isSatisfied,
            IReadOnlyList<KLEPLockExpressionResult> results,
            string explanation)
        {
            LockId = lockId;
            IsSatisfied = isSatisfied;
            Results = results;
            Explanation = explanation;
        }

        public string LockId { get; }
        public bool IsSatisfied { get; }
        public IReadOnlyList<KLEPLockExpressionResult> Results { get; }
        public string Explanation { get; }
    }
}
