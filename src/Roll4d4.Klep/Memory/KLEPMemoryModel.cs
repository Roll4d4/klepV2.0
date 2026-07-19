using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Emotion;
using Roll4d4.Klep.Ethics;

namespace Roll4d4.Klep.Memory
{
    public enum KLEPMemoryMomentRole
    {
        Prior,
        During,
        Consequence
    }

    public enum KLEPMemoryDetailLevel
    {
        Full,
        KeyIdentityGist
    }

    public enum KLEPMemoryTransitionKind
    {
        Recorded,
        Reinforced,
        TraumaArchived,
        Cooled,
        Archived,
        Forgotten,
        DetailFaded,
        WorkingDisplaced,
        ArchiveEvicted,
        Restored
    }

    /// <summary>
    /// Deterministic policy for one Agent's Memory. Heat combines freshness
    /// and repetition. Emotional salience is recorded separately and may
    /// trigger consolidation without pretending to be repetition.
    /// </summary>
    public sealed class KLEPMemoryConfiguration
    {
        public KLEPMemoryConfiguration(
            string axisXName = "X",
            string axisYName = "Y",
            float initialHeat = 1f,
            float repetitionHeat = 0.75f,
            float emotionalSalienceScale = 1f,
            float coolingPerTick = 0.1f,
            float maximumHeat = 10f,
            float repetitionSimilarityThreshold = 0.6f,
            float coreKeyFrequencyThreshold = 0.5f,
            float traumaSwingThreshold = 1f,
            float archiveSwingThreshold = 0.5f,
            int archiveRepetitionThreshold = 2,
            int indelibleTraumaRepetitions = 3,
            int workingCapacity = 64,
            int archivedCapacity = 128,
            int recentEpisodeCapacity = 3,
            int memorableEpisodeCapacity = 2,
            long fullDetailRetentionTicks = 50,
            int snapshotCapacity = 32,
            float recallSimilarityThreshold = 0.2f,
            float recallRepetitionScale = 4f)
        {
            AxisXName = KLEPMemoryValidation.RequireId(
                axisXName,
                nameof(axisXName));
            AxisYName = KLEPMemoryValidation.RequireId(
                axisYName,
                nameof(axisYName));
            if (string.Equals(AxisXName, AxisYName, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Memory requires distinct Emotion axis names.",
                    nameof(axisYName));
            }

            InitialHeat = KLEPMemoryValidation.RequireFiniteNonNegative(
                initialHeat,
                nameof(initialHeat));
            RepetitionHeat = KLEPMemoryValidation.RequireFiniteNonNegative(
                repetitionHeat,
                nameof(repetitionHeat));
            EmotionalSalienceScale =
                KLEPMemoryValidation.RequireFiniteNonNegative(
                    emotionalSalienceScale,
                    nameof(emotionalSalienceScale));
            CoolingPerTick = KLEPMemoryValidation.RequireFinitePositive(
                coolingPerTick,
                nameof(coolingPerTick));
            MaximumHeat = KLEPMemoryValidation.RequireFinitePositive(
                maximumHeat,
                nameof(maximumHeat));
            RepetitionSimilarityThreshold =
                KLEPMemoryValidation.RequireUnitInterval(
                    repetitionSimilarityThreshold,
                    nameof(repetitionSimilarityThreshold),
                    allowZero: false);
            CoreKeyFrequencyThreshold =
                KLEPMemoryValidation.RequireUnitInterval(
                    coreKeyFrequencyThreshold,
                    nameof(coreKeyFrequencyThreshold),
                    allowZero: false);
            TraumaSwingThreshold =
                KLEPMemoryValidation.RequireFiniteNonNegative(
                    traumaSwingThreshold,
                    nameof(traumaSwingThreshold));
            ArchiveSwingThreshold =
                KLEPMemoryValidation.RequireFiniteNonNegative(
                    archiveSwingThreshold,
                    nameof(archiveSwingThreshold));

            if (archiveRepetitionThreshold <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(archiveRepetitionThreshold));
            }

            if (indelibleTraumaRepetitions <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(indelibleTraumaRepetitions));
            }

            ArchiveRepetitionThreshold = archiveRepetitionThreshold;
            IndelibleTraumaRepetitions = indelibleTraumaRepetitions;
            WorkingCapacity = KLEPMemoryValidation.RequirePositiveCapacity(
                workingCapacity,
                nameof(workingCapacity));
            ArchivedCapacity = KLEPMemoryValidation.RequirePositiveCapacity(
                archivedCapacity,
                nameof(archivedCapacity));
            RecentEpisodeCapacity = KLEPMemoryValidation.RequirePositiveCapacity(
                recentEpisodeCapacity,
                nameof(recentEpisodeCapacity));
            MemorableEpisodeCapacity =
                KLEPMemoryValidation.RequirePositiveCapacity(
                    memorableEpisodeCapacity,
                    nameof(memorableEpisodeCapacity));

