using System;
using System.Collections.Generic;
using Roll4d4.Klep.Behaviors;
using Roll4d4.Klep.Core;

internal static class Program
{
    private static int assertions;

    private static int Main()
    {
        PayloadRequiresExplicitWorldTick();
        DirectObservationReplacesOnlyItsOwnFact();
        SelectorUsesPayloadFreshnessNotStoreClocks();
        FrozenChainCopiesBeforeRelayCleanup();
        SelectorUsesTheAcceptedTotalTieOrder();
        SelectorExpiresStaleKnowledge();
        MalformedKnowledgeFaults();
        InvestigationGoalConsumesSelectedKnowledge();

        Console.WriteLine(
            $"KLEP HumanSighting smoke tests passed ({assertions} assertions).");
        return 0;
    }

    private static void PayloadRequiresExplicitWorldTick()
    {
        KLEPHumanSighting expected = Sighting(
            17,
            "neuron.scout",
            "human.001",
            2d,
            3d,
            4d);
        KLEPHumanSighting actual =
            KLEPHumanSighting.Read(expected.ToPayload());
        Expect(actual.ObservedWorldTick == 17,
            "observedWorldTick round-trips as Int64");
        Expect(actual.ObserverNeuronId == "neuron.scout" &&
               actual.EntityId == "human.001" &&
               actual.TeamId == "team.human",
            "stable observer, entity, and team identities round-trip");
        Expect(actual.PositionX == 2d &&
               actual.PositionY == 3d &&
               actual.PositionZ == 4d,
            "finite position round-trips exactly");

        var numericTick = new KLEPKeyPayload(new[]
        {
            Pair(KLEPHumanSighting.ObservedWorldTickField, 17d),
            Pair(KLEPHumanSighting.ObserverNeuronIdField, "neuron.scout"),
            Pair(KLEPHumanSighting.EntityIdField, "human.001"),
            Pair(KLEPHumanSighting.TeamIdField, "team.human"),
            Pair(KLEPHumanSighting.PositionXField, 0d),
            Pair(KLEPHumanSighting.PositionYField, 0d),
            Pair(KLEPHumanSighting.PositionZField, 0d)
        });
        Expect(!KLEPHumanSighting.TryRead(numericTick, out _),
            "floating-point numbers cannot impersonate the Int64 world clock");
    }

    private static void DirectObservationReplacesOnlyItsOwnFact()
    {
        KLEPKeyDefinition human = PersistentKey(
            "key.human-sighting",
            "Human Sighting");
        KLEPKeyDefinition best = OneCycleKey(
            "key.best-human-sighting",
            "Best Human Sighting");
        var sensor = new KLEPHumanSightingSensorExecutable(
            TandemDefinition(
                "sensor.human-sighting",
                "Human Sighting Sensor",
                KLEPExecutableKind.Sensor,
                null,
                human),
            human,
            "neuron.receiver");
        var router = Router(
            "neuron.receiver",
            human,
            best,
            maximumAge: 100);
        var neuron = new KLEPNeuron("neuron.receiver");
        neuron.InitializeKey(
            human,
            Sighting(3, "neuron.receiver", "human.old", 1d).ToPayload());
        neuron.InitializeKey(
            human,
            Sighting(7, "neuron.foreign", "human.foreign", 2d).ToPayload());
        neuron.RegisterExecutable(sensor);
        neuron.RegisterExecutable(router);

        sensor.SetObservation(
            Sighting(10, "neuron.receiver", "human.new", 9d));
        router.SetWorldTick(10);
        KLEPKeySnapshot snapshot =
            new KLEPAgent(neuron).Tick().Decision.KeySnapshot;
        IReadOnlyList<KLEPKeyFact> facts = snapshot.FindAll(human.Id);
        Expect(facts.Count == 2,
            "a new direct sample replaces rather than accumulates own facts");

        int ownCount = 0;
        int foreignCount = 0;
        foreach (KLEPKeyFact fact in facts)
        {
            KLEPHumanSighting sighting =
                KLEPHumanSighting.Read(fact.Payload);
            if (sighting.ObserverNeuronId == "neuron.receiver")
            {
                ownCount++;
                Expect(sighting.ObservedWorldTick == 10 &&
                       sighting.EntityId == "human.new",
                    "the retained own occurrence carries the new direct sample");
            }
            else if (sighting.ObserverNeuronId == "neuron.foreign")
            {
                foreignCount++;
                Expect(sighting.ObservedWorldTick == 7 &&
                       sighting.EntityId == "human.foreign",
                    "foreign copied knowledge remains opaque and untouched");
            }
        }

        Expect(ownCount == 1 && foreignCount == 1,
            "exactly one own and one foreign sighting remain");
    }

