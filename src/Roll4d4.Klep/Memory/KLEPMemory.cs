using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Emotion;

[assembly: InternalsVisibleTo("Roll4d4.Klep.Cognition")]

namespace Roll4d4.Klep.Memory
{
    /// <summary>
    /// Owner-bound, Tick-driven episodic Memory. It observes immutable records
    /// supplied by its host and never produces Keys or mutates Core state.
    /// </summary>
    public sealed class KLEPMemory
    {
        private sealed class TransactionCheckpoint
        {
            internal TransactionCheckpoint(
                KLEPMemory owner,
                IReadOnlyList<MutableCluster> clusters,
                IReadOnlyCollection<string> seenExperienceIds,
                IReadOnlyList<KLEPMemorySnapshot> snapshotHistory,
                long nextClusterSequence,
                long currentTick,
                KLEPMemorySnapshot snapshot)
            {
                Owner = owner;
                Clusters = clusters;
                SeenExperienceIds = seenExperienceIds;
                SnapshotHistory = snapshotHistory;
                NextClusterSequence = nextClusterSequence;
                CurrentTick = currentTick;
                Snapshot = snapshot;
            }

            internal KLEPMemory Owner { get; }
            internal IReadOnlyList<MutableCluster> Clusters { get; }
            internal IReadOnlyCollection<string> SeenExperienceIds { get; }
            internal IReadOnlyList<KLEPMemorySnapshot> SnapshotHistory { get; }
            internal long NextClusterSequence { get; }
            internal long CurrentTick { get; }
            internal KLEPMemorySnapshot Snapshot { get; }
        }

        private const double MaximumEmotionDistance = 2.8284271247461903d;
        private readonly List<MutableCluster> clusters = new List<MutableCluster>();
        private readonly HashSet<string> seenExperienceIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<KLEPMemorySnapshot> snapshotHistory =
            new List<KLEPMemorySnapshot>();
        // This is literally the next value to allocate. One is the first value;
        // CaptureState after one new cluster therefore stores two.
        private long nextClusterSequence = 1;

        public KLEPMemory(
            string ownerId,
            KLEPMemoryConfiguration configuration = null)
        {
            OwnerId = KLEPMemoryValidation.RequireId(ownerId, nameof(ownerId));
            Configuration = configuration ?? new KLEPMemoryConfiguration();
            CurrentTick = 0;
            Snapshot = new KLEPMemorySnapshot(
                OwnerId,
                CurrentTick,
                Array.Empty<KLEPMemoryClusterSnapshot>(),
                Array.Empty<KLEPMemoryTransition>());
        }

        public string OwnerId { get; }
        public KLEPMemoryConfiguration Configuration { get; }
        public long CurrentTick { get; private set; }
        public KLEPMemorySnapshot Snapshot { get; private set; }

        public KLEPMemorySnapshot Tick(long tick)
        {
            return Tick(tick, null);
        }

        public KLEPMemorySnapshot Tick(
            long tick,
            IReadOnlyList<KLEPMemoryExperience> experiences)
        {
            if (tick < 0 || tick <= CurrentTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tick),
                    "Memory Ticks must be non-negative and strictly increasing.");
            }

            List<KLEPMemoryExperience> orderedExperiences =
                ValidateAndOrderExperiences(tick, experiences);
            var workingClusters = new List<MutableCluster>(clusters.Count);
            for (int i = 0; i < clusters.Count; i++)
            {
                workingClusters.Add(clusters[i].Clone());
            }

            var workingSeen = new HashSet<string>(seenExperienceIds, StringComparer.Ordinal);
            var transitions = new List<KLEPMemoryTransition>();
            long workingNextSequence = nextClusterSequence;

            HashSet<string> coldBeforeTarget = CoolForElapsedTicks(
                workingClusters,
                tick,
                transitions);
            ResolveColdClusters(
                workingClusters,
                tick,
                transitions,
                coldBeforeTarget);
            FadeOldDetail(workingClusters, tick, transitions);

            for (int i = 0; i < orderedExperiences.Count; i++)
            {
                KLEPMemoryExperience experience = orderedExperiences[i];
                MutableCluster cluster = FindAssociation(workingClusters, experience);
                bool isNew = cluster == null;
                if (isNew)
                {
                    if (workingNextSequence == long.MaxValue)
                    {
                        throw new InvalidOperationException(
                            "Memory exhausted its deterministic cluster sequence.");
                    }

                    cluster = new MutableCluster(
                        BuildClusterId(workingNextSequence),
                        experience.ActionStableId,
                        tick);
                    workingNextSequence++;
                    workingClusters.Add(cluster);
                }

                RecordExperience(cluster, experience, tick, isNew, transitions);
                workingSeen.Add(experience.ExperienceId);
            }

            // A pattern arriving on the Tick that its old heat reaches zero may
            // still reinforce it. Only clusters left cold after recording are
            // forgotten or consolidated.
            ResolveColdClusters(workingClusters, tick, transitions, null);
            EnforceWorkingCapacity(workingClusters, tick, transitions);
            EnforceArchivedCapacity(workingClusters, tick, transitions);
            workingClusters.Sort(CompareClusterIds);

            // Build every immutable projection before committing mutable state,
            // so validation failures cannot leave a half-advanced Memory.
            var nextSnapshot = new KLEPMemorySnapshot(
                OwnerId,
                tick,
                BuildClusterSnapshots(workingClusters),
                transitions);

