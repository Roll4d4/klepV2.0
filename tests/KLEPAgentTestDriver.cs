using System;
using System.Runtime.CompilerServices;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// Test-only convenience for older focused fixtures that are concerned
    /// with Key or Executable behavior rather than Agent construction. Every
    /// advance still travels through exactly one retained KLEPAgent.
    /// </summary>
    internal static class KLEPAgentTestDriver
    {
        private sealed class Driver
        {
            internal Driver(KLEPNeuron neuron, float certaintyThreshold)
            {
                CertaintyThreshold = certaintyThreshold;
                Agent = new KLEPAgent(
                    neuron,
                    new KLEPAgentConfiguration(
                        actionCertaintyThreshold: certaintyThreshold));
            }

            internal float CertaintyThreshold { get; }
            internal KLEPAgent Agent { get; }
        }

        private static readonly ConditionalWeakTable<KLEPNeuron, Driver> Drivers =
            new ConditionalWeakTable<KLEPNeuron, Driver>();

        internal static KLEPAgent AgentViaTest(
            this KLEPNeuron neuron,
            float certaintyThreshold = 0f)
        {
            if (neuron == null)
            {
                throw new ArgumentNullException(nameof(neuron));
            }

            if (float.IsNaN(certaintyThreshold) ||
                float.IsInfinity(certaintyThreshold))
            {
                throw new ArgumentOutOfRangeException(nameof(certaintyThreshold));
            }

            Driver driver = Drivers.GetValue(
                neuron,
                value => new Driver(value, certaintyThreshold));
            if (driver.CertaintyThreshold != certaintyThreshold)
            {
                throw new InvalidOperationException(
                    "A test Neuron's retained Agent cannot change its certainty " +
                    "threshold after construction.");
            }

            return driver.Agent;
        }

        internal static KLEPDecisionTrace TickViaAgent(
            this KLEPNeuron neuron,
            float certaintyThreshold = 0f)
        {
            return neuron.AgentViaTest(certaintyThreshold).Tick().Decision;
        }

        internal static KLEPDecisionTrace LastDecisionViaAgent(
            this KLEPNeuron neuron)
        {
            return neuron.AgentViaTest().LastTrace.Decision;
        }
    }
}
