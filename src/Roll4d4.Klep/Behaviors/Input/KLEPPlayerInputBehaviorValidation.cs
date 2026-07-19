using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    internal static class KLEPPlayerInputBehaviorValidation
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

        internal static void RequireLocalOneCycle(
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
        }

        internal static void RequireExactDeclaredOutput(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition output,
            string role)
        {
            if (!definition.TryGetDeclaredOutput(
                    output.Id,
                    out KLEPKeyDefinition declared) ||
                !ReferenceEquals(declared, output))
            {
                throw new ArgumentException(
                    $"{role} Key '{output.Id}' must be the exact declared " +
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
                if (!ids.Add(definition.Id))
                {
                    throw new ArgumentException(
                        $"Input Key '{definition.Id}' was supplied more than once.",
                        parameterName);
                }
            }
        }

        internal static bool TryReadSingleAim(
            KLEPKeySnapshot keys,
            KLEPKeyDefinition aimDefinition,
            out KLEPMouseAimObservation aim)
        {
            IReadOnlyList<KLEPKeyFact> facts = keys.FindAll(aimDefinition.Id);
            if (facts.Count == 0)
            {
                aim = null;
                return false;
            }

            if (facts.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Player locomotion expected exactly one " +
                    $"'{aimDefinition.Id}' occurrence, but found {facts.Count}.");
            }

            aim = KLEPMouseAimObservation.Read(facts[0].Payload);
            return true;
        }
    }
}
