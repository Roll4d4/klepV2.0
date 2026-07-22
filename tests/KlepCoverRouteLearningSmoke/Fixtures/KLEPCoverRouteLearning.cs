using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Imagination;

namespace Roll4d4.Klep.ZombieTest
{
    /// <summary>
    /// Engine-free X/Z point used by the demo's isolated cover-route sandbox.
    /// It is intentionally not a Unity vector and grants no live-world access.
    /// </summary>
    public readonly struct KLEPCoverRoutePoint :
        IEquatable<KLEPCoverRoutePoint>
    {
        public KLEPCoverRoutePoint(double x, double z)
        {
            RequireFinite(x, nameof(x));
            RequireFinite(z, nameof(z));
            X = x;
            Z = z;
        }

        public double X { get; }
        public double Z { get; }

        public double DistanceTo(KLEPCoverRoutePoint other)
        {
            double x = other.X - X;
            double z = other.Z - Z;
            return Math.Sqrt((x * x) + (z * z));
        }

        public bool Equals(KLEPCoverRoutePoint other)
        {
            return X.Equals(other.X) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is KLEPCoverRoutePoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Z.GetHashCode();
            }
        }

        public override string ToString()
        {
            return "(" + X.ToString("0.###", CultureInfo.InvariantCulture) +
                   ", " + Z.ToString("0.###", CultureInfo.InvariantCulture) +
                   ")";
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }

    /// <summary>
    /// One immutable axis-aligned obstacle in the sandbox's X/Z plane.
    /// </summary>
    public sealed class KLEPCoverRouteObstacle
    {
        public KLEPCoverRouteObstacle(
            string stableId,
            double minimumX,
            double maximumX,
            double minimumZ,
            double maximumZ)
        {
            StableId = RequireId(stableId, nameof(stableId));
            RequireFinite(minimumX, nameof(minimumX));
            RequireFinite(maximumX, nameof(maximumX));
            RequireFinite(minimumZ, nameof(minimumZ));
            RequireFinite(maximumZ, nameof(maximumZ));
            if (maximumX <= minimumX || maximumZ <= minimumZ)
            {
                throw new ArgumentException(
                    "A cover obstacle requires positive X and Z extent.");
            }

            MinimumX = minimumX;
            MaximumX = maximumX;
            MinimumZ = minimumZ;
            MaximumZ = maximumZ;
        }

        public string StableId { get; }
        public double MinimumX { get; }
        public double MaximumX { get; }
        public double MinimumZ { get; }
        public double MaximumZ { get; }

        private static string RequireId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A cover obstacle requires a stable identity.", name);
            }