    private static void SelectorUsesPayloadFreshnessNotStoreClocks()
    {
        KLEPKeyDefinition human = PersistentKey(
            "key.clock-sighting",
            "Clock Sighting");
        KLEPKeyDefinition best = OneCycleKey(
            "key.clock-best",
            "Clock Best");

        var donor = new KLEPNeuron("neuron.donor");
        donor.InitializeKey(
            human,
            Sighting(100, "neuron.donor", "human.copied", 1d).ToPayload());
        KLEPKeySnapshot donorSnapshot =
            new KLEPAgent(donor).Tick().Decision.KeySnapshot;
        KLEPKeyFact donorFact = Only(donorSnapshot, human.Id);

        var recipient = new KLEPNeuron("neuron.recipient");
        recipient.InitializeKey(
            human,
            Sighting(
                101,
                "neuron.recipient",
                "human.own",
                2d).ToPayload());
        var router = Router(
            recipient.StableId,
            human,
            best,
            maximumAge: 1000);
        recipient.RegisterExecutable(router);
        var agent = new KLEPAgent(recipient);
        router.SetWorldTick(200);
        agent.Tick();

        KLEPKeyExchangeResult copied = KLEPKeyExchange.CopyKey(
            "exchange.clock-proof",
            donor,
            donorFact,
            recipient);
        Expect(copied.Succeeded,
            "the later-activated foreign occurrence is copied successfully");
        KLEPKeySnapshot snapshot = agent.Tick().Decision.KeySnapshot;
        KLEPHumanSighting selected =
            KLEPHumanSighting.Read(Only(snapshot, best.Id).Payload);
        Expect(selected.EntityId == "human.own" &&
               selected.ObservedWorldTick == 101,
            "newer payload observation wins despite the copy's later activation");

        IReadOnlyList<KLEPKeyFact> knowledge = snapshot.FindAll(human.Id);
        KLEPKeyFact foreign = FindByObserver(knowledge, "neuron.donor");
        KLEPKeyFact own = FindByObserver(knowledge, "neuron.recipient");
        Expect(foreign.ActivatedTick > own.ActivatedTick,
            "the test actually gives the losing copy a later ActivatedTick");
    }

