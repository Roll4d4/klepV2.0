using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Immutable edge evidence reduced from eight ordinal ground probes around
    /// an agent. Probe directions and avoidance coordinates are local to the
    /// sampling frame: Forward, ForwardRight, Right, BackRight, Back,
    /// BackLeft, Left, and ForwardLeft.
    /// </summary>
    public sealed class KLEPEdgeObservation
    {
        public const int ProbeCount = 8;
        public const int AllSupportedProbeMask = 0xFF;

        public const string SupportedProbeMaskField = "supportedProbeMask";
        public const string MissingProbeMaskField = "missingProbeMask";
        public const string MissingCountField = "missingCount";
        public const string AvoidanceXField = "avoidanceX";
        public const string AvoidanceZField = "avoidanceZ";

        private const double DiagonalComponent =
            0.70710678118654752440084436210485d;
        private const double SymmetryEpsilonSquared = 1e-24d;
        private const double PayloadTolerance = 1e-12d;

        private static readonly double[] ProbeDirectionX =
        {
            0d,
            DiagonalComponent,
            1d,
            DiagonalComponent,
            0d,
            -DiagonalComponent,
            -1d,
            -DiagonalComponent
        };

        private static readonly double[] ProbeDirectionZ =
        {
            1d,
            DiagonalComponent,
            0d,
            -DiagonalComponent,
            -1d,
            -DiagonalComponent,
            0d,
            DiagonalComponent
        };

        private KLEPEdgeObservation(
            int supportedProbeMask,
            int missingProbeMask,
            int missingCount,
            double avoidanceX,
            double avoidanceZ)
        {
            SupportedProbeMask = supportedProbeMask;
            MissingProbeMask = missingProbeMask;
            MissingCount = missingCount;
            AvoidanceX = avoidanceX;
            AvoidanceZ = avoidanceZ;
        }

        /// <summary>
        /// Bit <c>n</c> is one when ordinal probe <c>n</c> found supporting
        /// ground. Only the low eight bits are valid.
        /// </summary>
        public int SupportedProbeMask { get; }

        /// <summary>
        /// The low-eight-bit complement of <see cref="SupportedProbeMask"/>.
        /// </summary>
        public int MissingProbeMask { get; }

        public int MissingCount { get; }

        /// <summary>
        /// Finite unit avoidance direction along the sampling frame's local X
        /// axis. It points away from the missing probes.
        /// </summary>
        public double AvoidanceX { get; }

        /// <summary>
        /// Finite unit avoidance direction along the sampling frame's local Z
        /// axis. It points away from the missing probes.
        /// </summary>
        public double AvoidanceZ { get; }

        /// <summary>
        /// Returns the fixed local unit direction assigned to one probe bit.
        /// This keeps Unity sampling geometry and pure mask reduction on the
        /// same ordinal contract.
        /// </summary>
        public static void GetProbeDirection(
            int probeIndex,
            out double directionX,
            out double directionZ)
        {
            if (probeIndex < 0 || probeIndex >= ProbeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(probeIndex));
            }

            directionX = ProbeDirectionX[probeIndex];
            directionZ = ProbeDirectionZ[probeIndex];
        }

        /// <summary>
        /// Reduces one complete support mask to edge evidence. All-supported is
        /// a valid no-edge sample and returns false. Invalid high bits throw.
        /// Symmetric missing-probe sums fall back opposite the lowest ordinal
        /// missing probe, so every non-empty missing mask has one stable unit
        /// direction.
        /// </summary>
        public static bool TryCreate(
            int supportedProbeMask,
            out KLEPEdgeObservation observation)
        {
            if (supportedProbeMask < 0 ||
                supportedProbeMask > AllSupportedProbeMask)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(supportedProbeMask),
                    "An edge support mask may use only its low eight bits.");
            }

            int missingProbeMask =
                AllSupportedProbeMask ^ supportedProbeMask;
            if (missingProbeMask == 0)
            {
                observation = null;
                return false;
            }

            int missingCount = 0;
            int firstMissingProbe = -1;
            double missingDirectionX = 0d;
            double missingDirectionZ = 0d;
            for (int probeIndex = 0;
                 probeIndex < ProbeCount;
                 probeIndex++)
            {
                int bit = 1 << probeIndex;
                if ((missingProbeMask & bit) == 0)
                {
                    continue;
                }

                if (firstMissingProbe < 0)
                {
                    firstMissingProbe = probeIndex;
                }

                missingCount++;
                missingDirectionX += ProbeDirectionX[probeIndex];
                missingDirectionZ += ProbeDirectionZ[probeIndex];
            }

            double avoidanceX = -missingDirectionX;
            double avoidanceZ = -missingDirectionZ;
            double magnitudeSquared =
                avoidanceX * avoidanceX + avoidanceZ * avoidanceZ;
            if (magnitudeSquared <= SymmetryEpsilonSquared)
            {
                avoidanceX = -ProbeDirectionX[firstMissingProbe];
                avoidanceZ = -ProbeDirectionZ[firstMissingProbe];
                magnitudeSquared =
                    avoidanceX * avoidanceX + avoidanceZ * avoidanceZ;
            }

            double inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
            avoidanceX = NormalizeSignedZero(
                avoidanceX * inverseMagnitude);
            avoidanceZ = NormalizeSignedZero(
                avoidanceZ * inverseMagnitude);
            RequireFiniteUnitDirection(avoidanceX, avoidanceZ);

            observation = new KLEPEdgeObservation(
                supportedProbeMask,
                missingProbeMask,
                missingCount,
                avoidanceX,
                avoidanceZ);
            return true;
        }

        public KLEPKeyPayload ToPayload()
        {
            return new KLEPKeyPayload(new[]
            {
                new KeyValuePair<string, KLEPKeyValue>(
                    SupportedProbeMaskField,
                    SupportedProbeMask),
                new KeyValuePair<string, KLEPKeyValue>(
                    MissingProbeMaskField,
                    MissingProbeMask),
                new KeyValuePair<string, KLEPKeyValue>(
                    MissingCountField,
                    MissingCount),
                new KeyValuePair<string, KLEPKeyValue>(
                    AvoidanceXField,
                    AvoidanceX),
                new KeyValuePair<string, KLEPKeyValue>(
                    AvoidanceZField,
                    AvoidanceZ)
            });
        }

        public static bool TryRead(
            KLEPKeyPayload payload,
            out KLEPEdgeObservation observation)
        {
            observation = null;
            if (payload == null ||
                !payload.TryGetInteger(
                    SupportedProbeMaskField,
                    out long supportedProbeMask) ||
                !payload.TryGetInteger(
                    MissingProbeMaskField,
                    out long missingProbeMask) ||
                !payload.TryGetInteger(
                    MissingCountField,
                    out long missingCount) ||
                !payload.TryGetNumber(
                    AvoidanceXField,
                    out double avoidanceX) ||
                !payload.TryGetNumber(
                    AvoidanceZField,
                    out double avoidanceZ) ||
                supportedProbeMask < 0 ||
                supportedProbeMask > AllSupportedProbeMask)
            {
                return false;
            }

            if (!TryCreate((int)supportedProbeMask, out KLEPEdgeObservation reduced) ||
                missingProbeMask != reduced.MissingProbeMask ||
                missingCount != reduced.MissingCount ||
                !NearlyEqual(avoidanceX, reduced.AvoidanceX) ||
                !NearlyEqual(avoidanceZ, reduced.AvoidanceZ))
            {
                return false;
            }

            observation = reduced;
            return true;
        }

        public static KLEPEdgeObservation Read(KLEPKeyPayload payload)
        {
            if (!TryRead(payload, out KLEPEdgeObservation observation))
            {
                throw new InvalidOperationException(
                    "An Edge observation payload requires a non-all-supported " +
                    "low-eight-bit support mask, its exact complementary missing " +
                    "mask and count, and the corresponding finite normalized " +
                    "probe-local avoidance X/Z direction.");
            }

            return observation;
        }

        private static double NormalizeSignedZero(double value)
        {
            return Math.Abs(value) <= PayloadTolerance ? 0d : value;
        }

        private static bool NearlyEqual(double left, double right)
        {
            return !double.IsNaN(left) &&
                !double.IsInfinity(left) &&
                Math.Abs(left - right) <= PayloadTolerance;
        }

        private static void RequireFiniteUnitDirection(double x, double z)
        {
            if (double.IsNaN(x) ||
                double.IsInfinity(x) ||
                double.IsNaN(z) ||
                double.IsInfinity(z))
            {
                throw new InvalidOperationException(
                    "Edge avoidance reduction produced a non-finite direction.");
            }

            double lengthSquared = x * x + z * z;
            if (Math.Abs(lengthSquared - 1d) > PayloadTolerance)
            {
                throw new InvalidOperationException(
                    "Edge avoidance reduction did not produce a unit direction.");
            }
        }
    }
}
