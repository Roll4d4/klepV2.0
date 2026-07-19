using System;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// One immutable keyboard sample supplied by a host before a KLEP Tick.
    /// The sample itself is not a Key payload: each pressed direction is
    /// published as its own Local OneCycle presence Key.
    /// </summary>
    public sealed class KLEPKeyboardMovementInput
    {
        private const double DiagonalComponent = 0.7071067811865475244d;

        public KLEPKeyboardMovementInput(
            bool w,
            bool a,
            bool s,
            bool d)
        {
            W = w;
            A = a;
            S = s;
            D = d;

            int horizontal = (d ? 1 : 0) - (a ? 1 : 0);
            int forward = (w ? 1 : 0) - (s ? 1 : 0);
            if (horizontal != 0 && forward != 0)
            {
                LocalX = horizontal * DiagonalComponent;
                LocalZ = forward * DiagonalComponent;
            }
            else
            {
                LocalX = horizontal;
                LocalZ = forward;
            }
        }

        public static KLEPKeyboardMovementInput None { get; } =
            new KLEPKeyboardMovementInput(false, false, false, false);

        public bool W { get; }
        public bool A { get; }
        public bool S { get; }
        public bool D { get; }

        /// <summary>
        /// Gets the normalized actor-local right component. A and D cancel.
        /// </summary>
        public double LocalX { get; }

        /// <summary>
        /// Gets the normalized actor-local forward component. W and S cancel.
        /// </summary>
        public double LocalZ { get; }

        public bool HasMovement => LocalX != 0d || LocalZ != 0d;
    }
}
