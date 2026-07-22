using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Imagination;
using Roll4d4.Klep.ZombieTest;

internal static class Program
{
    private const string ActiveRequest =
        "request.cover-route.fixture.headless.v1";
    private static int assertions;

    private static readonly KLEPKeyDefinition Ground = Key(
        KLEPCoverRouteLearningSession.GroundKeyId,
        "Ground");
    private static readonly KLEPKeyDefinition RouteProblem = Key(
        KLEPCoverRouteLearningSession.RouteProblemKeyId,
        "Route Problem");
    private static readonly KLEPKeyDefinition EdgeDanger = Key(
        KLEPCoverRouteLearningSession.EdgeDangerKeyId,
        "Edge Danger");
    private static readonly KLEPKeyDefinition RouteCompleted = Key(
        KLEPCoverRouteLearningSession.RouteCompletedKeyId,
        "Route Completed");

    private static void Main()
    {
        var session = new KLEPCoverRouteLearningSession(
            Ground,
            RouteProblem,
            EdgeDanger,
            RouteCompleted);
        VerifyCheckedInFixture(session);
        VerifyValidButUnfitManifestRejects(session);
        VerifyProblemAndHostHelpers(session);
        VerifyPreSandboxProjectBinding(session);
        VerifyFixedSandboxPolicy();

        Console.WriteLine(
            "KLEP Cover/Route Learning smoke passed: " +
            assertions.ToString(CultureInfo.InvariantCulture) +
            " assertions.");
    }

