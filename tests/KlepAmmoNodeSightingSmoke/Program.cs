using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.ZombieTest;

internal static class Program
{
    private static int assertions;

    private static int Main()
    {
        PayloadRoundTripsAndRejectsOpenShapes();
        MultipleNodesFromOneObserverCoexist();
        RefreshingOnePairPreservesOtherAndForeignFacts();
        PayloadWorldTickIsIndependentOfDeliveryClocks();
        DirectRefreshRejectsOlderPayloadTime();
        DirectRefreshRejectsEqualTickContradiction();

        Console.WriteLine(
            $"KLEP AmmoNodeSighting smoke tests passed ({assertions} assertions).");
        return 0;
    }

    private static void PayloadRoundTripsAndRejectsOpenShapes()
    {
        KLEPAmmoNodeSighting expected = Sighting(
            27,
            "neuron.scout",
            "ammo-node.002",
            1.25d,
            0.5d,
            -7d);
        KLEPAmmoNodeSighting actual =
            KLEPAmmoNodeSighting.Read(expected.ToPayload());

        Expect(actual.HasSameObservation(expected),
            "the exact V1 payload round-trips every observation field");
        Expect(actual.ObservedWorldTick == 27 &&
               actual.ObserverNeuronId == "neuron.scout" &&
               actual.AmmoNodeId == "ammo-node.002",
            "world Tick and stable pair identity retain their exact types");

        var openFields = new List<KeyValuePair<string, KLEPKeyValue>>();
        foreach (KLEPKeyField field in expected.ToPayload().Fields)
        {
            openFields.Add(Pair(field.Name, field.Value));
        }

        openFields.Add(Pair("unexpected", true));
        Expect(!KLEPAmmoNodeSighting.TryRead(
                new KLEPKeyPayload(openFields),
                out _),
            "an otherwise-valid payload with one extra field is rejected");

        var numericTick = new KLEPKeyPayload(new[]
        {
            Pair(KLEPAmmoNodeSighting.SchemaField,
                KLEPAmmoNodeSighting.Schema),
            Pair(KLEPAmmoNodeSighting.ObservedWorldTickField, 27d),
            Pair(KLEPAmmoNodeSighting.ObserverNeuronIdField,
                "neuron.scout"),
            Pair(KLEPAmmoNodeSighting.AmmoNodeIdField, "ammo-node.002"),
            Pair(KLEPAmmoNodeSighting.PositionXField, 1.25d),
            Pair(KLEPAmmoNodeSighting.PositionYField, 0.5d),
            Pair(KLEPAmmoNodeSighting.PositionZField, -7d)
        });
        Expect(!KLEPAmmoNodeSighting.TryRead(numericTick, out _),
            "a Double cannot impersonate the Int64 observed world Tick");
    }

    private static void MultipleNodesFromOneObserverCoexist()
    {
        KLEPKeyDefinition knowledge = KnowledgeKey(
            "key.ammo-node.multi");
        var sensor = Sensor(
            "sensor.ammo-node.multi",
            knowledge,
            "neuron.civilian.001");
        sensor.SetObservations(new[]
        {
            Sighting(10, sensor.ObserverNeuronId, "ammo-node.002", 2d),
            Sighting(10, sensor.ObserverNeuronId, "ammo-node.001", 1d)
        });
        var neuron = new KLEPNeuron(sensor.ObserverNeuronId);
        neuron.RegisterExecutable(sensor);

        KLEPKeySnapshot snapshot =
            new KLEPAgent(neuron).Tick().Decision.KeySnapshot;
        IReadOnlyList<KLEPKeyFact> facts = snapshot.FindAll(knowledge.Id);
        Expect(facts.Count == 2,
            "one observer retains one persistent fact for each visible node");
        Expect(Find(facts, sensor.ObserverNeuronId, "ammo-node.001") != null &&
               Find(facts, sensor.ObserverNeuronId, "ammo-node.002") != null,
            "node identity, not observer identity alone, separates occurrences");
    }