            if (fullDetailRetentionTicks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fullDetailRetentionTicks));
            }

            FullDetailRetentionTicks = fullDetailRetentionTicks;
            SnapshotCapacity = KLEPMemoryValidation.RequirePositiveCapacity(
                snapshotCapacity,
                nameof(snapshotCapacity));
            RecallSimilarityThreshold =
                KLEPMemoryValidation.RequireUnitInterval(
                    recallSimilarityThreshold,
                    nameof(recallSimilarityThreshold),
                    allowZero: true);
            RecallRepetitionScale = KLEPMemoryValidation.RequireFinitePositive(
                recallRepetitionScale,
                nameof(recallRepetitionScale));

            if (InitialHeat > MaximumHeat ||
                RepetitionHeat > MaximumHeat)
            {
                throw new ArgumentException(
                    "Initial and repetition heat cannot exceed maximum heat.");
            }
        }

        public string AxisXName { get; }
        public string AxisYName { get; }
        public float InitialHeat { get; }
        public float RepetitionHeat { get; }
        public float EmotionalSalienceScale { get; }
        public float CoolingPerTick { get; }
        public float MaximumHeat { get; }
        public float RepetitionSimilarityThreshold { get; }
        public float CoreKeyFrequencyThreshold { get; }
        public float TraumaSwingThreshold { get; }
        public float ArchiveSwingThreshold { get; }
        public int ArchiveRepetitionThreshold { get; }
        public int IndelibleTraumaRepetitions { get; }
        public int WorkingCapacity { get; }
        public int ArchivedCapacity { get; }
        public int RecentEpisodeCapacity { get; }
        public int MemorableEpisodeCapacity { get; }
        public long FullDetailRetentionTicks { get; }
        public int SnapshotCapacity { get; }
        public float RecallSimilarityThreshold { get; }
        public float RecallRepetitionScale { get; }
    }

    /// <summary>
    /// Designer-owned emotional resting preference. This is stability around a
    /// desired point, not an assumption that neutral (0,0) is universally good.
    /// </summary>
    public sealed class KLEPEmotionalPreference
    {
        private const double MaximumGraphDistance = 2.8284271247461903d;

        public KLEPEmotionalPreference(
            string axisXName,
            string axisYName,
            KLEPEmotionVector desiredState,
            float stabilityRadius = 0.1f,
            float maximumStableSpeed = 0.1f)
        {
            AxisXName = KLEPMemoryValidation.RequireId(
                axisXName,
                nameof(axisXName));
            AxisYName = KLEPMemoryValidation.RequireId(
                axisYName,
                nameof(axisYName));
            if (string.Equals(AxisXName, AxisYName, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "An emotional preference requires distinct axes.",
                    nameof(axisYName));
            }

            StabilityRadius = KLEPMemoryValidation.RequireFiniteNonNegative(
                stabilityRadius,
                nameof(stabilityRadius));
            if (StabilityRadius > MaximumGraphDistance)
            {
                throw new ArgumentOutOfRangeException(nameof(stabilityRadius));
            }

            DesiredState = desiredState;
            MaximumStableSpeed = KLEPMemoryValidation.RequireUnitInterval(
                maximumStableSpeed,
                nameof(maximumStableSpeed),
                allowZero: true);
        }

        public string AxisXName { get; }
        public string AxisYName { get; }
        public KLEPEmotionVector DesiredState { get; }
        public float StabilityRadius { get; }
        public float MaximumStableSpeed { get; }

        public float EvaluateAffinity(KLEPEmotionVector producedState)
        {
            return EvaluateAffinityFromDistance(
                DesiredState.DistanceTo(producedState));
        }

        public float EvaluateAffinityFromDistance(float distance)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) ||
                distance < 0f || distance > MaximumGraphDistance)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }

            if (distance <= StabilityRadius)
            {
                return 1f;
            }

            double remaining = MaximumGraphDistance - StabilityRadius;
            if (remaining <= 0d)
            {
                return 1f;
            }

            double normalized = (distance - StabilityRadius) / remaining;
            return KLEPMemoryValidation.ClampSignedUnit(1d - (2d * normalized));
        }

        public float EvaluateStabilityAffinity(
            KLEPEmotionVector producedState,
            KLEPEmotionVector producedVelocity)
        {
            return EvaluateStabilityAffinity(
                producedState,
                producedVelocity.Magnitude);
        }

        public float EvaluateStabilityAffinity(
            KLEPEmotionVector producedState,
            float producedSpeed)
        {
            return EvaluateStabilityAffinityFromDistance(
                DesiredState.DistanceTo(producedState),
                producedSpeed);
        }

        public float EvaluateStabilityAffinityFromDistance(
            float positionDistance,
            float producedSpeed)
        {
            float positionAffinity = EvaluateAffinityFromDistance(
                positionDistance);
            KLEPMemoryValidation.RequireUnitInterval(
                producedSpeed,
                nameof(producedSpeed),
                allowZero: true);
            if (producedSpeed <= MaximumStableSpeed)
            {
                return positionAffinity;
            }

            double remaining = 1d - MaximumStableSpeed;
            float speedAffinity = remaining <= 0d
                ? 1f
                : KLEPMemoryValidation.ClampSignedUnit(
                    1d - (2d * ((producedSpeed - MaximumStableSpeed) / remaining)));
            return Math.Min(positionAffinity, speedAffinity);
        }
    }

    public readonly struct KLEPMemoryKeyCell :
        IEquatable<KLEPMemoryKeyCell>,
        IComparable<KLEPMemoryKeyCell>
    {
        public KLEPMemoryKeyCell(KLEPKeyScope scope, string keyId)
        {
            if (!Enum.IsDefined(typeof(KLEPKeyScope), scope))
            {
                throw new ArgumentOutOfRangeException(nameof(scope));
            }

            Scope = scope;
            KeyId = KLEPMemoryValidation.RequireId(keyId, nameof(keyId));
        }

        public KLEPKeyScope Scope { get; }
        public string KeyId { get; }

        public int CompareTo(KLEPMemoryKeyCell other)
        {
            int scope = Scope.CompareTo(other.Scope);
            return scope != 0
                ? scope
                : StringComparer.Ordinal.Compare(KeyId, other.KeyId);
        }

        public bool Equals(KLEPMemoryKeyCell other) =>
            Scope == other.Scope &&
            string.Equals(KeyId, other.KeyId, StringComparison.Ordinal);
        public override bool Equals(object obj) =>
            obj is KLEPMemoryKeyCell other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Scope * 397) ^
                    StringComparer.Ordinal.GetHashCode(KeyId ?? string.Empty);
            }
        }

        public override string ToString() => $"{Scope}:{KeyId}";
    }

    /// <summary>
    /// One projector-slide cell with causal position retained. A Key in the
    /// prior state is not interchangeable with the same Key appearing only as
    /// a consequence.
    /// </summary>
    public readonly struct KLEPMemoryPhaseKeyCell :
        IEquatable<KLEPMemoryPhaseKeyCell>,
        IComparable<KLEPMemoryPhaseKeyCell>
    {
        public KLEPMemoryPhaseKeyCell(
            KLEPMemoryMomentRole role,
            KLEPMemoryKeyCell keyCell)
        {
            if (!Enum.IsDefined(typeof(KLEPMemoryMomentRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role));
            }

            KLEPMemoryValidation.ValidateCell(keyCell, nameof(keyCell));
            Role = role;
            KeyCell = keyCell;
        }

        public KLEPMemoryMomentRole Role { get; }
        public KLEPMemoryKeyCell KeyCell { get; }

        public int CompareTo(KLEPMemoryPhaseKeyCell other)
        {
            int role = Role.CompareTo(other.Role);
            return role != 0 ? role : KeyCell.CompareTo(other.KeyCell);
        }

        public bool Equals(KLEPMemoryPhaseKeyCell other) =>
            Role == other.Role && KeyCell.Equals(other.KeyCell);
        public override bool Equals(object obj) =>
            obj is KLEPMemoryPhaseKeyCell other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Role * 397) ^ KeyCell.GetHashCode();
            }
        }

        public override string ToString() => $"{Role}:{KeyCell}";
    }

    /// <summary>
    /// Independent copy of one perceived Key occurrence. Full records retain
    /// payload and occurrence provenance. A gist retains only stable identity
    /// and scope, matching the owner's "key details remain" aging model.
    /// </summary>
    public sealed class KLEPMemoryKeyRecord
    {
        private readonly ReadOnlyCollection<KLEPKeyField> payloadFields;

        public KLEPMemoryKeyRecord(
            KLEPKeyScope scope,
            string keyId,
            KLEPMemoryDetailLevel detailLevel,
            string occurrenceStoreId = "",
            long occurrenceSequence = 0,
            KLEPKeyLifetime? lifetime = null,
            long issuedTick = -1,
            long activatedTick = -1,
            string sourceId = "",
            IReadOnlyList<KLEPKeyField> payloadFields = null)
        {
            if (!Enum.IsDefined(typeof(KLEPKeyScope), scope))
            {
                throw new ArgumentOutOfRangeException(nameof(scope));
            }

            if (!Enum.IsDefined(typeof(KLEPMemoryDetailLevel), detailLevel))
            {
                throw new ArgumentOutOfRangeException(nameof(detailLevel));
            }

            if (lifetime.HasValue &&
                !Enum.IsDefined(typeof(KLEPKeyLifetime), lifetime.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            }

            Scope = scope;
            KeyId = KLEPMemoryValidation.RequireId(keyId, nameof(keyId));
            DetailLevel = detailLevel;

            if (detailLevel == KLEPMemoryDetailLevel.Full)
            {
                if (!lifetime.HasValue)
                {
                    throw new ArgumentNullException(
                        nameof(lifetime),
                        "A full remembered Key requires factual lifetime evidence.");
                }

                OccurrenceStoreId = KLEPMemoryValidation.RequireId(
                    occurrenceStoreId,
                    nameof(occurrenceStoreId));
                if (occurrenceSequence <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(occurrenceSequence));
                }

                if (issuedTick < 0 || activatedTick < issuedTick)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(issuedTick),
                        "A perceived full Key must be active at or after its nonnegative issued Tick.");
                }

                OccurrenceSequence = occurrenceSequence;
                Lifetime = lifetime.Value;
                IssuedTick = issuedTick;
                ActivatedTick = activatedTick;
                SourceId = sourceId ?? string.Empty;
                this.payloadFields = KLEPMemoryValidation.CopyFields(
                    payloadFields,
                    nameof(payloadFields));
            }
            else
            {
                if (lifetime.HasValue)
                {
                    throw new ArgumentException(
                        "A Key-identity gist cannot claim discarded lifetime evidence.",
                        nameof(lifetime));
                }

                OccurrenceStoreId = string.Empty;
                OccurrenceSequence = 0;
                Lifetime = null;
                IssuedTick = -1;
                ActivatedTick = -1;
                SourceId = string.Empty;
                this.payloadFields = new ReadOnlyCollection<KLEPKeyField>(
                    Array.Empty<KLEPKeyField>());
            }
        }

        public KLEPKeyScope Scope { get; }
        public string KeyId { get; }
        public KLEPMemoryKeyCell Cell => new KLEPMemoryKeyCell(Scope, KeyId);
        public KLEPMemoryDetailLevel DetailLevel { get; }
        public string OccurrenceStoreId { get; }
        public long OccurrenceSequence { get; }
        public KLEPKeyLifetime? Lifetime { get; }
        public long IssuedTick { get; }
        public long ActivatedTick { get; }
        public string SourceId { get; }
        public IReadOnlyList<KLEPKeyField> PayloadFields => payloadFields;

        public static KLEPMemoryKeyRecord Capture(KLEPKeyFact fact)
        {
            if (fact == null)
            {
                throw new ArgumentNullException(nameof(fact));
            }

            return new KLEPMemoryKeyRecord(
                fact.Scope,
                fact.KeyId.Value,
                KLEPMemoryDetailLevel.Full,
                fact.OccurrenceId.StoreId,
                fact.OccurrenceId.Sequence,
                fact.Lifetime,
                fact.IssuedTick,
                fact.ActivatedTick,
                fact.SourceId,
                fact.Payload.Fields);
        }

        public KLEPMemoryKeyRecord ToGist()
        {
            return DetailLevel == KLEPMemoryDetailLevel.KeyIdentityGist
                ? this
                : new KLEPMemoryKeyRecord(
                    Scope,
                    KeyId,
                    KLEPMemoryDetailLevel.KeyIdentityGist);
        }
    }

    public sealed class KLEPMemoryMoment
    {
        private readonly ReadOnlyCollection<KLEPMemoryKeyRecord> keys;
        private readonly ReadOnlyCollection<KLEPMemoryKeyCell> keyCells;

        public KLEPMemoryMoment(
            string momentId,
            KLEPMemoryMomentRole role,
            long capturedTick,
            int waveIndex,
            IReadOnlyList<KLEPMemoryKeyRecord> keys)
        {
            MomentId = KLEPMemoryValidation.RequireId(
                momentId,
                nameof(momentId));
            if (!Enum.IsDefined(typeof(KLEPMemoryMomentRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role));
            }

            if (capturedTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capturedTick));
            }

            if (waveIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }

            Role = role;
            CapturedTick = capturedTick;
            WaveIndex = waveIndex;
            this.keys = KLEPMemoryValidation.CopyKeys(keys, nameof(keys));
            var occurrences = new HashSet<string>(StringComparer.Ordinal);
            var gistCells = new HashSet<KLEPMemoryKeyCell>();
            var scopeByKeyId = new Dictionary<string, KLEPKeyScope>(
                StringComparer.Ordinal);
            KLEPMemoryDetailLevel? collectionDetail = null;
            for (int i = 0; i < this.keys.Count; i++)
            {
                KLEPMemoryKeyRecord key = this.keys[i];
                if (collectionDetail.HasValue &&
                    collectionDetail.Value != key.DetailLevel)
                {
                    throw new ArgumentException(
                        "One remembered moment cannot mix full facts with faded gists.",
                        nameof(keys));
                }

                collectionDetail = key.DetailLevel;
                if (scopeByKeyId.TryGetValue(
                        key.KeyId,
                        out KLEPKeyScope existingScope) &&
                    existingScope != key.Scope)
                {
                    throw new ArgumentException(
                        $"Key '{key.KeyId}' cannot be perceived in both Local and Global scope.",
                        nameof(keys));
                }

                scopeByKeyId[key.KeyId] = key.Scope;
                if (key.DetailLevel == KLEPMemoryDetailLevel.Full)
                {
                    if (key.ActivatedTick > capturedTick ||
                        (key.Lifetime == KLEPKeyLifetime.OneCycle &&
                         key.ActivatedTick != capturedTick))
                    {
                        throw new ArgumentException(
                            "A full remembered Key must have been visible in its captured snapshot.",
                            nameof(keys));
                    }

                    string occurrence = ((int)key.Scope).ToString(
                        CultureInfo.InvariantCulture) + "\0" +
                        key.OccurrenceStoreId + "\0" +
                        key.OccurrenceSequence.ToString(
                            CultureInfo.InvariantCulture);
                    if (!occurrences.Add(occurrence))
                    {
                        throw new ArgumentException(
                            $"Key occurrence '{key.OccurrenceStoreId}:{key.OccurrenceSequence}' is duplicated.",
                            nameof(keys));
                    }
                }
                else if (!gistCells.Add(key.Cell))
                {
                    throw new ArgumentException(
                        $"Faded Key cell '{key.Cell}' is duplicated.",
                        nameof(keys));
                }
            }

            keyCells = KLEPMemoryValidation.UniqueCells(this.keys);
        }

        public string MomentId { get; }
        public KLEPMemoryMomentRole Role { get; }
        public long CapturedTick { get; }
        public int WaveIndex { get; }
        public IReadOnlyList<KLEPMemoryKeyRecord> Keys => keys;
        public IReadOnlyList<KLEPMemoryKeyCell> KeyCells => keyCells;
        public bool HasFullDetail
        {
            get
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i].DetailLevel == KLEPMemoryDetailLevel.Full)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static KLEPMemoryMoment Capture(
            string momentId,
            KLEPMemoryMomentRole role,
            KLEPKeySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var copied = new List<KLEPMemoryKeyRecord>(snapshot.Facts.Count);
            for (int i = 0; i < snapshot.Facts.Count; i++)
            {
                copied.Add(KLEPMemoryKeyRecord.Capture(snapshot.Facts[i]));
            }

            return new KLEPMemoryMoment(
                momentId,
                role,
                snapshot.Tick,
                snapshot.WaveIndex,
                copied);
        }

        public KLEPMemoryMoment ToGist()
        {
            if (!HasFullDetail)
            {
                return this;
            }

            var gist = new List<KLEPMemoryKeyRecord>(keyCells.Count);
            for (int i = 0; i < keyCells.Count; i++)
            {
                KLEPMemoryKeyCell cell = keyCells[i];
                gist.Add(new KLEPMemoryKeyRecord(
                    cell.Scope,
                    cell.KeyId,
                    KLEPMemoryDetailLevel.KeyIdentityGist));
            }

            return new KLEPMemoryMoment(
                MomentId,
                Role,
                CapturedTick,
                WaveIndex,
                gist);
        }
    }

    /// <summary>
    /// Factual lifecycle consequence. Succeeded is the only successful state;
    /// Ethics and Emotion are recorded elsewhere and never rewrite this fact.
    /// </summary>
    public sealed class KLEPMemoryActionOutcome
    {
        public KLEPMemoryActionOutcome(
            string executableStableId,
            long runIndex,
            long startedTick,
            long completedTick,
            KLEPExecutableState terminalState,
            KLEPExecutableExitReason? exitReason,
            int waveIndex = 0,
            int startedWaveIndex = 0)
        {
            ExecutableStableId = KLEPMemoryValidation.RequireId(
                executableStableId,
                nameof(executableStableId));
            if (runIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(runIndex));
            }

            if (startedTick < 0 || completedTick < startedTick)
            {
                throw new ArgumentOutOfRangeException(nameof(completedTick));
            }

            if (terminalState != KLEPExecutableState.Succeeded &&
                terminalState != KLEPExecutableState.Failed &&
                terminalState != KLEPExecutableState.Cancelled &&
                terminalState != KLEPExecutableState.Faulted)
            {
                throw new ArgumentException(
                    "A remembered action outcome must be terminal.",
                    nameof(terminalState));
            }

            if (exitReason == null)
            {
                throw new ArgumentNullException(nameof(exitReason));
            }

            if (!Enum.IsDefined(typeof(KLEPExecutableExitReason), exitReason.Value) ||
                !IsConsistent(terminalState, exitReason.Value))
            {
                throw new ArgumentException(
                    "The terminal Executable state and exit reason are inconsistent.",
                    nameof(exitReason));
            }

            if (waveIndex < 0 || startedWaveIndex < 0 ||
                (startedTick == completedTick && startedWaveIndex > waveIndex))
            {
                throw new ArgumentOutOfRangeException(nameof(startedWaveIndex));
            }

            RunIndex = runIndex;
            StartedTick = startedTick;
            CompletedTick = completedTick;
            TerminalState = terminalState;
            ExitReason = exitReason.Value;
            WaveIndex = waveIndex;
            StartedWaveIndex = startedWaveIndex;
        }

        public string ExecutableStableId { get; }
        public long RunIndex { get; }
        public long StartedTick { get; }
        public long CompletedTick { get; }
        public KLEPExecutableState TerminalState { get; }
        public KLEPExecutableExitReason ExitReason { get; }
        public int WaveIndex { get; }
        public int StartedWaveIndex { get; }
        public bool WasSuccessful => TerminalState == KLEPExecutableState.Succeeded;

        public static KLEPMemoryActionOutcome Capture(
            KLEPExecutionResult result,
            long startedTick,
            int startedWaveIndex = 0)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!result.IsTerminal || result.ExitReason == null)
            {
                throw new ArgumentException(
                    "Only a terminal Execution result can become a Memory outcome.",
                    nameof(result));
            }

            return new KLEPMemoryActionOutcome(
                result.ExecutableStableId,
                result.RunIndex,
                startedTick,
                result.CycleIndex,
                result.State,
                result.ExitReason,
                result.WaveIndex,
                startedWaveIndex);
        }

        private static bool IsConsistent(
            KLEPExecutableState state,
            KLEPExecutableExitReason reason)
        {
            if (state == KLEPExecutableState.Succeeded)
            {
                return reason == KLEPExecutableExitReason.Succeeded;
            }

            if (state == KLEPExecutableState.Failed)
            {
                return reason == KLEPExecutableExitReason.Failed;
            }

            if (state == KLEPExecutableState.Faulted)
            {
                return reason == KLEPExecutableExitReason.Faulted;
            }

            return state == KLEPExecutableState.Cancelled &&
                (reason == KLEPExecutableExitReason.LocksClosed ||
                 reason == KLEPExecutableExitReason.BelowThreshold ||
                 reason == KLEPExecutableExitReason.Interrupted ||
                 reason == KLEPExecutableExitReason.WaveAborted ||
                 reason == KLEPExecutableExitReason.Removed);
        }
    }

    public sealed class KLEPMemoryEthicsTraceRecord
    {
        private readonly ReadOnlyCollection<string> evidenceIds;

        public KLEPMemoryEthicsTraceRecord(KLEPEthicsTraceEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            SourceId = entry.SourceId;
            Applied = entry.Applied;
            Weight = entry.Weight;
            ProposedImpulse = entry.ProposedImpulse;
            ReasonCode = entry.ReasonCode;
            ContributionX = entry.ContributionX;
            ContributionY = entry.ContributionY;
            evidenceIds = KLEPMemoryValidation.CopyIds(
                entry.EvidenceIds,
                nameof(entry));
        }

        public KLEPMemoryEthicsTraceRecord(
            string sourceId,
            bool applied,
            float weight,
            KLEPEmotionVector proposedImpulse,
            string reasonCode,
            IReadOnlyList<string> evidenceIds = null)
        {
            var entry = new KLEPEthicsTraceEntry(
                sourceId,
                applied,
                weight,
                proposedImpulse,
                reasonCode,
                evidenceIds);
            SourceId = entry.SourceId;
            Applied = entry.Applied;
            Weight = entry.Weight;
            ProposedImpulse = entry.ProposedImpulse;
            ReasonCode = entry.ReasonCode;
            ContributionX = entry.ContributionX;
            ContributionY = entry.ContributionY;
            this.evidenceIds = KLEPMemoryValidation.CopyIds(
                entry.EvidenceIds,
                nameof(evidenceIds));
        }

        public string SourceId { get; }
        public bool Applied { get; }
        public float Weight { get; }
        public KLEPEmotionVector ProposedImpulse { get; }
        public string ReasonCode { get; }
        public double ContributionX { get; }
        public double ContributionY { get; }
        public IReadOnlyList<string> EvidenceIds => evidenceIds;
    }

    public sealed class KLEPMemoryEthicsRecord
    {
        private readonly ReadOnlyCollection<KLEPMemoryEthicsTraceRecord> trace;

        public KLEPMemoryEthicsRecord(
            string evaluationId,
            long evaluationTick,
            KLEPEmotionInfluenceOrigin causeOrigin,
            string evaluatorId,
            string evaluatorVersion,
            string axisXName,
            string axisYName,
            string contextId,
            string contextSchemaId,
            string contextSchemaVersion,
            double rawX,
            double rawY,
            bool wasClamped,
            KLEPEmotionVector impulse,
            IReadOnlyList<KLEPMemoryEthicsTraceRecord> trace)
        {
            EvaluationId = KLEPMemoryValidation.RequireId(
                evaluationId,
                nameof(evaluationId));
            if (evaluationTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(evaluationTick));
            }

            if (!Enum.IsDefined(
                    typeof(KLEPEmotionInfluenceOrigin),
                    causeOrigin))
            {
                throw new ArgumentOutOfRangeException(nameof(causeOrigin));
            }

            EvaluationTick = evaluationTick;
            CauseOrigin = causeOrigin;
            EvaluatorId = KLEPMemoryValidation.RequireId(
                evaluatorId,
                nameof(evaluatorId));
            EvaluatorVersion = KLEPMemoryValidation.RequireId(
                evaluatorVersion,
                nameof(evaluatorVersion));
            AxisXName = KLEPMemoryValidation.RequireId(
                axisXName,
                nameof(axisXName));
            AxisYName = KLEPMemoryValidation.RequireId(
                axisYName,
                nameof(axisYName));
            if (string.Equals(AxisXName, AxisYName, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Remembered Ethics axes must be distinct.",
                    nameof(axisYName));
            }
            ContextId = KLEPMemoryValidation.RequireId(
                contextId,
                nameof(contextId));
            ContextSchemaId = KLEPMemoryValidation.RequireId(
                contextSchemaId,
                nameof(contextSchemaId));
            ContextSchemaVersion = KLEPMemoryValidation.RequireId(
                contextSchemaVersion,
                nameof(contextSchemaVersion));
            if (double.IsNaN(rawX) || double.IsInfinity(rawX) ||
                double.IsNaN(rawY) || double.IsInfinity(rawY))
            {
                throw new ArgumentOutOfRangeException(nameof(rawX));
            }

            RawX = rawX;
            RawY = rawY;
            WasClamped = wasClamped;
            Impulse = impulse;
            this.trace = KLEPMemoryValidation.CopyEthicsTrace(
                trace,
                nameof(trace));
            if (this.trace.Count == 0)
            {
                throw new ArgumentException(
                    "A remembered Ethics evaluation requires an inspectable trace.",
                    nameof(trace));
            }

            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            double tracedX = 0d;
            double tracedY = 0d;
            for (int i = 0; i < this.trace.Count; i++)
            {
                KLEPMemoryEthicsTraceRecord entry = this.trace[i];
                if (!sourceIds.Add(entry.SourceId))
                {
                    throw new ArgumentException(
                        $"Ethics trace source '{entry.SourceId}' occurs more than once.",
                        nameof(trace));
                }

                tracedX += entry.ContributionX;
                tracedY += entry.ContributionY;
            }

            bool expectedClamp = tracedX < -1d || tracedX > 1d ||
                tracedY < -1d || tracedY > 1d;
            var expectedImpulse = new KLEPEmotionVector(
                KLEPMemoryValidation.ClampSignedUnit(tracedX),
                KLEPMemoryValidation.ClampSignedUnit(tracedY));
            if (!tracedX.Equals(rawX) || !tracedY.Equals(rawY) ||
                expectedClamp != wasClamped || expectedImpulse != impulse)
            {
                throw new ArgumentException(
                    "Remembered Ethics totals, clamp state, and impulse must match the trace.",
                    nameof(trace));
            }
        }

        public string EvaluationId { get; }
        public long EvaluationTick { get; }
        public KLEPEmotionInfluenceOrigin CauseOrigin { get; }
        public string EvaluatorId { get; }
        public string EvaluatorVersion { get; }
        public string AxisXName { get; }
        public string AxisYName { get; }
        public string ContextId { get; }
        public string ContextSchemaId { get; }
        public string ContextSchemaVersion { get; }
        public double RawX { get; }
        public double RawY { get; }
        public bool WasClamped { get; }
        public KLEPEmotionVector Impulse { get; }
        public IReadOnlyList<KLEPMemoryEthicsTraceRecord> Trace => trace;

        public static KLEPMemoryEthicsRecord Capture<TContext>(
            KLEPEthicsEvaluation<TContext> evaluation)
        {
            if (evaluation == null)
            {
                throw new ArgumentNullException(nameof(evaluation));
            }

            var copiedTrace = new List<KLEPMemoryEthicsTraceRecord>(
                evaluation.Judgment.Trace.Count);
            for (int i = 0; i < evaluation.Judgment.Trace.Count; i++)
            {
                copiedTrace.Add(new KLEPMemoryEthicsTraceRecord(
                    evaluation.Judgment.Trace[i]));
            }

            return new KLEPMemoryEthicsRecord(
                evaluation.EvaluationId,
                evaluation.EvaluationTick,
                evaluation.CauseOrigin,
                evaluation.EvaluatorId,
                evaluation.EvaluatorVersion,
                evaluation.EmotionConfiguration.AxisXName,
                evaluation.EmotionConfiguration.AxisYName,
                evaluation.ContextIdentity.ContextId,
                evaluation.ContextIdentity.SchemaId,
                evaluation.ContextIdentity.SchemaVersion,
                evaluation.Judgment.RawX,
                evaluation.Judgment.RawY,
                evaluation.Judgment.WasClamped,
                evaluation.Judgment.Impulse,
                copiedTrace);
        }
    }

    public sealed class KLEPMemoryEmotionalConsequence
    {
        public KLEPMemoryEmotionalConsequence(
            string axisXName,
            string axisYName,
            long startingTick,
            long producedTick,
            KLEPEmotionVector startingState,
            KLEPEmotionVector producedState,
            KLEPEmotionVector startingVelocity,
            KLEPEmotionVector producedIntegratedVelocity,
            KLEPEmotionVector producedVelocity,
            KLEPEmotionVector producedNetInfluence,
            long producedUnchangedPositionTickCount = 0)
        {
            AxisXName = KLEPMemoryValidation.RequireId(
                axisXName,
                nameof(axisXName));
            AxisYName = KLEPMemoryValidation.RequireId(
                axisYName,
                nameof(axisYName));
            if (string.Equals(AxisXName, AxisYName, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "An emotional consequence requires distinct axes.",
                    nameof(axisYName));
            }

            if (startingTick < 0 || producedTick <= startingTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(producedTick),
                    "An emotional consequence must be produced by a later Emotion Tick.");
            }

            if (producedUnchangedPositionTickCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(producedUnchangedPositionTickCount));
            }

            StartingTick = startingTick;
            ProducedTick = producedTick;
            StartingState = startingState;
            ProducedState = producedState;
            StartingVelocity = startingVelocity;
            ProducedIntegratedVelocity = producedIntegratedVelocity;
            ProducedVelocity = producedVelocity;
            ProducedNetInfluence = producedNetInfluence;
            ProducedUnchangedPositionTickCount =
                producedUnchangedPositionTickCount;
            SwingMagnitude = startingState.DistanceTo(producedState);
        }

        public string AxisXName { get; }
        public string AxisYName { get; }
        public long StartingTick { get; }
        public long ProducedTick { get; }
        public KLEPEmotionVector StartingState { get; }
        public KLEPEmotionVector ProducedState { get; }
        public KLEPEmotionVector StartingVelocity { get; }
        public KLEPEmotionVector ProducedIntegratedVelocity { get; }
        public KLEPEmotionVector ProducedVelocity { get; }
        public KLEPEmotionVector ProducedNetInfluence { get; }
        public long ProducedUnchangedPositionTickCount { get; }
        public bool ProducedIsAtRest =>
            ProducedVelocity == KLEPEmotionVector.Zero;
        public float SwingMagnitude { get; }

        public static KLEPMemoryEmotionalConsequence Capture(
            long startingTick,
            KLEPEmotionVector startingState,
            KLEPEmotionSnapshot producedSnapshot)
        {
            if (producedSnapshot == null)
            {
                throw new ArgumentNullException(nameof(producedSnapshot));
            }

            if (startingTick != producedSnapshot.Tick - 1 ||
                startingState != producedSnapshot.PositionBefore)
            {
                throw new ArgumentException(
                    "Snapshot capture describes one exact Emotion transition; multi-Tick experiences must supply their actual starting state and velocity explicitly.",
                    nameof(startingTick));
            }

            return new KLEPMemoryEmotionalConsequence(
                producedSnapshot.Configuration.AxisXName,
                producedSnapshot.Configuration.AxisYName,
                startingTick,
                producedSnapshot.Tick,
                startingState,
                producedSnapshot.Position,
                producedSnapshot.VelocityBefore,
                producedSnapshot.IntegratedVelocity,
                producedSnapshot.Velocity,
                producedSnapshot.NetInfluence,
                producedSnapshot.UnchangedPositionTickCount);
        }
    }

    /// <summary>
    /// One perceived experience: ordered inner-state moments, optional factual
    /// action outcome, optional Ethics evidence, and the produced Emotion state.
    /// </summary>
    public sealed class KLEPMemoryExperience
    {
        private readonly ReadOnlyCollection<KLEPMemoryMoment> moments;
        private readonly ReadOnlyCollection<KLEPMemoryEthicsRecord> ethics;
        private readonly ReadOnlyCollection<KLEPMemoryKeyCell> keyCells;
        private readonly ReadOnlyCollection<KLEPMemoryKeyCell> priorKeyCells;
        private readonly ReadOnlyCollection<KLEPMemoryKeyCell> duringKeyCells;
        private readonly ReadOnlyCollection<KLEPMemoryKeyCell> consequenceKeyCells;
        private readonly ReadOnlyCollection<KLEPMemoryPhaseKeyCell> phaseKeyCells;

        public KLEPMemoryExperience(
            string experienceId,
            long recordedTick,
            IReadOnlyList<KLEPMemoryMoment> moments,
            KLEPMemoryActionOutcome actionOutcome = null,
            IReadOnlyList<KLEPMemoryEthicsRecord> ethics = null,
            KLEPMemoryEmotionalConsequence emotion = null)
        {
            ExperienceId = KLEPMemoryValidation.RequireId(
                experienceId,
                nameof(experienceId));
            if (recordedTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(recordedTick));
            }

            RecordedTick = recordedTick;
            this.moments = KLEPMemoryValidation.CopyMoments(
                moments,
                nameof(moments));
            if (this.moments.Count < 2)
            {
                throw new ArgumentException(
                    "A Memory experience requires a prior and a consequence moment.",
                    nameof(moments));
            }

            if (this.moments[0].Role != KLEPMemoryMomentRole.Prior ||
                this.moments[this.moments.Count - 1].Role !=
                    KLEPMemoryMomentRole.Consequence)
            {
                throw new ArgumentException(
                    "An experience must begin with Prior and end with Consequence.",
                    nameof(moments));
            }

            long previousTick = -1;
            int previousWave = -1;
            var momentIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < this.moments.Count; i++)
            {
                KLEPMemoryMoment moment = this.moments[i];
                if (i > 0 && i < this.moments.Count - 1 &&
                    moment.Role != KLEPMemoryMomentRole.During)
                {
                    throw new ArgumentException(
                        "Only During moments may appear between Prior and Consequence.",
                        nameof(moments));
                }

                if (moment.CapturedTick < previousTick ||
                    (moment.CapturedTick == previousTick &&
                     moment.WaveIndex < previousWave))
                {
                    throw new ArgumentException(
                        "Experience moments must be ordered by nondecreasing Tick and wave.",
                        nameof(moments));
                }

                if (!momentIds.Add(moment.MomentId))
                {
                    throw new ArgumentException(
                        $"Moment ID '{moment.MomentId}' occurs more than once.",
                        nameof(moments));
                }

                previousTick = moment.CapturedTick;
                previousWave = moment.WaveIndex;
            }

            if (recordedTick < previousTick ||
                (actionOutcome != null && recordedTick < actionOutcome.CompletedTick) ||
                (emotion != null && recordedTick < emotion.ProducedTick))
            {
                throw new ArgumentException(
                    "Recorded Tick cannot precede the experience consequence.",
                    nameof(recordedTick));
            }

            if (actionOutcome != null &&
                (KLEPMemoryValidation.IsAfter(
                     this.moments[0].CapturedTick,
                     this.moments[0].WaveIndex,
                     actionOutcome.StartedTick,
                     actionOutcome.StartedWaveIndex) ||
                 KLEPMemoryValidation.IsAfter(
                     actionOutcome.CompletedTick,
                     actionOutcome.WaveIndex,
                     this.moments[this.moments.Count - 1].CapturedTick,
                     this.moments[this.moments.Count - 1].WaveIndex)))
            {
                throw new ArgumentException(
                    "The factual action run must fit between the remembered prior and consequence moments.",
                    nameof(actionOutcome));
            }

            if (emotion != null &&
                (emotion.StartingTick < this.moments[0].CapturedTick ||
                 emotion.ProducedTick >
                    this.moments[this.moments.Count - 1].CapturedTick))
            {
                throw new ArgumentException(
                    "The emotional consequence must fit within the remembered experience timeline.",
                    nameof(emotion));
            }

            ActionOutcome = actionOutcome;
            this.ethics = KLEPMemoryValidation.CopyEthics(
                ethics,
                nameof(ethics));
            var evaluationIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < this.ethics.Count; i++)
            {
                KLEPMemoryEthicsRecord evaluation = this.ethics[i];
                if (!evaluationIds.Add(evaluation.EvaluationId))
                {
                    throw new ArgumentException(
                        $"Ethics evaluation '{evaluation.EvaluationId}' occurs more than once.",
                        nameof(ethics));
                }

                long evidenceEndTick = emotion == null
                    ? this.moments[this.moments.Count - 1].CapturedTick
                    : Math.Min(
                        this.moments[this.moments.Count - 1].CapturedTick,
                        emotion.ProducedTick);
                if (evaluation.EvaluationTick < this.moments[0].CapturedTick ||
                    evaluation.EvaluationTick > evidenceEndTick)
                {
                    throw new ArgumentException(
                        "Ethics evidence must occur within the remembered timeline.",
                        nameof(ethics));
                }

                if (emotion != null &&
                    (!string.Equals(
                         evaluation.AxisXName,
                         emotion.AxisXName,
                         StringComparison.Ordinal) ||
                     !string.Equals(
                         evaluation.AxisYName,
                         emotion.AxisYName,
                         StringComparison.Ordinal)))
                {
                    throw new ArgumentException(
                        "Remembered Ethics and Emotion must use the same named axes.",
                        nameof(ethics));
                }
            }

            Emotion = emotion;
            keyCells = KLEPMemoryValidation.UnionCells(this.moments);
            priorKeyCells = KLEPMemoryValidation.CopyUniqueCells(
                this.moments[0].KeyCells,
                nameof(moments));
            duringKeyCells = KLEPMemoryValidation.UnionCells(
                this.moments,
                KLEPMemoryMomentRole.During);
            consequenceKeyCells = KLEPMemoryValidation.CopyUniqueCells(
                this.moments[this.moments.Count - 1].KeyCells,
                nameof(moments));
            phaseKeyCells = KLEPMemoryValidation.BuildPhaseCells(this.moments);
            PriorGistId = KLEPMemoryValidation.BuildGistId(priorKeyCells);
            DuringGistId = KLEPMemoryValidation.BuildGistId(duringKeyCells);
            ConsequenceGistId =
                KLEPMemoryValidation.BuildGistId(consequenceKeyCells);
            CanonicalGistId =
                KLEPMemoryValidation.BuildPhaseGistId(phaseKeyCells);
        }

        public string ExperienceId { get; }
        public long RecordedTick { get; }
        public IReadOnlyList<KLEPMemoryMoment> Moments => moments;
        public KLEPMemoryActionOutcome ActionOutcome { get; }
        public IReadOnlyList<KLEPMemoryEthicsRecord> Ethics => ethics;
        public KLEPMemoryEmotionalConsequence Emotion { get; }
        public IReadOnlyList<KLEPMemoryKeyCell> KeyCells => keyCells;
        public IReadOnlyList<KLEPMemoryKeyCell> PriorKeyCells => priorKeyCells;
        public IReadOnlyList<KLEPMemoryKeyCell> DuringKeyCells => duringKeyCells;
        public IReadOnlyList<KLEPMemoryKeyCell> ConsequenceKeyCells =>
            consequenceKeyCells;
        public IReadOnlyList<KLEPMemoryPhaseKeyCell> PhaseKeyCells =>
            phaseKeyCells;
        public string PriorGistId { get; }
        public string DuringGistId { get; }
        public string ConsequenceGistId { get; }
        public string CanonicalGistId { get; }
        public string ActionStableId =>
            ActionOutcome == null ? string.Empty : ActionOutcome.ExecutableStableId;
        public bool WasSuccessful =>
            ActionOutcome != null && ActionOutcome.WasSuccessful;
        public bool HasFullDetail
        {
            get
            {
                for (int i = 0; i < moments.Count; i++)
                {
                    if (moments[i].HasFullDetail)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public KLEPMemoryExperience ToGist()
        {
            if (!HasFullDetail)
            {
                return this;
            }

            var gistMoments = new List<KLEPMemoryMoment>(moments.Count);
            for (int i = 0; i < moments.Count; i++)
            {
                gistMoments.Add(moments[i].ToGist());
            }

            return new KLEPMemoryExperience(
                ExperienceId,
                RecordedTick,
                gistMoments,
                ActionOutcome,
                ethics,
                Emotion);
        }
    }

    public sealed class KLEPMemoryCue
    {
        private readonly ReadOnlyCollection<KLEPMemoryKeyCell> keyCells;

        public KLEPMemoryCue(
            IReadOnlyList<KLEPMemoryKeyCell> keyCells,
            string actionStableId = "",
            KLEPEmotionalPreference preference = null)
        {
            this.keyCells = KLEPMemoryValidation.CopyUniqueCells(
                keyCells,
                nameof(keyCells));
            ActionStableId = actionStableId ?? string.Empty;
            Preference = preference;
        }

        public IReadOnlyList<KLEPMemoryKeyCell> KeyCells => keyCells;
        public string ActionStableId { get; }
        public bool HasActionFilter => !string.IsNullOrWhiteSpace(ActionStableId);
        public KLEPEmotionalPreference Preference { get; }

        public static KLEPMemoryCue Capture(
            KLEPKeySnapshot snapshot,
            string actionStableId = "",
            KLEPEmotionalPreference preference = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var cells = new List<KLEPMemoryKeyCell>(snapshot.Facts.Count);
            for (int i = 0; i < snapshot.Facts.Count; i++)
            {
                KLEPKeyFact fact = snapshot.Facts[i];
                cells.Add(new KLEPMemoryKeyCell(
                    fact.Scope,
                    fact.KeyId.Value));
            }

            return new KLEPMemoryCue(cells, actionStableId, preference);
        }
    }

    public sealed class KLEPMemoryKeyFrequency
    {
        public KLEPMemoryKeyFrequency(
            KLEPMemoryKeyCell cell,
            long hitCount,
            long encounterCount)
        {
            KLEPMemoryValidation.ValidateCell(cell, nameof(cell));
            if (hitCount <= 0 || encounterCount <= 0 || hitCount > encounterCount)
            {
                throw new ArgumentOutOfRangeException(nameof(hitCount));
            }

            Cell = cell;
            HitCount = hitCount;
            EncounterCount = encounterCount;
        }

        public KLEPMemoryKeyCell Cell { get; }
        public long HitCount { get; }
        public long EncounterCount { get; }
        public float Frequency => (float)((double)HitCount / EncounterCount);
    }

    public sealed class KLEPMemoryPhaseKeyFrequency
    {
        public KLEPMemoryPhaseKeyFrequency(
            KLEPMemoryPhaseKeyCell cell,
            long hitCount,
            long encounterCount)
        {
            KLEPMemoryValidation.ValidatePhaseCell(cell, nameof(cell));
            if (hitCount <= 0 || encounterCount <= 0 || hitCount > encounterCount)
            {
                throw new ArgumentOutOfRangeException(nameof(hitCount));
            }

            Cell = cell;
            HitCount = hitCount;
            EncounterCount = encounterCount;
        }

        public KLEPMemoryPhaseKeyCell Cell { get; }
        public long HitCount { get; }
        public long EncounterCount { get; }
        public float Frequency => (float)((double)HitCount / EncounterCount);
    }

    public sealed class KLEPMemoryTransition
    {
        public KLEPMemoryTransition(
            long tick,
            KLEPMemoryTransitionKind kind,
            string clusterId,
            string experienceId,
            float heatBefore,
            float freshnessHeat,
            float repetitionHeat,
            float emotionalSalience,
            float cooling,
            float heatAfter,
            string reasonCode)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }

            if (!Enum.IsDefined(typeof(KLEPMemoryTransitionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            KLEPMemoryValidation.RequireFiniteNonNegative(
                heatBefore,
                nameof(heatBefore));
            KLEPMemoryValidation.RequireFiniteNonNegative(
                freshnessHeat,
                nameof(freshnessHeat));
            KLEPMemoryValidation.RequireFiniteNonNegative(
                repetitionHeat,
                nameof(repetitionHeat));
            KLEPMemoryValidation.RequireFiniteNonNegative(
                emotionalSalience,
                nameof(emotionalSalience));
            KLEPMemoryValidation.RequireFiniteNonNegative(
                cooling,
                nameof(cooling));
            KLEPMemoryValidation.RequireFiniteNonNegative(
                heatAfter,
                nameof(heatAfter));
            Tick = tick;
            Kind = kind;
            ClusterId = clusterId ?? string.Empty;
            ExperienceId = experienceId ?? string.Empty;
            HeatBefore = heatBefore;
            FreshnessHeat = freshnessHeat;
            RepetitionHeat = repetitionHeat;
            EmotionalSalience = emotionalSalience;
            Cooling = cooling;
            HeatAfter = heatAfter;
            ReasonCode = KLEPMemoryValidation.RequireId(
                reasonCode,
                nameof(reasonCode));
        }

        public long Tick { get; }
        public KLEPMemoryTransitionKind Kind { get; }
        public string ClusterId { get; }
        public string ExperienceId { get; }
        public float HeatBefore { get; }
        public float FreshnessHeat { get; }
        public float RepetitionHeat { get; }
        public float EmotionalSalience { get; }
        public float Cooling { get; }
        public float HeatAfter { get; }
        public string ReasonCode { get; }
    }

    public sealed class KLEPMemoryClusterSnapshot
    {
        private readonly ReadOnlyCollection<KLEPMemoryPhaseKeyFrequency>
            phaseKeyFrequencies;
        private readonly ReadOnlyCollection<KLEPMemoryPhaseKeyCell>
            corePhaseKeyCells;
        private readonly ReadOnlyCollection<KLEPMemoryExperience> recentEpisodes;
        private readonly ReadOnlyCollection<KLEPMemoryExperience> memorableEpisodes;

        public KLEPMemoryClusterSnapshot(
            string clusterId,
            string actionStableId,
            long encounterCount,
            long firstEncounterTick,
            long lastEncounterTick,
            float heat,
            bool isWorking,
            bool isArchived,
            bool isIndelible,
            long traumaCount,
            float peakEmotionalSwing,
            long succeededCount,
            long failedCount,
            long cancelledCount,
            long faultedCount,
            long producedEmotionCount,
            double producedEmotionSumX,
            double producedEmotionSumY,
            double producedPositionSquaredMagnitudeSum,
            double producedVelocitySumX,
            double producedVelocitySumY,
            double producedSpeedSum,
            KLEPEmotionVector mostRecentProducedEmotion,
            KLEPEmotionVector mostRecentProducedVelocity,
            IReadOnlyList<KLEPMemoryPhaseKeyFrequency> phaseKeyFrequencies,
            IReadOnlyList<KLEPMemoryPhaseKeyCell> corePhaseKeyCells,
            IReadOnlyList<KLEPMemoryExperience> recentEpisodes,
            IReadOnlyList<KLEPMemoryExperience> memorableEpisodes)
        {
            ClusterId = KLEPMemoryValidation.RequireId(
                clusterId,
                nameof(clusterId));
            if (encounterCount <= 0 ||
                firstEncounterTick < 0 ||
                lastEncounterTick < firstEncounterTick)
            {
                throw new ArgumentOutOfRangeException(nameof(encounterCount));
            }

            KLEPMemoryValidation.RequireFiniteNonNegative(heat, nameof(heat));
            KLEPMemoryValidation.RequireFiniteNonNegative(
                peakEmotionalSwing,
                nameof(peakEmotionalSwing));
            if ((!isWorking && !isArchived) ||
                (isIndelible && !isArchived) ||
                traumaCount < 0 || traumaCount > encounterCount ||
                succeededCount < 0 || failedCount < 0 ||
                cancelledCount < 0 || faultedCount < 0)
            {
                throw new ArgumentException(
                    "Cluster state contains impossible retention or outcome counts.",
                    nameof(encounterCount));
            }

            if (!KLEPMemoryValidation.TrySumOutcomeCounts(
                    encounterCount,
                    succeededCount,
                    failedCount,
                    cancelledCount,
                    faultedCount,
                    out long outcomeCount))
            {
                throw new ArgumentException(
                    "Cluster terminal outcome counts cannot exceed its encounters.",
                    nameof(encounterCount));
            }

            ActionStableId = string.IsNullOrEmpty(actionStableId)
                ? string.Empty
                : KLEPMemoryValidation.RequireId(
                    actionStableId,
                    nameof(actionStableId));
            if ((ActionStableId.Length == 0 && outcomeCount != 0) ||
                (ActionStableId.Length > 0 && outcomeCount != encounterCount))
            {
                throw new ArgumentException(
                    "A cluster's action identity and terminal outcome counts must describe every encounter or none.",
                    nameof(actionStableId));
            }

            EncounterCount = encounterCount;
            FirstEncounterTick = firstEncounterTick;
            LastEncounterTick = lastEncounterTick;
            Heat = heat;
            IsWorking = isWorking;
            IsArchived = isArchived;
            IsIndelible = isIndelible;
            TraumaCount = traumaCount;
            PeakEmotionalSwing = peakEmotionalSwing;
            SucceededCount = succeededCount;
            FailedCount = failedCount;
            CancelledCount = cancelledCount;
            FaultedCount = faultedCount;
            if (producedEmotionCount < 0 ||
                producedEmotionCount > encounterCount ||
                traumaCount > producedEmotionCount ||
                peakEmotionalSwing >
                    KLEPMemoryValidation.MaximumEmotionDistance ||
                double.IsNaN(producedEmotionSumX) ||
                double.IsInfinity(producedEmotionSumX) ||
                double.IsNaN(producedEmotionSumY) ||
                double.IsInfinity(producedEmotionSumY) ||
                double.IsNaN(producedPositionSquaredMagnitudeSum) ||
                double.IsInfinity(producedPositionSquaredMagnitudeSum) ||
                double.IsNaN(producedVelocitySumX) ||
                double.IsInfinity(producedVelocitySumX) ||
                double.IsNaN(producedVelocitySumY) ||
                double.IsInfinity(producedVelocitySumY) ||
                double.IsNaN(producedSpeedSum) ||
                double.IsInfinity(producedSpeedSum))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(producedEmotionCount));
            }

            if (producedEmotionCount == 0 &&
                (producedEmotionSumX != 0d || producedEmotionSumY != 0d ||
                 producedPositionSquaredMagnitudeSum != 0d ||
                 producedVelocitySumX != 0d || producedVelocitySumY != 0d ||
                 producedSpeedSum != 0d || peakEmotionalSwing != 0f ||
                 mostRecentProducedEmotion != KLEPEmotionVector.Zero ||
                 mostRecentProducedVelocity != KLEPEmotionVector.Zero))
            {
                throw new ArgumentException(
                    "A cluster with no emotional consequences cannot have emotional sums.",
                    nameof(producedEmotionSumX));
            }

            if (Math.Abs(producedEmotionSumX) > producedEmotionCount ||
                Math.Abs(producedEmotionSumY) > producedEmotionCount ||
                producedPositionSquaredMagnitudeSum < 0d ||
                producedPositionSquaredMagnitudeSum >
                    2d * producedEmotionCount ||
                Math.Abs(producedVelocitySumX) > producedEmotionCount ||
                Math.Abs(producedVelocitySumY) > producedEmotionCount ||
                producedSpeedSum < 0d ||
                producedSpeedSum > producedEmotionCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(producedEmotionSumX),
                    "Produced Emotion sums must be possible on normalized axes.");
            }

            if (producedEmotionCount > 0)
            {
                double positionLowerBound =
                    ((producedEmotionSumX * producedEmotionSumX) +
                     (producedEmotionSumY * producedEmotionSumY)) /
                    producedEmotionCount;
                double speedLowerBound = Math.Sqrt(
                    (producedVelocitySumX * producedVelocitySumX) +
                    (producedVelocitySumY * producedVelocitySumY));
                double positionTolerance = 1e-9d * Math.Max(
                    1d,
                    positionLowerBound);
                double speedTolerance = 1e-9d * Math.Max(
                    1d,
                    speedLowerBound);
                if (producedPositionSquaredMagnitudeSum + positionTolerance <
                        positionLowerBound ||
                    producedSpeedSum + speedTolerance < speedLowerBound ||
                    mostRecentProducedVelocity.Magnitude > 1f)
                {
                    throw new ArgumentException(
                        "Produced Emotion aggregates cannot contradict their non-cancelling position or speed evidence.",
                        nameof(producedEmotionSumX));
                }
            }

            ProducedEmotionCount = producedEmotionCount;
            ProducedEmotionSumX = producedEmotionSumX;
            ProducedEmotionSumY = producedEmotionSumY;
            ProducedPositionSquaredMagnitudeSum =
                producedPositionSquaredMagnitudeSum;
            ProducedVelocitySumX = producedVelocitySumX;
            ProducedVelocitySumY = producedVelocitySumY;
            ProducedSpeedSum = producedSpeedSum;
            MostRecentProducedEmotion = mostRecentProducedEmotion;
            MostRecentProducedVelocity = mostRecentProducedVelocity;
            this.phaseKeyFrequencies =
                KLEPMemoryValidation.CopyPhaseFrequencies(
                    phaseKeyFrequencies,
                    nameof(phaseKeyFrequencies));
            for (int i = 0; i < this.phaseKeyFrequencies.Count; i++)
            {
                if (this.phaseKeyFrequencies[i].EncounterCount != encounterCount)
                {
                    throw new ArgumentException(
                        "Every projector frequency must use the cluster encounter count.",
                        nameof(phaseKeyFrequencies));
                }
            }
            this.corePhaseKeyCells = KLEPMemoryValidation.CopyUniquePhaseCells(
                corePhaseKeyCells,
                nameof(corePhaseKeyCells));
            this.recentEpisodes = KLEPMemoryValidation.CopyExperiences(
                recentEpisodes,
                nameof(recentEpisodes));
            this.memorableEpisodes = KLEPMemoryValidation.CopyExperiences(
                memorableEpisodes,
                nameof(memorableEpisodes));
        }

        public string ClusterId { get; }
        public string ActionStableId { get; }
        public long EncounterCount { get; }
        public long FirstEncounterTick { get; }
        public long LastEncounterTick { get; }
        public float Heat { get; }
        public bool IsWorking { get; }
        public bool IsArchived { get; }
        public bool IsIndelible { get; }
        public long TraumaCount { get; }
        public float PeakEmotionalSwing { get; }
        public long SucceededCount { get; }
        public long FailedCount { get; }
        public long CancelledCount { get; }
        public long FaultedCount { get; }
        public long ProducedEmotionCount { get; }
        public double ProducedEmotionSumX { get; }
        public double ProducedEmotionSumY { get; }
        public double ProducedPositionSquaredMagnitudeSum { get; }
        public double ProducedVelocitySumX { get; }
        public double ProducedVelocitySumY { get; }
        public double ProducedSpeedSum { get; }
        public bool HasProducedEmotion => ProducedEmotionCount > 0;
        public KLEPEmotionVector AverageProducedEmotion =>
            HasProducedEmotion
                ? new KLEPEmotionVector(
                    (float)(ProducedEmotionSumX / ProducedEmotionCount),
                    (float)(ProducedEmotionSumY / ProducedEmotionCount))
                : KLEPEmotionVector.Zero;
        public KLEPEmotionVector AverageProducedVelocity =>
            HasProducedEmotion
                ? new KLEPEmotionVector(
                    (float)(ProducedVelocitySumX / ProducedEmotionCount),
                    (float)(ProducedVelocitySumY / ProducedEmotionCount))
                : KLEPEmotionVector.Zero;
        public float AverageProducedSpeed => HasProducedEmotion
            ? (float)(ProducedSpeedSum / ProducedEmotionCount)
            : 0f;
        public float RootMeanSquareDistanceTo(KLEPEmotionVector desiredState)
        {
            if (!HasProducedEmotion)
            {
                return 0f;
            }

            double meanSquared =
                (ProducedPositionSquaredMagnitudeSum /
                    ProducedEmotionCount) -
                (2d * desiredState.X * ProducedEmotionSumX /
                    ProducedEmotionCount) -
                (2d * desiredState.Y * ProducedEmotionSumY /
                    ProducedEmotionCount) +
                ((double)desiredState.X * desiredState.X) +
                ((double)desiredState.Y * desiredState.Y);
            // Exact float-derived sums should be nonnegative. Clamp a tiny
            // negative caused only by double arithmetic order at restoration.
            return (float)Math.Sqrt(Math.Max(0d, meanSquared));
        }
        public KLEPEmotionVector MostRecentProducedEmotion { get; }
        public KLEPEmotionVector MostRecentProducedVelocity { get; }
        public IReadOnlyList<KLEPMemoryPhaseKeyFrequency> PhaseKeyFrequencies =>
            phaseKeyFrequencies;
        public IReadOnlyList<KLEPMemoryPhaseKeyCell> CorePhaseKeyCells =>
            corePhaseKeyCells;
        public IReadOnlyList<KLEPMemoryExperience> RecentEpisodes => recentEpisodes;
        public IReadOnlyList<KLEPMemoryExperience> MemorableEpisodes => memorableEpisodes;
    }

    public sealed class KLEPMemorySnapshot
    {
        private readonly ReadOnlyCollection<KLEPMemoryClusterSnapshot> clusters;
        private readonly ReadOnlyCollection<KLEPMemoryTransition> transitions;

        public KLEPMemorySnapshot(
            string ownerId,
            long tick,
            IReadOnlyList<KLEPMemoryClusterSnapshot> clusters,
            IReadOnlyList<KLEPMemoryTransition> transitions)
        {
            OwnerId = KLEPMemoryValidation.RequireId(
                ownerId,
                nameof(ownerId));
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }

            Tick = tick;
            this.clusters = KLEPMemoryValidation.CopyClusters(
                clusters,
                nameof(clusters));
            this.transitions = KLEPMemoryValidation.CopyTransitions(
                transitions,
                nameof(transitions));
            for (int i = 0; i < this.clusters.Count; i++)
            {
                if (this.clusters[i].LastEncounterTick > tick)
                {
                    throw new ArgumentException(
                        "A snapshot cannot contain a cluster from a future Tick.",
                        nameof(clusters));
                }
            }

            for (int i = 0; i < this.transitions.Count; i++)
            {
                if (this.transitions[i].Tick != tick)
                {
                    throw new ArgumentException(
                        "Every snapshot transition must belong to the snapshot Tick.",
                        nameof(transitions));
                }
            }
        }

        public string OwnerId { get; }
        public long Tick { get; }
        public IReadOnlyList<KLEPMemoryClusterSnapshot> Clusters => clusters;
        public IReadOnlyList<KLEPMemoryTransition> Transitions => transitions;
        public int WorkingClusterCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < clusters.Count; i++)
                {
                    if (clusters[i].IsWorking)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ArchivedClusterCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < clusters.Count; i++)
                {
                    if (clusters[i].IsArchived)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }

    public sealed class KLEPMemoryRecall
    {
        internal KLEPMemoryRecall(
            KLEPMemoryClusterSnapshot cluster,
            float cueSimilarity,
            float repetitionStrength,
            float freshnessStrength,
            float emotionalStrength,
            float recallStrength,
            float? preferenceAffinity)
        {
            Cluster = cluster;
            CueSimilarity = cueSimilarity;
            RepetitionStrength = repetitionStrength;
            FreshnessStrength = freshnessStrength;
            EmotionalStrength = emotionalStrength;
            RecallStrength = recallStrength;
            PreferenceAffinity = preferenceAffinity;
        }

        public KLEPMemoryClusterSnapshot Cluster { get; }
        public float CueSimilarity { get; }
        public float RepetitionStrength { get; }
        public float FreshnessStrength { get; }
        public float EmotionalStrength { get; }
        public float RecallStrength { get; }
        public float? PreferenceAffinity { get; }
    }

    public sealed class KLEPMemoryRecallResult
    {
        private readonly ReadOnlyCollection<KLEPMemoryRecall> matches;

        internal KLEPMemoryRecallResult(
            long tick,
            KLEPMemoryCue cue,
            IReadOnlyList<KLEPMemoryRecall> matches)
        {
            Tick = tick;
            Cue = cue;
            this.matches = KLEPMemoryValidation.CopyRecalls(
                matches,
                nameof(matches));
        }

        public long Tick { get; }
        public KLEPMemoryCue Cue { get; }
        public IReadOnlyList<KLEPMemoryRecall> Matches => matches;
    }

    /// <summary>
    /// Complete data-first state for host-owned save/load. KLEP does not choose
    /// a file path or Unity serializer; a host may encode this as binary, XML,
    /// or another project format and restore this Agent's Memory subsystem
    /// state later. Live Keys, Executables, Emotion, and Agent learning are
    /// separate host-owned persistence concerns.
    /// </summary>
    public sealed class KLEPMemoryState
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<KLEPMemoryClusterSnapshot> clusters;
        private readonly ReadOnlyCollection<string> seenExperienceIds;
        private readonly ReadOnlyCollection<KLEPMemorySnapshot> snapshotHistory;
        private readonly ReadOnlyCollection<KLEPMemoryTransition> lastTransitions;

        public KLEPMemoryState(
            string ownerId,
            long tick,
            long nextClusterSequence,
            KLEPMemoryConfiguration configuration,
            IReadOnlyList<KLEPMemoryClusterSnapshot> clusters,
            IReadOnlyList<string> seenExperienceIds,
            IReadOnlyList<KLEPMemorySnapshot> snapshotHistory,
            IReadOnlyList<KLEPMemoryTransition> lastTransitions = null,
            int schemaVersion = CurrentSchemaVersion)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new NotSupportedException(
                    $"Memory state schema {schemaVersion} is not supported.");
            }

            SchemaVersion = schemaVersion;
            OwnerId = KLEPMemoryValidation.RequireId(ownerId, nameof(ownerId));
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }

            if (nextClusterSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nextClusterSequence));
            }

            Tick = tick;
            NextClusterSequence = nextClusterSequence;
            Configuration = configuration ??
                throw new ArgumentNullException(nameof(configuration));
            this.clusters = KLEPMemoryValidation.CopyClusters(
                clusters,
                nameof(clusters));
            this.seenExperienceIds = KLEPMemoryValidation.CopyIds(
                seenExperienceIds,
                nameof(seenExperienceIds));
            ReadOnlyCollection<KLEPMemorySnapshot> suppliedHistory =
                KLEPMemoryValidation.CopySnapshots(
                snapshotHistory,
                nameof(snapshotHistory));
            this.lastTransitions = KLEPMemoryValidation.CopyTransitions(
                lastTransitions,
                nameof(lastTransitions));
            if (suppliedHistory.Count == 0)
            {
                this.snapshotHistory = suppliedHistory;
            }
            else
            {
                KLEPMemorySnapshot suppliedTail =
                    suppliedHistory[suppliedHistory.Count - 1];
                if (!StringComparer.Ordinal.Equals(
                        suppliedTail.OwnerId,
                        OwnerId) ||
                    suppliedTail.Tick != Tick)
                {
                    throw new ArgumentException(
                        "Memory snapshot history must end at the continuation owner and Tick.",
                        nameof(snapshotHistory));
                }

                var canonicalHistory = new List<KLEPMemorySnapshot>(
                    suppliedHistory.Count);
                for (int i = 0; i < suppliedHistory.Count - 1; i++)
                {
                    canonicalHistory.Add(suppliedHistory[i]);
                }

                canonicalHistory.Add(new KLEPMemorySnapshot(
                    OwnerId,
                    Tick,
                    this.clusters,
                    this.lastTransitions));
                this.snapshotHistory =
                    new ReadOnlyCollection<KLEPMemorySnapshot>(
                        canonicalHistory);
            }
        }

        public int SchemaVersion { get; }
        public string OwnerId { get; }
        public long Tick { get; }
        public long NextClusterSequence { get; }
        public KLEPMemoryConfiguration Configuration { get; }
        public IReadOnlyList<KLEPMemoryClusterSnapshot> Clusters => clusters;
        public IReadOnlyList<string> SeenExperienceIds => seenExperienceIds;
        public IReadOnlyList<KLEPMemorySnapshot> SnapshotHistory =>
            snapshotHistory;
        public IReadOnlyList<KLEPMemoryTransition> LastTransitions =>
            lastTransitions;
    }

    internal static class KLEPMemoryValidation
    {
        internal const float MaximumEmotionDistance = 2.8284272f;

        internal static bool TrySumOutcomeCounts(
            long encounterCount,
            long succeededCount,
            long failedCount,
            long cancelledCount,
            long faultedCount,
            out long total)
        {
            total = 0;
            if (encounterCount < 0 || succeededCount < 0 || failedCount < 0 ||
                cancelledCount < 0 || faultedCount < 0)
            {
                return false;
            }

            long remaining = encounterCount;
            if (succeededCount > remaining)
            {
                return false;
            }

            remaining -= succeededCount;
            if (failedCount > remaining)
            {
                return false;
            }

            remaining -= failedCount;
            if (cancelledCount > remaining)
            {
                return false;
            }

            remaining -= cancelledCount;
            if (faultedCount > remaining)
            {
                return false;
            }

            remaining -= faultedCount;
            total = encounterCount - remaining;
            return true;
        }

        internal static void ValidateCell(
            KLEPMemoryKeyCell cell,
            string parameterName)
        {
            if (!Enum.IsDefined(typeof(KLEPKeyScope), cell.Scope) ||
                string.IsNullOrWhiteSpace(cell.KeyId))
            {
                throw new ArgumentException(
                    "Memory Key cells must contain a valid scope and stable Key ID.",
                    parameterName);
            }
        }

        internal static void ValidatePhaseCell(
            KLEPMemoryPhaseKeyCell cell,
            string parameterName)
        {
            if (!Enum.IsDefined(typeof(KLEPMemoryMomentRole), cell.Role))
            {
                throw new ArgumentException(
                    "Memory phase cells require a valid causal role.",
                    parameterName);
            }

            ValidateCell(cell.KeyCell, parameterName);
        }

        internal static bool IsAfter(
            long leftTick,
            int leftWave,
            long rightTick,
            int rightWave)
        {
            return leftTick > rightTick ||
                (leftTick == rightTick && leftWave > rightWave);
        }

        internal static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Memory identities cannot be empty.",
                    parameterName);
            }

            return value;
        }

        internal static float RequireFiniteNonNegative(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static float RequireFinitePositive(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static float RequireUnitInterval(
            float value,
            string parameterName,
            bool allowZero)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) ||
                value > 1f || (allowZero ? value < 0f : value <= 0f))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static int RequirePositiveCapacity(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static float ClampSignedUnit(double value)
        {
            if (value <= -1d)
            {
                return -1f;
            }

            if (value >= 1d)
            {
                return 1f;
            }

            return (float)value;
        }

        internal static ReadOnlyCollection<KLEPKeyField> CopyFields(
            IReadOnlyList<KLEPKeyField> source,
            string parameterName)
        {
            if (source == null)
            {
                return new ReadOnlyCollection<KLEPKeyField>(
                    Array.Empty<KLEPKeyField>());
            }

            var copied = new List<KLEPKeyField>(source.Count);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.Count; i++)
            {
                KLEPKeyField field = source[i];
                if (string.IsNullOrWhiteSpace(field.Name) ||
                    !Enum.IsDefined(typeof(KLEPKeyValueKind), field.Value.Kind) ||
                    field.Value.Kind == KLEPKeyValueKind.None)
                {
                    throw new ArgumentException(
                        "Remembered payload fields require a stable name and explicit scalar value.",
                        parameterName);
                }

                if (!names.Add(field.Name))
                {
                    throw new ArgumentException(
                        $"Payload field '{field.Name}' occurs more than once.",
                        parameterName);
                }

                copied.Add(field);
            }

            copied.Sort((left, right) =>
                StringComparer.Ordinal.Compare(left.Name, right.Name));
            return new ReadOnlyCollection<KLEPKeyField>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryKeyRecord> CopyKeys(
            IReadOnlyList<KLEPMemoryKeyRecord> source,
            string parameterName)
        {
            var copied = new List<KLEPMemoryKeyRecord>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    copied.Add(source[i] ?? throw new ArgumentException(
                        "Memory Key collections cannot contain null.",
                        parameterName));
                }
            }

            copied.Sort(CompareKeys);
            return new ReadOnlyCollection<KLEPMemoryKeyRecord>(copied);
        }

        internal static int CompareKeys(
            KLEPMemoryKeyRecord left,
            KLEPMemoryKeyRecord right)
        {
            int cell = left.Cell.CompareTo(right.Cell);
            if (cell != 0)
            {
                return cell;
            }

            int store = StringComparer.Ordinal.Compare(
                left.OccurrenceStoreId,
                right.OccurrenceStoreId);
            return store != 0
                ? store
                : left.OccurrenceSequence.CompareTo(right.OccurrenceSequence);
        }

        internal static ReadOnlyCollection<KLEPMemoryKeyCell> UniqueCells(
            IReadOnlyList<KLEPMemoryKeyRecord> keys)
        {
            var set = new HashSet<KLEPMemoryKeyCell>();
            for (int i = 0; i < keys.Count; i++)
            {
                set.Add(keys[i].Cell);
            }

            var copied = new List<KLEPMemoryKeyCell>(set);
            copied.Sort();
            return new ReadOnlyCollection<KLEPMemoryKeyCell>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryKeyCell> CopyUniqueCells(
            IReadOnlyList<KLEPMemoryKeyCell> source,
            string parameterName)
        {
            var set = new HashSet<KLEPMemoryKeyCell>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    ValidateCell(source[i], parameterName);
                    set.Add(source[i]);
                }
            }

            var copied = new List<KLEPMemoryKeyCell>(set);
            copied.Sort();
            return new ReadOnlyCollection<KLEPMemoryKeyCell>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryMoment> CopyMoments(
            IReadOnlyList<KLEPMemoryMoment> source,
            string parameterName)
        {
            var copied = new List<KLEPMemoryMoment>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    copied.Add(source[i] ?? throw new ArgumentException(
                        "Experience moments cannot contain null.",
                        parameterName));
                }
            }

            return new ReadOnlyCollection<KLEPMemoryMoment>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryKeyCell> UnionCells(
            IReadOnlyList<KLEPMemoryMoment> moments)
        {
            var set = new HashSet<KLEPMemoryKeyCell>();
            for (int i = 0; i < moments.Count; i++)
            {
                IReadOnlyList<KLEPMemoryKeyCell> cells = moments[i].KeyCells;
                for (int j = 0; j < cells.Count; j++)
                {
                    set.Add(cells[j]);
                }
            }

            var copied = new List<KLEPMemoryKeyCell>(set);
            copied.Sort();
            return new ReadOnlyCollection<KLEPMemoryKeyCell>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryKeyCell> UnionCells(
            IReadOnlyList<KLEPMemoryMoment> moments,
            KLEPMemoryMomentRole role)
        {
            var set = new HashSet<KLEPMemoryKeyCell>();
            for (int i = 0; i < moments.Count; i++)
            {
                if (moments[i].Role != role)
                {
                    continue;
                }

                IReadOnlyList<KLEPMemoryKeyCell> cells = moments[i].KeyCells;
                for (int j = 0; j < cells.Count; j++)
                {
                    set.Add(cells[j]);
                }
            }

            var copied = new List<KLEPMemoryKeyCell>(set);
            copied.Sort();
            return new ReadOnlyCollection<KLEPMemoryKeyCell>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryPhaseKeyCell>
            BuildPhaseCells(IReadOnlyList<KLEPMemoryMoment> moments)
        {
            var set = new HashSet<KLEPMemoryPhaseKeyCell>();
            for (int i = 0; i < moments.Count; i++)
            {
                KLEPMemoryMoment moment = moments[i];
                for (int j = 0; j < moment.KeyCells.Count; j++)
                {
                    set.Add(new KLEPMemoryPhaseKeyCell(
                        moment.Role,
                        moment.KeyCells[j]));
                }
            }

            var copied = new List<KLEPMemoryPhaseKeyCell>(set);
            copied.Sort();
            return new ReadOnlyCollection<KLEPMemoryPhaseKeyCell>(copied);
        }

        internal static string BuildGistId(
            IReadOnlyList<KLEPMemoryKeyCell> cells)
        {
            if (cells.Count == 0)
            {
                return "<empty>";
            }

            var text = new StringBuilder();
            for (int i = 0; i < cells.Count; i++)
            {
                KLEPMemoryKeyCell cell = cells[i];
                text.Append(cell.Scope == KLEPKeyScope.Local ? 'L' : 'G')
                    .Append(':')
                    .Append(cell.KeyId.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(cell.KeyId)
                    .Append(';');
            }

            return text.ToString();
        }

        internal static string BuildPhaseGistId(
            IReadOnlyList<KLEPMemoryPhaseKeyCell> cells)
        {
            if (cells.Count == 0)
            {
                return "<empty>";
            }

            var text = new StringBuilder();
            for (int i = 0; i < cells.Count; i++)
            {
                KLEPMemoryPhaseKeyCell cell = cells[i];
                text.Append(cell.Role.ToString())
                    .Append(':')
                    .Append(cell.KeyCell.Scope == KLEPKeyScope.Local ? 'L' : 'G')
                    .Append(':')
                    .Append(cell.KeyCell.KeyId.Length.ToString(
                        CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(cell.KeyCell.KeyId)
                    .Append(';');
            }

            return text.ToString();
        }

        internal static ReadOnlyCollection<KLEPMemoryEthicsTraceRecord>
            CopyEthicsTrace(
                IReadOnlyList<KLEPMemoryEthicsTraceRecord> source,
                string parameterName)
        {
            var copied = new List<KLEPMemoryEthicsTraceRecord>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    copied.Add(source[i] ?? throw new ArgumentException(
                        "Ethics trace records cannot contain null.",
                        parameterName));
                }
            }

            return new ReadOnlyCollection<KLEPMemoryEthicsTraceRecord>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryEthicsRecord> CopyEthics(
            IReadOnlyList<KLEPMemoryEthicsRecord> source,
            string parameterName)
        {
            var copied = new List<KLEPMemoryEthicsRecord>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    copied.Add(source[i] ?? throw new ArgumentException(
                        "Ethics records cannot contain null.",
                        parameterName));
                }
            }

            return new ReadOnlyCollection<KLEPMemoryEthicsRecord>(copied);
        }

        internal static ReadOnlyCollection<string> CopyIds(
            IReadOnlyList<string> source,
            string parameterName)
        {
            var copied = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    string id = RequireId(source[i], parameterName);
                    if (!ids.Add(id))
                    {
                        throw new ArgumentException(
                            $"Identity '{id}' occurs more than once.",
                            parameterName);
                    }

                    copied.Add(id);
                }
            }

            return new ReadOnlyCollection<string>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryKeyFrequency> CopyFrequencies(
            IReadOnlyList<KLEPMemoryKeyFrequency> source,
            string parameterName)
        {
            var copied = new List<KLEPMemoryKeyFrequency>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    copied.Add(source[i] ?? throw new ArgumentException(
                        "Key frequencies cannot contain null.",
                        parameterName));
                }
            }

            copied.Sort((left, right) => left.Cell.CompareTo(right.Cell));
            return new ReadOnlyCollection<KLEPMemoryKeyFrequency>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryPhaseKeyCell>
            CopyUniquePhaseCells(
                IReadOnlyList<KLEPMemoryPhaseKeyCell> source,
                string parameterName)
        {
            var set = new HashSet<KLEPMemoryPhaseKeyCell>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    ValidatePhaseCell(source[i], parameterName);
                    set.Add(source[i]);
                }
            }

            var copied = new List<KLEPMemoryPhaseKeyCell>(set);
            copied.Sort();
            return new ReadOnlyCollection<KLEPMemoryPhaseKeyCell>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryPhaseKeyFrequency>
            CopyPhaseFrequencies(
                IReadOnlyList<KLEPMemoryPhaseKeyFrequency> source,
                string parameterName)
        {
            var copied = new List<KLEPMemoryPhaseKeyFrequency>();
            var cells = new HashSet<KLEPMemoryPhaseKeyCell>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    KLEPMemoryPhaseKeyFrequency frequency = source[i] ??
                        throw new ArgumentException(
                            "Phase Key frequencies cannot contain null.",
                            parameterName);
                    if (!cells.Add(frequency.Cell))
                    {
                        throw new ArgumentException(
                            $"Phase Key cell '{frequency.Cell}' occurs more than once.",
                            parameterName);
                    }

                    copied.Add(frequency);
                }
            }

            copied.Sort((left, right) => left.Cell.CompareTo(right.Cell));
            return new ReadOnlyCollection<KLEPMemoryPhaseKeyFrequency>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryExperience> CopyExperiences(
            IReadOnlyList<KLEPMemoryExperience> source,
            string parameterName)
        {
            var copied = new List<KLEPMemoryExperience>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    copied.Add(source[i] ?? throw new ArgumentException(
                        "Experience collections cannot contain null.",
                        parameterName));
                }
            }

            return new ReadOnlyCollection<KLEPMemoryExperience>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryClusterSnapshot> CopyClusters(
            IReadOnlyList<KLEPMemoryClusterSnapshot> source,
            string parameterName)
        {
            var copied = new List<KLEPMemoryClusterSnapshot>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    copied.Add(source[i] ?? throw new ArgumentException(
                        "Cluster collections cannot contain null.",
                        parameterName));
                }
            }

            copied.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.ClusterId,
                right.ClusterId));
            return new ReadOnlyCollection<KLEPMemoryClusterSnapshot>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryTransition> CopyTransitions(
            IReadOnlyList<KLEPMemoryTransition> source,
            string parameterName)
        {
            var copied = new List<KLEPMemoryTransition>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    copied.Add(source[i] ?? throw new ArgumentException(
                        "Memory transitions cannot contain null.",
                        parameterName));
                }
            }

            return new ReadOnlyCollection<KLEPMemoryTransition>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemorySnapshot> CopySnapshots(
            IReadOnlyList<KLEPMemorySnapshot> source,
            string parameterName)
        {
            var copied = new List<KLEPMemorySnapshot>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    copied.Add(source[i] ?? throw new ArgumentException(
                        "Memory snapshot history cannot contain null.",
                        parameterName));
                }
            }

            return new ReadOnlyCollection<KLEPMemorySnapshot>(copied);
        }

        internal static ReadOnlyCollection<KLEPMemoryRecall> CopyRecalls(
            IReadOnlyList<KLEPMemoryRecall> source,
            string parameterName)
        {
            var copied = new List<KLEPMemoryRecall>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    copied.Add(source[i] ?? throw new ArgumentException(
                        "Recall results cannot contain null.",
                        parameterName));
                }
            }

            return new ReadOnlyCollection<KLEPMemoryRecall>(copied);
        }
    }
}
