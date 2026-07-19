using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    public enum KLEPEnemyTargetRoute
    {
        None,
        Move,
        Attack
    }

    /// <summary>
    /// Selects the nearest EnemyDetected occurrence for one deterministic
    /// target branch. Successful completion guarantees the definition's one
    /// declared MoveTarget or AttackTarget output; a non-matching observation
    /// fails without emitting a target.
    /// </summary>
    public sealed class KLEPEnemyTargetRouterExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition enemyDetectedDefinition;
        private readonly KLEPKeyDefinition moveTargetDefinition;
        private readonly KLEPKeyDefinition attackTargetDefinition;

        public KLEPEnemyTargetRouterExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition enemyDetectedDefinition,
            KLEPKeyDefinition moveTargetDefinition,
            KLEPKeyDefinition attackTargetDefinition,
            double attackRange)
            : base(definition)
        {
            KLEPZombieBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Router,
                KLEPExecutionMode.Tandem,
                "An enemy target router");
            KLEPZombieBehaviorValidation.RequireLocalOneCycle(
                enemyDetectedDefinition,
                nameof(enemyDetectedDefinition),
                "EnemyDetected");
            KLEPZombieBehaviorValidation.RequireLocalOneCycle(
                moveTargetDefinition,
                nameof(moveTargetDefinition),
                "MoveTarget");
            KLEPZombieBehaviorValidation.RequireLocalOneCycle(
                attackTargetDefinition,
                nameof(attackTargetDefinition),
                "AttackTarget");

            if (moveTargetDefinition.Id == attackTargetDefinition.Id)
            {
                throw new ArgumentException(
                    "MoveTarget and AttackTarget require different stable Key IDs.",
                    nameof(attackTargetDefinition));
            }

            if (definition.DeclaredOutputs.Count != 1)
            {
                throw new ArgumentException(
                    "An enemy target router requires exactly one declared output.",
                    nameof(definition));
            }

            KLEPKeyDefinition declaredOutput = definition.DeclaredOutputs[0];
            if (ReferenceEquals(declaredOutput, moveTargetDefinition))
            {
                ConfiguredRoute = KLEPEnemyTargetRoute.Move;
            }
            else if (ReferenceEquals(declaredOutput, attackTargetDefinition))
            {
                ConfiguredRoute = KLEPEnemyTargetRoute.Attack;
            }
            else
            {
                throw new ArgumentException(
                    "An enemy target router's one declared output must be " +
                    "the exact MoveTarget or AttackTarget definition instance.",
                    nameof(definition));
            }

            if (double.IsNaN(attackRange) ||
                double.IsInfinity(attackRange) ||
                attackRange < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attackRange),
                    "Attack range must be finite and cannot be negative.");
            }

            this.enemyDetectedDefinition = enemyDetectedDefinition;
            this.moveTargetDefinition = moveTargetDefinition;
            this.attackTargetDefinition = attackTargetDefinition;
            AttackRange = attackRange;
        }

        public double AttackRange { get; }
        public KLEPEnemyTargetRoute ConfiguredRoute { get; }
        public KLEPEnemyTargetRoute LastRoute { get; private set; }
        public KLEPEnemyObservation LastTarget { get; private set; }
        public KLEPKeyDefinition EnemyDetectedDefinition =>
            enemyDetectedDefinition;
        public KLEPKeyDefinition MoveTargetDefinition => moveTargetDefinition;
        public KLEPKeyDefinition AttackTargetDefinition => attackTargetDefinition;

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            IReadOnlyList<KLEPKeyFact> facts =
                context.Keys.FindAll(enemyDetectedDefinition.Id);
            if (facts.Count == 0)
            {
                LastTarget = null;
                LastRoute = KLEPEnemyTargetRoute.None;
                return KLEPExecutableTickStatus.Failed;
            }

            var entityIds = new HashSet<string>(StringComparer.Ordinal);
            KLEPEnemyObservation selected = null;
            foreach (KLEPKeyFact fact in facts)
            {
                KLEPEnemyObservation candidate =
                    KLEPEnemyObservation.Read(fact.Payload);
                if (!entityIds.Add(candidate.EntityId))
                {
                    throw new InvalidOperationException(
                        $"Enemy entity ID '{candidate.EntityId}' has more than " +
                        "one EnemyDetected occurrence in the router snapshot.");
                }

                if (selected == null ||
                    KLEPEnemyObservation.CompareForTargetSelection(
                        candidate,
                        selected) < 0)
                {
                    selected = candidate;
                }
            }

            LastTarget = selected;
            LastRoute = selected.Distance <= AttackRange
                ? KLEPEnemyTargetRoute.Attack
                : KLEPEnemyTargetRoute.Move;
            if (LastRoute != ConfiguredRoute)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            KLEPKeyDefinition output = ConfiguredRoute ==
                KLEPEnemyTargetRoute.Attack
                    ? attackTargetDefinition
                    : moveTargetDefinition;
            context.Add(output, selected.ToPayload());

            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
