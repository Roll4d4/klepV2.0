using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// Owns runtime Key occurrences for exactly one scope. All mutations are
    /// staged and become visible at an owner-controlled boundary. A Local
    /// owner may also publish a deterministic within-cycle tandem barrier.
    /// One explicit owner uses a store; concurrent mutation is not supported.
    /// </summary>
    public sealed class KLEPKeyStore
    {
        private readonly List<Entry> visible = new List<Entry>();
        private readonly List<KLEPKeyFact> pendingAdditions = new List<KLEPKeyFact>();
        private readonly HashSet<KLEPKeyOccurrenceId> pendingRemovals =
            new HashSet<KLEPKeyOccurrenceId>();
        // Cross-neuron exchanges publish only at the next top-level Local
        // boundary. Keeping them separate prevents an unrelated Tandem wave
        // from exposing half of an exchange inside the current Tick.
        private readonly List<KLEPKeyFact> pendingExchangeAdditions =
            new List<KLEPKeyFact>();
        private readonly HashSet<KLEPKeyOccurrenceId> pendingExchangeRemovals =
            new HashSet<KLEPKeyOccurrenceId>();
        private readonly object ownerToken = new object();
        private long nextSequence;
        private long mutationRevision;

        public KLEPKeyStore(string stableId, KLEPKeyScope scope)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("A non-empty KeyStore ID is required.", nameof(stableId));
            }

            if (!Enum.IsDefined(typeof(KLEPKeyScope), scope))
            {
                throw new ArgumentOutOfRangeException(nameof(scope));
            }

            StableId = stableId;
            Scope = scope;
        }

        public string StableId { get; }
        public KLEPKeyScope Scope { get; }
        public long LastCommittedTick { get; private set; } = -1;
        internal bool HasPendingChanges =>
            pendingAdditions.Count > 0 || pendingRemovals.Count > 0;
        internal bool Owns(KLEPKeyFact fact) =>
            fact != null && fact.IsOwnedBy(ownerToken);
        internal bool HasPendingAddition(KLEPKeyId keyId) =>
            pendingAdditions.Exists(fact => fact.KeyId == keyId);
        internal bool HasVisibleOrPendingAddition(KLEPKeyId keyId) =>
            visible.Exists(entry => entry.Fact.KeyId == keyId) ||
            pendingAdditions.Exists(fact => fact.KeyId == keyId) ||
            pendingExchangeAdditions.Exists(fact => fact.KeyId == keyId);

        internal bool CanStageExact(KLEPKeyFact fact)
        {
            return Owns(fact) && fact.IsActivated &&
                !pendingRemovals.Contains(fact.OccurrenceId) &&
                visible.Exists(entry =>
                    entry.Fact.OccurrenceId == fact.OccurrenceId);
        }

        internal bool CanAllocateOccurrences(int count)
        {
            return count >= 0 && nextSequence <= long.MaxValue - count;
        }

        internal PendingCheckpoint CapturePendingCheckpoint()
        {
            return new PendingCheckpoint(
                StableId,
                pendingAdditions,
                pendingRemovals,
                nextSequence);
        }

        internal void RestorePendingCheckpoint(PendingCheckpoint checkpoint)
        {
            if (checkpoint == null)
            {
                throw new ArgumentNullException(nameof(checkpoint));
            }

            if (!StringComparer.Ordinal.Equals(checkpoint.StoreId, StableId))
            {
                throw new InvalidOperationException(
                    "A pending-state checkpoint belongs to another KeyStore.");
            }

            pendingAdditions.Clear();
            pendingAdditions.AddRange(checkpoint.Additions);
            pendingRemovals.Clear();
            pendingRemovals.UnionWith(checkpoint.Removals);
            nextSequence = checkpoint.NextSequence;
        }

        public KLEPKeyFact CreateAndStage(
            KLEPKeyDefinition definition,
            KLEPKeyPayload payload = null,
            KLEPKeyLifetime? lifetime = null,
            string sourceId = "")
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.Scope != Scope)
            {
                throw new InvalidOperationException(
                    $"Key '{definition.Id}' is {definition.Scope} and cannot enter {Scope} store '{StableId}'.");
            }

            KLEPKeyLifetime resolvedLifetime = lifetime ?? definition.DefaultLifetime;
            if (!Enum.IsDefined(typeof(KLEPKeyLifetime), resolvedLifetime))
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            }

            var fact = new KLEPKeyFact(
                ownerToken,
                new KLEPKeyOccurrenceId(StableId, TakeNextSequence()),
                definition,
                payload,
                resolvedLifetime,
                CurrentIssuedTick,
                sourceId);
            pendingAdditions.Add(fact);
            mutationRevision++;
            return fact;
        }

        private bool StageRemove(KLEPKeyOccurrenceId occurrenceId)
        {
            if (!StringComparer.Ordinal.Equals(occurrenceId.StoreId, StableId))
            {
                return false;
            }

            // A successfully staged exchange remains an all-or-none operation
            // until its top-level boundary publishes it.
            if (pendingExchangeRemovals.Contains(occurrenceId) ||
                pendingExchangeAdditions.Exists(
                    fact => fact.OccurrenceId == occurrenceId))
            {
                return false;
            }

            int pendingIndex = pendingAdditions.FindIndex(
                fact => fact.OccurrenceId == occurrenceId);
            if (pendingIndex >= 0)
            {
                pendingAdditions.RemoveAt(pendingIndex);
                mutationRevision++;
                return true;
            }

            bool exists = visible.Exists(entry => entry.Fact.OccurrenceId == occurrenceId);
            if (!exists || !pendingRemovals.Add(occurrenceId))
            {
                return false;
            }

            mutationRevision++;
            return true;
        }

        public bool StageRemove(KLEPKeyFact fact)
        {
            if (fact == null)
            {
                throw new ArgumentNullException(nameof(fact));
            }

            return fact.IsOwnedBy(ownerToken) && StageRemove(fact.OccurrenceId);
        }

        public KLEPKeyFact ReplaceAndStage(
            KLEPKeyFact current,
            KLEPKeyPayload payload,
            KLEPKeyLifetime? lifetime = null,
            string sourceId = "")
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (!current.IsOwnedBy(ownerToken))
            {
                throw new InvalidOperationException(
                    $"Key occurrence '{current.OccurrenceId}' does not belong to store '{StableId}'.");
            }

            KLEPKeyLifetime resolvedLifetime = lifetime ?? current.Lifetime;
            if (!Enum.IsDefined(typeof(KLEPKeyLifetime), resolvedLifetime))
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            }

            if (nextSequence == long.MaxValue)
            {
                throw new InvalidOperationException(
                    $"KeyStore '{StableId}' exhausted its occurrence sequence.");
            }

            if (!StageRemove(current))
            {
                throw new InvalidOperationException(
                    $"Key occurrence '{current.OccurrenceId}' is not available to replace.");
            }

            var replacement = new KLEPKeyFact(
                ownerToken,
                new KLEPKeyOccurrenceId(StableId, TakeNextSequence()),
                current.Definition,
                current.Payload.Merge(payload),
                resolvedLifetime,
                CurrentIssuedTick,
                sourceId);
            pendingAdditions.Add(replacement);
            mutationRevision++;
            return replacement;
        }

        // The owner advances this store exactly once per world boundary. For a
        // Local store the owner is its Neuron; for a Global store it is the
        // caller's explicit world/coordinator, never an individual Neuron.
        public void CommitBoundary(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }

            if (tick < LastCommittedTick)
            {
                throw new InvalidOperationException(
                    $"KeyStore '{StableId}' cannot move backward from tick {LastCommittedTick} to {tick}.");
            }

            // Recommitting the same boundary is deliberately idempotent.
            if (tick == LastCommittedTick)
            {
                return;
            }

            ApplyPending(tick, expirePriorOneCycleFacts: true);
            LastCommittedTick = tick;
            mutationRevision++;
        }

        // Publishes Local Tandem output at a wave barrier without advancing the
        // top-level cycle or expiring OneCycle facts. Existing snapshots remain
        // immutable; the Neuron creates a new snapshot after this returns.
        internal bool CommitWithinBoundary(long tick)
        {
            if (Scope != KLEPKeyScope.Local)
            {
                throw new InvalidOperationException(
                    "Only a Neuron-owned Local KeyStore may publish a tandem wave.");
            }

            if (tick != LastCommittedTick)
            {
                throw new InvalidOperationException(
                    $"KeyStore '{StableId}' can publish wave changes only at its " +
                    $"current boundary {LastCommittedTick}, not {tick}.");
            }

            bool changed = ApplyPending(tick, expirePriorOneCycleFacts: false);
            if (changed)
            {
                mutationRevision++;
            }

            return changed;
        }

        internal void AppendVisibleFacts(List<KLEPKeyFact> destination)
        {
            foreach (Entry entry in visible)
            {
                destination.Add(entry.Fact);
            }
        }

        internal bool OwnsAvailableActivatedFact(KLEPKeyFact fact)
        {
            if (fact == null || !fact.IsActivated || !fact.IsOwnedBy(ownerToken) ||
                pendingRemovals.Contains(fact.OccurrenceId) ||
                pendingExchangeRemovals.Contains(fact.OccurrenceId))
            {
                return false;
            }

            return visible.Exists(entry => entry.Fact.OccurrenceId == fact.OccurrenceId);
        }

        internal bool CanPrepareExchangeDeliveries(int deliveryCount)
        {
            return deliveryCount >= 0 && nextSequence <= long.MaxValue - deliveryCount;
        }

        internal KLEPKeyStorePreparedBatch PrepareExchangeBatch(
            IReadOnlyList<KLEPKeyFact> removals,
            IReadOnlyList<KLEPKeyFact> deliverySources)
        {
            removals = removals ?? Array.Empty<KLEPKeyFact>();
            deliverySources = deliverySources ?? Array.Empty<KLEPKeyFact>();

            if (!CanPrepareExchangeDeliveries(deliverySources.Count))
            {
                throw new InvalidOperationException(
                    $"KeyStore '{StableId}' cannot allocate {deliverySources.Count} exchange occurrence(s).");
            }

            var seenRemovalIds = new HashSet<KLEPKeyOccurrenceId>();
            var orderedRemovalIds = new List<KLEPKeyOccurrenceId>(removals.Count);
            foreach (KLEPKeyFact fact in removals)
            {
                if (!OwnsAvailableActivatedFact(fact))
                {
                    throw new InvalidOperationException(
                        $"Key occurrence '{fact?.OccurrenceId}' is not available for exchange removal " +
                        $"from store '{StableId}'.");
                }

                if (!seenRemovalIds.Add(fact.OccurrenceId))
                {
                    throw new InvalidOperationException(
                        $"Key occurrence '{fact.OccurrenceId}' appears more than once in one exchange batch.");
                }

                orderedRemovalIds.Add(fact.OccurrenceId);
            }

            var additions = new List<KLEPKeyFact>(deliverySources.Count);
            long preparedSequence = nextSequence;
            foreach (KLEPKeyFact source in deliverySources)
            {
                if (source == null)
                {
                    throw new ArgumentException(
                        "An exchange delivery source cannot be null.", nameof(deliverySources));
                }

                if (source.Definition.Scope != Scope)
                {
                    throw new InvalidOperationException(
                        $"Key '{source.KeyId}' is {source.Scope} and cannot enter " +
                        $"{Scope} store '{StableId}'.");
                }

                preparedSequence++;
                additions.Add(new KLEPKeyFact(
                    ownerToken,
                    new KLEPKeyOccurrenceId(StableId, preparedSequence),
                    source.Definition,
                    source.Payload,
                    source.Lifetime,
                    CurrentIssuedTick,
                    source.SourceId));
            }

            return new KLEPKeyStorePreparedBatch(
                this,
                mutationRevision,
                nextSequence,
                preparedSequence,
                new ReadOnlyCollection<KLEPKeyOccurrenceId>(orderedRemovalIds),
                new ReadOnlyCollection<KLEPKeyFact>(additions));
        }

        internal bool CanApplyExchangeBatch(KLEPKeyStorePreparedBatch batch)
        {
            return batch != null && ReferenceEquals(batch.Store, this) &&
                batch.PreparedRevision == mutationRevision &&
                batch.StartingSequence == nextSequence;
        }

        internal void ApplyExchangeBatch(KLEPKeyStorePreparedBatch batch)
        {
            if (!CanApplyExchangeBatch(batch))
            {
                throw new InvalidOperationException(
                    $"KeyStore '{StableId}' changed after its exchange batch was prepared.");
            }

            foreach (KLEPKeyOccurrenceId occurrenceId in batch.Removals)
            {
                pendingExchangeRemovals.Add(occurrenceId);
            }

            foreach (KLEPKeyFact addition in batch.Additions)
            {
                pendingExchangeAdditions.Add(addition);
            }

            nextSequence = batch.FinalSequence;
            mutationRevision++;
        }

        private long CurrentIssuedTick => LastCommittedTick < 0 ? 0 : LastCommittedTick;

        private bool ApplyPending(long tick, bool expirePriorOneCycleFacts)
        {
            // A top-level boundary advances to a new tick. A Tandem wave keeps
            // the current tick and must leave exchange staging deferred.
            bool includeExchanges = tick != LastCommittedTick;
            int removed = visible.RemoveAll(entry =>
                pendingRemovals.Contains(entry.Fact.OccurrenceId) ||
                (includeExchanges &&
                    pendingExchangeRemovals.Contains(entry.Fact.OccurrenceId)) ||
                (expirePriorOneCycleFacts &&
                    entry.Fact.Lifetime == KLEPKeyLifetime.OneCycle &&
                    entry.Fact.ActivatedTick < tick));

            int added = pendingAdditions.Count;
            foreach (KLEPKeyFact addition in pendingAdditions)
            {
                visible.Add(new Entry(addition.Activate(tick)));
            }

            if (includeExchanges)
            {
                added += pendingExchangeAdditions.Count;
                foreach (KLEPKeyFact addition in pendingExchangeAdditions)
                {
                    visible.Add(new Entry(addition.Activate(tick)));
                }
            }

            pendingRemovals.Clear();
            pendingAdditions.Clear();
            if (includeExchanges)
            {
                pendingExchangeRemovals.Clear();
                pendingExchangeAdditions.Clear();
            }

            return removed > 0 || added > 0;
        }

        private long TakeNextSequence()
        {
            if (nextSequence == long.MaxValue)
            {
                throw new InvalidOperationException(
                    $"KeyStore '{StableId}' exhausted its occurrence sequence.");
            }

            nextSequence++;
            return nextSequence;
        }

        private readonly struct Entry
        {
            public Entry(KLEPKeyFact fact)
            {
                Fact = fact;
            }

            public KLEPKeyFact Fact { get; }
        }

        internal sealed class PendingCheckpoint
        {
            internal PendingCheckpoint(
                string storeId,
                IEnumerable<KLEPKeyFact> additions,
                IEnumerable<KLEPKeyOccurrenceId> removals,
                long nextSequence)
            {
                StoreId = storeId;
                Additions = new List<KLEPKeyFact>(additions);
                Removals = new HashSet<KLEPKeyOccurrenceId>(removals);
                NextSequence = nextSequence;
            }

            internal string StoreId { get; }
            internal List<KLEPKeyFact> Additions { get; }
            internal HashSet<KLEPKeyOccurrenceId> Removals { get; }
            internal long NextSequence { get; }
        }
    }

    internal sealed class KLEPKeyStorePreparedBatch
    {
        internal KLEPKeyStorePreparedBatch(
            KLEPKeyStore store,
            long preparedRevision,
            long startingSequence,
            long finalSequence,
            IReadOnlyList<KLEPKeyOccurrenceId> removals,
            IReadOnlyList<KLEPKeyFact> additions)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
            PreparedRevision = preparedRevision;
            StartingSequence = startingSequence;
            FinalSequence = finalSequence;
            Removals = removals ?? throw new ArgumentNullException(nameof(removals));
            Additions = additions ?? throw new ArgumentNullException(nameof(additions));
        }

        internal KLEPKeyStore Store { get; }
        internal long PreparedRevision { get; }
        internal long StartingSequence { get; }
        internal long FinalSequence { get; }
        internal IReadOnlyList<KLEPKeyOccurrenceId> Removals { get; }
        internal IReadOnlyList<KLEPKeyFact> Additions { get; }
    }

    public sealed class KLEPKeySnapshot : IKLEPLockKeySource
    {
        private readonly ReadOnlyCollection<KLEPKeyFact> facts;

        private KLEPKeySnapshot(long tick)
        {
            Tick = tick;
            WaveIndex = 0;
            facts = new ReadOnlyCollection<KLEPKeyFact>(new List<KLEPKeyFact>());
        }

        internal static KLEPKeySnapshot Empty { get; } = new KLEPKeySnapshot(0);

        internal KLEPKeySnapshot(
            long tick,
            KLEPKeyStore localStore,
            KLEPKeyStore globalStore = null,
            int waveIndex = 0)
        {
            if (localStore == null)
            {
                throw new ArgumentNullException(nameof(localStore));
            }

            if (waveIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }

            Tick = tick;
            WaveIndex = waveIndex;
            var copy = new List<KLEPKeyFact>();
            localStore.AppendVisibleFacts(copy);
            globalStore?.AppendVisibleFacts(copy);
            copy.Sort(CompareFacts);

            for (int index = 1; index < copy.Count; index++)
            {
                KLEPKeyFact previous = copy[index - 1];
                KLEPKeyFact current = copy[index];
                if (previous.KeyId == current.KeyId && previous.Scope != current.Scope)
                {
                    throw new InvalidOperationException(
                        $"Key ID '{current.KeyId}' is visible as both {previous.Scope} and " +
                        $"{current.Scope}. One stable Key ID must have one scope.");
                }
            }

            facts = new ReadOnlyCollection<KLEPKeyFact>(copy);
        }

        public long Tick { get; }
        public int WaveIndex { get; }
        public IReadOnlyList<KLEPKeyFact> Facts => facts;

        public bool Contains(string stableKeyId)
        {
            if (string.IsNullOrWhiteSpace(stableKeyId))
            {
                return false;
            }

            return Contains(new KLEPKeyId(stableKeyId));
        }

        public bool Contains(KLEPKeyId keyId)
        {
            foreach (KLEPKeyFact fact in facts)
            {
                if (fact.KeyId == keyId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetFirst(KLEPKeyId keyId, out KLEPKeyFact fact)
        {
            foreach (KLEPKeyFact candidate in facts)
            {
                if (candidate.KeyId == keyId)
                {
                    fact = candidate;
                    return true;
                }
            }

            fact = null;
            return false;
        }

        public IReadOnlyList<KLEPKeyFact> FindAll(KLEPKeyId keyId)
        {
            var matches = new List<KLEPKeyFact>();
            foreach (KLEPKeyFact fact in facts)
            {
                if (fact.KeyId == keyId)
                {
                    matches.Add(fact);
                }
            }

            return new ReadOnlyCollection<KLEPKeyFact>(matches);
        }

        private static int CompareFacts(KLEPKeyFact left, KLEPKeyFact right)
        {
            int keyComparison = left.KeyId.CompareTo(right.KeyId);
            return keyComparison != 0
                ? keyComparison
                : left.OccurrenceId.CompareTo(right.OccurrenceId);
        }
    }
}
