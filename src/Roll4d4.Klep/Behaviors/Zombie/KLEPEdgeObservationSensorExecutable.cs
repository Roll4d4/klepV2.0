using System;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Publishes one externally sampled edge pattern as transient, immutable
    /// evidence during a Tandem wave. A null observation means all probes were
    /// supported (or the Unity source was inactive) and emits no Key.
    /// </summary>
    public sealed class KLEPEdgeObservationSensorExecutable :
        KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition edgeDangerDefinition;
        private KLEPEdgeObservation observation;

        public KLEPEdgeObservationSensorExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition edgeDangerDefinition)
            : base(definition)
        {
            KLEPZombieBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Sensor,
                KLEPExecutionMode.Tandem,
                "An edge observation sensor");
            KLEPZombieBehaviorValidation.RequireLocalOneCycle(
                edgeDangerDefinition,
                nameof(edgeDangerDefinition),
                "EdgeDanger");

            if (definition.DeclaredOutputs.Count != 1)
            {
                throw new ArgumentException(
                    "An edge observation sensor requires exactly one declared output.",
                    nameof(definition));
            }

            KLEPZombieBehaviorValidation.RequireExactDeclaredOutput(
                definition,
                edgeDangerDefinition,
                "EdgeDanger");
            this.edgeDangerDefinition = edgeDangerDefinition;
        }

        public KLEPKeyDefinition EdgeDangerDefinition =>
            edgeDangerDefinition;
        public KLEPEdgeObservation Observation => observation;

        public void SetObservation(KLEPEdgeObservation value)
        {
            observation = value;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (observation == null)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            context.Add(edgeDangerDefinition, observation.ToPayload());
            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
