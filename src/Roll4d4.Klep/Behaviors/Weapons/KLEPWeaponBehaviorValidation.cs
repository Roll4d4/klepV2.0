using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    internal static class KLEPWeaponBehaviorValidation
    {
        internal static void RequireExecutableShape(
            KLEPExecutableDefinition definition,
            KLEPExecutableKind kind,
            KLEPExecutionMode mode,
            string behaviorName)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.Kind != kind)
            {
                throw new ArgumentException(
                    $"{behaviorName} requires Executable Kind {kind}.",
                    nameof(definition));
            }

            if (definition.ExecutionMode != mode)
            {
                throw new ArgumentException(
                    $"{behaviorName} requires {mode} execution mode.",
                    nameof(definition));
            }
        }

        internal static void RequireClosedLocalOneCycle(
            KLEPKeyDefinition definition,
            string parameterName,
            string role)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (definition.Scope != KLEPKeyScope.Local ||
                definition.DefaultLifetime != KLEPKeyLifetime.OneCycle)
            {
                throw new ArgumentException(
                    $"{role} Key '{definition.Id}' must be Local and OneCycle.",
                    parameterName);
            }

            if (definition.DefaultPayload.Count != 0)
            {
                throw new ArgumentException(
                    $"{role} Key '{definition.Id}' must have an empty default " +
                    "payload so its closed typed schema cannot be silently merged.",
                    parameterName);
            }
        }

        internal static void RequireSingleExactDeclaredOutput(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition output,
            string role)
        {
            if (definition.DeclaredOutputs.Count != 1 ||
                !definition.TryGetDeclaredOutput(
                    output.Id,
                    out KLEPKeyDefinition declared) ||
                !ReferenceEquals(declared, output))
            {
                throw new ArgumentException(
                    $"{role} Key '{output.Id}' must be the one exact declared " +
                    $"output of Executable '{definition.StableId}'.",
                    nameof(definition));
            }
        }

        internal static void RequireDistinctDefinitions(
            string parameterName,
            params KLEPKeyDefinition[] definitions)
        {
            var ids = new HashSet<KLEPKeyId>();
            foreach (KLEPKeyDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentNullException(parameterName);
                }

                if (!ids.Add(definition.Id))
                {
                    throw new ArgumentException(
                        $"Weapon Key role '{definition.Id}' was supplied more " +
                        "than once.",
                        parameterName);
                }
            }
        }

        internal static bool TryReadSingleObservation(
            KLEPKeySnapshot keys,
            KLEPKeyDefinition observationDefinition,
            string behaviorName,
            out KLEPWeaponObservation observation)
        {
            IReadOnlyList<KLEPKeyFact> facts =
                keys.FindAll(observationDefinition.Id);
            if (facts.Count == 0)
            {
                observation = null;
                return false;
            }

            if (facts.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{behaviorName} expected exactly one " +
                    $"'{observationDefinition.Id}' occurrence, but found " +
                    $"{facts.Count}.");
            }

            observation = KLEPWeaponObservation.Read(facts[0].Payload);
            return true;
        }
    }
}