    private static void SelectorUsesTheAcceptedTotalTieOrder()
    {
        KLEPKeyDefinition human = PersistentKey(
            "key.tie-sighting",
            "Tie Sighting");
        KLEPKeyDefinition best = OneCycleKey(
            "key.tie-best",
            "Tie Best");
        var neuron = new KLEPNeuron("neuron.receiver");

        // Own wins at equal observation Tick even though foreign observer IDs
        // are ordinally earlier.
        neuron.InitializeKey(
            human,
            Sighting(50, "a.foreign", "human.a", 1d).ToPayload());
        neuron.InitializeKey(
            human,
            Sighting(50, "neuron.receiver", "human.z", 2d).ToPayload());
        var router = Router(
            neuron.StableId,
            human,
            best,
            maximumAge: 100);
        neuron.RegisterExecutable(router);
        router.SetWorldTick(50);
        KLEPHumanSighting ownSelected = KLEPHumanSighting.Read(
            Only(new KLEPAgent(neuron).Tick().Decision.KeySnapshot, best.Id).Payload);
        Expect(ownSelected.ObserverNeuronId == neuron.StableId,
            "receiver's own direct sighting wins an equal-Tick tie");

        // Without an own fact, observer ID and then entity ID are ordinal.
        var foreignOnly = new KLEPNeuron("neuron.other-receiver");
        foreignOnly.InitializeKey(
            human,
            Sighting(60, "observer.b", "human.a", 1d).ToPayload());
        foreignOnly.InitializeKey(
            human,
            Sighting(60, "observer.a", "human.z", 2d).ToPayload());
        foreignOnly.InitializeKey(
            human,
            Sighting(60, "observer.a", "human.a", 3d).ToPayload());
        foreignOnly.InitializeKey(
            human,
            Sighting(60, "observer.a", "human.a", 4d).ToPayload());
        var foreignRouter = Router(
            foreignOnly.StableId,
            human,
            best,
            maximumAge: 100);
        foreignOnly.RegisterExecutable(foreignRouter);
        foreignRouter.SetWorldTick(60);
        KLEPAgentTickTrace trace = new KLEPAgent(foreignOnly).Tick();
        KLEPHumanSighting foreignSelected = KLEPHumanSighting.Read(
            Only(trace.Decision.KeySnapshot, best.Id).Payload);
        Expect(foreignSelected.ObserverNeuronId == "observer.a" &&
               foreignSelected.EntityId == "human.a",
            "foreign ties use ordinal observer ID and then entity ID");

        KLEPKeyFact expectedFirst = null;
        foreach (KLEPKeyFact fact in trace.Decision.KeySnapshot.FindAll(human.Id))
        {
            KLEPHumanSighting candidate = KLEPHumanSighting.Read(fact.Payload);
            if (candidate.ObserverNeuronId == "observer.a" &&
                candidate.EntityId == "human.a")
            {
                expectedFirst = fact;
                break;
            }
        }

        Expect(foreignRouter.LastSelectedOccurrenceId ==
               expectedFirst.OccurrenceId,
            "the earliest occurrence ID wins the final otherwise-equal tie");
        Expect(foreignSelected.PositionX == 3d,
            "the final occurrence tie preserves the winning fact's payload");
    }

    private static void FrozenChainCopiesBeforeRelayCleanup()
    {
        KLEPKeyDefinition human = PersistentKey(
            "key.chain-sighting",
            "Chain Sighting");
        var first = new KLEPNeuron("neuron.chain.001");
        first.InitializeKey(
            human,
            Sighting(
                20,
                first.StableId,
                "human.new",
                20d).ToPayload());
        var relay = new KLEPNeuron("neuron.chain.002");
        relay.InitializeKey(
            human,
            Sighting(
                10,
                first.StableId,
                "human.old",
                10d).ToPayload());
        var last = new KLEPNeuron("neuron.chain.003");
        var firstAgent = new KLEPAgent(first);
        var relayAgent = new KLEPAgent(relay);
        var lastAgent = new KLEPAgent(last);
        KLEPKeyFact newer = Only(
            firstAgent.Tick().Decision.KeySnapshot,
            human.Id);
        KLEPKeyFact relayOld = Only(
            relayAgent.Tick().Decision.KeySnapshot,
            human.Id);
        lastAgent.Tick();

        KLEPKeyRequest relayRequest = KLEPKeyExchange.RequestKey(
            "request.chain.relay",
            relay,
            first,
            human.Id,
            KLEPKeyRequestKind.Copy);
        KLEPKeyRequest lastRequest = KLEPKeyExchange.RequestKey(
            "request.chain.last",
            last,
            relay,
            human.Id,
            KLEPKeyRequestKind.Copy);
        Expect(relayRequest.OwnerId == first.StableId &&
               lastRequest.OwnerId == relay.StableId,
            "the relay and downstream requests are explicit addressed intents");

        KLEPKeyExchangeResult intoRelay = KLEPKeyExchange.CopyKey(
            "copy.chain.new-to-relay",
            first,
            newer,
            relay);
        KLEPKeyExchangeResult throughRelay = KLEPKeyExchange.CopyKey(
            "copy.chain.old-to-last",
            relay,
            relayOld,
            last);
        Expect(intoRelay.Succeeded && throughRelay.Succeeded,
            "all frozen chain copies stage before any relay cleanup");
        Expect(relay.RemoveKey(relayOld),
            "the relay stages its older visible version for cleanup afterward");

        KLEPKeySnapshot relayAfter = relayAgent.Tick().Decision.KeySnapshot;
        KLEPKeySnapshot lastAfter = lastAgent.Tick().Decision.KeySnapshot;
        KLEPHumanSighting relayKnowledge = KLEPHumanSighting.Read(
            Only(relayAfter, human.Id).Payload);
        KLEPHumanSighting lastKnowledge = KLEPHumanSighting.Read(
            Only(lastAfter, human.Id).Payload);
        Expect(relayKnowledge.ObservedWorldTick == 20,
            "the relay publishes only its accepted newer replacement");
        Expect(lastKnowledge.ObservedWorldTick == 10,
            "the downstream Neuron still receives the frozen relay fact");
    }