    private static void VerifyCheckedInFixture(
        KLEPCoverRouteLearningSession session)
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "CoverRouteStrongManifest.json");
        string authoredJson = File.ReadAllText(fixturePath);
        Expect(authoredJson.Contains(
                "__ACTIVE_REQUEST_FINGERPRINT__",
                StringComparison.Ordinal),
            "The shipped fixture retains only the active-request placeholder");
        string boundJson = authoredJson.Replace(
            "__ACTIVE_REQUEST_FINGERPRINT__",
            ActiveRequest,
            StringComparison.Ordinal);

        KLEPImaginationManifest manifest = session.CompileStrong(boundJson);
        Expect(session.CreatedRuntimeCount == 0,
            "Strict compilation does not materialize a sandbox runtime");
        Expect(manifest.RequestFingerprint == ActiveRequest,
            "The host-bound request fingerprint survives compilation exactly");
        Expect(manifest.TargetKeyId == RouteCompleted.Id.Value,
            "The fixture targets the exact project RouteCompleted Key");
        Expect(manifest.CapabilityId ==
               KLEPCoverRouteLearningSession.CapabilityId &&
               manifest.CapabilityVersion ==
               KLEPCoverRouteLearningSession.CapabilityVersion,
            "The fixture binds the exact trusted route capability/version");

        IReadOnlyList<KLEPCoverRoutePoint> waypoints =
            KLEPCoverRouteLearningSession.GetManifestWaypoints(manifest);
        Expect(waypoints.Count == 3 &&
               waypoints[0].Equals(new KLEPCoverRoutePoint(0d, -3.2d)) &&
               waypoints[1].Equals(new KLEPCoverRoutePoint(4.8d, -3d)) &&
               waypoints[2].Equals(new KLEPCoverRoutePoint(6.2d, -1.5d)),
            "The checked-in Manifest exposes its exact three proposal waypoints");

        KLEPCoverRouteLearningResult result =
            session.EvaluateCompiledManifest(manifest);
        Console.WriteLine(
            $"Fixture verdict={result.Decision.Disposition} " +
            $"support={result.SandboxSupport} successes={result.Successes} " +
            $"mean={result.MeanFitness:0.000} " +
            $"clearance={result.MinimumClearance:0.000} " +
            $"reason={result.VerdictReason}");
        foreach (KLEPCoverRouteTrial diagnostic in result.Trials)
        {
            Console.WriteLine(
                $"  {diagnostic.Scenario.StableId}: " +
                $"outcome={diagnostic.Outcome} " +
                $"state={diagnostic.TerminalState}/" +
                $"{diagnostic.TerminalReason} " +
                $"moves={diagnostic.MovementTicks} " +
                $"collisions={diagnostic.CollisionCount} " +
                $"stalls={diagnostic.StallCount} " +
                $"fitness={diagnostic.Fitness:0.000} " +
                $"clearance={diagnostic.MinimumSurfaceClearance:0.000} " +
                diagnostic.Explanation);
        }
        Expect(result.IsAccepted && result.Decision.IsAccepted,
            "The exact checked-in Strong Manifest is admitted");
        Expect(result.TrialCount == 4 && result.SandboxSupport == 4 &&
               result.Successes == 4,
            "The bounded ledger retains exactly four unique successful trials");
        Expect(Approximately(result.SandboxConfidence, 0.5d),
            "Four retained trials produce sandbox confidence 0.5");
        Expect(result.MeanFitness >= 0.65d &&
               result.MeanFitness <= 1d &&
               result.SampleVariance >= 0d,
            "The ledger publishes finite accepted mean/variance fitness");
        Expect(result.MinimumClearance >=
               KLEPCoverRouteLearningSession.AuthoredClearance * 0.9d,
            "All routes retain at least the admitted body-surface clearance");
        Expect(result.SandboxMaterializationCount == 4 &&
               session.CreatedRuntimeCount == 4,
            "Every deterministic trial receives one fresh materialization");

        var runtimeIds = new HashSet<long>();
        var trialIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (KLEPCoverRouteTrial trial in result.Trials)
        {
            Expect(trialIds.Add(trial.TrialId),
                "Each retained trial has a unique exact identity");
            Expect(runtimeIds.Add(trial.RuntimeInstanceSequence),
                "Each trial owns a distinct capability runtime instance");
            Expect(trial.Succeeded && trial.CollisionFree &&
                   trial.CollisionCount == 0 && trial.StallCount == 0,
                "Each sandbox route succeeds without collision or stall");
            Expect(trial.TerminalState == KLEPExecutableState.Succeeded &&
                   trial.TerminalReason == KLEPExecutableExitReason.Succeeded,
                "Each trial retains the exact successful lifecycle result");
            Expect(trial.ExactDeclaredOutputObserved &&
                   trial.ObservedOutputKeyId == RouteCompleted.Id.Value,
                "Each success emits exactly the descriptor-owned output");
            Expect(trial.Intents.Count > 0 &&
                   trial.Intents.Count == trial.MovementTicks,
                "Every movement Tick retains one exact run/cycle-bound intent");
            Expect(trial.ActualRoute[trial.ActualRoute.Count - 1]
                       .DistanceTo(trial.Scenario.Target) <= 0.8d,
                "Success is grounded in factual arrival at the exact target");
            Expect(trial.Fitness >= 0.65d && trial.Fitness <= 1d,
                "Every accepted trial owns finite project fitness");
        }

        KLEPImaginedExecutable live = session.MaterializeAccepted(result);
        Expect(live != null && session.CreatedRuntimeCount == 5,
            "Acceptance creates a fifth fresh never-sandboxed materialization");
        Expect(result.SandboxSupport == 4 &&
               Approximately(result.SandboxConfidence, 0.5d),
            "Live materialization cannot rewrite sandbox support/confidence");
    }

    private static void VerifyProblemAndHostHelpers(
        KLEPCoverRouteLearningSession session)
    {
        var problem = new KLEPCoverRouteProblem(
            "problem.test",
            "target.test",
            42,
            new KLEPCoverRoutePoint(-1d, 0d),
            new KLEPCoverRoutePoint(8d, -0.5d));
        Expect(KLEPCoverRouteProblem.TryFromPayload(
                problem.ToPayload(),
                out KLEPCoverRouteProblem copy) &&
               copy.ProblemId == problem.ProblemId &&
               copy.TargetId == problem.TargetId &&
               copy.ObservedWorldTick == 42 &&
               copy.Position.Equals(problem.Position) &&
               copy.Target.Equals(problem.Target),
            "The exact RouteProblem payload round-trips without hidden geometry");

        KLEPCoverRouteProblemSensorExecutable sensor =
            session.CreateProblemSensor("sensor.test.route-problem");
        sensor.SetCurrentProblem(problem);
        Expect(ReferenceEquals(sensor.CurrentProblem, problem) &&
               sensor.DeclaredOutputs.Count == 1 &&
               sensor.DeclaredOutputs[0].Id == RouteProblem.Id,
            "The host-fed Sensor exposes only the exact RouteProblem output");
        sensor.ClearCurrentProblem();
        Expect(sensor.CurrentProblem == null,
            "The host can explicitly withdraw the current route observation");

        KLEPCoverRouteFitness success =
            KLEPCoverRouteLearningSession.CalculateFitness(
                true,
                routeLength: 12d,
                directDistance: 9d,
                stallCount: 0);
        KLEPCoverRouteFitness failure =
            KLEPCoverRouteLearningSession.CalculateFitness(
                false,
                routeLength: 12d,
                directDistance: 9d,
                stallCount: 0);
        Expect(success.Value > 0.65d && failure.Value == 0d &&
               success.StallScore == 1d,
            "Sandbox and live hosts share one deterministic fitness function");
    }

    private static void VerifyValidButUnfitManifestRejects(
        KLEPCoverRouteLearningSession session)
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "CoverRouteStrongManifest.json");
        string blockedJson = File.ReadAllText(fixturePath)
            .Replace(
                "__ACTIVE_REQUEST_FINGERPRINT__",
                ActiveRequest + ".blocked",
                StringComparison.Ordinal)
            .Replace(
                "\"waypoint1X\": 0.0",
                "\"waypoint1X\": 2.5",
                StringComparison.Ordinal)
            .Replace(
                "\"waypoint1Z\": -3.2",
                "\"waypoint1Z\": 0.0",
                StringComparison.Ordinal);
        KLEPImaginationManifest manifest = session.CompileStrong(blockedJson);
        long before = session.CreatedRuntimeCount;
        KLEPCoverRouteLearningResult rejected =
            session.EvaluateCompiledManifest(manifest);
        Expect(!rejected.IsAccepted &&
               rejected.Decision.Disposition ==
                   KLEPCoverRouteAdmissionDisposition.RejectedTrialFailure,
            "A schema-valid route through cover is explicitly rejected as unfit");
        Expect(rejected.SandboxSupport == 4 &&
               Approximately(rejected.SandboxConfidence, 0.5d) &&
               session.CreatedRuntimeCount == before + 4,
            "Rejected quality evidence still retains the bounded four-trial ledger");
        Exception promotion = Catch(() =>
            session.MaterializeAccepted(rejected));
        Expect(promotion is InvalidOperationException &&
               session.CreatedRuntimeCount == before + 4,
            "Rejection creates no fresh live materialization");
    }

    private static void VerifyPreSandboxProjectBinding(
        KLEPCoverRouteLearningSession session)
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "CoverRouteStrongManifest.json");
        string wrongRequestJson = File.ReadAllText(fixturePath).Replace(
            "__ACTIVE_REQUEST_FINGERPRINT__",
            "request.not-active",
            StringComparison.Ordinal);
        long before = session.CreatedRuntimeCount;
        KLEPImaginationManifest wrongRequest =
            session.CompileStrong(wrongRequestJson);
        Expect(wrongRequest.RequestFingerprint != ActiveRequest &&
               session.CreatedRuntimeCount == before,
            "A host can reject a request mismatch before any sandbox runtime exists");

        string wrongTargetJson = wrongRequestJson.Replace(
            RouteCompleted.Id.Value,
            Ground.Id.Value,
            StringComparison.Ordinal);
        KLEPImaginationManifest wrongTarget =
            session.CompileStrong(wrongTargetJson);
        Exception rejection = Catch(() =>
            session.EvaluateCompiledManifest(wrongTarget));
        Expect(rejection is KLEPImaginationRejectedException &&
               session.CreatedRuntimeCount == before,
            "A target mismatch rejects before any sandbox materialization");
    }

    private static void VerifyFixedSandboxPolicy()
    {
        Exception confidenceDrift = Catch(() =>
            new KLEPCoverRouteTrialLedger(
                "proposal.test",
                KLEPCoverRouteLearningSession.SandboxVersion,
                "catalog.test",
                sandboxConfidenceScale: 1d));
        Expect(confidenceDrift is ArgumentOutOfRangeException,
            "V1 cannot relabel another confidence formula as sandbox confidence");

        IReadOnlyList<KLEPCoverRouteScenario> defaults =
            KLEPCoverRouteLearningSession.CreateCentralWallScenarios();
        var drifted = new List<KLEPCoverRouteScenario>(defaults);
        KLEPCoverRouteScenario first = defaults[0];
        drifted[0] = new KLEPCoverRouteScenario(
            first.StableId,
            first.Start,
            first.Target,
            first.Obstacle,
            first.ArenaMinimumX,
            first.ArenaMaximumX,
            first.ArenaMinimumZ,
            first.ArenaMaximumZ,
            agentRadius: 0.25d,
            movementPerTick: first.MovementPerTick);
        Exception physicsDrift = Catch(() =>
            new KLEPCoverRouteLearningSession(
                Ground,
                RouteProblem,
                EdgeDanger,
                RouteCompleted,
                drifted));
        Expect(physicsDrift is ArgumentException,
            "Injected scenarios cannot weaken descriptor-owned collision physics");

        var easySuite = new List<KLEPCoverRouteScenario>(defaults);
        easySuite[0] = new KLEPCoverRouteScenario(
            "central.easy",
            first.Start,
            first.Target,
            first.Obstacle,
            first.ArenaMinimumX,
            first.ArenaMaximumX,
            first.ArenaMinimumZ,
            first.ArenaMaximumZ,
            first.AgentRadius,
            first.MovementPerTick);
        Exception identityDrift = Catch(() =>
            new KLEPCoverRouteLearningSession(
                Ground,
                RouteProblem,
                EdgeDanger,
                RouteCompleted,
                easySuite));
        Expect(identityDrift is ArgumentException,
            "Sandbox-v1 evidence requires the exact four authored scenarios");

        var obstacle = new KLEPCoverRouteObstacle(
            "cover.central",
            2.2d,
            2.8d,
            -2d,
            2d);
        var from = new KLEPCoverRoutePoint(1.3d, -2.8d);
        var to = new KLEPCoverRoutePoint(3.7d, -2.8d);
        double endpointMinimum = Math.Min(
            KLEPCoverRouteGeometry.SurfaceClearance(from, obstacle, 0.5d),
            KLEPCoverRouteGeometry.SurfaceClearance(to, obstacle, 0.5d));
        double sweptMinimum =
            KLEPCoverRouteGeometry.SegmentSurfaceClearance(
                from,
                to,
                obstacle,
                0.5d);
        Expect(endpointMinimum >
               KLEPCoverRouteLearningSession.AuthoredClearance * 0.9d &&
               sweptMinimum <
               KLEPCoverRouteLearningSession.AuthoredClearance * 0.9d,
            "Swept clearance catches a below-threshold near-wall segment " +
            "that endpoint sampling would miss");
    }

    private static KLEPKeyDefinition Key(string id, string displayName)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(id),
            displayName,
            scope: KLEPKeyScope.Local,
            defaultLifetime: KLEPKeyLifetime.OneCycle);
    }

    private static Exception Catch(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static bool Approximately(double left, double right)
    {
        return Math.Abs(left - right) <= 0.000000001d;
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException("FAILED: " + message);
        }

        assertions++;
    }
}
