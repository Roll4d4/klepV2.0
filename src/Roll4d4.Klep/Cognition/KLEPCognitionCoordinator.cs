using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Roll4d4.Klep.Emotion;
using Roll4d4.Klep.Ethics;
using Roll4d4.Klep.Memory;

namespace Roll4d4.Klep.Cognition
{
    /// <summary>
    /// The three state-producing phases of one explicit causal experience.
    /// This is an ordering trace, not a claim that subsystem clocks are one
    /// global clock.
    /// </summary>
    public enum KLEPCognitionPhase
    {
        EthicsEvaluated,
        EmotionAdvanced,
        MemoryRecorded
    }

    public sealed class KLEPCognitionStepTrace
    {
        internal KLEPCognitionStepTrace(
            KLEPCognitionPhase phase,
            long tick,
            string provenanceId)
        {
            Phase = phase;
            Tick = tick;
            ProvenanceId = KLEPCognitionValidation.RequireId(
                provenanceId,
                nameof(provenanceId));
        }

        public KLEPCognitionPhase Phase { get; }
        public long Tick { get; }
        public string ProvenanceId { get; }
    }

    /// <summary>
    /// Explicit caller-owned facts and clocks used to close one experience.
    /// Moments and action outcome must already describe observed Core truth.
    /// </summary>
    public sealed class KLEPCognitionExperienceRequest<TContext>
    {
        private readonly ReadOnlyCollection<KLEPMemoryMoment> moments;

        public KLEPCognitionExperienceRequest(
            string experienceId,
            long memoryTick,
            long emotionTick,
            string evaluationId,
            long evaluationTick,
            KLEPEmotionInfluenceOrigin causeOrigin,
            KLEPEthicsContextIdentity contextIdentity,
            TContext context,
            IReadOnlyList<KLEPMemoryMoment> moments,
            KLEPMemoryActionOutcome actionOutcome = null)
        {
            ExperienceId = KLEPCognitionValidation.RequireId(
                experienceId,
                nameof(experienceId));
            EvaluationId = KLEPCognitionValidation.RequireId(
                evaluationId,
                nameof(evaluationId));
            if (memoryTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(memoryTick));
            }

            if (emotionTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(emotionTick));
            }