    private static void SelectorExpiresStaleKnowledge()
    {
        KLEPKeyDefinition human = PersistentKey(
            "key.age-sighting",
            "Age Sighting");
        KLEPKeyDefinition best = OneCycleKey(
            "key.age-best",
            "Age Best");
        var neuron = new KLEPNeuron("neuron.age");
        neuron.InitializeKey(
            human,
            Sighting(14, "neuron.scout", "human.old", 1d).ToPayload());
        var router = Router(
            neuron.StableId,
            human,
            best,
            maximumAge: 5);
        neuron.RegisterExecutable(router);
        router.SetWorldTick(20);
        KLEPKeySnapshot snapshot =
            new KLEPAgent(neuron).Tick().Decision.KeySnapshot;
        Expect(snapshot.FindAll(human.Id).Count == 1,
            "aging does not rewrite or delete factual persistent knowledge");
        Expect(snapshot.FindAll(best.Id).Count == 0 &&
               router.LastSelectedSighting == null,
            "an observation older than the authored maximum emits no Best sighting");
    }

    private static void MalformedKnowledgeFaults()
    {
        KLEPKeyDefinition human = PersistentKey(
            "key.bad-sighting",
            "Bad Sighting");
        KLEPKeyDefinition best = OneCycleKey(
            "key.bad-best",
            "Bad Best");
        var neuron = new KLEPNeuron("neuron.bad");
        neuron.InitializeKey(
            human,
            new KLEPKeyPayload(new[]
            {
                Pair(KLEPHumanSighting.ObservedWorldTickField, 3L),
                Pair(KLEPHumanSighting.ObserverNeuronIdField, "neuron.scout")
            }));
        var router = Router(
            neuron.StableId,
            human,
            best,
            maximumAge: 5);
        neuron.RegisterExecutable(router);
        router.SetWorldTick(3);
        Throws<InvalidOperationException>(
            () => new KLEPAgent(neuron).Tick(),
            "malformed sighting knowledge faults instead of influencing behavior");
    }

