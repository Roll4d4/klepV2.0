using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// One structural question asked on behalf of an already-mapped structural
    /// Goal. The target is explicit so a Goal's diagnostic kind or output list
    /// never silently becomes its desired end.
    /// </summary>
    public sealed class KLEPGoalStructuralSolutionRequest
    {
        public KLEPGoalStructuralSolutionRequest(
            string requestingGoalStableId,
            string requestingGoalTenureId,
            KLEPKeyId targetKeyId)
        {
            RequestingGoalStableId = RequireId(
                requestingGoalStableId, nameof(requestingGoalStableId));
            RequestingGoalTenureId = RequireId(
                requestingGoalTenureId, nameof(requestingGoalTenureId));
            if (string.IsNullOrWhiteSpace(targetKeyId.Value))
            {
                throw new ArgumentException(
                    "A structural solution request requires a stable target Key ID.",
                    nameof(targetKeyId));
            }

            TargetKeyId = targetKeyId;
        }

        public string RequestingGoalStableId { get; }
        public string RequestingGoalTenureId { get; }
        public KLEPKeyId TargetKeyId { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable ID is required.", parameterName);
            }

            return value;
        }
    }

    public enum KLEPGoalStructuralSolutionDisposition
    {
        Solved,
        Conditional,
        NoSolution,
        Abstained
    }

    public enum KLEPGoalStructuralRuntimeConditionKind
    {
        ExternalKeyRequired,
        NegativeExpressionMustRemainFalse
    }

    /// <summary>
    /// A fact the structural abstraction cannot manufacture. Conditions remain
    /// ordinary runtime Lock obligations; they are never treated as Keys or
    /// permission to execute.
    /// </summary>
    public sealed class KLEPGoalStructuralRuntimeCondition
    {
        internal KLEPGoalStructuralRuntimeCondition(
            KLEPGoalStructuralRuntimeConditionKind kind,
            KLEPKeyId? keyId,
            string requiredByExecutableStableId,
            string requiredByRootTenureId,
            string lockStableId,
            string expressionPath,
            KLEPStructuralLockExpressionSnapshot expression)
        {
            Kind = kind;
            KeyId = keyId;
            RequiredByExecutableStableId =
                requiredByExecutableStableId ?? string.Empty;
            RequiredByRootTenureId = requiredByRootTenureId ?? string.Empty;
            LockStableId = lockStableId ?? string.Empty;
            ExpressionPath = expressionPath ?? string.Empty;
            Expression = expression;
        }

        public KLEPGoalStructuralRuntimeConditionKind Kind { get; }
        public KLEPKeyId? KeyId { get; }
        public string RequiredByExecutableStableId { get; }
        public string RequiredByRootTenureId { get; }
        public string LockStableId { get; }
        public string ExpressionPath { get; }
        public KLEPStructuralLockExpressionSnapshot Expression { get; }
    }

    public enum KLEPGoalStructuralSolutionDiagnosticCode
    {
        InvalidStructuralMap,
        RequestingGoalMissing,
        RequestingGoalTenureMismatch,
        RequestingExecutableIsNotStructuralGoal,
        RequestingGoalTargetMismatch,
        RequestingGoalShapeUnsupported,
        MissingTargetProducer,
        MissingPrerequisiteProducer,
        DependencyCycle,
        RequestingGoalProducerExcluded,
        GoalOwnedProducerExcluded,
        TandemProducerExcluded,
        GoalProducerExcluded,
        UnsupportedLockExpression,
        UnsatisfiedAnyExpression,
        NoUsableProducer
    }

    public sealed class KLEPGoalStructuralSolutionDiagnostic
    {
        internal KLEPGoalStructuralSolutionDiagnostic(
            KLEPGoalStructuralSolutionDiagnosticCode code,
            string path,
            KLEPKeyId? keyId,
            string executableStableId,
            string message)
        {
            Code = code;
            Path = path ?? string.Empty;
            KeyId = keyId;
            ExecutableStableId = executableStableId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public KLEPGoalStructuralSolutionDiagnosticCode Code { get; }
        public string Path { get; }
        public KLEPKeyId? KeyId { get; }
        public string ExecutableStableId { get; }
        public string Message { get; }
    }

    /// <summary>
    /// One prerequisite-first root Solo step in the selected symbolic proof.
    /// The retained structural node is immutable and does not expose a runtime.
    /// </summary>
    public sealed class KLEPGoalStructuralSolutionStep
    {
        private readonly ReadOnlyCollection<KLEPKeyId> requiredOutputKeyIds;

        internal KLEPGoalStructuralSolutionStep(
            int stepIndex,
            KLEPExecutableStructuralNode executable,
            IEnumerable<KLEPKeyId> requiredOutputKeyIds)
        {
            if (stepIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepIndex));
            }

            StepIndex = stepIndex;
            Executable = executable ?? throw new ArgumentNullException(
                nameof(executable));
            this.requiredOutputKeyIds = CopyKeyIds(requiredOutputKeyIds);
        }

        public int StepIndex { get; }
        public KLEPExecutableStructuralNode Executable { get; }
        public string ExecutableStableId => Executable.StableExecutableId;
        public string RootTenureId => Executable.RootTenureId;
        public string Path => Executable.Path;
        public IReadOnlyList<KLEPKeyId> RequiredOutputKeyIds =>
            requiredOutputKeyIds;

        private static ReadOnlyCollection<KLEPKeyId> CopyKeyIds(
            IEnumerable<KLEPKeyId> source)
        {
            var copy = new List<KLEPKeyId>();
            if (source != null)
            {
                foreach (KLEPKeyId keyId in source)
                {
                    copy.Add(keyId);
                }
            }

            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.Value, right.Value));
            return new ReadOnlyCollection<KLEPKeyId>(copy);
        }
    }

    public sealed class KLEPGoalStructuralSolutionCost
    {
        internal KLEPGoalStructuralSolutionCost(
            int distinctStepCount,
            int dependencyDepth,
            int externalConditionCount,
            int negativeConditionCount)
        {
            DistinctStepCount = distinctStepCount;
            DependencyDepth = dependencyDepth;
            ExternalConditionCount = externalConditionCount;
            NegativeConditionCount = negativeConditionCount;
        }

        public int DistinctStepCount { get; }
        public int DependencyDepth { get; }
        public int ExternalConditionCount { get; }
        public int NegativeConditionCount { get; }
        public bool IsLocallyClosed =>
            ExternalConditionCount == 0 && NegativeConditionCount == 0;
    }

    public sealed class KLEPGoalStructuralSolutionProvenance
    {
        internal KLEPGoalStructuralSolutionProvenance(
            string providerStableId,
            string providerVersion,
            string catalogRevision,
            KLEPStructuralMapFingerprint catalogFingerprint,
            KLEPGoalStructuralSolutionRequest request)
        {
            ProviderStableId = providerStableId ?? string.Empty;
            ProviderVersion = providerVersion ?? string.Empty;
            CatalogRevision = catalogRevision ?? string.Empty;
            CatalogFingerprint = catalogFingerprint ?? throw new ArgumentNullException(
                nameof(catalogFingerprint));
            RequestingGoalStableId = request.RequestingGoalStableId;
            RequestingGoalTenureId = request.RequestingGoalTenureId;
            TargetKeyId = request.TargetKeyId;
        }

        public string ProviderStableId { get; }
        public string ProviderVersion { get; }
        public string SolverStableId => ProviderStableId;
        public string SolverVersion => ProviderVersion;
        public string CatalogRevision { get; }
        public KLEPStructuralMapFingerprint CatalogFingerprint { get; }
        public string RequestingGoalStableId { get; }
        public string RequestingGoalTenureId { get; }
        public KLEPKeyId TargetKeyId { get; }
    }

    /// <summary>
    /// Immutable structural evidence. A solution is neither eligibility nor an
    /// executable Goal recipe and carries no authority to select or fire.
    /// </summary>
    public sealed class KLEPGoalStructuralSolution
    {
        private readonly ReadOnlyCollection<KLEPGoalStructuralSolutionStep> steps;
        private readonly ReadOnlyCollection<KLEPGoalStructuralRuntimeCondition>
            runtimeConditions;
        private readonly ReadOnlyCollection<KLEPGoalStructuralSolutionDiagnostic>
            diagnostics;

        internal KLEPGoalStructuralSolution(
            KLEPGoalStructuralSolutionDisposition disposition,
            KLEPGoalStructuralSolutionProvenance provenance,
            KLEPGoalStructuralSolutionCost cost,
            string canonicalRoute,
            IEnumerable<KLEPGoalStructuralSolutionStep> steps,
            IEnumerable<KLEPGoalStructuralRuntimeCondition> runtimeConditions,
            IEnumerable<KLEPGoalStructuralSolutionDiagnostic> diagnostics)
        {
            Disposition = disposition;
            Provenance = provenance ?? throw new ArgumentNullException(
                nameof(provenance));
            Cost = cost ?? throw new ArgumentNullException(nameof(cost));
            CanonicalRoute = canonicalRoute ?? string.Empty;
            this.steps = CopyRequired(steps, nameof(steps));
            this.runtimeConditions = CopyRequired(
                runtimeConditions, nameof(runtimeConditions));
            this.diagnostics = CopyRequired(diagnostics, nameof(diagnostics));
        }

        public KLEPGoalStructuralSolutionDisposition Disposition { get; }
        public KLEPGoalStructuralSolutionProvenance Provenance { get; }
        public KLEPGoalStructuralSolutionCost Cost { get; }
        public string CanonicalRoute { get; }
        public IReadOnlyList<KLEPGoalStructuralSolutionStep> Steps => steps;
        public IReadOnlyList<KLEPGoalStructuralRuntimeCondition>
            RuntimeConditions => runtimeConditions;
        public IReadOnlyList<KLEPGoalStructuralSolutionDiagnostic> Diagnostics =>
            diagnostics;
        public bool HasExecutableSolution =>
            Disposition == KLEPGoalStructuralSolutionDisposition.Solved ||
            Disposition == KLEPGoalStructuralSolutionDisposition.Conditional;

        private static ReadOnlyCollection<T> CopyRequired<T>(
            IEnumerable<T> source,
            string parameterName)
            where T : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<T>();
            foreach (T item in source)
            {
                copy.Add(item ?? throw new ArgumentException(
                    "Structural solution collections cannot contain null.",
                    parameterName));
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }

    /// <summary>
    /// Non-authoritative structural-solution query boundary. Implementations may
    /// compare mapped possibilities but cannot mutate the map or execute them.
    /// </summary>
    public interface IKLEPGoalStructuralSolutionObserver
    {
        string StableId { get; }
        string Version { get; }

        KLEPGoalStructuralSolution ObserveGoalStructuralSolution(
            KLEPExecutableStructuralMap structuralMap,
            KLEPGoalStructuralSolutionRequest request);
    }

    public sealed class KLEPBaselineGoalStructuralSolutionObserver :
        IKLEPGoalStructuralSolutionObserver
    {
        private KLEPBaselineGoalStructuralSolutionObserver()
        {
        }

        public static KLEPBaselineGoalStructuralSolutionObserver Instance { get; } =
            new KLEPBaselineGoalStructuralSolutionObserver();

        public string StableId => KLEPGoalStructuralSolver.StableId;
        public string Version => KLEPGoalStructuralSolver.Version;

        public KLEPGoalStructuralSolution ObserveGoalStructuralSolution(
            KLEPExecutableStructuralMap structuralMap,
            KLEPGoalStructuralSolutionRequest request)
        {
            return KLEPGoalStructuralSolver.Solve(structuralMap, request);
        }
    }

    /// <summary>
    /// Bounded-by-graph, deterministic backward structural solver. It reasons
    /// only from a valid immutable map. Current Key evidence, payloads, lifetime,
    /// probability, and execution are deliberately outside this abstraction.
    /// </summary>
    public static class KLEPGoalStructuralSolver
    {
        public const string StableId = "klep.goal-structural-solver.baseline";
        public const string Version = "1";

        public static KLEPGoalStructuralSolution Solve(
            KLEPExecutableStructuralMap structuralMap,
            KLEPGoalStructuralSolutionRequest request)
        {
            return Solve(
                structuralMap,
                request,
                StableId,
                Version);
        }

        public static KLEPGoalStructuralSolution Solve(
            KLEPExecutableStructuralMap structuralMap,
            KLEPGoalStructuralSolutionRequest request,
            string providerStableId,
            string providerVersion)
        {
            if (structuralMap == null)
            {
                throw new ArgumentNullException(nameof(structuralMap));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            providerStableId = RequireProviderIdentity(
                providerStableId, nameof(providerStableId));
            providerVersion = RequireProviderIdentity(
                providerVersion, nameof(providerVersion));
            var context = new SolverContext(
                structuralMap,
                request,
                providerStableId,
                providerVersion);
            return context.Solve();
        }

        private static string RequireProviderIdentity(
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A structural solution provider requires a non-empty stable " +
                    "ID and version.",
                    parameterName);
            }

            return value;
        }

        private sealed class SolverContext
        {
            private static readonly StringComparer IdComparer =
                StringComparer.Ordinal;

            private readonly KLEPExecutableStructuralMap map;
            private readonly KLEPGoalStructuralSolutionRequest request;
            private readonly string providerStableId;
            private readonly string providerVersion;
            private readonly List<KLEPGoalStructuralSolutionDiagnostic> diagnostics =
                new List<KLEPGoalStructuralSolutionDiagnostic>();
            private readonly HashSet<string> diagnosticIds =
                new HashSet<string>(IdComparer);
            private readonly List<KLEPKeyId> activeKeys = new List<KLEPKeyId>();

            internal SolverContext(
                KLEPExecutableStructuralMap map,
                KLEPGoalStructuralSolutionRequest request,
                string providerStableId,
                string providerVersion)
            {
                this.map = map;
                this.request = request;
                this.providerStableId = providerStableId;
                this.providerVersion = providerVersion;
            }

            internal KLEPGoalStructuralSolution Solve()
            {
                KLEPGoalStructuralSolutionProvenance provenance =
                    new KLEPGoalStructuralSolutionProvenance(
                        providerStableId,
                        providerVersion,
                        map.Snapshot.ProposedCatalogRevision,
                        map.Fingerprint,
                        request);

                if (!map.IsValid)
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .InvalidStructuralMap,
                        "catalog",
                        null,
                        request.RequestingGoalStableId,
                        "A structural solution requires a valid accepted map.");
                    return Finish(
                        KLEPGoalStructuralSolutionDisposition.Abstained,
                        provenance,
                        Branch.Failed);
                }

                if (!map.TryGetExecutable(
                        request.RequestingGoalStableId,
                        out KLEPExecutableStructuralNode requestingGoal))
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .RequestingGoalMissing,
                        "goal:" + request.RequestingGoalStableId,
                        request.TargetKeyId,
                        request.RequestingGoalStableId,
                        "The requesting structural Goal is not in the accepted map.");
                    return Finish(
                        KLEPGoalStructuralSolutionDisposition.Abstained,
                        provenance,
                        Branch.Failed);
                }

                if (!IdComparer.Equals(
                        requestingGoal.RootTenureId,
                        request.RequestingGoalTenureId))
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .RequestingGoalTenureMismatch,
                        requestingGoal.Path,
                        request.TargetKeyId,
                        requestingGoal.StableExecutableId,
                        "The request names a different registration tenure than " +
                        "the accepted structural Goal.");
                    return Finish(
                        KLEPGoalStructuralSolutionDisposition.Abstained,
                        provenance,
                        Branch.Failed);
                }

                if (!requestingGoal.IsRoot ||
                    !requestingGoal.IsGoalRecipe ||
                    !requestingGoal.IsStructuralGoal ||
                    requestingGoal.ExecutionMode != KLEPExecutionMode.Solo)
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .RequestingExecutableIsNotStructuralGoal,
                        requestingGoal.Path,
                        request.TargetKeyId,
                        requestingGoal.StableExecutableId,
                        "Only an accepted root Solo structural Goal may request " +
                        "a V1 structural solution.");
                    return Finish(
                        KLEPGoalStructuralSolutionDisposition.Abstained,
                        provenance,
                        Branch.Failed);
                }

                if (!requestingGoal.StructuralGoalTargetKeyId.HasValue ||
                    requestingGoal.StructuralGoalTargetKeyId.Value !=
                        request.TargetKeyId)
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .RequestingGoalTargetMismatch,
                        requestingGoal.Path,
                        request.TargetKeyId,
                        requestingGoal.StableExecutableId,
                        "The requested target does not match the structural " +
                        "Goal's authored target.");
                    return Finish(
                        KLEPGoalStructuralSolutionDisposition.Abstained,
                        provenance,
                        Branch.Failed);
                }

                if (requestingGoal.GoalLayers.Count != 0 ||
                    requestingGoal.GuaranteedDeclaredOutputs.Count != 0)
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .RequestingGoalShapeUnsupported,
                        requestingGoal.Path,
                        request.TargetKeyId,
                        requestingGoal.StableExecutableId,
                        "A V1 structural Goal must not also contain an authored " +
                        "recipe or guaranteed outputs.");
                    return Finish(
                        KLEPGoalStructuralSolutionDisposition.Abstained,
                        provenance,
                        Branch.Failed);
                }

                Branch branch = SolveKey(
                    request.TargetKeyId,
                    true,
                    RequirementContext.Empty);
                KLEPGoalStructuralSolutionDisposition disposition;
                if (!branch.Success)
                {
                    disposition = KLEPGoalStructuralSolutionDisposition.NoSolution;
                }
                else if (branch.Conditions.Count > 0)
                {
                    disposition = KLEPGoalStructuralSolutionDisposition.Conditional;
                }
                else
                {
                    disposition = KLEPGoalStructuralSolutionDisposition.Solved;
                }

                return Finish(disposition, provenance, branch);
            }

            private Branch SolveKey(
                KLEPKeyId keyId,
                bool isTarget,
                RequirementContext requirement)
            {
                int cycleStart = activeKeys.IndexOf(keyId);
                if (cycleStart >= 0)
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode.DependencyCycle,
                        "key:" + keyId.Value,
                        keyId,
                        requirement.ExecutableStableId,
                        "Dependency cycle: " + FormatCycle(cycleStart, keyId) + ".");
                    return Branch.Failed;
                }

                if (!map.TryGetKeyRelation(
                        keyId, out KLEPStructuralKeyRelation relation) ||
                    relation.Producers.Count == 0)
                {
                    if (isTarget)
                    {
                        AddDiagnostic(
                            KLEPGoalStructuralSolutionDiagnosticCode
                                .MissingTargetProducer,
                            "key:" + keyId.Value,
                            keyId,
                            request.RequestingGoalStableId,
                            "No mapped Executable guarantees the target Key '" +
                            keyId.Value + "'.");
                        return Branch.Failed;
                    }

                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .MissingPrerequisiteProducer,
                        requirement.Path,
                        keyId,
                        requirement.ExecutableStableId,
                        "No mapped Executable guarantees prerequisite Key '" +
                        keyId.Value + "'; it remains an external runtime condition.");
                    return Branch.WithCondition(
                        new KLEPGoalStructuralRuntimeCondition(
                            KLEPGoalStructuralRuntimeConditionKind
                                .ExternalKeyRequired,
                            keyId,
                            requirement.ExecutableStableId,
                            requirement.RootTenureId,
                            requirement.LockStableId,
                            requirement.ExpressionPath,
                            requirement.Expression));
                }

                activeKeys.Add(keyId);
                try
                {
                    Branch best = Branch.Failed;
                    foreach (KLEPStructuralKeyProducerRelation producer in
                             relation.Producers)
                    {
                        KLEPExecutableStructuralNode executable = producer.Producer;
                        if (!CanUseProducer(executable, keyId))
                        {
                            continue;
                        }

                        Branch candidate = SolveProducer(executable, keyId);
                        if (candidate.Success &&
                            (!best.Success || Compare(candidate, best) < 0))
                        {
                            best = candidate;
                        }
                    }

                    if (!best.Success)
                    {
                        AddDiagnostic(
                            KLEPGoalStructuralSolutionDiagnosticCode
                                .NoUsableProducer,
                            "key:" + keyId.Value,
                            keyId,
                            requirement.ExecutableStableId,
                            "Mapped producers exist for Key '" + keyId.Value +
                            "', but none form an allowed acyclic root-Solo V1 route.");
                    }

                    return best;
                }
                finally
                {
                    activeKeys.RemoveAt(activeKeys.Count - 1);
                }
            }

            private bool CanUseProducer(
                KLEPExecutableStructuralNode executable,
                KLEPKeyId requiredKey)
            {
                if (IdComparer.Equals(
                        executable.StableExecutableId,
                        request.RequestingGoalStableId) &&
                    IdComparer.Equals(
                        executable.RootTenureId,
                        request.RequestingGoalTenureId))
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .RequestingGoalProducerExcluded,
                        executable.Path,
                        requiredKey,
                        executable.StableExecutableId,
                        "A structural Goal cannot prove its target by selecting itself.");
                    return false;
                }

                if (!executable.IsRoot)
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .GoalOwnedProducerExcluded,
                        executable.Path,
                        requiredKey,
                        executable.StableExecutableId,
                        "A Goal-owned producer is not an independently schedulable " +
                        "V1 route step.");
                    return false;
                }

                if (executable.ExecutionMode != KLEPExecutionMode.Solo)
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .TandemProducerExcluded,
                        executable.Path,
                        requiredKey,
                        executable.StableExecutableId,
                        "A Tandem producer is automatic support, not a selectable " +
                        "root-Solo V1 route step.");
                    return false;
                }

                if (executable.IsGoalRecipe ||
                    executable.IsStructuralGoal ||
                    executable.Kind == KLEPExecutableKind.Goal)
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .GoalProducerExcluded,
                        executable.Path,
                        requiredKey,
                        executable.StableExecutableId,
                        "Goal producers remain authored composites and are not " +
                        "flattened into executable V1 route steps.");
                    return false;
                }

                return true;
            }

            private Branch SolveProducer(
                KLEPExecutableStructuralNode executable,
                KLEPKeyId requiredOutput)
            {
                Branch combined = Branch.Empty;
                foreach (KLEPStructuralLockSnapshot sourceLock in executable.Locks)
                {
                    RequirementContext requirement = new RequirementContext(
                        executable.StableExecutableId,
                        executable.RootTenureId,
                        sourceLock.StableId,
                        sourceLock.Expression.ExpressionPath,
                        executable.Path + "/lock:" + sourceLock.StableId + "/" +
                            sourceLock.Expression.ExpressionPath,
                        sourceLock.Expression);
                    Branch lockBranch = SolveExpression(
                        sourceLock.Expression,
                        requirement);
                    if (!lockBranch.Success)
                    {
                        return Branch.Failed;
                    }

                    combined = Branch.Combine(combined, lockBranch);
                }

                return combined.WithStep(executable, requiredOutput);
            }

            private Branch SolveExpression(
                KLEPStructuralLockExpressionSnapshot expression,
                RequirementContext requirement)
            {
                if (expression == null)
                {
                    AddDiagnostic(
                        KLEPGoalStructuralSolutionDiagnosticCode
                            .UnsupportedLockExpression,
                        requirement.Path,
                        null,
                        requirement.ExecutableStableId,
                        "A mapped Lock has no structural expression.");
                    return Branch.Failed;
                }

                RequirementContext current = requirement.WithExpression(expression);
                switch (expression.Kind)
                {
                    case KLEPLockExpressionKind.KeyPresent:
                        if (!expression.KeyId.HasValue ||
                            string.IsNullOrWhiteSpace(expression.KeyId.Value.Value))
                        {
                            AddDiagnostic(
                                KLEPGoalStructuralSolutionDiagnosticCode
                                    .UnsupportedLockExpression,
                                current.Path,
                                null,
                                current.ExecutableStableId,
                                "A KeyPresent expression has no stable Key ID.");
                            return Branch.Failed;
                        }

                        return SolveKey(expression.KeyId.Value, false, current);

                    case KLEPLockExpressionKind.All:
                    {
                        Branch all = Branch.Empty;
                        foreach (KLEPStructuralLockExpressionSnapshot child in
                                 expression.Children)
                        {
                            Branch childBranch = SolveExpression(child, current);
                            if (!childBranch.Success)
                            {
                                return Branch.Failed;
                            }

                            all = Branch.Combine(all, childBranch);
                        }

                        return all;
                    }

                    case KLEPLockExpressionKind.Any:
                    {
                        Branch best = Branch.Failed;
                        foreach (KLEPStructuralLockExpressionSnapshot child in
                                 expression.Children)
                        {
                            Branch childBranch = SolveExpression(child, current);
                            if (childBranch.Success &&
                                (!best.Success || Compare(childBranch, best) < 0))
                            {
                                best = childBranch;
                            }
                        }

                        if (!best.Success)
                        {
                            AddDiagnostic(
                                KLEPGoalStructuralSolutionDiagnosticCode
                                    .UnsatisfiedAnyExpression,
                                current.Path,
                                null,
                                current.ExecutableStableId,
                                "No authored Any branch has a usable V1 structural " +
                                "route.");
                        }

                        return best;
                    }

                    case KLEPLockExpressionKind.Not:
                    {
                        KLEPKeyId? simpleKey = null;
                        if (expression.Children.Count == 1 &&
                            expression.Children[0].Kind ==
                                KLEPLockExpressionKind.KeyPresent &&
                            expression.Children[0].KeyId.HasValue)
                        {
                            simpleKey = expression.Children[0].KeyId;
                        }

                        return Branch.WithCondition(
                            new KLEPGoalStructuralRuntimeCondition(
                                KLEPGoalStructuralRuntimeConditionKind
                                    .NegativeExpressionMustRemainFalse,
                                simpleKey,
                                current.ExecutableStableId,
                                current.RootTenureId,
                                current.LockStableId,
                                current.ExpressionPath,
                                expression));
                    }

                    default:
                        AddDiagnostic(
                            KLEPGoalStructuralSolutionDiagnosticCode
                                .UnsupportedLockExpression,
                            current.Path,
                            expression.KeyId,
                            current.ExecutableStableId,
                            "Lock expression kind '" + expression.Kind +
                            "' is not supported by Goal Structural Solution V1.");
                        return Branch.Failed;
                }
            }

            private KLEPGoalStructuralSolution Finish(
                KLEPGoalStructuralSolutionDisposition disposition,
                KLEPGoalStructuralSolutionProvenance provenance,
                Branch branch)
            {
                diagnostics.Sort(CompareDiagnostics);
                var steps = new List<KLEPGoalStructuralSolutionStep>();
                if (branch.Success)
                {
                    for (int index = 0; index < branch.Steps.Count; index++)
                    {
                        StepSeed seed = branch.Steps[index];
                        steps.Add(new KLEPGoalStructuralSolutionStep(
                            index,
                            seed.Executable,
                            seed.RequiredOutputKeyIds));
                    }
                }

                int externalCount = 0;
                int negativeCount = 0;
                if (branch.Success)
                {
                    foreach (KLEPGoalStructuralRuntimeCondition condition in
                             branch.Conditions)
                    {
                        if (condition.Kind ==
                            KLEPGoalStructuralRuntimeConditionKind
                                .ExternalKeyRequired)
                        {
                            externalCount++;
                        }
                        else
                        {
                            negativeCount++;
                        }
                    }
                }

                var cost = new KLEPGoalStructuralSolutionCost(
                    steps.Count,
                    branch.Success ? branch.Depth : 0,
                    externalCount,
                    negativeCount);
                return new KLEPGoalStructuralSolution(
                    disposition,
                    provenance,
                    cost,
                    branch.Success ? branch.CanonicalRoute : string.Empty,
                    steps,
                    branch.Success
                        ? branch.Conditions
                        : new List<KLEPGoalStructuralRuntimeCondition>(),
                    diagnostics);
            }

            private void AddDiagnostic(
                KLEPGoalStructuralSolutionDiagnosticCode code,
                string path,
                KLEPKeyId? keyId,
                string executableStableId,
                string message)
            {
                string identity = ((int)code).ToString(
                        CultureInfo.InvariantCulture) + "|" +
                    (path ?? string.Empty) + "|" +
                    (keyId.HasValue ? keyId.Value.Value : string.Empty) + "|" +
                    (executableStableId ?? string.Empty) + "|" +
                    (message ?? string.Empty);
                if (diagnosticIds.Add(identity))
                {
                    diagnostics.Add(new KLEPGoalStructuralSolutionDiagnostic(
                        code,
                        path,
                        keyId,
                        executableStableId,
                        message));
                }
            }

            private string FormatCycle(int start, KLEPKeyId repeated)
            {
                var parts = new List<string>();
                for (int index = start; index < activeKeys.Count; index++)
                {
                    parts.Add(activeKeys[index].Value);
                }

                parts.Add(repeated.Value);
                return string.Join(" -> ", parts);
            }

            private static int Compare(Branch left, Branch right)
            {
                int comparison = left.Steps.Count.CompareTo(right.Steps.Count);
                return comparison != 0
                    ? comparison
                    : IdComparer.Compare(
                        left.CanonicalRoute,
                        right.CanonicalRoute);
            }

            private static int CompareDiagnostics(
                KLEPGoalStructuralSolutionDiagnostic left,
                KLEPGoalStructuralSolutionDiagnostic right)
            {
                int comparison = ((int)left.Code).CompareTo((int)right.Code);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = IdComparer.Compare(left.Path, right.Path);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = IdComparer.Compare(
                    left.KeyId.HasValue ? left.KeyId.Value.Value : string.Empty,
                    right.KeyId.HasValue ? right.KeyId.Value.Value : string.Empty);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = IdComparer.Compare(
                    left.ExecutableStableId,
                    right.ExecutableStableId);
                return comparison != 0
                    ? comparison
                    : IdComparer.Compare(left.Message, right.Message);
            }
        }

        private sealed class RequirementContext
        {
            internal static RequirementContext Empty { get; } =
                new RequirementContext(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    null);

            internal RequirementContext(
                string executableStableId,
                string rootTenureId,
                string lockStableId,
                string expressionPath,
                string path,
                KLEPStructuralLockExpressionSnapshot expression)
            {
                ExecutableStableId = executableStableId ?? string.Empty;
                RootTenureId = rootTenureId ?? string.Empty;
                LockStableId = lockStableId ?? string.Empty;
                ExpressionPath = expressionPath ?? string.Empty;
                Path = path ?? string.Empty;
                Expression = expression;
            }

            internal string ExecutableStableId { get; }
            internal string RootTenureId { get; }
            internal string LockStableId { get; }
            internal string ExpressionPath { get; }
            internal string Path { get; }
            internal KLEPStructuralLockExpressionSnapshot Expression { get; }

            internal RequirementContext WithExpression(
                KLEPStructuralLockExpressionSnapshot expression)
            {
                string expressionPath = expression?.ExpressionPath ?? string.Empty;
                return new RequirementContext(
                    ExecutableStableId,
                    RootTenureId,
                    LockStableId,
                    expressionPath,
                    PathForExpression(Path, expressionPath),
                    expression);
            }

            private static string PathForExpression(
                string currentPath,
                string expressionPath)
            {
                if (string.IsNullOrEmpty(expressionPath) ||
                    currentPath.EndsWith(
                        "/" + expressionPath,
                        StringComparison.Ordinal))
                {
                    return currentPath;
                }

                return currentPath + "/" + expressionPath;
            }
        }

        private sealed class StepSeed
        {
            private readonly List<KLEPKeyId> requiredOutputKeyIds;

            internal StepSeed(
                KLEPExecutableStructuralNode executable,
                IEnumerable<KLEPKeyId> requiredOutputKeyIds)
            {
                Executable = executable;
                this.requiredOutputKeyIds = new List<KLEPKeyId>();
                MergeKeys(requiredOutputKeyIds);
            }

            internal KLEPExecutableStructuralNode Executable { get; }
            internal IReadOnlyList<KLEPKeyId> RequiredOutputKeyIds =>
                requiredOutputKeyIds;
            internal string Identity => Token(Executable.StableExecutableId) +
                "@" + Token(Executable.RootTenureId);

            internal StepSeed Copy()
            {
                return new StepSeed(Executable, requiredOutputKeyIds);
            }

            internal void MergeKeys(IEnumerable<KLEPKeyId> source)
            {
                if (source == null)
                {
                    return;
                }

                foreach (KLEPKeyId keyId in source)
                {
                    bool present = false;
                    foreach (KLEPKeyId existing in requiredOutputKeyIds)
                    {
                        if (existing == keyId)
                        {
                            present = true;
                            break;
                        }
                    }

                    if (!present)
                    {
                        requiredOutputKeyIds.Add(keyId);
                    }
                }

                requiredOutputKeyIds.Sort((left, right) =>
                    StringComparer.Ordinal.Compare(left.Value, right.Value));
            }

            private static string Token(string value)
            {
                value = value ?? string.Empty;
                return value.Length.ToString(CultureInfo.InvariantCulture) +
                    ":" + value;
            }
        }

        private sealed class Branch
        {
            private Branch(
                bool success,
                List<StepSeed> steps,
                List<KLEPGoalStructuralRuntimeCondition> conditions,
                int depth)
            {
                Success = success;
                Steps = steps;
                Conditions = conditions;
                Depth = depth;
            }

            internal static Branch Empty => new Branch(
                true,
                new List<StepSeed>(),
                new List<KLEPGoalStructuralRuntimeCondition>(),
                0);

            internal static Branch Failed => new Branch(
                false,
                new List<StepSeed>(),
                new List<KLEPGoalStructuralRuntimeCondition>(),
                0);

            internal bool Success { get; }
            internal List<StepSeed> Steps { get; }
            internal List<KLEPGoalStructuralRuntimeCondition> Conditions { get; }
            internal int Depth { get; }

            internal string CanonicalRoute
            {
                get
                {
                    var parts = new List<string>();
                    foreach (StepSeed step in Steps)
                    {
                        parts.Add("step:" + step.Identity);
                    }

                    var conditionParts = new List<string>();
                    foreach (KLEPGoalStructuralRuntimeCondition condition in
                             Conditions)
                    {
                        conditionParts.Add(ConditionIdentity(condition));
                    }

                    conditionParts.Sort(StringComparer.Ordinal);
                    for (int index = 0; index < conditionParts.Count; index++)
                    {
                        parts.Add("condition:" + conditionParts[index]);
                    }

                    return string.Join(" -> ", parts);
                }
            }

            internal static Branch WithCondition(
                KLEPGoalStructuralRuntimeCondition condition)
            {
                return new Branch(
                    true,
                    new List<StepSeed>(),
                    new List<KLEPGoalStructuralRuntimeCondition> { condition },
                    0);
            }

            internal Branch WithStep(
                KLEPExecutableStructuralNode executable,
                KLEPKeyId requiredOutput)
            {
                Branch copy = Combine(Empty, this);
                StepSeed existing = FindStep(copy.Steps, executable);
                if (existing == null)
                {
                    copy.Steps.Add(new StepSeed(
                        executable,
                        new[] { requiredOutput }));
                }
                else
                {
                    existing.MergeKeys(new[] { requiredOutput });
                }

                return new Branch(
                    true,
                    copy.Steps,
                    copy.Conditions,
                    Math.Max(copy.Depth + 1, 1));
            }

            internal static Branch Combine(Branch left, Branch right)
            {
                if (!left.Success || !right.Success)
                {
                    return Failed;
                }

                var steps = new List<StepSeed>();
                AppendSteps(steps, left.Steps);
                AppendSteps(steps, right.Steps);
                var conditions = new List<KLEPGoalStructuralRuntimeCondition>();
                AppendConditions(conditions, left.Conditions);
                AppendConditions(conditions, right.Conditions);
                return new Branch(
                    true,
                    steps,
                    conditions,
                    Math.Max(left.Depth, right.Depth));
            }

            private static void AppendSteps(
                List<StepSeed> destination,
                IEnumerable<StepSeed> source)
            {
                foreach (StepSeed incoming in source)
                {
                    StepSeed existing = FindStep(
                        destination, incoming.Executable);
                    if (existing == null)
                    {
                        destination.Add(incoming.Copy());
                    }
                    else
                    {
                        existing.MergeKeys(incoming.RequiredOutputKeyIds);
                    }
                }
            }

            private static StepSeed FindStep(
                List<StepSeed> steps,
                KLEPExecutableStructuralNode executable)
            {
                foreach (StepSeed step in steps)
                {
                    if (StringComparer.Ordinal.Equals(
                            step.Executable.StableExecutableId,
                            executable.StableExecutableId) &&
                        StringComparer.Ordinal.Equals(
                            step.Executable.RootTenureId,
                            executable.RootTenureId))
                    {
                        return step;
                    }
                }

                return null;
            }

            private static void AppendConditions(
                List<KLEPGoalStructuralRuntimeCondition> destination,
                IEnumerable<KLEPGoalStructuralRuntimeCondition> source)
            {
                foreach (KLEPGoalStructuralRuntimeCondition incoming in source)
                {
                    bool present = false;
                    foreach (KLEPGoalStructuralRuntimeCondition existing in
                             destination)
                    {
                        if (SameCondition(existing, incoming))
                        {
                            present = true;
                            break;
                        }
                    }

                    if (!present)
                    {
                        destination.Add(incoming);
                    }
                }
            }

            private static bool SameCondition(
                KLEPGoalStructuralRuntimeCondition left,
                KLEPGoalStructuralRuntimeCondition right)
            {
                return left.Kind == right.Kind &&
                    left.KeyId == right.KeyId &&
                    StringComparer.Ordinal.Equals(
                        left.RequiredByExecutableStableId,
                        right.RequiredByExecutableStableId) &&
                    StringComparer.Ordinal.Equals(
                        left.RequiredByRootTenureId,
                        right.RequiredByRootTenureId) &&
                    StringComparer.Ordinal.Equals(
                        left.LockStableId,
                        right.LockStableId) &&
                    StringComparer.Ordinal.Equals(
                        left.ExpressionPath,
                        right.ExpressionPath);
            }

            private static string ConditionIdentity(
                KLEPGoalStructuralRuntimeCondition condition)
            {
                return ((int)condition.Kind).ToString(
                           CultureInfo.InvariantCulture) + "|" +
                    Token(condition.KeyId.HasValue
                        ? condition.KeyId.Value.Value
                        : string.Empty) + "|" +
                    Token(condition.RequiredByExecutableStableId) + "|" +
                    Token(condition.RequiredByRootTenureId) + "|" +
                    Token(condition.LockStableId) + "|" +
                    Token(condition.ExpressionPath);
            }

            private static string Token(string value)
            {
                value = value ?? string.Empty;
                return value.Length.ToString(CultureInfo.InvariantCulture) +
                    ":" + value;
            }
        }
    }
}
