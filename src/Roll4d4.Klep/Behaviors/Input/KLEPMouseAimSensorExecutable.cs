using System;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Publishes the latest externally supplied world aim point as one Local
    /// OneCycle MouseAim occurrence during a Tandem wave.
    /// </summary>
    public sealed class KLEPMouseAimSensorExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition mouseAimDefinition;
        private KLEPMouseAimObservation observation;

        public KLEPMouseAimSensorExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition mouseAimDefinition)
            : base(definition)
        {
            KLEPPlayerInputBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Sensor,
                KLEPExecutionMode.Tandem,
                "A mouse aim sensor");
            KLEPPlayerInputBehaviorValidation.RequireLocalOneCycle(
                mouseAimDefinition,
                nameof(mouseAimDefinition),
                "MouseAim");

            if (definition.DeclaredOutputs.Count != 1)
            {
                throw new ArgumentException(
                    "A mouse aim sensor requires exactly one declared output.",
                    nameof(definition));
            }

            KLEPPlayerInputBehaviorValidation.RequireExactDeclaredOutput(
                definition,
                mouseAimDefinition,
                "MouseAim");
            this.mouseAimDefinition = mouseAimDefinition;
        }

        public KLEPKeyDefinition MouseAimDefinition => mouseAimDefinition;
        public KLEPMouseAimObservation Observation => observation;

        /// <summary>
        /// Supplies the next aim observation. Null explicitly clears it.
        /// </summary>
        public void SetObservation(KLEPMouseAimObservation observation)
        {
            this.observation = observation;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (observation == null)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            context.Add(mouseAimDefinition, observation.ToPayload());
            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
