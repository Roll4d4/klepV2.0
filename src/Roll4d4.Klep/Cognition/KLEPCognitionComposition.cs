using System;
using System.Collections.Generic;
using Roll4d4.Klep.Emotion;
using Roll4d4.Klep.Ethics;
using Roll4d4.Klep.Memory;
using Roll4d4.Klep.Observer;

namespace Roll4d4.Klep.Cognition
{
    /// <summary>
    /// Engine-free production composition root for one Agent's first
    /// higher-cognition arrangement. It owns the causal
    /// Ethics -> Emotion -> Memory coordinator and exposes one Observer built
    /// from the read-only Memory and Emotion policy adapters.
    ///
    /// The composition still does not construct or Tick an Agent or Neuron.
    /// A host injects Observer into its Agent boundary and explicitly calls
    /// Process only when it possesses a completed causal experience.
    /// "Owns" here means that this root retains and consistently wires the
    /// injected subsystem instances; it is not exclusive capability ownership.
    /// Because those portable subsystem APIs are public and mutable, the host
    /// must treat Process as their causal write boundary and avoid advancing
    /// Emotion or Memory independently.
    /// </summary>
    public sealed class KLEPCognitionComposition<TContext>
    {
        public KLEPCognitionComposition(
            string observerStableId,
            string observerVersion,
            KLEPEthics<TContext> ethics,
            KLEPEmotion emotion,
            KLEPMemory memory,
            IKLEPMemoryObserverEvidencePolicy memoryPolicy,
            IKLEPEmotionObserverEvidencePolicy emotionPolicy,
            IEnumerable<IKLEPObserverEvidenceSource> additionalEvidenceSources = null,
            KLEPObserverConfiguration observerConfiguration = null,
            int maximumMemoryMatches = 8)
        {
            Coordinator = new KLEPCognitionCoordinator<TContext>(
                ethics,
                emotion,
                memory);
            MemoryEvidenceSource = new KLEPMemoryObserverEvidenceSource(
                memory,
                memoryPolicy,
                maximumMemoryMatches);
            EmotionEvidenceSource = new KLEPEmotionObserverEvidenceSource(
                emotion,
                emotionPolicy);

            var sources = new List<IKLEPObserverEvidenceSource>
            {
                MemoryEvidenceSource,
                EmotionEvidenceSource
            };
            if (additionalEvidenceSources != null)
            {
                foreach (IKLEPObserverEvidenceSource source in
                         additionalEvidenceSources)
                {
                    sources.Add(source ?? throw new ArgumentException(
                        "Additional cognition evidence sources cannot contain null.",
                        nameof(additionalEvidenceSources)));
                }
            }

            Observer = new KLEPObserver(
                observerStableId,
                observerVersion,
                sources,
                observerConfiguration);
        }

        public KLEPCognitionCoordinator<TContext> Coordinator { get; }
        public KLEPEthics<TContext> Ethics => Coordinator.Ethics;
        public KLEPEmotion Emotion => Coordinator.Emotion;
        public KLEPMemory Memory => Coordinator.Memory;
        public KLEPMemoryObserverEvidenceSource MemoryEvidenceSource { get; }
        public KLEPEmotionObserverEvidenceSource EmotionEvidenceSource { get; }
        public KLEPObserver Observer { get; }

        public KLEPCognitionTransition<TContext> Process(
            KLEPCognitionExperienceRequest<TContext> request)
        {
            return Coordinator.Process(request);
        }
    }
}
