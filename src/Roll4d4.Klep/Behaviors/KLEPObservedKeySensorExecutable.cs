using System;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Converts one boolean observation supplied by a host into a transient Key.
    /// The host samples its world before the Agent Tick and calls
    /// <see cref="SetObservation"/>; this behavior does not read clocks, physics,
    /// Unity objects, or any other external state while the Tick is executing.
    /// </summary>
    public sealed class KLEPObservedKeySensorExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition outputDefinition;
        private bool isPresent;

        /// <summary>
        /// Creates a presence sensor from immutable authored definition data.
        /// </summary>
        /// <param name="definition">
        /// A Tandem Sensor definition declaring exactly one OneCycle output.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="definition"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The definition is not a Tandem Sensor, does not declare exactly one
        /// output, or declares an output whose lifetime is not OneCycle.
        /// </exception>
        public KLEPObservedKeySensorExecutable(KLEPExecutableDefinition definition)
            : base(definition)
        {
            if (definition.Kind != KLEPExecutableKind.Sensor)
            {
                throw new ArgumentException(
                    "An observed Key sensor requires Executable Kind Sensor.",
                    nameof(definition));
            }

            if (definition.ExecutionMode != KLEPExecutionMode.Tandem)
            {
                throw new ArgumentException(
                    "An observed Key sensor requires Tandem execution mode.",
                    nameof(definition));
            }

            if (definition.DeclaredOutputs.Count != 1)
            {
                throw new ArgumentException(
                    "An observed Key sensor requires exactly one declared output.",
                    nameof(definition));
            }

            outputDefinition = definition.DeclaredOutputs[0];
            if (outputDefinition.DefaultLifetime != KLEPKeyLifetime.OneCycle)
            {
                throw new ArgumentException(
                    "An observed Key sensor output must use OneCycle lifetime.",
                    nameof(definition));
            }
        }

        /// <summary>
        /// Gets the most recent presence value supplied by the host.
        /// </summary>
        public bool IsPresent => isPresent;

        /// <summary>
        /// Gets the exact immutable Key definition emitted for a present
        /// observation.
        /// </summary>
        public KLEPKeyDefinition OutputDefinition => outputDefinition;

        /// <summary>
        /// Supplies the deterministic observation that will be consumed by the
        /// next Agent Tick. Repeated calls before that Tick use the last value.
        /// </summary>
        /// <param name="isPresent">
        /// <see langword="true"/> when the observed condition is present;
        /// otherwise, <see langword="false"/>.
        /// </param>
        public void SetObservation(bool isPresent)
        {
            this.isPresent = isPresent;
        }

        /// <summary>
        /// Emits the exact declared OneCycle Key and succeeds for a present
        /// observation. An absent observation fails without output because a
        /// successful run must fulfill every declared output promise.
        /// </summary>
        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (!isPresent)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            context.Add(outputDefinition);
            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
