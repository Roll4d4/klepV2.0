using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// One immutable Unity-free mouse aim point in world coordinates.
    /// </summary>
    public sealed class KLEPMouseAimObservation
    {
        public const string WorldXField = "worldX";
        public const string WorldYField = "worldY";
        public const string WorldZField = "worldZ";

        public KLEPMouseAimObservation(
            double worldX,
            double worldY,
            double worldZ)
        {
            RequireFinite(worldX, nameof(worldX));
            RequireFinite(worldY, nameof(worldY));
            RequireFinite(worldZ, nameof(worldZ));

            WorldX = worldX;
            WorldY = worldY;
            WorldZ = worldZ;
        }

        public double WorldX { get; }
        public double WorldY { get; }
        public double WorldZ { get; }

        public KLEPKeyPayload ToPayload()
        {
            return new KLEPKeyPayload(new[]
            {
                new KeyValuePair<string, KLEPKeyValue>(WorldXField, WorldX),
                new KeyValuePair<string, KLEPKeyValue>(WorldYField, WorldY),
                new KeyValuePair<string, KLEPKeyValue>(WorldZField, WorldZ)
            });
        }

        public static bool TryRead(
            KLEPKeyPayload payload,
            out KLEPMouseAimObservation observation)
        {
            observation = null;
            if (payload == null ||
                !payload.TryGetNumber(WorldXField, out double worldX) ||
                !payload.TryGetNumber(WorldYField, out double worldY) ||
                !payload.TryGetNumber(WorldZField, out double worldZ))
            {
                return false;
            }

            try
            {
                observation = new KLEPMouseAimObservation(
                    worldX,
                    worldY,
                    worldZ);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        public static KLEPMouseAimObservation Read(KLEPKeyPayload payload)
        {
            if (!TryRead(payload, out KLEPMouseAimObservation observation))
            {
                throw new InvalidOperationException(
                    "A mouse aim payload requires finite numeric 'worldX', " +
                    "'worldY', and 'worldZ' fields.");
            }

            return observation;
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Mouse aim coordinates must be finite.");
            }
        }
    }
}