    private static void RefreshingOnePairPreservesOtherAndForeignFacts()
    {
        KLEPKeyDefinition knowledge = KnowledgeKey(
            "key.ammo-node.refresh");
        const string receiverId = "neuron.civilian.receiver";
        var recipient = new KLEPNeuron(receiverId);
        recipient.InitializeKey(
            knowledge,
            Sighting(3, receiverId, "ammo-node.001", 1d).ToPayload());
        recipient.InitializeKey(
            knowledge,
            Sighting(4, receiverId, "ammo-node.002", 2d).ToPayload());
        var sensor = Sensor(
            "sensor.ammo-node.refresh",
            knowledge,
            receiverId);
        recipient.RegisterExecutable(sensor);
        var recipientAgent = new KLEPAgent(recipient);
        recipientAgent.Tick();

        var donor = new KLEPNeuron("neuron.civilian.donor");
        donor.InitializeKey(
            knowledge,
            Sighting(8, donor.StableId, "ammo-node.001", 8d).ToPayload());
        KLEPKeyFact donorFact = Only(
            new KLEPAgent(donor).Tick().Decision.KeySnapshot,
            knowledge.Id);
        KLEPKeyExchangeResult copied = KLEPKeyExchange.CopyKey(
            "copy.ammo-node.foreign",
            donor,
            donorFact,
            recipient);
        Expect(copied.Succeeded,
            "foreign ammo-node knowledge can be copied between Neurons");

        sensor.SetObservations(new[]
        {
            Sighting(11, receiverId, "ammo-node.001", 11d)
        });
        KLEPKeySnapshot snapshot =
            recipientAgent.Tick().Decision.KeySnapshot;
        IReadOnlyList<KLEPKeyFact> facts = snapshot.FindAll(knowledge.Id);
        Expect(facts.Count == 3,
            "refreshing one own pair preserves the other own node and foreign copy");

        KLEPAmmoNodeSighting ownRefreshed = Read(
            Find(facts, receiverId, "ammo-node.001"));
        KLEPAmmoNodeSighting ownUntouched = Read(
            Find(facts, receiverId, "ammo-node.002"));
        KLEPAmmoNodeSighting foreignUntouched = Read(
            Find(facts, donor.StableId, "ammo-node.001"));
        Expect(ownRefreshed.ObservedWorldTick == 11 &&
               ownRefreshed.PositionX == 11d,
            "the exact refreshed observer-node pair receives the new payload");
        Expect(ownUntouched.ObservedWorldTick == 4 &&
               foreignUntouched.ObservedWorldTick == 8,
            "nonmatching pair payloads remain opaque and unchanged");
    }

    private static void PayloadWorldTickIsIndependentOfDeliveryClocks()
    {
        KLEPKeyDefinition knowledge = KnowledgeKey(
            "key.ammo-node.clock");
        var donor = new KLEPNeuron("neuron.clock.donor");
        donor.InitializeKey(
            knowledge,
            Sighting(40, donor.StableId, "ammo-node.001", 4d).ToPayload());
        KLEPKeyFact donorFact = Only(
            new KLEPAgent(donor).Tick().Decision.KeySnapshot,
            knowledge.Id);

        var recipient = new KLEPNeuron("neuron.clock.recipient");
        recipient.InitializeKey(
            knowledge,
            Sighting(90, recipient.StableId, "ammo-node.001", 9d).ToPayload());
        var recipientAgent = new KLEPAgent(recipient);
        recipientAgent.Tick();
        recipientAgent.Tick();

        KLEPKeyExchangeResult copied = KLEPKeyExchange.CopyKey(
            "copy.ammo-node.clock",
            donor,
            donorFact,
            recipient);
        Expect(copied.Succeeded,
            "an older world observation can arrive in a later delivery");
        IReadOnlyList<KLEPKeyFact> facts = recipientAgent
            .Tick()
            .Decision
            .KeySnapshot
            .FindAll(knowledge.Id);
        KLEPKeyFact own = Find(
            facts,
            recipient.StableId,
            "ammo-node.001");
        KLEPKeyFact foreign = Find(
            facts,
            donor.StableId,
            "ammo-node.001");
        KLEPAmmoNodeSighting ownSighting = Read(own);
        KLEPAmmoNodeSighting foreignSighting = Read(foreign);

        Expect(foreign.ActivatedTick > own.ActivatedTick,
            "the copied older observation really has the later activation clock");
        Expect(ownSighting.ObservedWorldTick == 90 &&
               foreignSighting.ObservedWorldTick == 40 &&
               ownSighting.ObservedWorldTick > foreignSighting.ObservedWorldTick,
            "payload observedWorldTick, not ActivatedTick or IssuedTick, identifies freshness");
        Expect(foreignSighting.HasSameObservation(
                KLEPAmmoNodeSighting.Read(donorFact.Payload)),
            "copy delivery preserves the observation payload exactly");
    }