            if (evaluationTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(evaluationTick));
            }

            if (causeOrigin != KLEPEmotionInfluenceOrigin.Internal &&
                causeOrigin != KLEPEmotionInfluenceOrigin.External)
            {
                throw new ArgumentOutOfRangeException(nameof(causeOrigin));
            }

            ContextIdentity = contextIdentity ??
                throw new ArgumentNullException(nameof(contextIdentity));
            if (ReferenceEquals(context, null))
            {
                throw new ArgumentNullException(nameof(context));
            }

            var copiedMoments = new List<KLEPMemoryMoment>();
            if (moments != null)
            {
                for (int i = 0; i < moments.Count; i++)
                {
                    copiedMoments.Add(moments[i] ??
                        throw new ArgumentException(
                            "Cognition moments cannot contain null.",
                            nameof(moments)));
                }
            }

            MemoryTick = memoryTick;
            EmotionTick = emotionTick;
            EvaluationTick = evaluationTick;
            CauseOrigin = causeOrigin;
            Context = context;
            this.moments = new ReadOnlyCollection<KLEPMemoryMoment>(
                copiedMoments);
            ActionOutcome = actionOutcome;
        }

        public string ExperienceId { get; }
        public long MemoryTick { get; }
        public long EmotionTick { get; }
        public string EvaluationId { get; }
        public long EvaluationTick { get; }
        public KLEPEmotionInfluenceOrigin CauseOrigin { get; }
        public KLEPEthicsContextIdentity ContextIdentity { get; }
        public TContext Context { get; }
        public IReadOnlyList<KLEPMemoryMoment> Moments => moments;
        public KLEPMemoryActionOutcome ActionOutcome { get; }
    }

    /// <summary>
    /// Immutable causal record returned after all three subsystem phases
    /// complete successfully.
    /// </summary>
    public sealed class KLEPCognitionTransition<TContext>
    {
        private readonly ReadOnlyCollection<KLEPCognitionStepTrace> steps;

        internal KLEPCognitionTransition(
            KLEPEthicsEvaluation<TContext> ethicsEvaluation,
            KLEPEmotionSnapshot emotionSnapshot,
            KLEPMemoryExperience experience,
            KLEPMemorySnapshot memorySnapshot,
            IReadOnlyList<KLEPCognitionStepTrace> steps)
        {
            EthicsEvaluation = ethicsEvaluation ??
                throw new ArgumentNullException(nameof(ethicsEvaluation));
            EmotionSnapshot = emotionSnapshot ??
                throw new ArgumentNullException(nameof(emotionSnapshot));
            Experience = experience ??
                throw new ArgumentNullException(nameof(experience));
            MemorySnapshot = memorySnapshot ??
                throw new ArgumentNullException(nameof(memorySnapshot));
            this.steps = new ReadOnlyCollection<KLEPCognitionStepTrace>(
                new List<KLEPCognitionStepTrace>(steps ??
                    throw new ArgumentNullException(nameof(steps))));
        }

        public KLEPEthicsEvaluation<TContext> EthicsEvaluation { get; }
        public KLEPEmotionSnapshot EmotionSnapshot { get; }
        public KLEPMemoryExperience Experience { get; }
        public KLEPMemorySnapshot MemorySnapshot { get; }
        public IReadOnlyList<KLEPCognitionStepTrace> Steps => steps;
    }

    /// <summary>
    /// Pure .NET composition owner for the first causal higher-cognition
    /// vertical slice. It evaluates project Ethics, advances one consecutive
    /// Emotion Tick, then records one factual Memory experience. It exposes no
    /// selection, Lock, Key-output, Executable-advance, or Neuron API.
    /// </summary>
    public sealed class KLEPCognitionCoordinator<TContext>
    {
        private readonly KLEPEthics<TContext> ethics;
        private bool isProcessing;

        public KLEPCognitionCoordinator(
            KLEPEthics<TContext> ethics,
            KLEPEmotion emotion,
            KLEPMemory memory)
        {
            this.ethics = ethics ?? throw new ArgumentNullException(nameof(ethics));
            Emotion = emotion ?? throw new ArgumentNullException(nameof(emotion));
            Memory = memory ?? throw new ArgumentNullException(nameof(memory));
            RequireAxes(
                Emotion.Configuration.AxisXName,
                Emotion.Configuration.AxisYName,
                ethics.ExpectedAxisXName,
                ethics.ExpectedAxisYName,
                "Ethics and Emotion");
            RequireAxes(
                Emotion.Configuration.AxisXName,
                Emotion.Configuration.AxisYName,
                Memory.Configuration.AxisXName,
                Memory.Configuration.AxisYName,
                "Emotion and Memory");
        }

        public KLEPEthics<TContext> Ethics => ethics;
        public KLEPEmotion Emotion { get; }
        public KLEPMemory Memory { get; }
        public KLEPCognitionTransition<TContext> LastTransition { get; private set; }

        public KLEPCognitionTransition<TContext> Process(
            KLEPCognitionExperienceRequest<TContext> request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (isProcessing)
            {
                throw new InvalidOperationException(
                    "A Cognition coordinator cannot process recursively.");
            }

            isProcessing = true;
            try
            {
                Preflight(request);
                object emotionCheckpoint =
                    Emotion.CaptureTransactionCheckpoint();
                object memoryCheckpoint =
                    Memory.CaptureTransactionCheckpoint();

                try
                {
                    var ethicsRequest = new KLEPEthicsRequest<TContext>(
                        request.EvaluationId,
                        request.EvaluationTick,
                        request.CauseOrigin,
                        Emotion.Configuration,
                        request.ContextIdentity,
                        request.Context);
                    KLEPEthicsEvaluation<TContext> evaluation =
                        ethics.Evaluate(ethicsRequest);
                    KLEPMemoryEthicsRecord ethicsRecord =
                        KLEPMemoryEthicsRecord.Capture(evaluation);

                    // Validate the evaluated provenance against the complete
                    // observed timeline before advancing either subsystem.
                    new KLEPMemoryExperience(
                        request.ExperienceId,
                        request.MemoryTick,
                        request.Moments,
                        request.ActionOutcome,
                        new[] { ethicsRecord });

                    long startingEmotionTick = Emotion.Tick;
                    KLEPEmotionVector startingEmotionState = Emotion.Position;
                    KLEPEmotionSnapshot producedEmotion = Emotion.Advance(
                        request.EmotionTick,
                        new[] { evaluation.Influence });
                    KLEPMemoryEmotionalConsequence consequence =
                        KLEPMemoryEmotionalConsequence.Capture(
                            startingEmotionTick,
                            startingEmotionState,
                            producedEmotion);
                    var experience = new KLEPMemoryExperience(
                        request.ExperienceId,
                        request.MemoryTick,
                        request.Moments,
                        request.ActionOutcome,
                        new[] { ethicsRecord },
                        consequence);
                    KLEPMemorySnapshot memorySnapshot = Memory.Tick(
                        request.MemoryTick,
                        new[] { experience });

                    var steps = new[]
                    {
                        new KLEPCognitionStepTrace(
                            KLEPCognitionPhase.EthicsEvaluated,
                            request.EvaluationTick,
                            evaluation.EvaluationId),
                        new KLEPCognitionStepTrace(
                            KLEPCognitionPhase.EmotionAdvanced,
                            producedEmotion.Tick,
                            evaluation.Influence.SourceId),
                        new KLEPCognitionStepTrace(
                            KLEPCognitionPhase.MemoryRecorded,
                            memorySnapshot.Tick,
                            experience.ExperienceId)
                    };
                    var transition = new KLEPCognitionTransition<TContext>(
                        evaluation,
                        producedEmotion,
                        experience,
                        memorySnapshot,
                        steps);
                    LastTransition = transition;
                    return transition;
                }
                catch
                {
                    // Restore both exact instances: composition evidence
                    // adapters retain these references and must not be split
                    // onto replacement subsystem objects after a fault.
                    try
                    {
                        Memory.RestoreTransactionCheckpoint(memoryCheckpoint);
                    }
                    finally
                    {
                        Emotion.RestoreTransactionCheckpoint(emotionCheckpoint);
                    }

                    throw;
                }
            }
            finally
            {
                isProcessing = false;
            }
        }

        private void Preflight(KLEPCognitionExperienceRequest<TContext> request)
        {
            if (Emotion.Tick == long.MaxValue ||
                request.EmotionTick != Emotion.Tick + 1)
            {
                throw new ArgumentException(
                    "The requested Emotion Tick must be exactly consecutive.",
                    nameof(request));
            }

            if (request.MemoryTick <= Memory.CurrentTick)
            {
                throw new ArgumentException(
                    "The requested Memory Tick must be strictly increasing.",
                    nameof(request));
            }

            // This validates moment ordering, action causality, and the Memory
            // record clock without mutating Memory.
            var structuralExperience = new KLEPMemoryExperience(
                request.ExperienceId,
                request.MemoryTick,
                request.Moments,
                request.ActionOutcome);
            KLEPMemoryMoment prior = structuralExperience.Moments[0];
            KLEPMemoryMoment consequence = structuralExperience.Moments[
                structuralExperience.Moments.Count - 1];
            if (Emotion.Tick < prior.CapturedTick ||
                request.EmotionTick > consequence.CapturedTick)
            {
                throw new ArgumentException(
                    "The exact Emotion transition must fit inside the observed experience timeline.",
                    nameof(request));
            }

            if (request.EvaluationTick < prior.CapturedTick ||
                request.EvaluationTick > request.EmotionTick)
            {
                throw new ArgumentException(
                    "Ethics evaluation must occur inside the timeline and no later than its produced Emotion Tick.",
                    nameof(request));
            }

            KLEPMemoryState state = Memory.CaptureState();
            for (int i = 0; i < state.SeenExperienceIds.Count; i++)
            {
                if (StringComparer.Ordinal.Equals(
                        state.SeenExperienceIds[i],
                        request.ExperienceId))
                {
                    throw new InvalidOperationException(
                        $"Experience '{request.ExperienceId}' was already recorded.");
                }
            }
        }

        private static void RequireAxes(
            string leftX,
            string leftY,
            string rightX,
            string rightY,
            string relationship)
        {
            if (!StringComparer.Ordinal.Equals(leftX, rightX) ||
                !StringComparer.Ordinal.Equals(leftY, rightY))
            {
                throw new ArgumentException(
                    relationship + " must use the same named axes.");
            }
        }
    }

    internal static class KLEPCognitionValidation
    {
        internal static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable ID is required.",
                    parameterName);
            }

            return value;
        }

        internal static float RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }
}