    private static void InvestigationGoalConsumesSelectedKnowledge()
    {
        KLEPKeyDefinition human = PersistentKey(
            "key.goal-sighting",
            "Goal Sighting");
        KLEPKeyDefinition best = OneCycleKey(
            "key.goal-best",
            "Goal Best");
        var router = Router(
            "neuron.investigator",
            human,
            best,
            maximumAge: 20);
        var action = new KLEPZombieInvestigateHumanSightingExecutable(
            ActionDefinition(
                "action.zombie.investigate",
                "Move to reported human position",
                Present(best)),
            best);
        var goal = new KLEPGoal(
            GoalDefinition(
                "goal.zombie.investigate-human-sighting",
                "Investigate Human Sighting",
                30f,
                Present(best)),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AnyCanFire,
                    new KLEPExecutableBase[] { action })
            });
        var neuron = new KLEPNeuron("neuron.investigator");
        neuron.InitializeKey(
            human,
            Sighting(90, "neuron.scout", "human.reported", 8d).ToPayload());
        neuron.RegisterExecutable(router);
        neuron.RegisterExecutable(goal);
        router.SetWorldTick(90);
        KLEPAgentTickTrace trace = new KLEPAgent(neuron).Tick();
        Expect(trace.Decision.CurrentSoloExecutableId == goal.StableId,
            "the selected sighting opens the Investigate Goal without bypassing Locks");
        Expect(action.TryGetIntent(
                trace.Decision.CycleIndex,
                out KLEPHumanSighting intent) &&
               intent.EntityId == "human.reported" &&
               intent.PositionX == 8d,
            "the Goal child exposes the selected immutable sighting as an intent");
    }

    private static KLEPBestHumanSightingRouterExecutable Router(
        string receiverNeuronId,
        KLEPKeyDefinition human,
        KLEPKeyDefinition best,
        long maximumAge)
    {
        return new KLEPBestHumanSightingRouterExecutable(
            TandemDefinition(
                "router.best-human." + receiverNeuronId,
                "Choose Best Human Sighting",
                KLEPExecutableKind.Router,
                Present(human),
                best),
            human,
            best,
            receiverNeuronId,
            maximumAge);
    }

    private static KLEPHumanSighting Sighting(
        long worldTick,
        string observerNeuronId,
        string entityId,
        double x,
        double y = 0d,
        double z = 0d)
    {
        return new KLEPHumanSighting(
            worldTick,
            observerNeuronId,
            entityId,
            "team.human",
            x,
            y,
            z);
    }

    private static KeyValuePair<string, KLEPKeyValue> Pair(
        string name,
        KLEPKeyValue value)
    {
        return new KeyValuePair<string, KLEPKeyValue>(name, value);
    }

    private static KLEPKeyDefinition PersistentKey(
        string stableId,
        string displayName)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(stableId),
            displayName,
            scope: KLEPKeyScope.Local,
            defaultLifetime: KLEPKeyLifetime.Persistent);
    }

    private static KLEPKeyDefinition OneCycleKey(
        string stableId,
        string displayName)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(stableId),
            displayName,
            scope: KLEPKeyScope.Local,
            defaultLifetime: KLEPKeyLifetime.OneCycle);
    }

    private static KLEPExecutableDefinition TandemDefinition(
        string stableId,
        string displayName,
        KLEPExecutableKind kind,
        KLEPLockExpression expression,
        params KLEPKeyDefinition[] outputs)
    {
        return new KLEPExecutableDefinition(
            stableId,
            displayName,
            kind,
            executionLocks: expression == null
                ? null
                : new[]
                {
                    new KLEPLock(
                        stableId + ".lock",
                        displayName + " requirements",
                        expression)
                },
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: outputs);
    }

    private static KLEPExecutableDefinition ActionDefinition(
        string stableId,
        string displayName,
        KLEPLockExpression expression)
    {
        return new KLEPExecutableDefinition(
            stableId,
            displayName,
            KLEPExecutableKind.Action,
            executionLocks: new[]
            {
                new KLEPLock(
                    stableId + ".lock",
                    displayName + " requirements",
                    expression)
            },
            executionMode: KLEPExecutionMode.Solo);
    }

    private static KLEPExecutableDefinition GoalDefinition(
        string stableId,
        string displayName,
        float score,
        KLEPLockExpression expression)
    {
        return new KLEPExecutableDefinition(
            stableId,
            displayName,
            KLEPExecutableKind.Goal,
            executionLocks: new[]
            {
                new KLEPLock(
                    stableId + ".lock",
                    displayName + " requirements",
                    expression)
            },
            baseAttractiveness: score,
            executionMode: KLEPExecutionMode.Solo);
    }

    private static KLEPLockExpression Present(KLEPKeyDefinition key) =>
        new KLEPKeyPresent(key.Id.Value);

    private static KLEPKeyFact Only(
        KLEPKeySnapshot snapshot,
        KLEPKeyId keyId)
    {
        IReadOnlyList<KLEPKeyFact> facts = snapshot.FindAll(keyId);
        if (facts.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one '{keyId}' occurrence, found {facts.Count}.");
        }

        return facts[0];
    }

    private static KLEPKeyFact FindByObserver(
        IReadOnlyList<KLEPKeyFact> facts,
        string observerNeuronId)
    {
        foreach (KLEPKeyFact fact in facts)
        {
            if (KLEPHumanSighting.Read(fact.Payload).ObserverNeuronId ==
                observerNeuronId)
            {
                return fact;
            }
        }

        throw new InvalidOperationException(
            $"Observer '{observerNeuronId}' was not found.");
    }

    private static void Throws<TException>(Action action, string message)
        where TException : Exception
    {
        assertions++;
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Assertion failed: {message} (no {typeof(TException).Name}).");
    }

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException(
                $"Assertion failed: {message}");
        }
    }
}