            clusters.Clear();
            clusters.AddRange(workingClusters);
            seenExperienceIds.Clear();
            seenExperienceIds.UnionWith(workingSeen);
            nextClusterSequence = workingNextSequence;
            CurrentTick = tick;
            Snapshot = nextSnapshot;
            RememberSnapshot(Snapshot);
            return Snapshot;
        }

        /// <summary>
        /// Pure cue recall. It neither cools, reinforces, nor changes snapshot
        /// history. The cue is compared only with Prior-role projector cells.
        /// </summary>
        public KLEPMemoryRecallResult Recall(
            KLEPMemoryCue cue,
            int maximumMatches = 8)
        {
            if (cue == null)
            {
                throw new ArgumentNullException(nameof(cue));
            }

            if (maximumMatches <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumMatches));
            }

            ValidatePreferenceAxes(cue.Preference);
            var matches = new List<KLEPMemoryRecall>();
            for (int i = 0; i < Snapshot.Clusters.Count; i++)
            {
                KLEPMemoryClusterSnapshot cluster = Snapshot.Clusters[i];
                if (cue.HasActionFilter &&
                    !StringComparer.Ordinal.Equals(
                        cue.ActionStableId,
                        cluster.ActionStableId))
                {
                    continue;
                }

                List<KLEPMemoryKeyCell> priorCore = GetPriorCoreCells(
                    cluster.CorePhaseKeyCells);
                float similarity = cue.KeyCells.Count == 0
                    ? 1f
                    : Jaccard(cue.KeyCells, priorCore);
                if (similarity < Configuration.RecallSimilarityThreshold)
                {
                    continue;
                }

                float repetition = ClampUnit(
                    cluster.EncounterCount /
                    (cluster.EncounterCount +
                     (double)Configuration.RecallRepetitionScale));
                float freshness = ClampUnit(
                    cluster.Heat / (double)Configuration.MaximumHeat);
                float emotional = ClampUnit(
                    (cluster.PeakEmotionalSwing *
                     Configuration.EmotionalSalienceScale) /
                    MaximumEmotionDistance);
                float? affinity = null;
                float preferenceStrength = 0f;
                if (cue.Preference != null && cluster.HasProducedEmotion)
                {
                    affinity = cue.Preference.EvaluateStabilityAffinityFromDistance(
                        cluster.RootMeanSquareDistanceTo(
                            cue.Preference.DesiredState),
                        cluster.AverageProducedSpeed);
                    preferenceStrength = (affinity.Value + 1f) * 0.5f;
                }

                float componentSum = similarity + repetition + freshness +
                    emotional;
                int componentCount = 4;
                if (affinity.HasValue)
                {
                    componentSum += preferenceStrength;
                    componentCount++;
                }

                float strength = componentSum / componentCount;
                matches.Add(new KLEPMemoryRecall(
                    cluster,
                    similarity,
                    repetition,
                    freshness,
                    emotional,
                    ClampUnit(strength),
                    affinity));
            }

            matches.Sort(CompareRecalls);
            if (matches.Count > maximumMatches)
            {
                matches.RemoveRange(maximumMatches, matches.Count - maximumMatches);
            }

            return new KLEPMemoryRecallResult(CurrentTick, cue, matches);
        }

        public IReadOnlyList<KLEPMemorySnapshot> GetSnapshotHistory()
        {
            return Array.AsReadOnly(snapshotHistory.ToArray());
        }

        public KLEPMemoryState CaptureState()
        {
            var ids = new List<string>(seenExperienceIds);
            ids.Sort(StringComparer.Ordinal);
            return new KLEPMemoryState(
                OwnerId,
                CurrentTick,
                nextClusterSequence,
                Configuration,
                Snapshot.Clusters,
                ids,
                snapshotHistory,
                Snapshot.Transitions,
                KLEPMemoryState.CurrentSchemaVersion);
        }

        /// <summary>
        /// Captures exact mutable Memory state for the trusted Cognition
        /// transaction boundary. Public persistence remains CaptureState and
        /// Restore; this opaque checkpoint exists only for same-instance
        /// rollback so Observer adapters keep referencing the same Memory.
        /// </summary>
        internal object CaptureTransactionCheckpoint()
        {
            var copiedClusters = new List<MutableCluster>(clusters.Count);
            for (int i = 0; i < clusters.Count; i++)
            {
                copiedClusters.Add(clusters[i].Clone());
            }

            return new TransactionCheckpoint(
                this,
                copiedClusters.AsReadOnly(),
                new HashSet<string>(seenExperienceIds, StringComparer.Ordinal),
                snapshotHistory.ToArray(),
                nextClusterSequence,
                CurrentTick,
                Snapshot);
        }

        /// <summary>
        /// Restores a checkpoint captured from this exact Memory instance.
        /// The identity-preserving restore is reserved for Cognition rollback.
        /// </summary>
        internal void RestoreTransactionCheckpoint(object checkpoint)
        {
            var state = checkpoint as TransactionCheckpoint;
            if (state == null || !ReferenceEquals(state.Owner, this))
            {
                throw new ArgumentException(
                    "The Memory transaction checkpoint belongs to another instance.",
                    nameof(checkpoint));
            }

            clusters.Clear();
            for (int i = 0; i < state.Clusters.Count; i++)
            {
                clusters.Add(state.Clusters[i]);
            }

            seenExperienceIds.Clear();
            seenExperienceIds.UnionWith(state.SeenExperienceIds);
            snapshotHistory.Clear();
            for (int i = 0; i < state.SnapshotHistory.Count; i++)
            {
                snapshotHistory.Add(state.SnapshotHistory[i]);
            }

            nextClusterSequence = state.NextClusterSequence;
            CurrentTick = state.CurrentTick;
            Snapshot = state.Snapshot;
        }

        public static KLEPMemory Restore(KLEPMemoryState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.Configuration == null)
            {
                throw new ArgumentException(
                    "A Memory state requires configuration.",
                    nameof(state));
            }

            string ownerId = KLEPMemoryValidation.RequireId(
                state.OwnerId,
                nameof(state));
            if (state.Tick < 0 || state.NextClusterSequence < 1)
            {
                throw new ArgumentException(
                    "Memory state contains an invalid Tick or next cluster sequence.",
                    nameof(state));
            }

            var restored = new KLEPMemory(ownerId, state.Configuration);
            restored.ValidateState(state);
            restored.clusters.Clear();
            for (int i = 0; i < state.Clusters.Count; i++)
            {
                restored.clusters.Add(MutableCluster.Restore(state.Clusters[i]));
            }

            restored.clusters.Sort(CompareClusterIds);
            restored.seenExperienceIds.Clear();
            for (int i = 0; i < state.SeenExperienceIds.Count; i++)
            {
                restored.seenExperienceIds.Add(state.SeenExperienceIds[i]);
            }

            restored.nextClusterSequence = state.NextClusterSequence;
            restored.CurrentTick = state.Tick;
            restored.Snapshot = restored.BuildSnapshot(state.LastTransitions);
            restored.snapshotHistory.Clear();
            for (int i = 0; i < state.SnapshotHistory.Count - 1; i++)
            {
                restored.snapshotHistory.Add(state.SnapshotHistory[i]);
            }

            if (state.SnapshotHistory.Count > 0)
            {
                // Rebuild the current entry from canonical continuation state
                // so Snapshot and the history tail remain the same immutable
                // object after rehydration, just as they are during live use.
                restored.snapshotHistory.Add(restored.Snapshot);
            }
            return restored;
        }

        private List<KLEPMemoryExperience> ValidateAndOrderExperiences(
            long tick,
            IReadOnlyList<KLEPMemoryExperience> experiences)
        {
            var ordered = new List<KLEPMemoryExperience>();
            var suppliedIds = new HashSet<string>(StringComparer.Ordinal);
            if (experiences != null)
            {
                for (int i = 0; i < experiences.Count; i++)
                {
                    KLEPMemoryExperience experience = experiences[i] ??
                        throw new ArgumentException(
                            "A Memory Tick cannot contain a null experience.",
                            nameof(experiences));
                    if (experience.RecordedTick != tick)
                    {
                        throw new ArgumentException(
                            $"Experience '{experience.ExperienceId}' belongs to Tick " +
                            $"{experience.RecordedTick}, not {tick}.",
                            nameof(experiences));
                    }

                    if (!suppliedIds.Add(experience.ExperienceId) ||
                        seenExperienceIds.Contains(experience.ExperienceId))
                    {
                        throw new InvalidOperationException(
                            $"Experience '{experience.ExperienceId}' was already recorded.");
                    }

                    ValidateEmotionAxes(experience);
                    ordered.Add(experience);
                }
            }

            ordered.Sort(CompareExperiencesByConsequence);
            return ordered;
        }

        private void ValidateEmotionAxes(KLEPMemoryExperience experience)
        {
            if (experience.Emotion == null)
            {
                return;
            }

            if (!StringComparer.Ordinal.Equals(
                    experience.Emotion.AxisXName,
                    Configuration.AxisXName) ||
                !StringComparer.Ordinal.Equals(
                    experience.Emotion.AxisYName,
                    Configuration.AxisYName))
            {
                throw new ArgumentException(
                    $"Experience '{experience.ExperienceId}' uses Emotion axes that " +
                    "do not match its owning Memory.",
                    nameof(experience));
            }

            if (experience.Emotion.ProducedVelocity.Magnitude > 1f)
            {
                throw new ArgumentException(
                    $"Experience '{experience.ExperienceId}' has produced Emotion " +
                    "speed outside the normalized Memory range.",
                    nameof(experience));
            }
        }

        private void ValidatePreferenceAxes(KLEPEmotionalPreference preference)
        {
            if (preference != null &&
                (!StringComparer.Ordinal.Equals(
                    preference.AxisXName,
                    Configuration.AxisXName) ||
                 !StringComparer.Ordinal.Equals(
                    preference.AxisYName,
                    Configuration.AxisYName)))
            {
                throw new ArgumentException(
                    "Recall preference axes do not match this Memory.",
                    nameof(preference));
            }
        }

        private HashSet<string> CoolForElapsedTicks(
            List<MutableCluster> working,
            long tick,
            List<KLEPMemoryTransition> transitions)
        {
            var coldBeforeTarget = new HashSet<string>(StringComparer.Ordinal);
            if (working.Count == 0)
            {
                return coldBeforeTarget;
            }

            long elapsed = tick - CurrentTick;
            working.Sort(CompareClusterIds);
            for (int i = 0; i < working.Count; i++)
            {
                MutableCluster cluster = working[i];
                float before = cluster.Heat;
                float cooling = (float)Math.Min(
                    before,
                    (double)Configuration.CoolingPerTick * elapsed);
                if (cooling <= 0f)
                {
                    continue;
                }

                float after = Math.Max(0f, before - cooling);
                if (after == before)
                {
                    // Extremely small positive cooling can round away when
                    // subtracted from a Single. A configured positive cooling
                    // rate must not leave immortal working heat, so the same
                    // exact-rest rule used by Emotion applies here: an
                    // unrepresentable reduction reaches exact cold.
                    after = 0f;
                    cooling = before;
                }

                cluster.Heat = after;
                if (cluster.IsWorking && cluster.Heat <= 0f)
                {
                    double ticksToCold = Math.Ceiling(
                        before / (double)Configuration.CoolingPerTick);
                    if (ticksToCold < elapsed)
                    {
                        coldBeforeTarget.Add(cluster.ClusterId);
                    }
                }

                transitions.Add(new KLEPMemoryTransition(
                    tick,
                    KLEPMemoryTransitionKind.Cooled,
                    cluster.ClusterId,
                    string.Empty,
                    before,
                    0f,
                    0f,
                    0f,
                    cooling,
                    cluster.Heat,
                    elapsed == 1
                        ? "one-tick-explicit-cooling"
                        : "elapsed-ticks-equivalent-cooling"));
            }

            return coldBeforeTarget;
        }

        private void ResolveColdClusters(
            List<MutableCluster> working,
            long tick,
            List<KLEPMemoryTransition> transitions,
            HashSet<string> eligibleClusterIds)
        {
            for (int i = working.Count - 1; i >= 0; i--)
            {
                MutableCluster cluster = working[i];
                if (!cluster.IsWorking || cluster.Heat > 0f ||
                    (eligibleClusterIds != null &&
                     !eligibleClusterIds.Contains(cluster.ClusterId)))
                {
                    continue;
                }

                if (cluster.IsArchived || ShouldArchive(cluster))
                {
                    cluster.IsWorking = false;
                    cluster.IsArchived = true;
                    transitions.Add(SimpleTransition(
                        tick,
                        KLEPMemoryTransitionKind.Archived,
                        cluster,
                        string.Empty,
                        "cold-cluster-retained-by-salience-or-repetition"));
                }
                else
                {
                    transitions.Add(SimpleTransition(
                        tick,
                        KLEPMemoryTransitionKind.Forgotten,
                        cluster,
                        string.Empty,
                        "cold-cluster-below-archive-threshold"));
                    working.RemoveAt(i);
                }
            }
        }

        private void FadeOldDetail(
            List<MutableCluster> working,
            long tick,
            List<KLEPMemoryTransition> transitions)
        {
            working.Sort(CompareClusterIds);
            for (int i = 0; i < working.Count; i++)
            {
                MutableCluster cluster = working[i];
                var fadedIds = new HashSet<string>(StringComparer.Ordinal);
                FadeList(cluster.RecentEpisodes, tick, fadedIds);
                FadeList(cluster.MemorableEpisodes, tick, fadedIds);
                var orderedIds = new List<string>(fadedIds);
                orderedIds.Sort(StringComparer.Ordinal);
                for (int j = 0; j < orderedIds.Count; j++)
                {
                    transitions.Add(SimpleTransition(
                        tick,
                        KLEPMemoryTransitionKind.DetailFaded,
                        cluster,
                        orderedIds[j],
                        "full-detail-retention-elapsed"));
                }
            }
        }

        private void FadeList(
            List<KLEPMemoryExperience> episodes,
            long tick,
            HashSet<string> fadedIds)
        {
            for (int i = 0; i < episodes.Count; i++)
            {
                KLEPMemoryExperience episode = episodes[i];
                if (episode.HasFullDetail &&
                    tick - episode.RecordedTick >
                        Configuration.FullDetailRetentionTicks)
                {
                    episodes[i] = episode.ToGist();
                    fadedIds.Add(episode.ExperienceId);
                }
            }
        }

        private MutableCluster FindAssociation(
            List<MutableCluster> working,
            KLEPMemoryExperience experience)
        {
            MutableCluster best = null;
            float bestSimilarity = -1f;
            for (int i = 0; i < working.Count; i++)
            {
                MutableCluster candidate = working[i];
                if (!StringComparer.Ordinal.Equals(
                    candidate.ActionStableId,
                    experience.ActionStableId))
                {
                    continue;
                }

                List<KLEPMemoryPhaseKeyCell> candidateCore =
                    candidate.GetCorePhaseCells(
                        Configuration.CoreKeyFrequencyThreshold);
                if (IsExactCausalReversal(experience, candidateCore))
                {
                    continue;
                }

                float similarity = JaccardPhase(
                    experience.PhaseKeyCells,
                    candidateCore);
                if (similarity < Configuration.RepetitionSimilarityThreshold)
                {
                    continue;
                }

                if (best == null || similarity > bestSimilarity ||
                    (similarity.Equals(bestSimilarity) &&
                     StringComparer.Ordinal.Compare(
                        candidate.ClusterId,
                        best.ClusterId) < 0))
                {
                    best = candidate;
                    bestSimilarity = similarity;
                }
            }

            return best;
        }

        private void RecordExperience(
            MutableCluster cluster,
            KLEPMemoryExperience experience,
            long tick,
            bool isNew,
            List<KLEPMemoryTransition> transitions)
        {
            float heatBefore = cluster.Heat;
            float freshness = Configuration.InitialHeat;
            float repetition = isNew ? 0f : Configuration.RepetitionHeat;
            float swing = experience.Emotion == null
                ? 0f
                : experience.Emotion.SwingMagnitude;
            float emotionalSalience = (float)Math.Min(
                float.MaxValue,
                (double)swing * Configuration.EmotionalSalienceScale);
            // Emotional salience is inspectable association evidence. It does
            // not silently become thermal persistence.
            cluster.Heat = Math.Min(
                Configuration.MaximumHeat,
                heatBefore + freshness + repetition);
            cluster.AddExperience(experience, Configuration);
            // Archived knowledge may become hot working context again without
            // losing its consolidated archive identity.
            cluster.IsWorking = true;

            transitions.Add(new KLEPMemoryTransition(
                tick,
                isNew
                    ? KLEPMemoryTransitionKind.Recorded
                    : KLEPMemoryTransitionKind.Reinforced,
                cluster.ClusterId,
                experience.ExperienceId,
                heatBefore,
                freshness,
                repetition,
                emotionalSalience,
                0f,
                cluster.Heat,
                isNew
                    ? "new-action-and-prior-context-cluster"
                    : "same-action-phase-jaccard-reinforcement"));

            bool trauma = experience.Emotion != null &&
                swing >= Configuration.TraumaSwingThreshold;
            if (trauma)
            {
                cluster.TraumaCount++;
                cluster.IsWorking = false;
                cluster.IsArchived = true;
                if (cluster.TraumaCount >= Configuration.IndelibleTraumaRepetitions)
                {
                    cluster.IsIndelible = true;
                }

                transitions.Add(SimpleTransition(
                    tick,
                    KLEPMemoryTransitionKind.TraumaArchived,
                    cluster,
                    experience.ExperienceId,
                    cluster.IsIndelible
                        ? "repeated-positive-or-negative-trauma-indelible"
                        : "positive-or-negative-trauma-immediate-archive"));
                return;
            }

            if (cluster.IsWorking && !cluster.IsArchived && ShouldArchive(cluster))
            {
                cluster.IsArchived = true;
                transitions.Add(SimpleTransition(
                    tick,
                    KLEPMemoryTransitionKind.Archived,
                    cluster,
                    experience.ExperienceId,
                    cluster.EncounterCount >= Configuration.ArchiveRepetitionThreshold
                        ? "repetition-threshold-immediate-consolidation"
                        : "emotional-salience-threshold-immediate-consolidation"));
            }
        }

        private void EnforceWorkingCapacity(
            List<MutableCluster> working,
            long tick,
            List<KLEPMemoryTransition> transitions)
        {
            while (CountWorking(working) > Configuration.WorkingCapacity)
            {
                MutableCluster victim = null;
                for (int i = 0; i < working.Count; i++)
                {
                    if (working[i].IsWorking &&
                        (victim == null || CompareRetention(
                            working[i], victim) < 0))
                    {
                        victim = working[i];
                    }
                }

                if (victim.IsArchived)
                {
                    victim.IsWorking = false;
                    transitions.Add(SimpleTransition(
                        tick,
                        KLEPMemoryTransitionKind.WorkingDisplaced,
                        victim,
                        string.Empty,
                        "working-capacity-returned-hot-archive-to-cold-archive"));
                }
                else if (ShouldArchive(victim))
                {
                    victim.IsWorking = false;
                    victim.IsArchived = true;
                    transitions.Add(SimpleTransition(
                        tick,
                        KLEPMemoryTransitionKind.WorkingDisplaced,
                        victim,
                        string.Empty,
                        "working-capacity-displaced-to-archive"));
                }
                else
                {
                    transitions.Add(SimpleTransition(
                        tick,
                        KLEPMemoryTransitionKind.WorkingDisplaced,
                        victim,
                        string.Empty,
                        "working-capacity-displaced-and-forgotten"));
                    working.Remove(victim);
                }
            }
        }

        private void EnforceArchivedCapacity(
            List<MutableCluster> working,
            long tick,
            List<KLEPMemoryTransition> transitions)
        {
            while (CountArchived(working) > Configuration.ArchivedCapacity)
            {
                MutableCluster victim = null;
                for (int i = 0; i < working.Count; i++)
                {
                    MutableCluster candidate = working[i];
                    if (!candidate.IsArchived || candidate.IsIndelible)
                    {
                        continue;
                    }

                    if (victim == null || CompareArchiveRetention(
                        candidate, victim) < 0)
                    {
                        victim = candidate;
                    }
                }

                if (victim == null)
                {
                    // Indelible trauma deliberately makes this a soft capacity.
                    break;
                }

                if (victim.IsWorking)
                {
                    victim.IsArchived = false;
                    transitions.Add(SimpleTransition(
                        tick,
                        KLEPMemoryTransitionKind.ArchiveEvicted,
                        victim,
                        string.Empty,
                        "archive-capacity-cleared-archive-copy-but-kept-hot-working-cluster"));
                }
                else
                {
                    transitions.Add(SimpleTransition(
                        tick,
                        KLEPMemoryTransitionKind.ArchiveEvicted,
                        victim,
                        string.Empty,
                        "archive-capacity-evicted-weakest-nonindelible-cluster"));
                    working.Remove(victim);
                }
            }
        }

        private bool ShouldArchive(MutableCluster cluster)
        {
            return cluster.EncounterCount >= Configuration.ArchiveRepetitionThreshold ||
                (cluster.ProducedEmotionCount > 0 &&
                 cluster.PeakEmotionalSwing >= Configuration.ArchiveSwingThreshold) ||
                cluster.TraumaCount > 0 ||
                cluster.IsIndelible;
        }

        private KLEPMemorySnapshot BuildSnapshot(
            IReadOnlyList<KLEPMemoryTransition> transitions)
        {
            return new KLEPMemorySnapshot(
                OwnerId,
                CurrentTick,
                BuildClusterSnapshots(clusters),
                transitions);
        }

        private List<KLEPMemoryClusterSnapshot> BuildClusterSnapshots(
            IReadOnlyList<MutableCluster> source)
        {
            var snapshots = new List<KLEPMemoryClusterSnapshot>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                snapshots.Add(source[i].ToSnapshot(
                    Configuration.CoreKeyFrequencyThreshold));
            }

            snapshots.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.ClusterId,
                right.ClusterId));
            return snapshots;
        }

        private void RememberSnapshot(KLEPMemorySnapshot snapshot)
        {
            if (snapshotHistory.Count == Configuration.SnapshotCapacity)
            {
                snapshotHistory.RemoveAt(0);
            }

            snapshotHistory.Add(snapshot);
        }

        private string BuildClusterId(long sequence)
        {
            return OwnerId + ".memory.cluster." +
                sequence.ToString("D20", CultureInfo.InvariantCulture);
        }

        private void ValidateState(KLEPMemoryState state)
        {
            var seen = new HashSet<string>(state.SeenExperienceIds, StringComparer.Ordinal);
            string prefix = OwnerId + ".memory.cluster.";
            ValidateClusterCollection(
                state.Clusters,
                state,
                seen,
                prefix,
                state.Tick);
            ValidateTransitions(
                state.LastTransitions,
                state.Tick,
                state,
                seen,
                prefix);
            ValidateSnapshotHistory(state, seen, prefix);
        }

        private void ValidateClusterCollection(
            IReadOnlyList<KLEPMemoryClusterSnapshot> source,
            KLEPMemoryState state,
            HashSet<string> seen,
            string prefix,
            long validationTick)
        {
            var clusterIds = new HashSet<string>(StringComparer.Ordinal);
            var retainedOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            int workingCount = 0;
            int archivedCount = 0;
            int nonIndelibleArchivedCount = 0;
            for (int i = 0; i < source.Count; i++)
            {
                KLEPMemoryClusterSnapshot cluster = source[i];
                if (cluster == null || !clusterIds.Add(cluster.ClusterId) ||
                    !IsValidClusterIdentity(
                        cluster.ClusterId,
                        prefix,
                        state.NextClusterSequence))
                {
                    throw new ArgumentException(
                        "Memory state contains an invalid or duplicate cluster identity.",
                        nameof(state));
                }

                ValidateClusterState(
                    cluster,
                    state,
                    seen,
                    retainedOwners,
                    validationTick);
                if (cluster.IsWorking)
                {
                    workingCount++;
                }

                if (cluster.IsArchived)
                {
                    archivedCount++;
                    if (!cluster.IsIndelible)
                    {
                        nonIndelibleArchivedCount++;
                    }
                }
            }

            if (workingCount > Configuration.WorkingCapacity ||
                (archivedCount > Configuration.ArchivedCapacity &&
                 nonIndelibleArchivedCount > 0))
            {
                throw new ArgumentException(
                    "Memory state exceeds its configured retention capacities.",
                    nameof(state));
            }
        }

        private void ValidateSnapshotHistory(
            KLEPMemoryState state,
            HashSet<string> seen,
            string prefix)
        {
            if (state.SnapshotHistory.Count > Configuration.SnapshotCapacity ||
                (state.Tick == 0 && state.SnapshotHistory.Count != 0) ||
                (state.Tick > 0 && state.SnapshotHistory.Count == 0))
            {
                throw new ArgumentException(
                    "Memory state contains an impossible snapshot-history length.",
                    nameof(state));
            }

            long previousTick = 0;
            for (int i = 0; i < state.SnapshotHistory.Count; i++)
            {
                KLEPMemorySnapshot snapshot = state.SnapshotHistory[i];
                if (!StringComparer.Ordinal.Equals(snapshot.OwnerId, OwnerId) ||
                    snapshot.Tick <= previousTick ||
                    snapshot.Tick > state.Tick)
                {
                    throw new ArgumentException(
                        "Memory state contains noncanonical snapshot history.",
                        nameof(state));
                }

                ValidateClusterCollection(
                    snapshot.Clusters,
                    state,
                    seen,
                    prefix,
                    snapshot.Tick);
                ValidateTransitions(
                    snapshot.Transitions,
                    snapshot.Tick,
                    state,
                    seen,
                    prefix);
                previousTick = snapshot.Tick;
            }

            if (state.SnapshotHistory.Count > 0 && previousTick != state.Tick)
            {
                throw new ArgumentException(
                    "Memory snapshot history must end at the continuation Tick.",
                    nameof(state));
            }
        }

        private void ValidateClusterState(
            KLEPMemoryClusterSnapshot cluster,
            KLEPMemoryState state,
            HashSet<string> seen,
            Dictionary<string, string> retainedOwners,
            long validationTick)
        {
            bool hasTraumaEvidence = cluster.ProducedEmotionCount > 0 &&
                cluster.PeakEmotionalSwing >=
                    Configuration.TraumaSwingThreshold;
            bool shouldBeIndelible = cluster.TraumaCount >=
                Configuration.IndelibleTraumaRepetitions;
            bool hasArchiveEvidence =
                cluster.EncounterCount >=
                    Configuration.ArchiveRepetitionThreshold ||
                (cluster.ProducedEmotionCount > 0 &&
                 cluster.PeakEmotionalSwing >=
                    Configuration.ArchiveSwingThreshold) ||
                cluster.TraumaCount > 0 ||
                cluster.IsIndelible;
            if (cluster.EncounterCount <= 0 ||
                cluster.FirstEncounterTick < 0 ||
                cluster.LastEncounterTick < cluster.FirstEncounterTick ||
                cluster.LastEncounterTick > validationTick ||
                float.IsNaN(cluster.Heat) || float.IsInfinity(cluster.Heat) ||
                cluster.Heat < 0f || cluster.Heat > Configuration.MaximumHeat ||
                (!cluster.IsWorking && !cluster.IsArchived) ||
                (cluster.IsWorking && cluster.Heat <= 0f) ||
                cluster.IsIndelible != shouldBeIndelible ||
                (cluster.IsIndelible && !cluster.IsArchived) ||
                cluster.IsArchived && !hasArchiveEvidence ||
                cluster.ProducedEmotionCount < 0 ||
                cluster.ProducedEmotionCount > cluster.EncounterCount ||
                cluster.TraumaCount > cluster.ProducedEmotionCount ||
                (cluster.TraumaCount > 0) != hasTraumaEvidence ||
                cluster.PeakEmotionalSwing >
                    KLEPMemoryValidation.MaximumEmotionDistance ||
                (cluster.ProducedEmotionCount == 0 &&
                 (cluster.PeakEmotionalSwing != 0f ||
                  cluster.MostRecentProducedEmotion != KLEPEmotionVector.Zero ||
                  cluster.MostRecentProducedVelocity != KLEPEmotionVector.Zero)))
            {
                throw new ArgumentException(
                    $"Cluster '{cluster.ClusterId}' has impossible persisted state.",
                    nameof(state));
            }

            if (!KLEPMemoryValidation.TrySumOutcomeCounts(
                    cluster.EncounterCount,
                    cluster.SucceededCount,
                    cluster.FailedCount,
                    cluster.CancelledCount,
                    cluster.FaultedCount,
                    out long outcomeCount) ||
                (string.IsNullOrEmpty(cluster.ActionStableId) && outcomeCount != 0) ||
                (!string.IsNullOrEmpty(cluster.ActionStableId) &&
                 outcomeCount != cluster.EncounterCount))
            {
                throw new ArgumentException(
                    $"Cluster '{cluster.ClusterId}' has inconsistent action outcomes.",
                    nameof(state));
            }

            var frequencies = new Dictionary<KLEPMemoryPhaseKeyCell, long>();
            for (int i = 0; i < cluster.PhaseKeyFrequencies.Count; i++)
            {
                KLEPMemoryPhaseKeyFrequency frequency =
                    cluster.PhaseKeyFrequencies[i];
                if (frequency.EncounterCount != cluster.EncounterCount ||
                    frequencies.ContainsKey(frequency.Cell))
                {
                    throw new ArgumentException(
                        $"Cluster '{cluster.ClusterId}' has invalid projector frequencies.",
                        nameof(state));
                }

                frequencies.Add(frequency.Cell, frequency.HitCount);
            }

            List<KLEPMemoryPhaseKeyCell> expectedCore = BuildCorePhaseCells(
                frequencies,
                cluster.EncounterCount,
                Configuration.CoreKeyFrequencyThreshold);
            if (!SequenceEqual(expectedCore, cluster.CorePhaseKeyCells))
            {
                throw new ArgumentException(
                    $"Cluster '{cluster.ClusterId}' has a noncanonical projector core.",
                    nameof(state));
            }

            if (cluster.RecentEpisodes.Count > Configuration.RecentEpisodeCapacity ||
                cluster.MemorableEpisodes.Count > Configuration.MemorableEpisodeCapacity)
            {
                throw new ArgumentException(
                    $"Cluster '{cluster.ClusterId}' exceeds episode retention capacity.",
                    nameof(state));
            }

            ValidateEpisodes(
                cluster,
                cluster.RecentEpisodes,
                state,
                seen,
                retainedOwners,
                validationTick);
            ValidateEpisodes(
                cluster,
                cluster.MemorableEpisodes,
                state,
                seen,
                retainedOwners,
                validationTick);
            ValidateEpisodeOrder(cluster, state);
        }

        private void ValidateEpisodes(
            KLEPMemoryClusterSnapshot cluster,
            IReadOnlyList<KLEPMemoryExperience> episodes,
            KLEPMemoryState state,
            HashSet<string> seen,
            Dictionary<string, string> retainedOwners,
            long validationTick)
        {
            var local = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < episodes.Count; i++)
            {
                KLEPMemoryExperience episode = episodes[i];
                if (episode == null || episode.RecordedTick > validationTick ||
                    !StringComparer.Ordinal.Equals(
                        episode.ActionStableId,
                        cluster.ActionStableId) ||
                    !seen.Contains(episode.ExperienceId) ||
                    !local.Add(episode.ExperienceId))
                {
                    throw new ArgumentException(
                        $"Cluster '{cluster.ClusterId}' has invalid retained episodes.",
                        nameof(state));
                }

                if (retainedOwners.TryGetValue(
                        episode.ExperienceId,
                        out string ownerCluster) &&
                    !StringComparer.Ordinal.Equals(ownerCluster, cluster.ClusterId))
                {
                    throw new ArgumentException(
                        $"Experience '{episode.ExperienceId}' is retained by multiple clusters.",
                        nameof(state));
                }

                retainedOwners[episode.ExperienceId] = cluster.ClusterId;
                ValidateEmotionAxes(episode);
                if (episode.HasFullDetail &&
                    validationTick - episode.RecordedTick >
                        Configuration.FullDetailRetentionTicks)
                {
                    throw new ArgumentException(
                        $"Experience '{episode.ExperienceId}' retained stale full detail.",
                        nameof(state));
                }
            }
        }

        private static void ValidateEpisodeOrder(
            KLEPMemoryClusterSnapshot cluster,
            KLEPMemoryState state)
        {
            for (int i = 1; i < cluster.RecentEpisodes.Count; i++)
            {
                if (CompareRecentExperiences(
                    cluster.RecentEpisodes[i - 1],
                    cluster.RecentEpisodes[i]) > 0)
                {
                    throw new ArgumentException(
                        $"Cluster '{cluster.ClusterId}' has noncanonical recent episodes.",
                        nameof(state));
                }
            }

            for (int i = 1; i < cluster.MemorableEpisodes.Count; i++)
            {
                if (CompareMemorableExperiences(
                    cluster.MemorableEpisodes[i - 1],
                    cluster.MemorableEpisodes[i]) > 0)
                {
                    throw new ArgumentException(
                        $"Cluster '{cluster.ClusterId}' has noncanonical memorable episodes.",
                        nameof(state));
                }
            }
        }

        private void ValidateTransitions(
            IReadOnlyList<KLEPMemoryTransition> transitions,
            long expectedTick,
            KLEPMemoryState state,
            HashSet<string> seen,
            string clusterPrefix)
        {
            for (int i = 0; i < transitions.Count; i++)
            {
                KLEPMemoryTransition transition = transitions[i];
                if (transition == null || transition.Tick != expectedTick ||
                    !Enum.IsDefined(typeof(KLEPMemoryTransitionKind), transition.Kind) ||
                    (!string.IsNullOrEmpty(transition.ClusterId) &&
                     !IsValidClusterIdentity(
                         transition.ClusterId,
                         clusterPrefix,
                         state.NextClusterSequence)) ||
                    (!string.IsNullOrEmpty(transition.ExperienceId) &&
                     !seen.Contains(transition.ExperienceId)) ||
                    !IsFiniteNonNegative(transition.HeatBefore) ||
                    !IsFiniteNonNegative(transition.FreshnessHeat) ||
                    !IsFiniteNonNegative(transition.RepetitionHeat) ||
                    !IsFiniteNonNegative(transition.EmotionalSalience) ||
                    !IsFiniteNonNegative(transition.Cooling) ||
                    !IsFiniteNonNegative(transition.HeatAfter) ||
                    transition.HeatBefore > Configuration.MaximumHeat ||
                    transition.FreshnessHeat > Configuration.MaximumHeat ||
                    transition.RepetitionHeat > Configuration.MaximumHeat ||
                    transition.Cooling > Configuration.MaximumHeat ||
                    transition.HeatAfter > Configuration.MaximumHeat ||
                    string.IsNullOrWhiteSpace(transition.ReasonCode))
                {
                    throw new ArgumentException(
                        "Memory state contains an invalid final transition trace.",
                        nameof(state));
                }
            }
        }

        private static bool IsValidClusterIdentity(
            string clusterId,
            string prefix,
            long nextClusterSequence)
        {
            return !string.IsNullOrEmpty(clusterId) &&
                clusterId.StartsWith(prefix, StringComparison.Ordinal) &&
                clusterId.Length == prefix.Length + 20 &&
                long.TryParse(
                    clusterId.Substring(prefix.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long sequence) &&
                sequence >= 1 && sequence < nextClusterSequence;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static KLEPMemoryTransition SimpleTransition(
            long tick,
            KLEPMemoryTransitionKind kind,
            MutableCluster cluster,
            string experienceId,
            string reasonCode)
        {
            return new KLEPMemoryTransition(
                tick,
                kind,
                cluster.ClusterId,
                experienceId,
                cluster.Heat,
                0f,
                0f,
                0f,
                0f,
                cluster.Heat,
                reasonCode);
        }

        private static int CountWorking(IReadOnlyList<MutableCluster> source)
        {
            int count = 0;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].IsWorking)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountArchived(IReadOnlyList<MutableCluster> source)
        {
            int count = 0;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].IsArchived)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CompareClusterIds(MutableCluster left, MutableCluster right)
        {
            return StringComparer.Ordinal.Compare(left.ClusterId, right.ClusterId);
        }

        private static int CompareRetention(MutableCluster left, MutableCluster right)
        {
            int heat = left.Heat.CompareTo(right.Heat);
            if (heat != 0) return heat;
            int encounters = left.EncounterCount.CompareTo(right.EncounterCount);
            if (encounters != 0) return encounters;
            int swing = left.PeakEmotionalSwing.CompareTo(right.PeakEmotionalSwing);
            if (swing != 0) return swing;
            int recent = left.LastEncounterTick.CompareTo(right.LastEncounterTick);
            return recent != 0
                ? recent
                : StringComparer.Ordinal.Compare(left.ClusterId, right.ClusterId);
        }

        private static int CompareArchiveRetention(
            MutableCluster left,
            MutableCluster right)
        {
            int trauma = left.TraumaCount.CompareTo(right.TraumaCount);
            if (trauma != 0) return trauma;
            int swing = left.PeakEmotionalSwing.CompareTo(right.PeakEmotionalSwing);
            if (swing != 0) return swing;
            return CompareRetention(left, right);
        }

        private static int CompareRecalls(KLEPMemoryRecall left, KLEPMemoryRecall right)
        {
            int strength = right.RecallStrength.CompareTo(left.RecallStrength);
            if (strength != 0) return strength;
            int similarity = right.CueSimilarity.CompareTo(left.CueSimilarity);
            if (similarity != 0) return similarity;
            int encounters = right.Cluster.EncounterCount.CompareTo(
                left.Cluster.EncounterCount);
            if (encounters != 0) return encounters;
            int recent = right.Cluster.LastEncounterTick.CompareTo(
                left.Cluster.LastEncounterTick);
            return recent != 0
                ? recent
                : StringComparer.Ordinal.Compare(
                    left.Cluster.ClusterId,
                    right.Cluster.ClusterId);
        }

        private static int CompareRecentExperiences(
            KLEPMemoryExperience left,
            KLEPMemoryExperience right)
        {
            int tick = right.RecordedTick.CompareTo(left.RecordedTick);
            return tick != 0
                ? tick
                : StringComparer.Ordinal.Compare(
                    left.ExperienceId,
                    right.ExperienceId);
        }

        private static int CompareExperiencesByConsequence(
            KLEPMemoryExperience left,
            KLEPMemoryExperience right)
        {
            KLEPMemoryMoment leftConsequence =
                left.Moments[left.Moments.Count - 1];
            KLEPMemoryMoment rightConsequence =
                right.Moments[right.Moments.Count - 1];
            int tick = leftConsequence.CapturedTick.CompareTo(
                rightConsequence.CapturedTick);
            if (tick != 0)
            {
                return tick;
            }

            int wave = leftConsequence.WaveIndex.CompareTo(
                rightConsequence.WaveIndex);
            return wave != 0
                ? wave
                : StringComparer.Ordinal.Compare(
                    left.ExperienceId,
                    right.ExperienceId);
        }

        private static int CompareMemorableExperiences(
            KLEPMemoryExperience left,
            KLEPMemoryExperience right)
        {
            float leftSwing = left.Emotion == null ? 0f : left.Emotion.SwingMagnitude;
            float rightSwing = right.Emotion == null ? 0f : right.Emotion.SwingMagnitude;
            int swing = rightSwing.CompareTo(leftSwing);
            return swing != 0 ? swing : CompareRecentExperiences(left, right);
        }

        private static float Jaccard(
            IReadOnlyList<KLEPMemoryKeyCell> left,
            IReadOnlyList<KLEPMemoryKeyCell> right)
        {
            if (left.Count == 0 && right.Count == 0)
            {
                return 1f;
            }

            var leftSet = new HashSet<KLEPMemoryKeyCell>(left);
            var union = new HashSet<KLEPMemoryKeyCell>(leftSet);
            int intersection = 0;
            for (int i = 0; i < right.Count; i++)
            {
                if (leftSet.Contains(right[i]))
                {
                    intersection++;
                }

                union.Add(right[i]);
            }

            return union.Count == 0
                ? 1f
                : (float)((double)intersection / union.Count);
        }

        private static float JaccardPhase(
            IReadOnlyList<KLEPMemoryPhaseKeyCell> left,
            IReadOnlyList<KLEPMemoryPhaseKeyCell> right)
        {
            if (left.Count == 0 && right.Count == 0)
            {
                return 1f;
            }

            var leftSet = new HashSet<KLEPMemoryPhaseKeyCell>(left);
            var union = new HashSet<KLEPMemoryPhaseKeyCell>(leftSet);
            int intersection = 0;
            for (int i = 0; i < right.Count; i++)
            {
                if (leftSet.Contains(right[i]))
                {
                    intersection++;
                }

                union.Add(right[i]);
            }

            return union.Count == 0
                ? 1f
                : (float)((double)intersection / union.Count);
        }

        private static bool IsExactCausalReversal(
            KLEPMemoryExperience experience,
            IReadOnlyList<KLEPMemoryPhaseKeyCell> candidateCore)
        {
            List<KLEPMemoryKeyCell> candidatePrior = GetRoleCoreCells(
                candidateCore,
                KLEPMemoryMomentRole.Prior);
            List<KLEPMemoryKeyCell> candidateConsequence = GetRoleCoreCells(
                candidateCore,
                KLEPMemoryMomentRole.Consequence);
            bool swapped = SequenceEqual(
                    experience.PriorKeyCells,
                    candidateConsequence) &&
                SequenceEqual(
                    experience.ConsequenceKeyCells,
                    candidatePrior);
            if (!swapped)
            {
                return false;
            }

            // A stationary A->A pattern is its own mathematical reversal but
            // not a causal reversal and remains eligible for reinforcement.
            bool sameDirection = SequenceEqual(
                    experience.PriorKeyCells,
                    candidatePrior) &&
                SequenceEqual(
                    experience.ConsequenceKeyCells,
                    candidateConsequence);
            return !sameDirection;
        }

        private static float ClampUnit(double value)
        {
            if (value <= 0d) return 0f;
            if (value >= 1d) return 1f;
            return (float)value;
        }

        private static List<KLEPMemoryKeyCell> GetPriorCoreCells(
            IReadOnlyList<KLEPMemoryPhaseKeyCell> phaseCells)
        {
            return GetRoleCoreCells(
                phaseCells,
                KLEPMemoryMomentRole.Prior);
        }

        private static List<KLEPMemoryKeyCell> GetRoleCoreCells(
            IReadOnlyList<KLEPMemoryPhaseKeyCell> phaseCells,
            KLEPMemoryMomentRole role)
        {
            var cells = new List<KLEPMemoryKeyCell>();
            for (int i = 0; i < phaseCells.Count; i++)
            {
                if (phaseCells[i].Role == role)
                {
                    cells.Add(phaseCells[i].KeyCell);
                }
            }

            cells.Sort();
            return cells;
        }

        private static List<KLEPMemoryPhaseKeyCell> BuildCorePhaseCells(
            IReadOnlyDictionary<KLEPMemoryPhaseKeyCell, long> frequencies,
            long encounterCount,
            float threshold)
        {
            var core = new List<KLEPMemoryPhaseKeyCell>();
            foreach (KeyValuePair<KLEPMemoryPhaseKeyCell, long> pair in frequencies)
            {
                if ((double)pair.Value / encounterCount >= threshold)
                {
                    core.Add(pair.Key);
                }
            }

            core.Sort();
            return core;
        }

        private static bool SequenceEqual<T>(
            IReadOnlyList<T> left,
            IReadOnlyList<T> right)
            where T : IEquatable<T>
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (!left[i].Equals(right[i])) return false;
            }

            return true;
        }

        private sealed class MutableCluster
        {
            internal MutableCluster(string clusterId, string actionStableId, long tick)
            {
                ClusterId = clusterId;
                ActionStableId = actionStableId ?? string.Empty;
                FirstEncounterTick = tick;
                LastEncounterTick = tick;
                IsWorking = true;
            }

            internal string ClusterId;
            internal string ActionStableId;
            internal long EncounterCount;
            internal long FirstEncounterTick;
            internal long LastEncounterTick;
            internal float Heat;
            internal bool IsWorking;
            internal bool IsArchived;
            internal bool IsIndelible;
            internal long TraumaCount;
            internal float PeakEmotionalSwing;
            internal long SucceededCount;
            internal long FailedCount;
            internal long CancelledCount;
            internal long FaultedCount;
            internal long ProducedEmotionCount;
            internal double ProducedEmotionSumX;
            internal double ProducedEmotionSumY;
            internal double ProducedPositionSquaredMagnitudeSum;
            internal double ProducedVelocitySumX;
            internal double ProducedVelocitySumY;
            internal double ProducedSpeedSum;
            internal KLEPEmotionVector MostRecentProducedEmotion;
            internal KLEPEmotionVector MostRecentProducedVelocity;
            internal readonly Dictionary<KLEPMemoryPhaseKeyCell, long> Frequencies =
                new Dictionary<KLEPMemoryPhaseKeyCell, long>();
            internal readonly List<KLEPMemoryExperience> RecentEpisodes =
                new List<KLEPMemoryExperience>();
            internal readonly List<KLEPMemoryExperience> MemorableEpisodes =
                new List<KLEPMemoryExperience>();

            internal void AddExperience(
                KLEPMemoryExperience experience,
                KLEPMemoryConfiguration configuration)
            {
                if (EncounterCount == long.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"Memory cluster '{ClusterId}' exhausted its encounter count.");
                }

                EncounterCount++;
                LastEncounterTick = experience.RecordedTick;
                for (int i = 0; i < experience.PhaseKeyCells.Count; i++)
                {
                    KLEPMemoryPhaseKeyCell cell = experience.PhaseKeyCells[i];
                    Frequencies.TryGetValue(cell, out long hits);
                    if (hits == long.MaxValue)
                    {
                        throw new InvalidOperationException(
                            $"Memory cluster '{ClusterId}' exhausted a projector frequency.");
                    }

                    Frequencies[cell] = hits + 1;
                }

                if (experience.ActionOutcome != null)
                {
                    switch (experience.ActionOutcome.TerminalState)
                    {
                        case KLEPExecutableState.Succeeded:
                            SucceededCount++;
                            break;
                        case KLEPExecutableState.Failed:
                            FailedCount++;
                            break;
                        case KLEPExecutableState.Cancelled:
                            CancelledCount++;
                            break;
                        case KLEPExecutableState.Faulted:
                            FaultedCount++;
                            break;
                    }
                }

                if (experience.Emotion != null)
                {
                    ProducedEmotionCount++;
                    ProducedEmotionSumX += experience.Emotion.ProducedState.X;
                    ProducedEmotionSumY += experience.Emotion.ProducedState.Y;
                    ProducedPositionSquaredMagnitudeSum +=
                        ((double)experience.Emotion.ProducedState.X *
                            experience.Emotion.ProducedState.X) +
                        ((double)experience.Emotion.ProducedState.Y *
                            experience.Emotion.ProducedState.Y);
                    ProducedVelocitySumX += experience.Emotion.ProducedVelocity.X;
                    ProducedVelocitySumY += experience.Emotion.ProducedVelocity.Y;
                    ProducedSpeedSum += experience.Emotion.ProducedVelocity.Magnitude;
                    MostRecentProducedEmotion = experience.Emotion.ProducedState;
                    MostRecentProducedVelocity = experience.Emotion.ProducedVelocity;
                    PeakEmotionalSwing = Math.Max(
                        PeakEmotionalSwing,
                        experience.Emotion.SwingMagnitude);
                }

                RecentEpisodes.Add(experience);
                RecentEpisodes.Sort(CompareRecentEpisodes);
                Trim(RecentEpisodes, configuration.RecentEpisodeCapacity);

                MemorableEpisodes.Add(experience);
                MemorableEpisodes.Sort(CompareMemorableEpisodes);
                Trim(MemorableEpisodes, configuration.MemorableEpisodeCapacity);
            }

            internal List<KLEPMemoryPhaseKeyCell> GetCorePhaseCells(float threshold)
            {
                return BuildCorePhaseCells(Frequencies, EncounterCount, threshold);
            }

            internal KLEPMemoryClusterSnapshot ToSnapshot(float coreThreshold)
            {
                var frequencies = new List<KLEPMemoryPhaseKeyFrequency>(Frequencies.Count);
                foreach (KeyValuePair<KLEPMemoryPhaseKeyCell, long> pair in Frequencies)
                {
                    frequencies.Add(new KLEPMemoryPhaseKeyFrequency(
                        pair.Key,
                        pair.Value,
                        EncounterCount));
                }

                frequencies.Sort((left, right) => left.Cell.CompareTo(right.Cell));
                return new KLEPMemoryClusterSnapshot(
                    ClusterId,
                    ActionStableId,
                    EncounterCount,
                    FirstEncounterTick,
                    LastEncounterTick,
                    Heat,
                    IsWorking,
                    IsArchived,
                    IsIndelible,
                    TraumaCount,
                    PeakEmotionalSwing,
                    SucceededCount,
                    FailedCount,
                    CancelledCount,
                    FaultedCount,
                    ProducedEmotionCount,
                    ProducedEmotionSumX,
                    ProducedEmotionSumY,
                    ProducedPositionSquaredMagnitudeSum,
                    ProducedVelocitySumX,
                    ProducedVelocitySumY,
                    ProducedSpeedSum,
                    MostRecentProducedEmotion,
                    MostRecentProducedVelocity,
                    frequencies,
                    BuildCorePhaseCells(Frequencies, EncounterCount, coreThreshold),
                    RecentEpisodes,
                    MemorableEpisodes);
            }

            internal MutableCluster Clone()
            {
                var clone = new MutableCluster(ClusterId, ActionStableId, FirstEncounterTick)
                {
                    EncounterCount = EncounterCount,
                    LastEncounterTick = LastEncounterTick,
                    Heat = Heat,
                    IsWorking = IsWorking,
                    IsArchived = IsArchived,
                    IsIndelible = IsIndelible,
                    TraumaCount = TraumaCount,
                    PeakEmotionalSwing = PeakEmotionalSwing,
                    SucceededCount = SucceededCount,
                    FailedCount = FailedCount,
                    CancelledCount = CancelledCount,
                    FaultedCount = FaultedCount,
                    ProducedEmotionCount = ProducedEmotionCount,
                    ProducedEmotionSumX = ProducedEmotionSumX,
                    ProducedEmotionSumY = ProducedEmotionSumY,
                    ProducedPositionSquaredMagnitudeSum =
                        ProducedPositionSquaredMagnitudeSum,
                    ProducedVelocitySumX = ProducedVelocitySumX,
                    ProducedVelocitySumY = ProducedVelocitySumY,
                    ProducedSpeedSum = ProducedSpeedSum,
                    MostRecentProducedEmotion = MostRecentProducedEmotion,
                    MostRecentProducedVelocity = MostRecentProducedVelocity
                };
                foreach (KeyValuePair<KLEPMemoryPhaseKeyCell, long> pair in Frequencies)
                {
                    clone.Frequencies.Add(pair.Key, pair.Value);
                }

                clone.RecentEpisodes.AddRange(RecentEpisodes);
                clone.MemorableEpisodes.AddRange(MemorableEpisodes);
                return clone;
            }

            internal static MutableCluster Restore(KLEPMemoryClusterSnapshot snapshot)
            {
                var restored = new MutableCluster(
                    snapshot.ClusterId,
                    snapshot.ActionStableId,
                    snapshot.FirstEncounterTick)
                {
                    EncounterCount = snapshot.EncounterCount,
                    LastEncounterTick = snapshot.LastEncounterTick,
                    Heat = snapshot.Heat,
                    IsWorking = snapshot.IsWorking,
                    IsArchived = snapshot.IsArchived,
                    IsIndelible = snapshot.IsIndelible,
                    TraumaCount = snapshot.TraumaCount,
                    PeakEmotionalSwing = snapshot.PeakEmotionalSwing,
                    SucceededCount = snapshot.SucceededCount,
                    FailedCount = snapshot.FailedCount,
                    CancelledCount = snapshot.CancelledCount,
                    FaultedCount = snapshot.FaultedCount,
                    ProducedEmotionCount = snapshot.ProducedEmotionCount,
                    ProducedEmotionSumX = snapshot.ProducedEmotionSumX,
                    ProducedEmotionSumY = snapshot.ProducedEmotionSumY,
                    ProducedPositionSquaredMagnitudeSum =
                        snapshot.ProducedPositionSquaredMagnitudeSum,
                    ProducedVelocitySumX = snapshot.ProducedVelocitySumX,
                    ProducedVelocitySumY = snapshot.ProducedVelocitySumY,
                    ProducedSpeedSum = snapshot.ProducedSpeedSum,
                    MostRecentProducedEmotion = snapshot.MostRecentProducedEmotion,
                    MostRecentProducedVelocity = snapshot.MostRecentProducedVelocity
                };
                for (int i = 0; i < snapshot.PhaseKeyFrequencies.Count; i++)
                {
                    KLEPMemoryPhaseKeyFrequency frequency =
                        snapshot.PhaseKeyFrequencies[i];
                    restored.Frequencies.Add(frequency.Cell, frequency.HitCount);
                }

                restored.RecentEpisodes.AddRange(snapshot.RecentEpisodes);
                restored.MemorableEpisodes.AddRange(snapshot.MemorableEpisodes);
                return restored;
            }

            private static int CompareRecentEpisodes(
                KLEPMemoryExperience left,
                KLEPMemoryExperience right)
            {
                return CompareRecentExperiences(left, right);
            }

            private static int CompareMemorableEpisodes(
                KLEPMemoryExperience left,
                KLEPMemoryExperience right)
            {
                return CompareMemorableExperiences(left, right);
            }

            private static void Trim<T>(List<T> list, int capacity)
            {
                if (list.Count > capacity)
                {
                    list.RemoveRange(capacity, list.Count - capacity);
                }
            }
        }
    }
}
