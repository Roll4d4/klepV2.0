using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Publishes an externally supplied enemy sample as deterministic,
    /// transient EnemyDetected occurrences during a Tandem wave.
    /// </summary>
    public sealed class KLEPEnemyObservationSensorExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition enemyDetectedDefinition;
        private IReadOnlyList<KLEPEnemyObservation> observations =
            Array.Empty<KLEPEnemyObservation>();

        public KLEPEnemyObservationSensorExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition enemyDetectedDefinition)
            : base(definition)
        {
            KLEPZombieBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Sensor,
                KLEPExecutionMode.Tandem,
                "An enemy observation sensor");
            KLEPZombieBehaviorValidation.RequireLocalOneCycle(
                enemyDetectedDefinition,
                nameof(enemyDetectedDefinition),
                "EnemyDetected");

            if (definition.DeclaredOutputs.Count != 1)
            {
                throw new ArgumentException(
                    "An enemy observation sensor requires exactly one declared output.",
                    nameof(definition));
            }

            KLEPZombieBehaviorValidation.RequireExactDeclaredOutput(
                definition,
                enemyDetectedDefinition,
                "EnemyDetected");
            this.enemyDetectedDefinition = enemyDetectedDefinition;
        }

        public KLEPKeyDefinition EnemyDetectedDefinition =>
            enemyDetectedDefinition;
        public IReadOnlyList<KLEPEnemyObservation> Observations => observations;

        /// <summary>
        /// Copies, ordinally sorts, and validates the complete sample for the
        /// next Tick. Several colliders for one entity must be collapsed by the
        /// host before this boundary; duplicate entity IDs are rejected.
        /// </summary>
        public void SetObservations(
            IEnumerable<KLEPEnemyObservation> observations)
        {
            if (observations == null)
            {
                throw new ArgumentNullException(nameof(observations));
            }

            var copy = new List<KLEPEnemyObservation>();
            foreach (KLEPEnemyObservation observation in observations)
            {
                if (observation == null)
                {
                    throw new ArgumentException(
                        "Enemy observations cannot contain null.",
                        nameof(observations));
                }

                copy.Add(observation);
            }

            copy.Sort(KLEPEnemyObservation.CompareByEntityId);
            for (int index = 1; index < copy.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(
                        copy[index - 1].EntityId,
                        copy[index].EntityId))
                {
                    throw new ArgumentException(
                        $"Enemy entity ID '{copy[index].EntityId}' appears more " +
                        "than once in one observation sample.",
                        nameof(observations));
                }
            }

            this.observations =
                new ReadOnlyCollection<KLEPEnemyObservation>(copy);
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (observations.Count == 0)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            foreach (KLEPEnemyObservation observation in observations)
            {
                context.Add(
                    enemyDetectedDefinition,
                    observation.ToPayload());
            }

            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