            return value;
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }

    /// <summary>
    /// One immutable deterministic sandbox problem. The four default variants
    /// all cross the central showcase wall but use distinct start/target facts.
    /// </summary>
    public sealed class KLEPCoverRouteScenario
    {
        public KLEPCoverRouteScenario(
            string stableId,
            KLEPCoverRoutePoint start,
            KLEPCoverRoutePoint target,
            KLEPCoverRouteObstacle obstacle,
            double arenaMinimumX,
            double arenaMaximumX,
            double arenaMinimumZ,
            double arenaMaximumZ,
            double agentRadius = 0.5d,
            double movementPerTick = 0.25d)
        {
            StableId = RequireId(stableId, nameof(stableId));
            Obstacle = obstacle ?? throw new ArgumentNullException(
                nameof(obstacle));
            RequireFinite(arenaMinimumX, nameof(arenaMinimumX));
            RequireFinite(arenaMaximumX, nameof(arenaMaximumX));
            RequireFinite(arenaMinimumZ, nameof(arenaMinimumZ));
            RequireFinite(arenaMaximumZ, nameof(arenaMaximumZ));
            RequirePositive(agentRadius, nameof(agentRadius));
            RequirePositive(movementPerTick, nameof(movementPerTick));
            if (arenaMaximumX <= arenaMinimumX ||
                arenaMaximumZ <= arenaMinimumZ)
            {
                throw new ArgumentException(
                    "A route scenario requires positive arena extents.");
            }

            Start = start;
            Target = target;
            ArenaMinimumX = arenaMinimumX;
            ArenaMaximumX = arenaMaximumX;
            ArenaMinimumZ = arenaMinimumZ;
            ArenaMaximumZ = arenaMaximumZ;
            AgentRadius = agentRadius;
            MovementPerTick = movementPerTick;
            if (!KLEPCoverRouteGeometry.IsInsideArena(this, start) ||
                !KLEPCoverRouteGeometry.IsInsideArena(this, target))
            {
                throw new ArgumentException(
                    "Scenario start and target must fit inside the arena.");
            }

            if (!KLEPCoverRouteGeometry.SegmentIntersectsObstacle(
                    start,
                    target,
                    obstacle,
                    agentRadius))
            {
                throw new ArgumentException(
                    "A cover-route scenario must begin with cover blocking " +
                    "the direct route.");
            }
        }

        public string StableId { get; }
        public KLEPCoverRoutePoint Start { get; }
        public KLEPCoverRoutePoint Target { get; }
        public KLEPCoverRouteObstacle Obstacle { get; }
        public double ArenaMinimumX { get; }
        public double ArenaMaximumX { get; }
        public double ArenaMinimumZ { get; }
        public double ArenaMaximumZ { get; }
        public double AgentRadius { get; }
        public double MovementPerTick { get; }

        private static string RequireId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A route scenario requires a stable identity.", name);
            }

            return value;
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static void RequirePositive(double value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0d)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }

    public enum KLEPCoverRouteTrialOutcome
    {
        Succeeded,
        Failed,
        Faulted
    }

    /// <summary>
    /// Immutable, parsed movement intent emitted by the trusted route runtime.
    /// The Unity host may consume this value only after separately validating
    /// the enclosing Executable cycle and run identities.
    /// </summary>
    public sealed class KLEPCoverRouteIntent
    {
        internal KLEPCoverRouteIntent(
            string routeId,
            string problemId,
            string targetId,
            long runtimeInstanceSequence,
            long phase,
            KLEPCoverRoutePoint destination)
        {
            RouteId = RequireId(routeId, nameof(routeId));
            ProblemId = RequireId(problemId, nameof(problemId));
            TargetId = RequireId(targetId, nameof(targetId));
            if (runtimeInstanceSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runtimeInstanceSequence));
            }

            if (phase < 0 || phase > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(phase));
            }

            RuntimeInstanceSequence = runtimeInstanceSequence;
            Phase = phase;
            Destination = destination;
        }

        public string RouteId { get; }
        public string ProblemId { get; }
        public string TargetId { get; }
        public long RuntimeInstanceSequence { get; }
        public long Phase { get; }
        public KLEPCoverRoutePoint Destination { get; }

        private static string RequireId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A route intent requires exact identities.", name);
            }

            return value;
        }
    }

    /// <summary>
    /// One exact cycle/run-bound sandbox intent retained as proposal evidence.
    /// </summary>
    public sealed class KLEPCoverRouteIntentTrace
    {
        internal KLEPCoverRouteIntentTrace(
            long sequence,
            long cycleIndex,
            long runIndex,
            KLEPCoverRouteIntent intent)
        {
            if (sequence <= 0 || cycleIndex <= 0 || runIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Sequence = sequence;
            CycleIndex = cycleIndex;
            RunIndex = runIndex;
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
        }

        public long Sequence { get; }
        public long CycleIndex { get; }
        public long RunIndex { get; }
        public KLEPCoverRouteIntent Intent { get; }
    }

    /// <summary>
    /// Shared project fitness result for both isolated and separately observed
    /// live trials. It owns no selection, expectation, or world authority.
    /// </summary>
    public readonly struct KLEPCoverRouteFitness
    {
        internal KLEPCoverRouteFitness(
            double efficiency,
            double stallScore,
            double value)
        {
            Efficiency = efficiency;
            StallScore = stallScore;
            Value = value;
        }

        public double Efficiency { get; }
        public double StallScore { get; }
        public double Value { get; }
    }

    /// <summary>
    /// Immutable evidence from one fresh scratch Neuron/Agent/materialization.
    /// It is proposal evidence, not a Key, Desire effect, Memory, or live fact.
    /// </summary>
    public sealed class KLEPCoverRouteTrial
    {
        private readonly ReadOnlyCollection<KLEPCoverRoutePoint> proposedRoute;
        private readonly ReadOnlyCollection<KLEPCoverRoutePoint> actualRoute;
        private readonly ReadOnlyCollection<KLEPCoverRouteIntentTrace> intents;

        internal KLEPCoverRouteTrial(
            long sequence,
            string trialId,
            string sandboxVersion,
            string proposalFingerprint,
            string capabilityCatalogFingerprint,
            KLEPCoverRouteScenario scenario,
            long runtimeInstanceSequence,
            string executableStableId,
            long runIndex,
            long terminalCycle,
            KLEPExecutableState terminalState,
            KLEPExecutableExitReason terminalReason,
            KLEPCoverRouteTrialOutcome outcome,
            IEnumerable<KLEPCoverRoutePoint> proposed,
            IEnumerable<KLEPCoverRoutePoint> actual,
            IEnumerable<KLEPCoverRouteIntentTrace> retainedIntents,
            int movementTicks,
            int collisionCount,
            int stallCount,
            bool exactDeclaredOutputObserved,
            string observedOutputKeyId,
            double routeLength,
            double projectedTotalLength,
            double directDistance,
            double minimumSurfaceClearance,
            double efficiency,
            double clearanceScore,
            double fitness,
            string explanation)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Sequence = sequence;
            TrialId = RequireId(trialId, nameof(trialId));
            SandboxVersion = RequireId(
                sandboxVersion, nameof(sandboxVersion));
            ProposalFingerprint = RequireId(
                proposalFingerprint, nameof(proposalFingerprint));
            CapabilityCatalogFingerprint = RequireId(
                capabilityCatalogFingerprint,
                nameof(capabilityCatalogFingerprint));
            Scenario = scenario ?? throw new ArgumentNullException(
                nameof(scenario));
            if (runtimeInstanceSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runtimeInstanceSequence));
            }

            RuntimeInstanceSequence = runtimeInstanceSequence;
            ExecutableStableId = RequireId(
                executableStableId, nameof(executableStableId));
            if (runIndex <= 0 || terminalCycle <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runIndex),
                    "A sandbox trial requires exact positive run/cycle identity.");
            }

            if (!Enum.IsDefined(typeof(KLEPExecutableState), terminalState) ||
                !Enum.IsDefined(
                    typeof(KLEPExecutableExitReason), terminalReason) ||
                !Enum.IsDefined(
                    typeof(KLEPCoverRouteTrialOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            RunIndex = runIndex;
            TerminalCycle = terminalCycle;
            TerminalState = terminalState;
            TerminalReason = terminalReason;
            Outcome = outcome;
            proposedRoute = CopyPoints(proposed, nameof(proposed));
            actualRoute = CopyPoints(actual, nameof(actual));
            intents = CopyIntents(retainedIntents, nameof(retainedIntents));
            if (proposedRoute.Count < 2 || actualRoute.Count < 1)
            {
                throw new ArgumentException(
                    "A route trial requires proposed and actual geometry.");
            }

            if (movementTicks < 0 || collisionCount < 0 || stallCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(movementTicks));
            }

            MovementTicks = movementTicks;
            CollisionCount = collisionCount;
            StallCount = stallCount;
            ExactDeclaredOutputObserved = exactDeclaredOutputObserved;
            ObservedOutputKeyId = observedOutputKeyId ?? string.Empty;
            RouteLength = RequireNonnegativeFinite(
                routeLength, nameof(routeLength));
            ProjectedTotalLength = RequireNonnegativeFinite(
                projectedTotalLength, nameof(projectedTotalLength));
            DirectDistance = RequireNonnegativeFinite(
                directDistance, nameof(directDistance));
            MinimumSurfaceClearance = RequireNonnegativeFinite(
                minimumSurfaceClearance,
                nameof(minimumSurfaceClearance));
            Efficiency = RequireUnitInterval(
                efficiency, nameof(efficiency));
            ClearanceScore = RequireUnitInterval(
                clearanceScore, nameof(clearanceScore));
            Fitness = RequireUnitInterval(fitness, nameof(fitness));
            Explanation = explanation ?? string.Empty;
        }

        public long Sequence { get; }
        public string TrialId { get; }
        public string SandboxVersion { get; }
        public string ProposalFingerprint { get; }
        public string CapabilityCatalogFingerprint { get; }
        public KLEPCoverRouteScenario Scenario { get; }
        public long RuntimeInstanceSequence { get; }
        public string ExecutableStableId { get; }
        public long RunIndex { get; }
        public long TerminalCycle { get; }
        public KLEPExecutableState TerminalState { get; }
        public KLEPExecutableExitReason TerminalReason { get; }
        public KLEPCoverRouteTrialOutcome Outcome { get; }
        public IReadOnlyList<KLEPCoverRoutePoint> ProposedRoute =>
            proposedRoute;
        public IReadOnlyList<KLEPCoverRoutePoint> ActualRoute => actualRoute;
        public IReadOnlyList<KLEPCoverRouteIntentTrace> Intents => intents;
        public int MovementTicks { get; }
        public int CollisionCount { get; }
        public int StallCount { get; }
        public bool CollisionFree => CollisionCount == 0;
        public bool ExactDeclaredOutputObserved { get; }
        public string ObservedOutputKeyId { get; }
        public double RouteLength { get; }
        public double ProjectedTotalLength { get; }
        public double DirectDistance { get; }
        public double MinimumSurfaceClearance { get; }
        public double Efficiency { get; }
        public double ClearanceScore { get; }
        public double Fitness { get; }
        public string Explanation { get; }
        public bool Succeeded =>
            Outcome == KLEPCoverRouteTrialOutcome.Succeeded;

        private static ReadOnlyCollection<KLEPCoverRoutePoint> CopyPoints(
            IEnumerable<KLEPCoverRoutePoint> source,
            string name)
        {
            if (source == null)
            {
                throw new ArgumentNullException(name);
            }

            return new ReadOnlyCollection<KLEPCoverRoutePoint>(
                new List<KLEPCoverRoutePoint>(source));
        }

        private static ReadOnlyCollection<KLEPCoverRouteIntentTrace>
            CopyIntents(
                IEnumerable<KLEPCoverRouteIntentTrace> source,
                string name)
        {
            if (source == null)
            {
                throw new ArgumentNullException(name);
            }

            var copy = new List<KLEPCoverRouteIntentTrace>();
            foreach (KLEPCoverRouteIntentTrace trace in source)
            {
                copy.Add(trace ?? throw new ArgumentException(
                    "A route trial cannot retain a null intent trace.",
                    name));
            }

            return new ReadOnlyCollection<KLEPCoverRouteIntentTrace>(copy);
        }

        private static string RequireId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Route trial evidence requires stable identities.", name);
            }

            return value;
        }

        private static double RequireNonnegativeFinite(
            double value,
            string name)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value < 0d)
            {
                throw new ArgumentOutOfRangeException(name);
            }

            return value;
        }

        private static double RequireUnitInterval(double value, string name)
        {
            RequireNonnegativeFinite(value, name);
            if (value > 1d)
            {
                throw new ArgumentOutOfRangeException(name);
            }

            return value;
        }
    }

    public sealed class KLEPCoverRouteLedgerSnapshot
    {
        private readonly ReadOnlyCollection<KLEPCoverRouteTrial> trials;

        internal KLEPCoverRouteLedgerSnapshot(
            string proposalFingerprint,
            string sandboxVersion,
            string capabilityCatalogFingerprint,
            long revision,
            long support,
            long successes,
            double meanFitness,
            double sampleVariance,
            double confidence,
            double minimumClearance,
            IEnumerable<KLEPCoverRouteTrial> retainedTrials)
        {
            ProposalFingerprint = proposalFingerprint;
            SandboxVersion = sandboxVersion;
            CapabilityCatalogFingerprint = capabilityCatalogFingerprint;
            Revision = revision;
            Support = support;
            Successes = successes;
            MeanFitness = meanFitness;
            SampleVariance = sampleVariance;
            Confidence = confidence;
            MinimumClearance = minimumClearance;
            trials = new ReadOnlyCollection<KLEPCoverRouteTrial>(
                new List<KLEPCoverRouteTrial>(retainedTrials));
        }

        public string ProposalFingerprint { get; }
        public string SandboxVersion { get; }
        public string CapabilityCatalogFingerprint { get; }
        public long Revision { get; }
        public long Support { get; }
        public long Successes { get; }
        public long Failures => Support - Successes;
        public double SuccessRate => Support == 0
            ? 0d
            : (double)Successes / Support;
        public double MeanFitness { get; }
        public double SampleVariance { get; }
        public double Confidence { get; }
        public double MinimumClearance { get; }
        public IReadOnlyList<KLEPCoverRouteTrial> Trials => trials;
    }

    /// <summary>
    /// Project-owned bounded aggregate for sandbox evidence. It deliberately
    /// does not implement a learned-expectation or Agent selection interface.
    /// </summary>
    public sealed class KLEPCoverRouteTrialLedger
    {
        public const int MaximumSupport = 4;

        private readonly string proposalFingerprint;
        private readonly string sandboxVersion;
        private readonly string capabilityCatalogFingerprint;
        private readonly double confidenceScale;
        private readonly List<KLEPCoverRouteTrial> trials =
            new List<KLEPCoverRouteTrial>();
        private readonly HashSet<string> trialIds =
            new HashSet<string>(StringComparer.Ordinal);
        private long revision;
        private long successes;
        private double mean;
        private double m2;
        private double minimumClearance = double.PositiveInfinity;

        public KLEPCoverRouteTrialLedger(
            string exactProposalFingerprint,
            string exactSandboxVersion,
            string exactCapabilityCatalogFingerprint,
            double sandboxConfidenceScale = 4d)
        {
            proposalFingerprint = RequireId(
                exactProposalFingerprint,
                nameof(exactProposalFingerprint));
            sandboxVersion = RequireId(
                exactSandboxVersion,
                nameof(exactSandboxVersion));
            capabilityCatalogFingerprint = RequireId(
                exactCapabilityCatalogFingerprint,
                nameof(exactCapabilityCatalogFingerprint));
            if (double.IsNaN(sandboxConfidenceScale) ||
                double.IsInfinity(sandboxConfidenceScale) ||
                !sandboxConfidenceScale.Equals(
                    KLEPCoverRouteLearningSession.DefaultConfidenceScale))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sandboxConfidenceScale),
                    "Cover/Route Learning V1 fixes sandbox confidence to " +
                    "support / (support + 4).");
            }

            confidenceScale = sandboxConfidenceScale;
        }

        public long Revision => revision;
        public long Support => trials.Count;
        public double ConfidenceScale => confidenceScale;

        public KLEPCoverRouteLedgerSnapshot Record(
            KLEPCoverRouteTrial trial)
        {
            if (trial == null)
            {
                throw new ArgumentNullException(nameof(trial));
            }

            long expectedSequence = trials.Count + 1L;
            if (expectedSequence > MaximumSupport)
            {
                throw new InvalidOperationException(
                    "Cover/Route Learning V1 retains at most four trials.");
            }

            if (trial.Sequence != expectedSequence)
            {
                throw new InvalidOperationException(
                    $"Route trial sequence {trial.Sequence} cannot follow " +
                    $"support {trials.Count}; expected {expectedSequence}.");
            }

            if (!StringComparer.Ordinal.Equals(
                    proposalFingerprint,
                    trial.ProposalFingerprint) ||
                !StringComparer.Ordinal.Equals(
                    sandboxVersion,
                    trial.SandboxVersion) ||
                !StringComparer.Ordinal.Equals(
                    capabilityCatalogFingerprint,
                    trial.CapabilityCatalogFingerprint))
            {
                throw new InvalidOperationException(
                    "Route trial evidence belongs to another proposal, " +
                    "sandbox, or capability catalog.");
            }

            if (trialIds.Contains(trial.TrialId))
            {
                throw new InvalidOperationException(
                    $"Route trial '{trial.TrialId}' cannot be replayed.");
            }

            // Validate all evidence before mutating the aggregate.
            double nextSupport = expectedSequence;
            double delta = trial.Fitness - mean;
            double nextMean = mean + (delta / nextSupport);
            double nextM2 = m2 + delta * (trial.Fitness - nextMean);
            if (double.IsNaN(nextMean) ||
                double.IsInfinity(nextMean) ||
                double.IsNaN(nextM2) ||
                double.IsInfinity(nextM2))
            {
                throw new InvalidOperationException(
                    "Route fitness exceeded the finite numeric domain.");
            }

            trialIds.Add(trial.TrialId);
            trials.Add(trial);
            mean = nextMean;
            m2 = nextM2;
            if (trial.Succeeded)
            {
                successes++;
            }

            minimumClearance = Math.Min(
                minimumClearance,
                trial.MinimumSurfaceClearance);
            revision++;
            return CaptureSnapshot();
        }

        public KLEPCoverRouteLedgerSnapshot CaptureSnapshot()
        {
            long support = trials.Count;
            double variance = support < 2 ? 0d : m2 / (support - 1d);
            double confidence = support == 0
                ? 0d
                : support / (support + confidenceScale);
            return new KLEPCoverRouteLedgerSnapshot(
                proposalFingerprint,
                sandboxVersion,
                capabilityCatalogFingerprint,
                revision,
                support,
                successes,
                support == 0 ? 0d : mean,
                variance,
                confidence,
                support == 0 ? 0d : minimumClearance,
                trials);
        }

        private static string RequireId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A route ledger requires stable evidence identities.",
                    name);
            }

            return value;
        }
    }

    public enum KLEPCoverRouteAdmissionDisposition
    {
        Accepted,
        RejectedInsufficientEvidence,
        RejectedTrialFailure,
        RejectedClearance,
        RejectedFitness
    }

    public sealed class KLEPCoverRouteAdmissionDecision
    {
        internal KLEPCoverRouteAdmissionDecision(
            KLEPCoverRouteAdmissionDisposition disposition,
            string proposalFingerprint,
            long support,
            double meanFitness,
            double confidence,
            string explanation)
        {
            Disposition = disposition;
            ProposalFingerprint = proposalFingerprint;
            Support = support;
            MeanFitness = meanFitness;
            Confidence = confidence;
            Explanation = explanation ?? string.Empty;
        }

        public KLEPCoverRouteAdmissionDisposition Disposition { get; }
        public string ProposalFingerprint { get; }
        public long Support { get; }
        public double MeanFitness { get; }
        public double Confidence { get; }
        public string Explanation { get; }
        public bool IsAccepted =>
            Disposition == KLEPCoverRouteAdmissionDisposition.Accepted;
    }

    public sealed class KLEPCoverRouteLearningResult
    {
        internal KLEPCoverRouteLearningResult(
            KLEPImaginationManifest manifest,
            KLEPCoverRouteLedgerSnapshot ledger,
            KLEPCoverRouteAdmissionDecision decision,
            int sandboxMaterializationCount)
        {
            Manifest = manifest ?? throw new ArgumentNullException(
                nameof(manifest));
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            Decision = decision ?? throw new ArgumentNullException(
                nameof(decision));
            SandboxMaterializationCount = sandboxMaterializationCount;
        }

        public KLEPImaginationManifest Manifest { get; }
        public KLEPCoverRouteLedgerSnapshot Ledger { get; }
        public KLEPCoverRouteAdmissionDecision Decision { get; }
        public int SandboxMaterializationCount { get; }
        public IReadOnlyList<KLEPCoverRouteTrial> Trials => Ledger.Trials;
        public long TrialCount => Ledger.Support;
        public long Successes => Ledger.Successes;
        public double MeanFitness => Ledger.MeanFitness;
        public double SampleVariance => Ledger.SampleVariance;
        public long SandboxSupport => Ledger.Support;
        public double SandboxConfidence => Ledger.Confidence;
        public double MinimumClearance => Ledger.MinimumClearance;
        public string VerdictReason => Decision.Explanation;
        public bool IsAccepted => Decision.IsAccepted;
    }

    /// <summary>
    /// Complete model-free V1: strict Strong Manifest compilation, one fresh
    /// scratch materialization/Neuron/Agent per deterministic scenario,
    /// separate sandbox aggregation, and an explicit admission result.
    /// </summary>
    public sealed class KLEPCoverRouteLearningSession
    {
        public const string SandboxVersion = "cover-route-sandbox-v1";
        public const string CapabilityId = "demo.navigate-cover-route";
        public const string CapabilityVersion = "1";
        public const string GroundKeyId =
            "b044f26ef0b44fcca6fa6d93a786b67b";
        public const string RouteProblemKeyId =
            "d98813d61fdf4210a023ed6a4b336eff";
        public const string EdgeDangerKeyId =
            "61f236db1ee54d8b92f75c89f2f120a1";
        public const string RouteCompletedKeyId =
            "a41b6299dc524774be0616710f9387af";

        public const double DefaultConfidenceScale = 4d;
        public const double DefaultMinimumMeanFitness = 0.65d;
        public const double DefaultMinimumClearanceFraction = 0.90d;
        public const double AuthoredClearance = 0.40d;
        public const double AuthoredCollisionRadius = 0.50d;
        public const double AuthoredMovementPerTick = 0.25d;
        public const double ArenaMinimumX = -11.5d;
        public const double ArenaMaximumX = 16.5d;
        public const double ArenaMinimumZ = -9d;
        public const double ArenaMaximumZ = 9d;

        private readonly ReadOnlyCollection<KLEPCoverRouteScenario> scenarios;
        private readonly KLEPCoverRouteRuntimeFactory runtimeFactory;
        private readonly KLEPImaginationManifestCompiler compiler =
            new KLEPImaginationManifestCompiler();
        private readonly KLEPImaginationMaterializer materializer =
            new KLEPImaginationMaterializer();
        private readonly double confidenceScale;
        private readonly double minimumMeanFitness;
        private readonly double minimumClearanceFraction;

        public KLEPCoverRouteLearningSession(
            KLEPKeyDefinition groundKey,
            KLEPKeyDefinition routeProblemKey,
            KLEPKeyDefinition edgeDangerKey,
            KLEPKeyDefinition routeCompletedKey,
            IEnumerable<KLEPCoverRouteScenario> sandboxScenarios = null)
        {
            GroundKey = RequireProjectKey(
                groundKey, GroundKeyId, nameof(groundKey));
            RouteProblemKey = RequireProjectKey(
                routeProblemKey, RouteProblemKeyId, nameof(routeProblemKey));
            EdgeDangerKey = RequireProjectKey(
                edgeDangerKey, EdgeDangerKeyId, nameof(edgeDangerKey));
            RouteCompletedKey = RequireProjectKey(
                routeCompletedKey,
                RouteCompletedKeyId,
                nameof(routeCompletedKey));
            if (GroundKey.DefaultLifetime != KLEPKeyLifetime.OneCycle ||
                RouteProblemKey.DefaultLifetime != KLEPKeyLifetime.OneCycle ||
                EdgeDangerKey.DefaultLifetime != KLEPKeyLifetime.OneCycle ||
                RouteCompletedKey.DefaultLifetime != KLEPKeyLifetime.OneCycle)
            {
                throw new ArgumentException(
                    "The four project route Keys must all be OneCycle facts.");
            }

            confidenceScale = DefaultConfidenceScale;
            minimumMeanFitness = DefaultMinimumMeanFitness;
            minimumClearanceFraction = DefaultMinimumClearanceFraction;
            var copiedScenarios = new List<KLEPCoverRouteScenario>();
            foreach (KLEPCoverRouteScenario scenario in
                     sandboxScenarios ?? CreateCentralWallScenarios())
            {
                copiedScenarios.Add(scenario ?? throw new ArgumentException(
                    "Sandbox scenarios cannot contain null.",
                    nameof(sandboxScenarios)));
            }

            if (copiedScenarios.Count !=
                KLEPCoverRouteTrialLedger.MaximumSupport)
            {
                throw new ArgumentException(
                    "Cover/Route Learning V1 requires exactly four " +
                    "deterministic scenarios.",
                    nameof(sandboxScenarios));
            }

            var scenarioIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < copiedScenarios.Count; index++)
            {
                KLEPCoverRouteScenario scenario = copiedScenarios[index];
                ValidateScenarioContract(scenario, index);
                if (!scenarioIds.Add(scenario.StableId))
                {
                    throw new ArgumentException(
                        $"Scenario '{scenario.StableId}' is duplicated.",
                        nameof(sandboxScenarios));
                }
            }

            scenarios = new ReadOnlyCollection<KLEPCoverRouteScenario>(
                copiedScenarios);
            runtimeFactory = new KLEPCoverRouteRuntimeFactory(
                RouteProblemKey,
                RouteCompletedKey);
            CapabilityDescriptor = CreateCapabilityDescriptor(
                GroundKey,
                RouteProblemKey,
                EdgeDangerKey,
                RouteCompletedKey);
            CapabilityCatalog = CreateCapabilityCatalog(
                CapabilityDescriptor,
                runtimeFactory);
        }

        public KLEPKeyDefinition GroundKey { get; }
        public KLEPKeyDefinition RouteProblemKey { get; }
        public KLEPKeyDefinition EdgeDangerKey { get; }
        public KLEPKeyDefinition RouteCompletedKey { get; }
        public KLEPImaginationCapabilityDescriptor CapabilityDescriptor
        {
            get;
        }
        public KLEPImaginationCapabilityCatalog CapabilityCatalog { get; }
        public IReadOnlyList<KLEPCoverRouteScenario> Scenarios => scenarios;
        public long CreatedRuntimeCount => runtimeFactory.CreatedCount;

        /// <summary>
        /// Performs only strict deterministic compilation. A project host must
        /// compare RequestFingerprint and TargetKeyId with its active request
        /// before passing the result to the sandbox.
        /// </summary>
        public KLEPImaginationManifest CompileStrong(string strongManifestJson)
        {
            return compiler.CompileStrong(
                strongManifestJson,
                CapabilityCatalog);
        }

        public KLEPCoverRouteLearningResult EvaluateStrongManifest(
            string strongManifestJson)
        {
            return EvaluateCompiledManifest(CompileStrong(strongManifestJson));
        }

        public KLEPCoverRouteLearningResult Run(
            KLEPImaginationManifest manifest)
        {
            return EvaluateCompiledManifest(manifest);
        }

        public KLEPCoverRouteLearningResult EvaluateCompiledManifest(
            KLEPImaginationManifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (!StringComparer.Ordinal.Equals(
                    manifest.TargetKeyId,
                    RouteCompletedKey.Id.Value))
            {
                throw new KLEPImaginationRejectedException(
                    KLEPImaginationRejectionCode.InvalidValue,
                    $"Cover-route manifests must hypothesize exact target " +
                    $"Key '{RouteCompletedKey.Id.Value}'.");
            }

            if (!StringComparer.Ordinal.Equals(
                    manifest.CapabilityCatalogFingerprint,
                    CapabilityCatalog.Fingerprint) ||
                !StringComparer.Ordinal.Equals(
                    manifest.CapabilityId,
                    CapabilityId) ||
                !StringComparer.Ordinal.Equals(
                    manifest.CapabilityVersion,
                    CapabilityVersion) ||
                !StringComparer.Ordinal.Equals(
                    manifest.CapabilityDescriptorFingerprint,
                    CapabilityDescriptor.DescriptorFingerprint))
            {
                throw new KLEPImaginationRejectedException(
                    KLEPImaginationRejectionCode.StaleCapabilityBinding,
                    "The compiled route Manifest is stale or belongs to " +
                    "another capability catalog/descriptor.");
            }

            long materializationsBefore = runtimeFactory.CreatedCount;
            var ledger = new KLEPCoverRouteTrialLedger(
                manifest.ProposalFingerprint,
                SandboxVersion,
                CapabilityCatalog.Fingerprint,
                confidenceScale);
            for (int index = 0; index < scenarios.Count; index++)
            {
                KLEPImaginedExecutable executable =
                    materializer.Materialize(manifest, CapabilityCatalog);
                KLEPCoverRouteTrial trial = RunScenario(
                    index + 1L,
                    manifest,
                    executable,
                    scenarios[index]);
                ledger.Record(trial);
            }

            KLEPCoverRouteLedgerSnapshot snapshot = ledger.CaptureSnapshot();
            KLEPCoverRouteAdmissionDecision decision = DecideAdmission(
                manifest,
                snapshot);
            return new KLEPCoverRouteLearningResult(
                manifest,
                snapshot,
                decision,
                checked((int)(runtimeFactory.CreatedCount -
                              materializationsBefore)));
        }

        /// <summary>
        /// Creates a fresh, never-sandboxed runtime only after this session's
        /// explicit admission policy accepted the exact manifest evidence.
        /// It does not register the result in any live Neuron.
        /// </summary>
        public KLEPImaginedExecutable MaterializeAccepted(
            KLEPCoverRouteLearningResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!result.Decision.IsAccepted)
            {
                throw new InvalidOperationException(
                    "A rejected cover-route proposal cannot be materialized " +
                    "for admission.");
            }

            if (!StringComparer.Ordinal.Equals(
                    result.Manifest.CapabilityCatalogFingerprint,
                    CapabilityCatalog.Fingerprint) ||
                !StringComparer.Ordinal.Equals(
                    result.Ledger.CapabilityCatalogFingerprint,
                    CapabilityCatalog.Fingerprint) ||
                !StringComparer.Ordinal.Equals(
                    result.Manifest.ProposalFingerprint,
                    result.Decision.ProposalFingerprint))
            {
                throw new InvalidOperationException(
                    "Accepted route evidence is stale or belongs to another " +
                    "capability catalog.");
            }

            return materializer.Materialize(
                result.Manifest,
                CapabilityCatalog);
        }

        public static string CreateDefaultStrongManifestJson(
            string routeId = "south-gap-v1",
            double waypoint1X = 0d,
            double waypoint1Z = -3.2d,
            double waypoint2X = 4.8d,
            double waypoint2Z = -3d,
            double waypoint3X = 6.2d,
            double waypoint3Z = -1.5d,
            double arrivalRadius = 0.8d,
            long maximumTicks = 240L,
            string requestFingerprint = "request.cover-route.central-wall.v1")
        {
            if (routeId == null)
            {
                throw new ArgumentNullException(nameof(routeId));
            }

            return "{" +
                "\"schema\":\"klep.imagination.strong.v1\"," +
                "\"requestFingerprint\":\"" + requestFingerprint + "\"," +
                "\"displayName\":\"Route around central cover\"," +
                "\"capability\":{" +
                    "\"id\":\"" + CapabilityId + "\"," +
                     "\"version\":\"" + CapabilityVersion + "\"," +
                     "\"arguments\":{" +
                        "\"arrivalRadius\":" + arrivalRadius.ToString(
                            "R", CultureInfo.InvariantCulture) + "," +
                        "\"maximumTicks\":" + maximumTicks.ToString(
                            CultureInfo.InvariantCulture) + "," +
                        "\"routeId\":\"" + routeId + "\"," +
                        "\"waypoint1X\":" + waypoint1X.ToString(
                            "R", CultureInfo.InvariantCulture) + "," +
                        "\"waypoint1Z\":" + waypoint1Z.ToString(
                            "R", CultureInfo.InvariantCulture) + "," +
                        "\"waypoint2X\":" + waypoint2X.ToString(
                            "R", CultureInfo.InvariantCulture) + "," +
                        "\"waypoint2Z\":" + waypoint2Z.ToString(
                            "R", CultureInfo.InvariantCulture) + "," +
                        "\"waypoint3X\":" + waypoint3X.ToString(
                            "R", CultureInfo.InvariantCulture) + "," +
                        "\"waypoint3Z\":" + waypoint3Z.ToString(
                            "R", CultureInfo.InvariantCulture) + "}}," +
                "\"hypothesis\":{" +
                    "\"targetKey\":\"" + RouteCompletedKeyId + "\"," +
                    "\"explanation\":\"Three bounded waypoints may route " +
                    "the learner around the authored cover walls to the " +
                    "fixed trial target.\"}}";
        }

        public static IReadOnlyList<KLEPCoverRouteScenario>
            CreateCentralWallScenarios()
        {
            var obstacle = new KLEPCoverRouteObstacle(
                "cover.central",
                2.2d,
                2.8d,
                -2d,
                2d);
            const double arenaMinimumX = -11.5d;
            const double arenaMaximumX = 16.5d;
            const double arenaMinimumZ = -9d;
            const double arenaMaximumZ = 9d;
            return new ReadOnlyCollection<KLEPCoverRouteScenario>(
                new List<KLEPCoverRouteScenario>
                {
                    new KLEPCoverRouteScenario(
                        "central.001",
                        new KLEPCoverRoutePoint(-3.5d, -0.75d),
                        new KLEPCoverRoutePoint(8d, -0.5d),
                        obstacle,
                        arenaMinimumX,
                        arenaMaximumX,
                        arenaMinimumZ,
                        arenaMaximumZ),
                    new KLEPCoverRouteScenario(
                        "central.002",
                        new KLEPCoverRoutePoint(-4d, -0.25d),
                        new KLEPCoverRoutePoint(8.5d, 0.25d),
                        obstacle,
                        arenaMinimumX,
                        arenaMaximumX,
                        arenaMinimumZ,
                        arenaMaximumZ),
                    new KLEPCoverRouteScenario(
                        "central.003",
                        new KLEPCoverRoutePoint(-3.75d, 0.25d),
                        new KLEPCoverRoutePoint(7.75d, 0.5d),
                        obstacle,
                        arenaMinimumX,
                        arenaMaximumX,
                        arenaMinimumZ,
                        arenaMaximumZ),
                    new KLEPCoverRouteScenario(
                        "central.004",
                        new KLEPCoverRoutePoint(-4.25d, 0.75d),
                        new KLEPCoverRoutePoint(8.25d, 0.75d),
                        obstacle,
                        arenaMinimumX,
                        arenaMaximumX,
                        arenaMinimumZ,
                        arenaMaximumZ)
                });
        }

        private KLEPCoverRouteTrial RunScenario(
            long sequence,
            KLEPImaginationManifest manifest,
            KLEPImaginedExecutable executable,
            KLEPCoverRouteScenario scenario)
        {
            string trialId = manifest.ProposalFingerprint + "." +
                             scenario.StableId;
            IReadOnlyList<KLEPCoverRoutePoint> proposed = GetProposalPoints(
                manifest,
                scenario.Start,
                scenario.Target);
            var actual = new List<KLEPCoverRoutePoint> { scenario.Start };
            var intents = new List<KLEPCoverRouteIntentTrace>();
            KLEPCoverRoutePoint position = scenario.Start;
            double routeLength = 0d;
            double minimumClearance =
                KLEPCoverRouteGeometry.SurfaceClearance(
                    position,
                    scenario.Obstacle,
                    scenario.AgentRadius);
            int movementTicks = 0;
            int collisionCount = 0;
            int stallCount = 0;
            bool exactOutputObserved = false;
            string observedOutputKeyId = string.Empty;
            long runtimeInstanceSequence = runtimeFactory.CreatedCount;
            long runIndex = 0;
            long terminalCycle = 0;
            KLEPExecutableState terminalState = KLEPExecutableState.Faulted;
            KLEPExecutableExitReason terminalReason =
                KLEPExecutableExitReason.Faulted;
            KLEPCoverRouteTrialOutcome outcome =
                KLEPCoverRouteTrialOutcome.Faulted;
            string explanation = "The sandbox did not reach a terminal result.";

            var neuron = new KLEPNeuron(
                "neuron.sandbox.cover-route." + scenario.StableId);
            KLEPCoverRouteProblemSensorExecutable problemSensor =
                CreateProblemSensor(
                    "sensor.sandbox.cover-route." + scenario.StableId);
            neuron.RegisterExecutable(problemSensor);
            neuron.RegisterExecutable(executable);
            var agent = new KLEPAgent(neuron);
            long authoredMaximumTicks =
                manifest.Arguments["maximumTicks"].AsInteger();
            long safetyLimit = checked(authoredMaximumTicks + 2L);
            for (long iteration = 0; iteration < safetyLimit; iteration++)
            {
                neuron.AddKey(
                    GroundKey,
                    null,
                    KLEPKeyLifetime.OneCycle,
                    "sandbox." + scenario.StableId);
                problemSensor.SetCurrentProblem(
                    new KLEPCoverRouteProblem(
                        scenario.StableId,
                        "target." + scenario.StableId,
                        iteration + 1L,
                        position,
                        scenario.Target));
                KLEPAgentTickTrace trace;
                try
                {
                    trace = agent.Tick();
                }
                catch (Exception exception)
                {
                    terminalCycle = Math.Max(1L, neuron.CycleIndex);
                    explanation = "Sandbox Agent fault: " +
                                  exception.GetType().Name + ": " +
                                  exception.Message;
                    break;
                }

                KLEPExecutableStepTrace step = FindSoloStep(
                    trace.Decision,
                    executable.StableId);
                if (step == null || step.Result == null)
                {
                    terminalCycle = trace.Decision.CycleIndex;
                    explanation = "The scratch Agent did not advance the " +
                                  "Lock-eligible imagined route.";
                    break;
                }

                runIndex = step.Result.RunIndex;
                terminalCycle = step.Result.CycleIndex;
                if (step.Result.IsTerminal)
                {
                    terminalState = step.Result.State;
                    terminalReason = step.Result.ExitReason ??
                        KLEPExecutableExitReason.Faulted;
                    exactOutputObserved = HasExactSuccessfulOutput(
                        step.Result);
                    if (step.Result.Outputs.Count == 1)
                    {
                        observedOutputKeyId =
                            step.Result.Outputs[0].KeyId.Value;
                    }

                    if (step.Result.State == KLEPExecutableState.Succeeded &&
                        exactOutputObserved &&
                        position.DistanceTo(scenario.Target) <=
                        manifest.Arguments["arrivalRadius"].AsNumber() &&
                        collisionCount == 0 &&
                        stallCount == 0)
                    {
                        outcome = KLEPCoverRouteTrialOutcome.Succeeded;
                        explanation = "The trusted capability observed factual " +
                                      "arrival at the exact target and kept its " +
                                      "RouteCompleted output promise.";
                    }
                    else if (step.Result.State == KLEPExecutableState.Failed &&
                             step.Result.Outputs.Count == 0)
                    {
                        outcome = KLEPCoverRouteTrialOutcome.Failed;
                        explanation = "The bounded route runtime exhausted " +
                                      "its trial without claiming success.";
                    }
                    else
                    {
                        outcome = KLEPCoverRouteTrialOutcome.Faulted;
                        explanation = "Terminal lifecycle/output evidence did " +
                                      "not match the trusted capability contract.";
                    }

                    break;
                }

                if (step.Result.State != KLEPExecutableState.Running ||
                    !executable.TryGetIntent(
                        trace.Decision.CycleIndex,
                        out KLEPImaginationIntent intent) ||
                    intent.RunIndex != runIndex ||
                    !TryReadIntent(
                        intent,
                        out KLEPCoverRouteIntent routeIntent) ||
                    routeIntent.RuntimeInstanceSequence !=
                        runtimeInstanceSequence)
                {
                    terminalState = KLEPExecutableState.Faulted;
                    terminalReason = KLEPExecutableExitReason.Faulted;
                    outcome = KLEPCoverRouteTrialOutcome.Faulted;
                    explanation = "Running route evidence omitted or corrupted " +
                                  "its exact host intent identity.";
                    break;
                }

                intents.Add(new KLEPCoverRouteIntentTrace(
                    intents.Count + 1L,
                    trace.Decision.CycleIndex,
                    runIndex,
                    routeIntent));
                KLEPCoverRoutePoint destination = routeIntent.Destination;

                KLEPCoverRoutePoint next =
                    KLEPCoverRouteGeometry.MoveToward(
                        position,
                        destination,
                        scenario.MovementPerTick);
                bool validMove =
                    KLEPCoverRouteGeometry.IsInsideArena(scenario, next) &&
                    !KLEPCoverRouteGeometry.SegmentIntersectsObstacle(
                        position,
                        next,
                        scenario.Obstacle,
                        scenario.AgentRadius);
                if (!validMove)
                {
                    collisionCount++;
                    stallCount++;
                }
                else
                {
                    double movement = position.DistanceTo(next);
                    if (movement <= 0.000000001d)
                    {
                        stallCount++;
                    }

                    routeLength += movement;
                    minimumClearance = Math.Min(
                        minimumClearance,
                        KLEPCoverRouteGeometry.SegmentSurfaceClearance(
                            position,
                            next,
                            scenario.Obstacle,
                            scenario.AgentRadius));
                    position = next;
                    actual.Add(position);
                }

                movementTicks++;
            }

            if (runIndex <= 0)
            {
                // A malformed/faulted scratch run still receives explicit
                // diagnostic identities without pretending it succeeded.
                runIndex = 1;
            }

            if (terminalCycle <= 0)
            {
                terminalCycle = 1;
            }

            double directDistance = scenario.Start.DistanceTo(scenario.Target);
            double projectedTotalLength = routeLength +
                position.DistanceTo(scenario.Target);
            KLEPCoverRouteFitness fitness = CalculateFitness(
                outcome == KLEPCoverRouteTrialOutcome.Succeeded,
                projectedTotalLength,
                directDistance,
                stallCount);
            double clearanceScore = Clamp01(
                minimumClearance / AuthoredClearance);
            return new KLEPCoverRouteTrial(
                sequence,
                trialId,
                SandboxVersion,
                manifest.ProposalFingerprint,
                CapabilityCatalog.Fingerprint,
                scenario,
                runtimeInstanceSequence,
                executable.StableId,
                runIndex,
                terminalCycle,
                terminalState,
                terminalReason,
                outcome,
                proposed,
                actual,
                intents,
                movementTicks,
                collisionCount,
                stallCount,
                exactOutputObserved,
                observedOutputKeyId,
                routeLength,
                projectedTotalLength,
                directDistance,
                Math.Max(0d, minimumClearance),
                fitness.Efficiency,
                clearanceScore,
                fitness.Value,
                explanation);
        }

        private KLEPCoverRouteAdmissionDecision DecideAdmission(
            KLEPImaginationManifest manifest,
            KLEPCoverRouteLedgerSnapshot snapshot)
        {
            KLEPCoverRouteAdmissionDisposition disposition;
            string explanation;
            if (snapshot.Support < scenarios.Count)
            {
                disposition = KLEPCoverRouteAdmissionDisposition
                    .RejectedInsufficientEvidence;
                explanation = "The proposal did not complete every authored " +
                              "deterministic scenario.";
            }
            else if (snapshot.Successes != snapshot.Support ||
                     HasCollisionOrStall(snapshot.Trials))
            {
                disposition = KLEPCoverRouteAdmissionDisposition
                    .RejectedTrialFailure;
                explanation = "At least one sandbox route failed, faulted, " +
                              "collided, or stalled.";
            }
            else
            {
                if (snapshot.MinimumClearance <
                    AuthoredClearance * minimumClearanceFraction)
                {
                    disposition = KLEPCoverRouteAdmissionDisposition
                        .RejectedClearance;
                    explanation = "Measured body-surface clearance fell below " +
                                  "the authored admission fraction.";
                }
                else if (snapshot.MeanFitness < minimumMeanFitness)
                {
                    disposition = KLEPCoverRouteAdmissionDisposition
                        .RejectedFitness;
                    explanation = "Mean deterministic route fitness was below " +
                                  "the project threshold.";
                }
                else
                {
                    disposition = KLEPCoverRouteAdmissionDisposition.Accepted;
                    explanation = "Every unique scenario succeeded with " +
                                  "adequate clearance and mean fitness.";
                }
            }

            return new KLEPCoverRouteAdmissionDecision(
                disposition,
                manifest.ProposalFingerprint,
                snapshot.Support,
                snapshot.MeanFitness,
                snapshot.Confidence,
                explanation);
        }

        public static KLEPImaginationCapabilityDescriptor
            CreateCapabilityDescriptor(
                KLEPKeyDefinition groundKey,
                KLEPKeyDefinition routeProblemKey,
                KLEPKeyDefinition edgeDangerKey,
                KLEPKeyDefinition routeCompletedKey)
        {
            groundKey = RequireProjectKey(
                groundKey, GroundKeyId, nameof(groundKey));
            routeProblemKey = RequireProjectKey(
                routeProblemKey,
                RouteProblemKeyId,
                nameof(routeProblemKey));
            edgeDangerKey = RequireProjectKey(
                edgeDangerKey,
                EdgeDangerKeyId,
                nameof(edgeDangerKey));
            routeCompletedKey = RequireProjectKey(
                routeCompletedKey,
                RouteCompletedKeyId,
                nameof(routeCompletedKey));
            var template = new KLEPExecutableDefinition(
                "template.demo.navigate-cover-route",
                "Trusted three-waypoint cover route",
                KLEPExecutableKind.Action,
                executionLocks: new[]
                {
                    new KLEPLock(
                        "lock.demo.navigate-cover-route.ready",
                        "Grounded route problem without edge danger",
                        new KLEPAll(
                            new KLEPKeyPresent(groundKey.Id.Value),
                            new KLEPKeyPresent(routeProblemKey.Id.Value),
                            new KLEPNot(
                                new KLEPKeyPresent(edgeDangerKey.Id.Value))))
                },
                baseAttractiveness: 60f,
                executionMode: KLEPExecutionMode.Solo,
                declaredOutputs: new[] { routeCompletedKey });
            return new KLEPImaginationCapabilityDescriptor(
                CapabilityId,
                CapabilityVersion,
                "descriptor.demo.navigate-cover-route.v1",
                template,
                new[]
                {
                    new KLEPImaginationParameterDefinition(
                        "routeId",
                        KLEPImaginationValueKind.Text,
                        maximumTextLength: 64),
                    new KLEPImaginationParameterDefinition(
                        "waypoint1X",
                        KLEPImaginationValueKind.Number,
                        minimumNumber: ArenaMinimumX +
                            AuthoredCollisionRadius,
                        maximumNumber: ArenaMaximumX -
                            AuthoredCollisionRadius),
                    new KLEPImaginationParameterDefinition(
                        "waypoint1Z",
                        KLEPImaginationValueKind.Number,
                        minimumNumber: ArenaMinimumZ +
                            AuthoredCollisionRadius,
                        maximumNumber: ArenaMaximumZ -
                            AuthoredCollisionRadius),
                    new KLEPImaginationParameterDefinition(
                        "waypoint2X",
                        KLEPImaginationValueKind.Number,
                        minimumNumber: ArenaMinimumX +
                            AuthoredCollisionRadius,
                        maximumNumber: ArenaMaximumX -
                            AuthoredCollisionRadius),
                    new KLEPImaginationParameterDefinition(
                        "waypoint2Z",
                        KLEPImaginationValueKind.Number,
                        minimumNumber: ArenaMinimumZ +
                            AuthoredCollisionRadius,
                        maximumNumber: ArenaMaximumZ -
                            AuthoredCollisionRadius),
                    new KLEPImaginationParameterDefinition(
                        "waypoint3X",
                        KLEPImaginationValueKind.Number,
                        minimumNumber: ArenaMinimumX +
                            AuthoredCollisionRadius,
                        maximumNumber: ArenaMaximumX -
                            AuthoredCollisionRadius),
                    new KLEPImaginationParameterDefinition(
                        "waypoint3Z",
                        KLEPImaginationValueKind.Number,
                        minimumNumber: ArenaMinimumZ +
                            AuthoredCollisionRadius,
                        maximumNumber: ArenaMaximumZ -
                            AuthoredCollisionRadius),
                    new KLEPImaginationParameterDefinition(
                        "arrivalRadius",
                        KLEPImaginationValueKind.Number,
                        minimumNumber: 0.05d,
                        maximumNumber: 1.5d),
                    new KLEPImaginationParameterDefinition(
                        "maximumTicks",
                        KLEPImaginationValueKind.Integer,
                        minimumInteger: 8L,
                        maximumInteger: 512L)
                });
        }

        public static KLEPImaginationCapabilityCatalog
            CreateCapabilityCatalog(
                KLEPImaginationCapabilityDescriptor descriptor,
                IKLEPImaginationCapabilityRuntimeFactory runtimeFactory)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (!StringComparer.Ordinal.Equals(
                    descriptor.StableId,
                    CapabilityId) ||
                !StringComparer.Ordinal.Equals(
                    descriptor.Version,
                    CapabilityVersion))
            {
                throw new ArgumentException(
                    "The catalog requires the exact Cover/Route V1 descriptor.",
                    nameof(descriptor));
            }

            return new KLEPImaginationCapabilityCatalog(
                new[]
                {
                    new KLEPImaginationCapabilityRegistration(
                        descriptor,
                        runtimeFactory ?? throw new ArgumentNullException(
                            nameof(runtimeFactory)))
                });
        }

        public KLEPCoverRouteProblemSensorExecutable CreateProblemSensor(
            string stableId)
        {
            var definition = new KLEPExecutableDefinition(
                stableId,
                "Cover route problem sensor",
                KLEPExecutableKind.Sensor,
                executionMode: KLEPExecutionMode.Tandem,
                declaredOutputs: new[] { RouteProblemKey });
            return new KLEPCoverRouteProblemSensorExecutable(
                definition,
                RouteProblemKey);
        }

        public static IReadOnlyList<KLEPCoverRoutePoint>
            GetManifestWaypoints(KLEPImaginationManifest manifest)
        {
            RequireRouteManifest(manifest);
            return new ReadOnlyCollection<KLEPCoverRoutePoint>(
                new List<KLEPCoverRoutePoint>
                {
                    new KLEPCoverRoutePoint(
                        manifest.Arguments["waypoint1X"].AsNumber(),
                        manifest.Arguments["waypoint1Z"].AsNumber()),
                    new KLEPCoverRoutePoint(
                        manifest.Arguments["waypoint2X"].AsNumber(),
                        manifest.Arguments["waypoint2Z"].AsNumber()),
                    new KLEPCoverRoutePoint(
                        manifest.Arguments["waypoint3X"].AsNumber(),
                        manifest.Arguments["waypoint3Z"].AsNumber())
                });
        }

        public static IReadOnlyList<KLEPCoverRoutePoint> GetProposalPoints(
            KLEPImaginationManifest manifest,
            KLEPCoverRoutePoint start,
            KLEPCoverRoutePoint target)
        {
            IReadOnlyList<KLEPCoverRoutePoint> waypoints =
                GetManifestWaypoints(manifest);
            var points = new List<KLEPCoverRoutePoint>(5) { start };
            for (int index = 0; index < waypoints.Count; index++)
            {
                points.Add(waypoints[index]);
            }

            if (!points[points.Count - 1].Equals(target))
            {
                points.Add(target);
            }

            return new ReadOnlyCollection<KLEPCoverRoutePoint>(points);
        }

        public static bool TryReadIntent(
            KLEPImaginationIntent intent,
            out KLEPCoverRouteIntent routeIntent)
        {
            routeIntent = null;
            if (intent == null || intent.Payload == null)
            {
                return false;
            }

            IReadOnlyDictionary<string, KLEPImaginationValue> fields =
                intent.Payload.Fields;
            if (fields.Count != 7 ||
                !fields.TryGetValue("routeId", out KLEPImaginationValue route) ||
                !fields.TryGetValue("problemId", out KLEPImaginationValue problem) ||
                !fields.TryGetValue("targetId", out KLEPImaginationValue target) ||
                !fields.TryGetValue("destinationX", out KLEPImaginationValue x) ||
                !fields.TryGetValue("destinationZ", out KLEPImaginationValue z) ||
                !fields.TryGetValue("phase", out KLEPImaginationValue phase) ||
                !fields.TryGetValue(
                    "runtimeInstance",
                    out KLEPImaginationValue runtime))
            {
                return false;
            }

            try
            {
                routeIntent = new KLEPCoverRouteIntent(
                    route.AsText(),
                    problem.AsText(),
                    target.AsText(),
                    runtime.AsInteger(),
                    phase.AsInteger(),
                    new KLEPCoverRoutePoint(
                        x.AsNumber(),
                        z.AsNumber()));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public static KLEPCoverRouteFitness CalculateFitness(
            bool succeeded,
            double routeLength,
            double directDistance,
            int stallCount)
        {
            if (double.IsNaN(routeLength) ||
                double.IsInfinity(routeLength) ||
                routeLength < 0d ||
                double.IsNaN(directDistance) ||
                double.IsInfinity(directDistance) ||
                directDistance < 0d ||
                stallCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(routeLength));
            }

            double efficiency = succeeded && routeLength > 0d
                ? Clamp01(directDistance / routeLength)
                : 0d;
            double stallScore = stallCount == 0 ? 1d : 0d;
            double value = succeeded
                ? Clamp01(0.50d + (0.35d * efficiency) +
                          (0.15d * stallScore))
                : 0d;
            return new KLEPCoverRouteFitness(
                efficiency,
                stallScore,
                value);
        }

        private static KLEPExecutableStepTrace FindSoloStep(
            KLEPDecisionTrace trace,
            string stableId)
        {
            foreach (KLEPExecutableStepTrace step in trace.Executions)
            {
                if (step.Kind == KLEPExecutableStepKind.Solo &&
                    StringComparer.Ordinal.Equals(
                        step.ExecutableStableId,
                        stableId))
                {
                    return step;
                }
            }

            return null;
        }

        private bool HasExactSuccessfulOutput(
            KLEPExecutionResult result)
        {
            return result.ExitReason == KLEPExecutableExitReason.Succeeded &&
                   result.Outputs.Count == 1 &&
                   result.Outputs[0].KeyId == RouteCompletedKey.Id;
        }

        private static bool HasCollisionOrStall(
            IReadOnlyList<KLEPCoverRouteTrial> trials)
        {
            for (int index = 0; index < trials.Count; index++)
            {
                if (trials[index].CollisionCount != 0 ||
                    trials[index].StallCount != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static KLEPImaginationManifest RequireRouteManifest(
            KLEPImaginationManifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (!StringComparer.Ordinal.Equals(
                    manifest.CapabilityId,
                    CapabilityId) ||
                !StringComparer.Ordinal.Equals(
                    manifest.CapabilityVersion,
                    CapabilityVersion))
            {
                throw new ArgumentException(
                    "The Manifest is not a Cover/Route V1 binding.",
                    nameof(manifest));
            }

            return manifest;
        }

        private static KLEPKeyDefinition RequireProjectKey(
            KLEPKeyDefinition definition,
            string expectedStableId,
            string name)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(name);
            }

            if (!StringComparer.Ordinal.Equals(
                    definition.Id.Value,
                    expectedStableId) ||
                definition.Scope != KLEPKeyScope.Local)
            {
                throw new ArgumentException(
                    $"Project Key '{name}' must be the exact Local Key " +
                    $"'{expectedStableId}'.",
                    name);
            }

            return definition;
        }

        private static void ValidateScenarioContract(
            KLEPCoverRouteScenario scenario,
            int ordinal)
        {
            KLEPCoverRouteObstacle obstacle = scenario.Obstacle;
            string expectedId;
            KLEPCoverRoutePoint expectedStart;
            KLEPCoverRoutePoint expectedTarget;
            switch (ordinal)
            {
                case 0:
                    expectedId = "central.001";
                    expectedStart = new KLEPCoverRoutePoint(-3.5d, -0.75d);
                    expectedTarget = new KLEPCoverRoutePoint(8d, -0.5d);
                    break;
                case 1:
                    expectedId = "central.002";
                    expectedStart = new KLEPCoverRoutePoint(-4d, -0.25d);
                    expectedTarget = new KLEPCoverRoutePoint(8.5d, 0.25d);
                    break;
                case 2:
                    expectedId = "central.003";
                    expectedStart = new KLEPCoverRoutePoint(-3.75d, 0.25d);
                    expectedTarget = new KLEPCoverRoutePoint(7.75d, 0.5d);
                    break;
                case 3:
                    expectedId = "central.004";
                    expectedStart = new KLEPCoverRoutePoint(-4.25d, 0.75d);
                    expectedTarget = new KLEPCoverRoutePoint(8.25d, 0.75d);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ordinal));
            }

            if (!scenario.AgentRadius.Equals(AuthoredCollisionRadius) ||
                !scenario.MovementPerTick.Equals(AuthoredMovementPerTick) ||
                !scenario.ArenaMinimumX.Equals(ArenaMinimumX) ||
                !scenario.ArenaMaximumX.Equals(ArenaMaximumX) ||
                !scenario.ArenaMinimumZ.Equals(ArenaMinimumZ) ||
                !scenario.ArenaMaximumZ.Equals(ArenaMaximumZ) ||
                !StringComparer.Ordinal.Equals(
                    obstacle.StableId,
                    "cover.central") ||
                !obstacle.MinimumX.Equals(2.2d) ||
                !obstacle.MaximumX.Equals(2.8d) ||
                !obstacle.MinimumZ.Equals(-2d) ||
                !obstacle.MaximumZ.Equals(2d) ||
                !StringComparer.Ordinal.Equals(
                    scenario.StableId,
                    expectedId) ||
                !scenario.Start.Equals(expectedStart) ||
                !scenario.Target.Equals(expectedTarget))
            {
                throw new ArgumentException(
                    $"Scenario ordinal {ordinal} drifts from the exact " +
                    "authored V1 identity, start/target, arena, movement, " +
                    "collision radius, or central-wall geometry.",
                    nameof(scenario));
            }
        }

        private static double Clamp01(double value)
        {
            if (value <= 0d)
            {
                return 0d;
            }

            return value >= 1d ? 1d : value;
        }

        private static double RequirePositiveFinite(double value, string name)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value <= 0d)
            {
                throw new ArgumentOutOfRangeException(name);
            }

            return value;
        }

        private static double RequireUnitInterval(double value, string name)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value < 0d ||
                value > 1d)
            {
                throw new ArgumentOutOfRangeException(name);
            }

            return value;
        }
    }

    public sealed class KLEPCoverRouteRuntimeFactory :
        IKLEPImaginationCapabilityRuntimeFactory
    {
        private readonly KLEPKeyDefinition routeProblem;
        private readonly KLEPKeyDefinition routeCompleted;

        public KLEPCoverRouteRuntimeFactory(
            KLEPKeyDefinition routeProblem,
            KLEPKeyDefinition routeCompleted)
        {
            this.routeProblem = routeProblem ??
                throw new ArgumentNullException(nameof(routeProblem));
            this.routeCompleted = routeCompleted ??
                throw new ArgumentNullException(nameof(routeCompleted));
            if (!StringComparer.Ordinal.Equals(
                    routeProblem.Id.Value,
                    KLEPCoverRouteLearningSession.RouteProblemKeyId) ||
                !StringComparer.Ordinal.Equals(
                    routeCompleted.Id.Value,
                    KLEPCoverRouteLearningSession.RouteCompletedKeyId))
            {
                throw new ArgumentException(
                    "The route runtime factory requires the exact project " +
                    "RouteProblem and RouteCompleted Keys.");
            }
        }

        public long CreatedCount { get; private set; }

        public KLEPImaginationCapabilityRuntime CreateRuntime(
            IReadOnlyDictionary<string, KLEPImaginationValue> arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            if (CreatedCount == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The cover-route runtime sequence is exhausted.");
            }

            CreatedCount++;
            return new KLEPCoverRouteCapabilityRuntime(
                routeProblem,
                routeCompleted,
                CreatedCount);
        }
    }

    internal sealed class KLEPCoverRouteCapabilityRuntime :
        KLEPImaginationCapabilityRuntime
    {
        private readonly KLEPKeyDefinition routeProblem;
        private readonly KLEPKeyDefinition routeCompleted;
        private readonly long runtimeInstanceSequence;
        private int routePhase;
        private long tickCount;
        private long lastObservedWorldTick;
        private string activeProblemId;
        private string activeTargetId;

        internal KLEPCoverRouteCapabilityRuntime(
            KLEPKeyDefinition routeProblem,
            KLEPKeyDefinition routeCompleted,
            long runtimeInstanceSequence)
        {
            this.routeProblem = routeProblem ??
                throw new ArgumentNullException(nameof(routeProblem));
            this.routeCompleted = routeCompleted ??
                throw new ArgumentNullException(nameof(routeCompleted));
            if (runtimeInstanceSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runtimeInstanceSequence));
            }

            this.runtimeInstanceSequence = runtimeInstanceSequence;
        }

        public override void Enter(KLEPImaginationCapabilityContext context)
        {
            routePhase = 0;
            tickCount = 0;
            lastObservedWorldTick = -1;
            activeProblemId = null;
            activeTargetId = null;
        }

        public override KLEPImaginationCapabilityResult Tick(
            KLEPImaginationCapabilityContext context)
        {
            if (!TryReadProblem(context.Keys, out KLEPCoverRouteProblem problem))
            {
                return KLEPImaginationCapabilityResult.Failed();
            }

            string routeId = context.Arguments["routeId"].AsText();
            double arrivalRadius =
                context.Arguments["arrivalRadius"].AsNumber();
            long maximumTicks =
                context.Arguments["maximumTicks"].AsInteger();
            tickCount++;

            if (activeProblemId == null)
            {
                activeProblemId = problem.ProblemId;
                activeTargetId = problem.TargetId;
            }
            else if (!StringComparer.Ordinal.Equals(
                         activeProblemId,
                         problem.ProblemId) ||
                     !StringComparer.Ordinal.Equals(
                         activeTargetId,
                         problem.TargetId) ||
                     problem.ObservedWorldTick < lastObservedWorldTick)
            {
                return KLEPImaginationCapabilityResult.Failed();
            }

            lastObservedWorldTick = problem.ObservedWorldTick;
            if (problem.Position.DistanceTo(problem.Target) <= arrivalRadius)
            {
                return KLEPImaginationCapabilityResult.Succeeded(
                    new[]
                    {
                        new KLEPImaginationSuccessfulOutput(
                            routeCompleted.Id,
                            new KLEPKeyPayload(
                                new[]
                                {
                                    new KeyValuePair<
                                        string,
                                        KLEPKeyValue>("routeId", routeId),
                                    new KeyValuePair<
                                        string,
                                        KLEPKeyValue>(
                                            "problemId",
                                            problem.ProblemId),
                                    new KeyValuePair<
                                        string,
                                        KLEPKeyValue>(
                                            "targetId",
                                            problem.TargetId),
                                    new KeyValuePair<
                                        string,
                                        KLEPKeyValue>(
                                            "observedWorldTick",
                                            problem.ObservedWorldTick),
                                    new KeyValuePair<
                                        string,
                                        KLEPKeyValue>(
                                            "runtimeInstance",
                                            runtimeInstanceSequence),
                                    new KeyValuePair<
                                        string,
                                        KLEPKeyValue>("ticks", tickCount)
                                }))
                    });
            }

            if (tickCount >= maximumTicks)
            {
                return KLEPImaginationCapabilityResult.Failed();
            }

            IReadOnlyList<KLEPCoverRoutePoint> waypoints =
                GetWaypoints(context.Arguments);
            while (routePhase < waypoints.Count &&
                   problem.Position.DistanceTo(waypoints[routePhase]) <=
                   arrivalRadius)
            {
                routePhase++;
            }

            KLEPCoverRoutePoint destination = routePhase < waypoints.Count
                ? waypoints[routePhase]
                : problem.Target;
            return KLEPImaginationCapabilityResult.Running(
                new KLEPImaginationIntentPayload(
                    new[]
                    {
                        new KeyValuePair<string, KLEPImaginationValue>(
                            "routeId",
                            KLEPImaginationValue.FromText(routeId)),
                        new KeyValuePair<string, KLEPImaginationValue>(
                            "problemId",
                            KLEPImaginationValue.FromText(problem.ProblemId)),
                        new KeyValuePair<string, KLEPImaginationValue>(
                            "targetId",
                            KLEPImaginationValue.FromText(problem.TargetId)),
                        new KeyValuePair<string, KLEPImaginationValue>(
                            "phase",
                            KLEPImaginationValue.FromInteger(routePhase)),
                        new KeyValuePair<string, KLEPImaginationValue>(
                            "runtimeInstance",
                            KLEPImaginationValue.FromInteger(
                                runtimeInstanceSequence)),
                        new KeyValuePair<string, KLEPImaginationValue>(
                            "destinationX",
                            KLEPImaginationValue.FromNumber(destination.X)),
                        new KeyValuePair<string, KLEPImaginationValue>(
                            "destinationZ",
                            KLEPImaginationValue.FromNumber(destination.Z))
                    }));
        }

        private static IReadOnlyList<KLEPCoverRoutePoint> GetWaypoints(
            IReadOnlyDictionary<string, KLEPImaginationValue> arguments)
        {
            return new ReadOnlyCollection<KLEPCoverRoutePoint>(
                new List<KLEPCoverRoutePoint>
                {
                    new KLEPCoverRoutePoint(
                        arguments["waypoint1X"].AsNumber(),
                        arguments["waypoint1Z"].AsNumber()),
                    new KLEPCoverRoutePoint(
                        arguments["waypoint2X"].AsNumber(),
                        arguments["waypoint2Z"].AsNumber()),
                    new KLEPCoverRoutePoint(
                        arguments["waypoint3X"].AsNumber(),
                        arguments["waypoint3Z"].AsNumber())
                });
        }

        private bool TryReadProblem(
            KLEPKeySnapshot snapshot,
            out KLEPCoverRouteProblem problem)
        {
            problem = null;
            IReadOnlyList<KLEPKeyFact> facts = snapshot.FindAll(
                routeProblem.Id);
            if (facts.Count != 1)
            {
                return false;
            }

            return KLEPCoverRouteProblem.TryFromPayload(
                facts[0].Payload,
                out problem);
        }
    }

    public sealed class KLEPCoverRouteProblem
    {
        public KLEPCoverRouteProblem(
            string problemId,
            string targetId,
            long observedWorldTick,
            KLEPCoverRoutePoint position,
            KLEPCoverRoutePoint target)
        {
            if (string.IsNullOrWhiteSpace(problemId))
            {
                throw new ArgumentException(
                    "A route problem requires an exact identity.",
                    nameof(problemId));
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "A route problem requires an exact target identity.",
                    nameof(targetId));
            }

            if (observedWorldTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observedWorldTick));
            }

            ProblemId = problemId;
            TargetId = targetId;
            ObservedWorldTick = observedWorldTick;
            Position = position;
            Target = target;
        }

        public string ProblemId { get; }
        public string TargetId { get; }
        public long ObservedWorldTick { get; }
        public KLEPCoverRoutePoint Position { get; }
        public KLEPCoverRoutePoint Target { get; }

        public KLEPKeyPayload ToPayload()
        {
            return new KLEPKeyPayload(
                new[]
                {
                    Field("problemId", ProblemId),
                    Field("targetId", TargetId),
                    Field("observedWorldTick", ObservedWorldTick),
                    Field("positionX", Position.X),
                    Field("positionZ", Position.Z),
                    Field("targetX", Target.X),
                    Field("targetZ", Target.Z)
                });
        }

        public static bool TryFromPayload(
            KLEPKeyPayload payload,
            out KLEPCoverRouteProblem problem)
        {
            problem = null;
            if (payload == null || payload.Count != 7 ||
                !payload.TryGetText("problemId", out string problemId) ||
                !payload.TryGetText("targetId", out string targetId) ||
                !payload.TryGetInteger(
                    "observedWorldTick",
                    out long observedWorldTick) ||
                !payload.TryGetNumber("positionX", out double positionX) ||
                !payload.TryGetNumber("positionZ", out double positionZ) ||
                !payload.TryGetNumber("targetX", out double targetX) ||
                !payload.TryGetNumber("targetZ", out double targetZ))
            {
                return false;
            }

            try
            {
                problem = new KLEPCoverRouteProblem(
                    problemId,
                    targetId,
                    observedWorldTick,
                    new KLEPCoverRoutePoint(positionX, positionZ),
                    new KLEPCoverRoutePoint(targetX, targetZ));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static KeyValuePair<string, KLEPKeyValue> Field(
            string name,
            KLEPKeyValue value)
        {
            return new KeyValuePair<string, KLEPKeyValue>(name, value);
        }
    }

    /// <summary>
    /// Pure host-fed Tandem Sensor. It turns one current immutable route
    /// observation into the exact project RouteProblem Key.
    /// </summary>
    public sealed class KLEPCoverRouteProblemSensorExecutable :
        KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition output;
        private KLEPCoverRouteProblem currentProblem;

        public KLEPCoverRouteProblemSensorExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition routeProblemKey)
            : base(definition)
        {
            output = routeProblemKey ??
                throw new ArgumentNullException(nameof(routeProblemKey));
            if (definition.Kind != KLEPExecutableKind.Sensor ||
                definition.ExecutionMode != KLEPExecutionMode.Tandem ||
                definition.DeclaredOutputs.Count != 1 ||
                definition.DeclaredOutputs[0].Id != output.Id ||
                output.DefaultLifetime != KLEPKeyLifetime.OneCycle ||
                !StringComparer.Ordinal.Equals(
                    output.Id.Value,
                    KLEPCoverRouteLearningSession.RouteProblemKeyId))
            {
                throw new ArgumentException(
                    "A route problem sensor requires the exact Tandem Sensor " +
                    "definition and RouteProblem output.",
                    nameof(definition));
            }
        }

        public KLEPCoverRouteProblem CurrentProblem => currentProblem;

        public void SetCurrentProblem(KLEPCoverRouteProblem problem)
        {
            currentProblem = problem ??
                throw new ArgumentNullException(nameof(problem));
        }

        public void ClearCurrentProblem()
        {
            currentProblem = null;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (currentProblem == null)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            context.Add(output, currentProblem.ToPayload());
            return KLEPExecutableTickStatus.Succeeded;
        }
    }

    internal static class KLEPCoverRouteGeometry
    {
        private const double Epsilon = 0.000000001d;

        internal static KLEPCoverRoutePoint MoveToward(
            KLEPCoverRoutePoint current,
            KLEPCoverRoutePoint destination,
            double maximumDistance)
        {
            if (double.IsNaN(maximumDistance) ||
                double.IsInfinity(maximumDistance) ||
                maximumDistance <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            }

            double distance = current.DistanceTo(destination);
            if (distance <= maximumDistance || distance <= Epsilon)
            {
                return destination;
            }

            double scale = maximumDistance / distance;
            return new KLEPCoverRoutePoint(
                current.X + ((destination.X - current.X) * scale),
                current.Z + ((destination.Z - current.Z) * scale));
        }

        internal static bool IsInsideArena(
            KLEPCoverRouteScenario scenario,
            KLEPCoverRoutePoint point)
        {
            return point.X >= scenario.ArenaMinimumX + scenario.AgentRadius &&
                   point.X <= scenario.ArenaMaximumX - scenario.AgentRadius &&
                   point.Z >= scenario.ArenaMinimumZ + scenario.AgentRadius &&
                   point.Z <= scenario.ArenaMaximumZ - scenario.AgentRadius;
        }

        internal static double SurfaceClearance(
            KLEPCoverRoutePoint point,
            KLEPCoverRouteObstacle obstacle,
            double agentRadius)
        {
            double x = point.X < obstacle.MinimumX
                ? obstacle.MinimumX - point.X
                : point.X > obstacle.MaximumX
                    ? point.X - obstacle.MaximumX
                    : 0d;
            double z = point.Z < obstacle.MinimumZ
                ? obstacle.MinimumZ - point.Z
                : point.Z > obstacle.MaximumZ
                    ? point.Z - obstacle.MaximumZ
                    : 0d;
            return Math.Max(0d, Math.Sqrt((x * x) + (z * z)) - agentRadius);
        }

        /// <summary>
        /// Exact minimum body-surface clearance over the complete swept
        /// segment, not merely its sampled endpoints.
        /// </summary>
        internal static double SegmentSurfaceClearance(
            KLEPCoverRoutePoint from,
            KLEPCoverRoutePoint to,
            KLEPCoverRouteObstacle obstacle,
            double agentRadius)
        {
            if (double.IsNaN(agentRadius) ||
                double.IsInfinity(agentRadius) ||
                agentRadius <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(agentRadius));
            }

            if (SegmentIntersectsObstacle(from, to, obstacle, 0d))
            {
                return 0d;
            }

            var southWest = new KLEPCoverRoutePoint(
                obstacle.MinimumX,
                obstacle.MinimumZ);
            var southEast = new KLEPCoverRoutePoint(
                obstacle.MaximumX,
                obstacle.MinimumZ);
            var northEast = new KLEPCoverRoutePoint(
                obstacle.MaximumX,
                obstacle.MaximumZ);
            var northWest = new KLEPCoverRoutePoint(
                obstacle.MinimumX,
                obstacle.MaximumZ);
            double distance = Math.Min(
                RawPointRectangleDistance(from, obstacle),
                RawPointRectangleDistance(to, obstacle));
            distance = Math.Min(
                distance,
                SegmentDistance(from, to, southWest, southEast));
            distance = Math.Min(
                distance,
                SegmentDistance(from, to, southEast, northEast));
            distance = Math.Min(
                distance,
                SegmentDistance(from, to, northEast, northWest));
            distance = Math.Min(
                distance,
                SegmentDistance(from, to, northWest, southWest));
            return Math.Max(0d, distance - agentRadius);
        }

        private static double RawPointRectangleDistance(
            KLEPCoverRoutePoint point,
            KLEPCoverRouteObstacle obstacle)
        {
            double x = point.X < obstacle.MinimumX
                ? obstacle.MinimumX - point.X
                : point.X > obstacle.MaximumX
                    ? point.X - obstacle.MaximumX
                    : 0d;
            double z = point.Z < obstacle.MinimumZ
                ? obstacle.MinimumZ - point.Z
                : point.Z > obstacle.MaximumZ
                    ? point.Z - obstacle.MaximumZ
                    : 0d;
            return Math.Sqrt((x * x) + (z * z));
        }

        private static double SegmentDistance(
            KLEPCoverRoutePoint firstStart,
            KLEPCoverRoutePoint firstEnd,
            KLEPCoverRoutePoint secondStart,
            KLEPCoverRoutePoint secondEnd)
        {
            return Math.Min(
                Math.Min(
                    PointSegmentDistance(
                        firstStart,
                        secondStart,
                        secondEnd),
                    PointSegmentDistance(
                        firstEnd,
                        secondStart,
                        secondEnd)),
                Math.Min(
                    PointSegmentDistance(
                        secondStart,
                        firstStart,
                        firstEnd),
                    PointSegmentDistance(
                        secondEnd,
                        firstStart,
                        firstEnd)));
        }

        private static double PointSegmentDistance(
            KLEPCoverRoutePoint point,
            KLEPCoverRoutePoint start,
            KLEPCoverRoutePoint end)
        {
            double x = end.X - start.X;
            double z = end.Z - start.Z;
            double lengthSquared = (x * x) + (z * z);
            if (lengthSquared <= Epsilon)
            {
                return point.DistanceTo(start);
            }

            double projection =
                (((point.X - start.X) * x) +
                 ((point.Z - start.Z) * z)) / lengthSquared;
            projection = Math.Max(0d, Math.Min(1d, projection));
            var closest = new KLEPCoverRoutePoint(
                start.X + (projection * x),
                start.Z + (projection * z));
            return point.DistanceTo(closest);
        }

        internal static bool SegmentIntersectsObstacle(
            KLEPCoverRoutePoint from,
            KLEPCoverRoutePoint to,
            KLEPCoverRouteObstacle obstacle,
            double expansion)
        {
            double minimumX = obstacle.MinimumX - expansion;
            double maximumX = obstacle.MaximumX + expansion;
            double minimumZ = obstacle.MinimumZ - expansion;
            double maximumZ = obstacle.MaximumZ + expansion;
            double directionX = to.X - from.X;
            double directionZ = to.Z - from.Z;
            double minimumT = 0d;
            double maximumT = 1d;
            return ClipAxis(
                       from.X,
                       directionX,
                       minimumX,
                       maximumX,
                       ref minimumT,
                       ref maximumT) &&
                   ClipAxis(
                       from.Z,
                       directionZ,
                       minimumZ,
                       maximumZ,
                       ref minimumT,
                       ref maximumT);
        }

        private static bool ClipAxis(
            double origin,
            double direction,
            double minimum,
            double maximum,
            ref double minimumT,
            ref double maximumT)
        {
            if (Math.Abs(direction) <= Epsilon)
            {
                return origin >= minimum && origin <= maximum;
            }

            double first = (minimum - origin) / direction;
            double second = (maximum - origin) / direction;
            if (first > second)
            {
                double temporary = first;
                first = second;
                second = temporary;
            }

            minimumT = Math.Max(minimumT, first);
            maximumT = Math.Min(maximumT, second);
            return minimumT <= maximumT &&
                   maximumT >= 0d &&
                   minimumT <= 1d;
        }
    }
}
