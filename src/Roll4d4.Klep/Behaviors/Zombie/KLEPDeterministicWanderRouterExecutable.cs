using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Publishes one deterministic local-space wander heading for each Tick.
    /// The heading changes only at an injected fixed Tick segment boundary.
    /// </summary>
    public sealed class KLEPDeterministicWanderRouterExecutable :
        KLEPExecutableBase
    {
        public const int HeadingCount = 8;
        public const string DirectionXField = "directionX";
        public const string DirectionZField = "directionZ";
        public const string HeadingIndexField = "headingIndex";

        // IEEE-754 representation of sqrt(1/2). Keeping the table authored
        // avoids platform or runtime trigonometry in the deterministic route.
        private const double Diagonal = 0.70710678118654752440084436210485d;

        private static readonly double[] HeadingX =
        {
            0d,
            Diagonal,
            1d,
            Diagonal,
            0d,
            -Diagonal,
            -1d,
            -Diagonal
        };

        private static readonly double[] HeadingZ =
        {
            1d,
            Diagonal,
            0d,
            -Diagonal,
            -1d,
            -Diagonal,
            0d,
            Diagonal
        };

        private readonly KLEPKeyDefinition wanderDirectionDefinition;

        public KLEPDeterministicWanderRouterExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition wanderDirectionDefinition,
            uint seed,
            int segmentTicks)
            : base(definition)
        {
            KLEPZombieBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Router,
                KLEPExecutionMode.Tandem,
                "A deterministic wander router");
            KLEPZombieBehaviorValidation.RequireLocalOneCycle(
                wanderDirectionDefinition,
                nameof(wanderDirectionDefinition),
                "WanderDirection");

            if (definition.DeclaredOutputs.Count != 1)
            {
                throw new ArgumentException(
                    "A deterministic wander router requires exactly one " +
                    "declared output.",
                    nameof(definition));
            }

            KLEPZombieBehaviorValidation.RequireExactDeclaredOutput(
                definition,
                wanderDirectionDefinition,
                "WanderDirection");

            if (segmentTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(segmentTicks),
                    "Wander segment Ticks must be greater than zero.");
            }

            this.wanderDirectionDefinition = wanderDirectionDefinition;
            Seed = seed;
            SegmentTicks = segmentTicks;
        }

        public KLEPKeyDefinition WanderDirectionDefinition =>
            wanderDirectionDefinition;
        public uint Seed { get; }
        public int SegmentTicks { get; }
        public long LastSegmentIndex { get; private set; } = -1;
        public int LastHeadingIndex { get; private set; } = -1;
        public double LastDirectionX { get; private set; }
        public double LastDirectionZ { get; private set; }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            long segmentIndex = SegmentIndexForCycle(
                context.CycleIndex,
                SegmentTicks);
            int headingIndex = SelectHeading(Seed, segmentIndex);
            GetHeading(
                headingIndex,
                out double directionX,
                out double directionZ);

            LastSegmentIndex = segmentIndex;
            LastHeadingIndex = headingIndex;
            LastDirectionX = directionX;
            LastDirectionZ = directionZ;

            context.Add(
                wanderDirectionDefinition,
                CreatePayload(directionX, directionZ, headingIndex));
            return KLEPExecutableTickStatus.Succeeded;
        }

        internal static void ReadPayload(
            KLEPKeyPayload payload,
            out double directionX,
            out double directionZ,
            out int headingIndex)
        {
            directionX = 0d;
            directionZ = 0d;
            headingIndex = -1;

            if (payload == null ||
                payload.Count != 3 ||
                !payload.TryGetNumber(DirectionXField, out directionX) ||
                !payload.TryGetNumber(DirectionZField, out directionZ) ||
                !payload.TryGetInteger(HeadingIndexField, out long storedHeading) ||
                !IsFinite(directionX) ||
                !IsFinite(directionZ) ||
                storedHeading < 0 ||
                storedHeading >= HeadingCount)
            {
                throw InvalidPayload();
            }

            headingIndex = (int)storedHeading;
            GetHeading(
                headingIndex,
                out double expectedX,
                out double expectedZ);
            if (directionX != expectedX || directionZ != expectedZ)
            {
                throw InvalidPayload();
            }
        }

        internal static void GetHeading(
            int headingIndex,
            out double directionX,
            out double directionZ)
        {
            if (headingIndex < 0 || headingIndex >= HeadingCount)
            {
                throw new ArgumentOutOfRangeException(nameof(headingIndex));
            }

            directionX = HeadingX[headingIndex];
            directionZ = HeadingZ[headingIndex];
        }

        private static KLEPKeyPayload CreatePayload(
            double directionX,
            double directionZ,
            int headingIndex)
        {
            return new KLEPKeyPayload(new[]
            {
                new KeyValuePair<string, KLEPKeyValue>(
                    DirectionXField,
                    directionX),
                new KeyValuePair<string, KLEPKeyValue>(
                    DirectionZField,
                    directionZ),
                new KeyValuePair<string, KLEPKeyValue>(
                    HeadingIndexField,
                    (long)headingIndex)
            });
        }

        private static long SegmentIndexForCycle(
            long cycleIndex,
            int segmentTicks)
        {
            // Core cycle indices are positive. Keeping this total for a hostile
            // context makes cycle zero share the first deterministic segment.
            return cycleIndex <= 1
                ? 0
                : (cycleIndex - 1) / segmentTicks;
        }

        private static int SelectHeading(uint seed, long segmentIndex)
        {
            unchecked
            {
                ulong segmentBits = (ulong)segmentIndex;
                uint low = (uint)segmentBits;
                uint high = (uint)(segmentBits >> 32);
                uint value = seed ^ 0x9E3779B9u;
                value ^= low * 0x85EBCA6Bu;
                value = RotateLeft(value, 13);
                value ^= high * 0xC2B2AE35u;

                // Fixed integer avalanche; no runtime RNG, string hash, time,
                // or process-specific state participates.
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (int)(value & (HeadingCount - 1));
            }
        }

        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static InvalidOperationException InvalidPayload()
        {
            return new InvalidOperationException(
                "A WanderDirection payload requires exactly finite numeric " +
                $"'{DirectionXField}' and '{DirectionZField}' fields plus an " +
                $"Int64 '{HeadingIndexField}' from 0 through " +
                $"{HeadingCount - 1}; the direction must match that fixed " +
                "normalized heading.");
        }
    }
}