    private static void DirectRefreshRejectsOlderPayloadTime()
    {
        KLEPKeyDefinition knowledge = KnowledgeKey(
            "key.ammo-node.stale-refresh");
        const string observerId = "neuron.clock.sensor";
        var neuron = new KLEPNeuron(observerId);
        neuron.InitializeKey(
            knowledge,
            Sighting(100, observerId, "ammo-node.001", 10d).ToPayload());
        var sensor = Sensor(
            "sensor.ammo-node.stale-refresh",
            knowledge,
            observerId);
        sensor.SetObservations(new[]
        {
            Sighting(99, observerId, "ammo-node.001", 99d)
        });
        neuron.RegisterExecutable(sensor);

        Throws<InvalidOperationException>(
            () => new KLEPAgent(neuron).Tick(),
            "a later-running Sensor cannot overwrite a newer world observation with an older payload Tick");
    }

    private static void DirectRefreshRejectsEqualTickContradiction()
    {
        KLEPKeyDefinition knowledge = KnowledgeKey(
            "key.ammo-node.equal-conflict");
        const string observerId = "neuron.clock.equal-conflict";
        var neuron = new KLEPNeuron(observerId);
        neuron.InitializeKey(
            knowledge,
            Sighting(100, observerId, "ammo-node.001", 10d).ToPayload());
        var sensor = Sensor(
            "sensor.ammo-node.equal-conflict",
            knowledge,
            observerId);
        sensor.SetObservations(new[]
        {
            Sighting(100, observerId, "ammo-node.001", 11d)
        });
        neuron.RegisterExecutable(sensor);

        Throws<InvalidOperationException>(
            () => new KLEPAgent(neuron).Tick(),
            "one observer and node cannot claim two positions at the same payload world Tick");
    }

    private static KLEPAmmoNodeSightingSensorExecutable Sensor(
        string stableId,
        KLEPKeyDefinition output,
        string observerNeuronId)
    {
        return new KLEPAmmoNodeSightingSensorExecutable(
            new KLEPExecutableDefinition(
                stableId,
                stableId,
                KLEPExecutableKind.Sensor,
                executionMode: KLEPExecutionMode.Tandem,
                declaredOutputs: new[] { output }),
            output,
            observerNeuronId);
    }

    private static KLEPKeyDefinition KnowledgeKey(string stableId)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(stableId),
            stableId,
            scope: KLEPKeyScope.Local,
            defaultLifetime: KLEPKeyLifetime.Persistent);
    }

    private static KLEPAmmoNodeSighting Sighting(
        long worldTick,
        string observerNeuronId,
        string ammoNodeId,
        double x,
        double y = 0d,
        double z = 0d)
    {
        return new KLEPAmmoNodeSighting(
            worldTick,
            observerNeuronId,
            ammoNodeId,
            x,
            y,
            z);
    }

    private static KLEPAmmoNodeSighting Read(KLEPKeyFact fact)
    {
        if (fact == null)
        {
            throw new InvalidOperationException("Expected fact was not found.");
        }

        return KLEPAmmoNodeSighting.Read(fact.Payload);
    }

    private static KLEPKeyFact Find(
        IReadOnlyList<KLEPKeyFact> facts,
        string observerNeuronId,
        string ammoNodeId)
    {
        for (int index = 0; index < facts.Count; index++)
        {
            KLEPAmmoNodeSighting sighting =
                KLEPAmmoNodeSighting.Read(facts[index].Payload);
            if (StringComparer.Ordinal.Equals(
                    sighting.ObserverNeuronId,
                    observerNeuronId) &&
                StringComparer.Ordinal.Equals(
                    sighting.AmmoNodeId,
                    ammoNodeId))
            {
                return facts[index];
            }
        }

        return null;
    }

    private static KLEPKeyFact Only(
        KLEPKeySnapshot snapshot,
        KLEPKeyId keyId)
    {
        IReadOnlyList<KLEPKeyFact> facts = snapshot.FindAll(keyId);
        if (facts.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one '{keyId}' occurrence, found {facts.Count}.");
        }

        return facts[0];
    }

    private static KeyValuePair<string, KLEPKeyValue> Pair(
        string name,
        KLEPKeyValue value)
    {
        return new KeyValuePair<string, KLEPKeyValue>(name, value);
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
