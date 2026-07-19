using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Roll4d4.Klep.Cognition")]

namespace Roll4d4.Klep.Emotion
{
    /// <summary>
    /// Identifies whether the evaluated cause occurred within the owning
    /// cognitive system or outside it. This describes the cause, not the
    /// authority or location of the evaluator. The value is provenance only;
    /// it does not change the Emotion math.
    /// </summary>
    public enum KLEPEmotionInfluenceOrigin
    {
        Internal,
        External
    }

    /// <summary>
    /// One point or impulse on KLEP's normalized, designer-labelled 2D
    /// emotional graph. Each axis is bounded to [-1, 1]. KLEP does not assign
    /// an ethical or psychological meaning to either axis.
    /// </summary>
    public readonly struct KLEPEmotionVector : IEquatable<KLEPEmotionVector>
    {
        public KLEPEmotionVector(float x, float y)
        {
            X = RequireAxisValue(x, nameof(x));
            Y = RequireAxisValue(y, nameof(y));
        }

        public static KLEPEmotionVector Zero { get; } =
            new KLEPEmotionVector(0f, 0f);

        public float X { get; }
        public float Y { get; }

        public float Magnitude =>
            (float)Math.Sqrt(((double)X * X) + ((double)Y * Y));

        public float DistanceTo(KLEPEmotionVector other)
        {
            double x = (double)other.X - X;
            double y = (double)other.Y - Y;
            return (float)Math.Sqrt((x * x) + (y * y));
        }

        public bool Equals(KLEPEmotionVector other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is KLEPEmotionVector other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(
            KLEPEmotionVector left,
            KLEPEmotionVector right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            KLEPEmotionVector left,
            KLEPEmotionVector right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0}, {1})",
                X,
                Y);
        }

        private static float RequireAxisValue(float value, string parameterName)
        {
            if (float.IsNaN(value) ||
                float.IsInfinity(value) ||
                value < -1f ||
                value > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Emotion axis values must be finite and within [-1, 1].");
            }

            return value;
        }
    }

    /// <summary>
    /// An already-evaluated emotional impulse. The caller supplies this value;
    /// Emotion records and integrates it but does not decide whether the
    /// evaluation is correct or who had authority to produce it.
    /// </summary>
    public readonly struct KLEPEmotionInfluence
    {
        public KLEPEmotionInfluence(
            string sourceId,
            KLEPEmotionInfluenceOrigin origin,
            KLEPEmotionVector impulse)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException(
                    "An Emotion influence requires a stable source ID.",
                    nameof(sourceId));
            }

            if (origin != KLEPEmotionInfluenceOrigin.Internal &&
                origin != KLEPEmotionInfluenceOrigin.External)
            {
                throw new ArgumentOutOfRangeException(nameof(origin));
            }

            SourceId = sourceId;
            Origin = origin;
            Impulse = impulse;
        }

        public string SourceId { get; }
        public KLEPEmotionInfluenceOrigin Origin { get; }
        public KLEPEmotionVector Impulse { get; }

        internal void Validate(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(SourceId) ||
                (Origin != KLEPEmotionInfluenceOrigin.Internal &&
                 Origin != KLEPEmotionInfluenceOrigin.External))
            {
                throw new ArgumentException(
                    "The influence collection contains an uninitialized or invalid value.",
                    parameterName);
            }
        }
    }

    /// <summary>
    /// Immutable configuration for a normalized 2D emotional body. Axis names
    /// are descriptive only. Friction reduces speed by the configured amount
    /// when that change is representable; a smaller change snaps to exact rest
    /// so unsupported motion always ends in finite time.
    /// </summary>
    public sealed class KLEPEmotionConfiguration
    {
        public KLEPEmotionConfiguration(
            string axisXName = "X",
            string axisYName = "Y",
            float frictionPerTick = 0.1f,
            float maximumSpeed = 1f,
            int snapshotCapacity = 32)
        {
            AxisXName = RequireAxisName(axisXName, nameof(axisXName));
            AxisYName = RequireAxisName(axisYName, nameof(axisYName));
            if (string.Equals(AxisXName, AxisYName, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Emotion axes require distinct inspectable names.",
                    nameof(axisYName));
            }
            FrictionPerTick = RequireUnitInterval(
                frictionPerTick,
                nameof(frictionPerTick),
                allowZero: false);
            MaximumSpeed = RequireUnitInterval(
                maximumSpeed,
                nameof(maximumSpeed),
                allowZero: false);

            if (snapshotCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshotCapacity));
            }

            SnapshotCapacity = snapshotCapacity;
        }

        public static KLEPEmotionConfiguration Default { get; } =
            new KLEPEmotionConfiguration();

        public string AxisXName { get; }
        public string AxisYName { get; }
        public float FrictionPerTick { get; }
        public float MaximumSpeed { get; }
        public int SnapshotCapacity { get; }

        private static string RequireAxisName(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Emotion axes require inspectable names.",
                    parameterName);
            }

            return value;
        }

        private static float RequireUnitInterval(
            float value,
            string parameterName,
            bool allowZero)
        {
            if (float.IsNaN(value) ||
                float.IsInfinity(value) ||
                (allowZero ? value < 0f : value <= 0f) ||
                value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// Immutable evidence of one Emotion Tick. A future Memory system may
    /// retain these snapshots and associate their changes with Executables;
    /// this type performs no recall, prediction, or selection itself.
    /// </summary>
    public sealed class KLEPEmotionSnapshot
    {
        internal KLEPEmotionSnapshot(
            KLEPEmotionConfiguration configuration,
            long tick,
            KLEPEmotionVector positionBefore,
            KLEPEmotionVector velocityBefore,
            IReadOnlyList<KLEPEmotionInfluence> influences,
            KLEPEmotionVector netInfluence,
            KLEPEmotionVector integratedVelocity,
            KLEPEmotionVector position,
            KLEPEmotionVector velocity,
            long unchangedPositionTickCount)
        {
            Configuration = configuration ??
                throw new ArgumentNullException(nameof(configuration));
            Tick = tick;
            PositionBefore = positionBefore;
            VelocityBefore = velocityBefore;
            Influences = influences ??
                throw new ArgumentNullException(nameof(influences));
            NetInfluence = netInfluence;
            IntegratedVelocity = integratedVelocity;
            Position = position;
            Velocity = velocity;
            UnchangedPositionTickCount = unchangedPositionTickCount;
        }

        public KLEPEmotionConfiguration Configuration { get; }
        public long Tick { get; }
        public KLEPEmotionVector PositionBefore { get; }
        public KLEPEmotionVector VelocityBefore { get; }
        public IReadOnlyList<KLEPEmotionInfluence> Influences { get; }
        public KLEPEmotionVector NetInfluence { get; }

        /// <summary>
        /// Velocity after the Tick's influences were applied and before
        /// friction was applied for the following Tick.
        /// </summary>
        public KLEPEmotionVector IntegratedVelocity { get; }

        public KLEPEmotionVector Position { get; }

        /// <summary>
        /// Velocity carried into the following Tick after friction.
        /// </summary>
        public KLEPEmotionVector Velocity { get; }

        public long UnchangedPositionTickCount { get; }
        public float DeltaX => Position.X - PositionBefore.X;
        public float DeltaY => Position.Y - PositionBefore.Y;
        public bool DidMove => Position != PositionBefore;
        public bool IsAtRest => Velocity == KLEPEmotionVector.Zero;
    }

    /// <summary>
    /// Deterministic, inspectable 2D Emotion state. Influences change velocity;
    /// velocity changes position; linear friction brings velocity to exact rest.
    /// Position does not drift toward neutral merely because time passes.
    /// </summary>
    public sealed class KLEPEmotion
    {
        private sealed class TransactionCheckpoint
        {
            internal TransactionCheckpoint(
                KLEPEmotion owner,
                KLEPEmotionVector position,
                KLEPEmotionVector velocity,
                long tick,
                long unchangedPositionTickCount,
                KLEPEmotionSnapshot lastSnapshot,
                IReadOnlyList<KLEPEmotionSnapshot> snapshots)
            {
                Owner = owner;
                Position = position;
                Velocity = velocity;
                Tick = tick;
                UnchangedPositionTickCount = unchangedPositionTickCount;
                LastSnapshot = lastSnapshot;
                Snapshots = snapshots;
            }

            internal KLEPEmotion Owner { get; }
            internal KLEPEmotionVector Position { get; }
            internal KLEPEmotionVector Velocity { get; }
            internal long Tick { get; }
            internal long UnchangedPositionTickCount { get; }
            internal KLEPEmotionSnapshot LastSnapshot { get; }
            internal IReadOnlyList<KLEPEmotionSnapshot> Snapshots { get; }
        }

        private readonly List<KLEPEmotionSnapshot> snapshots =
            new List<KLEPEmotionSnapshot>();

        public KLEPEmotion(KLEPEmotionConfiguration configuration = null)
            : this(
                configuration,
                KLEPEmotionVector.Zero,
                KLEPEmotionVector.Zero,
                0)
        {
        }

        public KLEPEmotion(
            KLEPEmotionConfiguration configuration,
            KLEPEmotionVector initialPosition,
            KLEPEmotionVector initialVelocity,
            long initialTick = 0)
        {
            Configuration = configuration ?? KLEPEmotionConfiguration.Default;

            if (initialTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialTick));
            }

            if (initialVelocity.Magnitude > Configuration.MaximumSpeed)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialVelocity),
                    "Initial emotional velocity exceeds the configured maximum speed.");
            }

            Position = initialPosition;
            Velocity = initialVelocity;
            Tick = initialTick;
        }

        public KLEPEmotionConfiguration Configuration { get; }
        public KLEPEmotionVector Position { get; private set; }
        public KLEPEmotionVector Velocity { get; private set; }
        public long Tick { get; private set; }
        public long UnchangedPositionTickCount { get; private set; }
        public KLEPEmotionSnapshot LastSnapshot { get; private set; }

        public KLEPEmotionSnapshot Advance(long tick)
        {
            return Advance(tick, null);
        }

        public KLEPEmotionSnapshot Advance(
            long tick,
            IReadOnlyList<KLEPEmotionInfluence> influences)
        {
            RequireNextTick(tick);
            ReadOnlyCollection<KLEPEmotionInfluence> copiedInfluences =
                CopyInfluences(influences);

            KLEPEmotionVector positionBefore = Position;
            KLEPEmotionVector velocityBefore = Velocity;
            KLEPEmotionVector netInfluence =
                CombineInfluences(copiedInfluences);
            KLEPEmotionVector integratedVelocity = ClampMagnitude(
                velocityBefore.X + netInfluence.X,
                velocityBefore.Y + netInfluence.Y,
                Configuration.MaximumSpeed);
            KLEPEmotionVector position = ClampAxes(
                positionBefore.X + integratedVelocity.X,
                positionBefore.Y + integratedVelocity.Y);
            KLEPEmotionVector velocity = ApplyFriction(
                integratedVelocity,
                Configuration.FrictionPerTick);

            long unchangedPositionTickCount = position == positionBefore
                ? IncrementSaturated(UnchangedPositionTickCount)
                : 0;

            var snapshot = new KLEPEmotionSnapshot(
                Configuration,
                tick,
                positionBefore,
                velocityBefore,
                copiedInfluences,
                netInfluence,
                integratedVelocity,
                position,
                velocity,
                unchangedPositionTickCount);

            Position = position;
            Velocity = velocity;
            Tick = tick;
            UnchangedPositionTickCount = unchangedPositionTickCount;
            LastSnapshot = snapshot;

            if (snapshots.Count == Configuration.SnapshotCapacity)
            {
                snapshots.RemoveAt(0);
            }

            snapshots.Add(snapshot);
            return snapshot;
        }

        public IReadOnlyList<KLEPEmotionSnapshot> GetSnapshotHistory()
        {
            return Array.AsReadOnly(snapshots.ToArray());
        }

        /// <summary>
        /// Captures exact mutable Emotion state for the trusted Cognition
        /// transaction boundary. This is deliberately internal: ordinary
        /// callers observe immutable snapshots and cannot rewind Emotion.
        /// </summary>
        internal object CaptureTransactionCheckpoint()
        {
            return new TransactionCheckpoint(
                this,
                Position,
                Velocity,
                Tick,
                UnchangedPositionTickCount,
                LastSnapshot,
                Array.AsReadOnly(snapshots.ToArray()));
        }

        /// <summary>
        /// Restores a checkpoint captured from this exact Emotion instance.
        /// Cognition uses this only when its later Memory phase faults.
        /// </summary>
        internal void RestoreTransactionCheckpoint(object checkpoint)
        {
            var state = checkpoint as TransactionCheckpoint;
            if (state == null || !ReferenceEquals(state.Owner, this))
            {
                throw new ArgumentException(
                    "The Emotion transaction checkpoint belongs to another instance.",
                    nameof(checkpoint));
            }

            Position = state.Position;
            Velocity = state.Velocity;
            Tick = state.Tick;
            UnchangedPositionTickCount = state.UnchangedPositionTickCount;
            LastSnapshot = state.LastSnapshot;
            snapshots.Clear();
            for (int i = 0; i < state.Snapshots.Count; i++)
            {
                snapshots.Add(state.Snapshots[i]);
            }
        }

        private void RequireNextTick(long tick)
        {
            if (Tick == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "Emotion cannot advance beyond Int64.MaxValue.");
            }

            if (tick != Tick + 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tick),
                    "Emotion must advance exactly once on the next supplied Tick.");
            }
        }

        private static ReadOnlyCollection<KLEPEmotionInfluence> CopyInfluences(
            IReadOnlyList<KLEPEmotionInfluence> influences)
        {
            if (influences == null)
            {
                return Array.AsReadOnly(Array.Empty<KLEPEmotionInfluence>());
            }

            var copied = new List<KLEPEmotionInfluence>();
            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < influences.Count; i++)
            {
                KLEPEmotionInfluence influence = influences[i];
                influence.Validate(nameof(influences));
                if (!sourceIds.Add(influence.SourceId))
                {
                    throw new ArgumentException(
                        $"Emotion influence source '{influence.SourceId}' occurs more than once in one Tick.",
                        nameof(influences));
                }

                copied.Add(influence);
            }

            return Array.AsReadOnly(copied.ToArray());
        }

        private static KLEPEmotionVector CombineInfluences(
            IReadOnlyList<KLEPEmotionInfluence> influences)
        {
            double x = 0d;
            double y = 0d;
            for (int i = 0; i < influences.Count; i++)
            {
                x += influences[i].Impulse.X;
                y += influences[i].Impulse.Y;
            }

            return ClampAxes(x, y);
        }

        private static KLEPEmotionVector ClampMagnitude(
            double x,
            double y,
            float maximumMagnitude)
        {
            double magnitude = Math.Sqrt((x * x) + (y * y));
            if (magnitude <= maximumMagnitude)
            {
                return new KLEPEmotionVector((float)x, (float)y);
            }

            double scale = maximumMagnitude / magnitude;
            return new KLEPEmotionVector(
                (float)(x * scale),
                (float)(y * scale));
        }

        private static KLEPEmotionVector ClampAxes(double x, double y)
        {
            return new KLEPEmotionVector(ClampAxis(x), ClampAxis(y));
        }

        private static float ClampAxis(double value)
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

        private static KLEPEmotionVector ApplyFriction(
            KLEPEmotionVector velocity,
            float friction)
        {
            double magnitude = velocity.Magnitude;
            if (magnitude <= friction)
            {
                return KLEPEmotionVector.Zero;
            }

            double remainingMagnitude = magnitude - friction;
            double scale = remainingMagnitude / magnitude;
            var reduced = new KLEPEmotionVector(
                (float)(velocity.X * scale),
                (float)(velocity.Y * scale));

            // Extremely small positive friction can round away when converted
            // back to Single. Exact rest is a protected invariant, so an
            // unrepresentable reduction stops rather than moving forever.
            return reduced == velocity
                ? KLEPEmotionVector.Zero
                : reduced;
        }

        private static long IncrementSaturated(long value)
        {
            return value == long.MaxValue ? long.MaxValue : value + 1;
        }
    }
}
