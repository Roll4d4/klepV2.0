using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roll4d4.Klep.Core;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        StableIdentityIsNotTheDisplayName();
        LocalKeysAreIsolated();
        OccurrenceAuthorityCannotCrossStores();
        InitializationAndCycleBoundariesAreExplicit();
        LocalWaveBarriersPublishWithoutAdvancingTheCycle();
        ExchangeStagingWaitsForTopLevelBoundary();
        DuplicateOccurrencesAreRemovedExactly();
        PayloadsAndReplacementAreImmutable();
        CopyPreservesOpaqueFactAndCreatesRecipientAuthority();
        GiveMovesOnlyTheExactOccurrence();
        TakeRequiresTheExactRecipientGrant();
        TradeStagesBothLegsOrNeither();
        RequestDoesNotRevealOrTransferFacts();
        ExchangesRejectGlobalAndOneCycleFacts();
        ExchangesRejectRecipientCrossScopeIdentityCollisions();
        HealthRemovalEnablesTheDeathLock();
        GlobalKeysHaveOneExplicitOwner();
        LateGlobalNeuronsCatchUp();
        ValidationRejectsAmbiguousState();
        BatchInitializationIsAtomic();
        RepeatedRunsAreByteIdentical();
        ExchangeRunsAreByteIdentical();

        Console.WriteLine($"KLEP Key smoke passed: {assertions} assertions.");
    }

    private static void StableIdentityIsNotTheDisplayName()
    {
        var healthId = new KLEPKeyId("key.health");
        var health = Definition(healthId, "Health", KLEPKeyLifetime.Persistent);
        var renamed = Definition(healthId, "Hit Points", KLEPKeyLifetime.Persistent);
        var impostor = Definition(new KLEPKeyId("key.not-health"), "Health");

        Equal(health.Id, renamed.Id, "Renaming display text preserves identity.");
        Equal("Health", health.DisplayName, "Original display name is authored data.");
        Equal("Hit Points", renamed.DisplayName, "Renamed display name is authored data.");
        Assert(health.Id != impostor.Id, "Same display name does not merge distinct IDs.");

        var neuron = new KLEPNeuron("identity-neuron");
        neuron.InitializeKey(health);
        KLEPKeySnapshot snapshot = neuron.Tick().KeySnapshot;

        var healthLock = new KLEPLock(
            "lock.health", "Has Health", new KLEPKeyPresent(renamed.Id.Value));
        var impostorLock = new KLEPLock(
            "lock.impostor", "Has impostor", new KLEPKeyPresent(impostor.Id.Value));
        Assert(healthLock.Evaluate(snapshot).IsSatisfied,
            "A Lock compiled from the renamed definition still matches the stable ID.");
        Assert(!impostorLock.Evaluate(snapshot).IsSatisfied,
            "A same-name definition with another stable ID does not match.");
    }

    private static void LocalKeysAreIsolated()
    {
        var team = Definition(new KLEPKeyId("key.team"), "Team", KLEPKeyLifetime.Persistent);
        var alpha = new KLEPNeuron("alpha");
        var beta = new KLEPNeuron("beta");

        alpha.InitializeKey(team, Payload(("teamId", (KLEPKeyValue)0)));
        KLEPKeySnapshot alphaCycleOne = alpha.Tick().KeySnapshot;
        KLEPKeySnapshot betaCycleOne = beta.Tick().KeySnapshot;

        Assert(alphaCycleOne.Contains(team.Id), "The owner sees its Local Key.");
        Assert(!betaCycleOne.Contains(team.Id), "Another Neuron does not see the Local Key.");
        Equal(1, alphaCycleOne.Facts.Count, "Alpha owns one occurrence.");
        Equal(0, betaCycleOne.Facts.Count, "Beta starts with an empty store.");

        KLEPKeyFact alphaFact = First(alphaCycleOne, team.Id);
        Assert(alpha.RemoveKey(alphaFact), "The owner can stage exact Local removal.");
        Assert(alphaCycleOne.Contains(team.Id), "An already-issued snapshot is immutable.");
        Assert(!alpha.Tick().KeySnapshot.Contains(team.Id),
            "The staged removal becomes visible at Alpha's next boundary.");
        Assert(!beta.Tick().KeySnapshot.Contains(team.Id),
            "Alpha's removal has no side effect on Beta.");
    }

    private static void OccurrenceAuthorityCannotCrossStores()
    {
        var marker = Definition(
            new KLEPKeyId("key.ownership"), "Ownership", KLEPKeyLifetime.Persistent);
        // Reusing a Neuron label is a configuration mistake, but it still must
        // not grant authority over another store's occurrence.
        var firstNeuron = new KLEPNeuron("same-label");
        var secondNeuron = new KLEPNeuron("same-label");
        firstNeuron.InitializeKey(marker, sourceId: "first");
        secondNeuron.InitializeKey(marker, sourceId: "second");
        KLEPKeyFact firstFact = First(firstNeuron.Tick().KeySnapshot, marker.Id);
        KLEPKeyFact secondFact = First(secondNeuron.Tick().KeySnapshot, marker.Id);

        Equal(firstFact.OccurrenceId, secondFact.OccurrenceId,
            "Textual trace IDs can collide when callers reuse a store label.");
        Assert(!secondNeuron.RemoveKey(firstFact),
            "A foreign fact cannot authorize removal despite a textual ID collision.");
        Throws<InvalidOperationException>(() => secondNeuron.ReplaceKey(
            firstFact, Payload(("value", (KLEPKeyValue)2))),
            "A foreign fact cannot authorize replacement despite a textual ID collision.");
        Assert(secondNeuron.Tick().KeySnapshot.Contains(marker.Id),
            "The second store remains intact after foreign mutation attempts.");
        Assert(firstNeuron.RemoveKey(firstFact), "The owning store accepts its own fact.");
        Assert(!firstNeuron.Tick().KeySnapshot.Contains(marker.Id),
            "The owning store commits its exact removal normally.");
        Assert(secondFact.Payload.Count == 0,
            "The unrelated occurrence remains unchanged.");
    }

    private static void InitializationAndCycleBoundariesAreExplicit()
    {
        var anchor = Definition(
            new KLEPKeyId("key.anchor"), "Anchor", KLEPKeyLifetime.Persistent);
        var pulse = Definition(
            new KLEPKeyId("key.pulse"), "Pulse", KLEPKeyLifetime.OneCycle);
        var neuron = new KLEPNeuron("boundary-neuron");

        KLEPKeyFact anchorFact = neuron.InitializeKey(anchor, sourceId: "scene-load");
        Equal(0L, anchorFact.IssuedTick, "Initialization is staged before cycle one.");
        Equal(0, neuron.LastTrace.KeySnapshot.Facts.Count,
            "Initialization never mutates the current cycle-zero snapshot.");

        KLEPKeySnapshot cycleOne = neuron.Tick().KeySnapshot;
        Equal(1L, cycleOne.Tick, "The first Tick creates cycle one.");
        Assert(cycleOne.Contains(anchor.Id), "Initialized Keys are visible in cycle one.");
        Assert(!anchorFact.IsActivated, "The staging handle remains an immutable pending record.");
        Equal(1L, First(cycleOne, anchor.Id).ActivatedTick,
            "The visible fact records its activation boundary.");
        Throws<InvalidOperationException>(() => neuron.InitializeKey(pulse),
            "InitializeKey is rejected after the first Tick.");

        KLEPKeyFact pulseFact = neuron.AddKey(pulse, sourceId: "sensor");
        Equal(1L, pulseFact.IssuedTick, "A runtime Key records the cycle where it was staged.");
        Assert(!cycleOne.Contains(pulse.Id), "A runtime add cannot alter the current snapshot.");

        KLEPKeySnapshot cycleTwo = neuron.Tick().KeySnapshot;
        Assert(cycleTwo.Contains(pulse.Id), "A staged runtime add appears next cycle.");
        Assert(cycleTwo.Contains(anchor.Id), "Persistent Keys survive cycle cleanup.");
        KLEPKeySnapshot cycleThree = neuron.Tick().KeySnapshot;
        Assert(!cycleThree.Contains(pulse.Id), "A OneCycle Key appears in exactly one snapshot.");
        Assert(cycleThree.Contains(anchor.Id), "Persistent Keys remain until exact removal.");
        Assert(cycleTwo.Contains(pulse.Id), "Later expiry cannot mutate an older snapshot.");

        var cancelled = new KLEPNeuron("cancelled-add");
        KLEPKeyFact pending = cancelled.AddKey(pulse);
        Assert(cancelled.RemoveKey(pending), "A pending occurrence can be cancelled exactly.");
        Assert(!cancelled.Tick().KeySnapshot.Contains(pulse.Id),
            "A cancelled pending occurrence never becomes visible.");
    }

    private static void LocalWaveBarriersPublishWithoutAdvancingTheCycle()
    {
        var pulse = Definition(
            new KLEPKeyId("key.wave-pulse"), "Wave pulse", KLEPKeyLifetime.OneCycle);
        var removable = Definition(
            new KLEPKeyId("key.wave-removable"),
            "Wave removable",
            KLEPKeyLifetime.Persistent);
        var emitted = Definition(
            new KLEPKeyId("key.wave-emitted"),
            "Wave emitted",
            KLEPKeyLifetime.Persistent);
        var local = new KLEPKeyStore("wave.local", KLEPKeyScope.Local);

        local.CreateAndStage(pulse, sourceId: "cycle-start");
        local.CreateAndStage(removable, sourceId: "cycle-start");
        local.CommitBoundary(7);
        var waveZero = new KLEPKeySnapshot(7, local, waveIndex: 0);
        KLEPKeyFact removableFact = First(waveZero, removable.Id);

        Equal(0, waveZero.WaveIndex, "The boundary snapshot is wave zero.");
        Equal(7L, local.LastCommittedTick,
            "The Local store starts wave publication at its current cycle.");
        Assert(!local.CommitWithinBoundary(7),
            "A wave barrier reports false when no pending mutation can change visibility.");
        Equal(7L, local.LastCommittedTick,
            "An empty wave barrier does not advance the top-level cycle.");

        local.CreateAndStage(emitted, sourceId: "instant-sensor");
        Assert(local.CommitWithinBoundary(7),
            "A wave barrier reports a staged Local addition as a visible change.");
        Equal(7L, local.LastCommittedTick,
            "Publishing a Local addition does not advance the top-level cycle.");

        var waveOne = new KLEPKeySnapshot(7, local, waveIndex: 1);
        Equal(7L, waveOne.Tick, "A wave snapshot keeps the top-level cycle index.");
        Equal(1, waveOne.WaveIndex, "A new snapshot records its deterministic wave index.");
        Assert(waveOne.Contains(emitted.Id),
            "The staged Local addition is visible after its wave barrier.");
        Assert(waveOne.Contains(pulse.Id),
            "A within-boundary publish does not expire an existing OneCycle fact.");
        Assert(!waveZero.Contains(emitted.Id),
            "Publishing a later wave cannot mutate the prior immutable snapshot.");

        Assert(local.StageRemove(removableFact),
            "An exact visible occurrence can be staged for removal during the cycle.");
        Assert(local.CommitWithinBoundary(7),
            "A wave barrier reports an exact Local removal as a visible change.");
        Equal(7L, local.LastCommittedTick,
            "Publishing a Local removal does not advance the top-level cycle.");

        var waveTwo = new KLEPKeySnapshot(7, local, waveIndex: 2);
        Equal(2, waveTwo.WaveIndex, "Successive snapshots can identify successive waves.");
        Assert(!waveTwo.Contains(removable.Id),
            "The exact removal is visible after its wave barrier.");
        Assert(waveTwo.Contains(pulse.Id),
            "Repeated within-boundary publication still preserves the OneCycle fact.");
        Assert(waveOne.Contains(removable.Id),
            "A later exact removal cannot mutate an earlier wave snapshot.");
        Assert(!local.CommitWithinBoundary(7),
            "A settled barrier reports no visible change when nothing else is pending.");

        var global = new KLEPKeyStore("wave.global", KLEPKeyScope.Global);
        global.CommitBoundary(7);
        Throws<InvalidOperationException>(() => global.CommitWithinBoundary(7),
            "A Global store rejects Neuron-owned within-boundary publication.");
        Equal(7L, global.LastCommittedTick,
            "Rejected Global wave publication does not alter its coordinated boundary.");
    }

    private static void ExchangeStagingWaitsForTopLevelBoundary()
    {
        var transferred = Definition(
            new KLEPKeyId("key.exchange-wave-transfer"),
            "Deferred exchange transfer",
            KLEPKeyLifetime.Persistent);
        var waveEmission = Definition(
            new KLEPKeyId("key.exchange-wave-emission"),
            "Ordinary wave emission",
            KLEPKeyLifetime.Persistent);
        var donor = new KLEPNeuron("exchange-wave-donor");
        var recipient = new KLEPNeuron("exchange-wave-recipient");
        donor.InitializeKey(
            transferred,
            Payload(("opaque", (KLEPKeyValue)"preserve-me")),
            sourceId: "exchange-wave-source");

        KLEPKeyFact sourceFact = First(donor.Tick().KeySnapshot, transferred.Id);
        recipient.Tick();
        KLEPKeyExchangeResult give = KLEPKeyExchange.GiveKey(
            "exchange.wave-deferred", donor, sourceFact, recipient);
        Assert(give.Succeeded, "The boundary regression setup stages its Give.");
        KLEPKeyFact pendingDelivery = give.Deliveries[0].RecipientFact;

        donor.AddKey(waveEmission, sourceId: "donor-tandem-output");
        recipient.AddKey(waveEmission, sourceId: "recipient-tandem-output");
        Assert(donor.LocalKeyStore.CommitWithinBoundary(donor.CycleIndex),
            "An ordinary donor emission publishes at the Tandem wave barrier.");
        Assert(recipient.LocalKeyStore.CommitWithinBoundary(recipient.CycleIndex),
            "An ordinary recipient emission publishes at the Tandem wave barrier.");

        var donorWave = new KLEPKeySnapshot(
            donor.CycleIndex, donor.LocalKeyStore, waveIndex: 1);
        var recipientWave = new KLEPKeySnapshot(
            recipient.CycleIndex, recipient.LocalKeyStore, waveIndex: 1);
        Assert(donorWave.Contains(waveEmission.Id) &&
                recipientWave.Contains(waveEmission.Id),
            "Ordinary Local output is visible in the current Tick's next wave.");
        Assert(ContainsOccurrence(donorWave, sourceFact.OccurrenceId),
            "A Give removal is not published by a same-Tick Tandem wave.");
        Assert(!recipientWave.Contains(transferred.Id),
            "A Give delivery is not published by a same-Tick Tandem wave.");
        Assert(!donor.RemoveKey(sourceFact),
            "A staged exchange removal remains reserved until publication.");
        Assert(!recipient.RemoveKey(pendingDelivery),
            "A staged exchange delivery cannot be cancelled independently.");

        KLEPKeySnapshot donorNext = donor.Tick().KeySnapshot;
        KLEPKeySnapshot recipientNext = recipient.Tick().KeySnapshot;
        Assert(!donorNext.Contains(transferred.Id),
            "The Give removal publishes at the donor's next top-level Local boundary.");
        KLEPKeyFact delivered = Exact(
            recipientNext, pendingDelivery.OccurrenceId);
        AssertPayloadEqual(sourceFact.Payload, delivered.Payload,
            "The deferred delivery preserves its opaque payload unchanged.");
        Equal(recipientNext.Tick, delivered.ActivatedTick,
            "The deferred delivery receives the recipient's next top-level activation tick.");
    }

    private static void DuplicateOccurrencesAreRemovedExactly()
    {
        var signal = Definition(
            new KLEPKeyId("key.signal"), "Signal", KLEPKeyLifetime.Persistent);
        var neuron = new KLEPNeuron("duplicate-neuron");
        KLEPKeyFact first = neuron.InitializeKey(signal, sourceId: "sensor-a");
        KLEPKeyFact second = neuron.InitializeKey(signal, sourceId: "sensor-b");

        Assert(first.OccurrenceId != second.OccurrenceId,
            "Each emission receives a distinct occurrence ID.");
        KLEPKeySnapshot cycleOne = neuron.Tick().KeySnapshot;
        Equal(2, cycleOne.FindAll(signal.Id).Count,
            "Distinct occurrences of one symbolic Key may coexist.");
        Assert(first.OccurrenceId.CompareTo(second.OccurrenceId) < 0,
            "Occurrence sequence is deterministic within a store.");

        Assert(neuron.RemoveKey(first), "The first occurrence can be removed exactly.");
        KLEPKeySnapshot cycleTwo = neuron.Tick().KeySnapshot;
        Equal(1, cycleTwo.FindAll(signal.Id).Count,
            "Removing one occurrence leaves the other occurrence.");
        Equal(second.OccurrenceId, First(cycleTwo, signal.Id).OccurrenceId,
            "The intended occurrence remains.");
        Assert(new KLEPLock("lock.signal", "Signal", new KLEPKeyPresent(signal.Id.Value))
            .Evaluate(cycleTwo).IsSatisfied,
            "Presence remains true while any occurrence remains.");

        Assert(neuron.RemoveKey(second),
            "Removal targets the exact occurrence handle.");
        Assert(!neuron.Tick().KeySnapshot.Contains(signal.Id),
            "Presence becomes false only after every occurrence is explicitly removed.");
    }

    private static void PayloadsAndReplacementAreImmutable()
    {
        var source = new List<KeyValuePair<string, KLEPKeyValue>>
        {
            new KeyValuePair<string, KLEPKeyValue>("status", "alive"),
            new KeyValuePair<string, KLEPKeyValue>("hp", 100),
            new KeyValuePair<string, KLEPKeyValue>("authority", true)
        };
        var defaults = new KLEPKeyPayload(source);
        source[1] = new KeyValuePair<string, KLEPKeyValue>("hp", -999);
        source.Clear();

        Assert(defaults.TryGetInteger("hp", out long hp) && hp == 100,
            "Payload construction copies its input.");
        Assert(defaults.TryGetNumber("hp", out double numericHp) && numericHp == 100d,
            "Integer fields can be read as finite numbers.");
        Assert(defaults.TryGetBoolean("authority", out bool authority) && authority,
            "Boolean fields round-trip.");
        Assert(defaults.TryGetText("status", out string status) && status == "alive",
            "Text fields round-trip.");
        Equal("authority", defaults.Fields[0].Name, "Payload fields use ordinal ordering.");
        Equal("hp", defaults.Fields[1].Name, "Payload field order is deterministic.");
        Equal("status", defaults.Fields[2].Name, "Payload field order is deterministic.");

        var health = new KLEPKeyDefinition(
            new KLEPKeyId("key.immutable-health"),
            "Health",
            "Hit points",
            KLEPKeyScope.Local,
            KLEPKeyLifetime.Persistent,
            2.5f,
            defaults);
        var neuron = new KLEPNeuron("payload-neuron");
        neuron.InitializeKey(health);
        KLEPKeySnapshot cycleOne = neuron.Tick().KeySnapshot;
        KLEPKeyFact original = First(cycleOne, health.Id);

        KLEPKeyFact replacement = neuron.ReplaceKey(
            original, Payload(("hp", (KLEPKeyValue)0)), sourceId: "damage-system");
        Assert(replacement.OccurrenceId != original.OccurrenceId,
            "A payload change creates a new occurrence instead of mutating the old fact.");
        Assert(original.Payload.TryGetInteger("hp", out long originalHp) && originalHp == 100,
            "The observed fact retains its original payload.");
        Assert(cycleOne.Contains(health.Id), "Replacement does not mutate the current snapshot.");

        KLEPKeySnapshot cycleTwo = neuron.Tick().KeySnapshot;
        KLEPKeyFact replaced = First(cycleTwo, health.Id);
        Equal(replacement.OccurrenceId, replaced.OccurrenceId,
            "The staged replacement appears at the next boundary.");
        Assert(replaced.Payload.TryGetInteger("hp", out long replacedHp) && replacedHp == 0,
            "The replacement carries the overridden HP.");
        Assert(replaced.Payload.TryGetText("status", out string inheritedStatus) &&
            inheritedStatus == "alive", "Replacement retains unmodified fields.");
        Equal("damage-system", replaced.SourceId, "Replacement records its source.");
        Assert(original.Payload.TryGetInteger("hp", out originalHp) && originalHp == 100,
            "Later replacement cannot alias the old fact.");

        Equal("key.immutable-health", health.Id.Value, "Runtime use does not mutate definition ID.");
        Equal("Health", health.DisplayName, "Runtime use does not mutate display name.");
        Equal("Hit points", health.Description, "Runtime use does not mutate description.");
        Equal(KLEPKeyScope.Local, health.Scope, "Runtime use does not mutate scope.");
        Equal(KLEPKeyLifetime.Persistent, health.DefaultLifetime,
            "Runtime use does not mutate lifetime.");
        Equal(2.5f, health.BaseAttractiveness,
            "Runtime use does not mutate preserved attractiveness.");
        Assert(ReferenceEquals(defaults, health.DefaultPayload),
            "The immutable authored payload needs no runtime clone.");
    }

    private static void CopyPreservesOpaqueFactAndCreatesRecipientAuthority()
    {
        var definition = Definition(
            new KLEPKeyId("key.exchange-copy"),
            "Opaque exchange fact",
            KLEPKeyLifetime.Persistent);
        var source = new KLEPNeuron("copy-source");
        var recipient = new KLEPNeuron("copy-recipient");
        source.InitializeKey(
            definition,
            Payload(
                ("flag", (KLEPKeyValue)true),
                ("count", (KLEPKeyValue)42),
                ("ratio", (KLEPKeyValue)3.25d),
                ("text", (KLEPKeyValue)"opaque")),
            sourceId: "origin.system");

        KLEPKeySnapshot sourceOne = source.Tick().KeySnapshot;
        recipient.Tick();
        KLEPKeySnapshot recipientTwo = recipient.Tick().KeySnapshot;
        KLEPKeyFact sourceFact = First(sourceOne, definition.Id);

        KLEPKeyExchangeResult result = KLEPKeyExchange.CopyKey(
            "exchange.copy", source, sourceFact, recipient);
        Assert(result.Succeeded, "Copy succeeds for an activated Persistent Local fact.");
        Equal("exchange.copy", result.ExchangeId, "Copy preserves the exchange trace ID.");
        Equal(KLEPKeyExchangeKind.Copy, result.Kind, "Copy reports its operation kind.");
        Equal(KLEPKeyExchangeFailure.None, result.Failure, "Successful Copy has no failure.");
        Equal(1, result.Deliveries.Count, "Copy stages exactly one delivery.");

        KLEPKeyExchangeDelivery delivery = result.Deliveries[0];
        Equal(source.StableId, delivery.SourceNeuronId, "Copy traces the source Neuron.");
        Equal(sourceFact.OccurrenceId, delivery.SourceOccurrenceId,
            "Copy traces the exact source occurrence.");
        Equal(recipient.StableId, delivery.RecipientNeuronId,
            "Copy traces the recipient Neuron.");

        KLEPKeyFact pendingCopy = delivery.RecipientFact;
        Assert(!ReferenceEquals(sourceFact, pendingCopy),
            "Copy creates a distinct recipient fact.");
        Assert(ReferenceEquals(sourceFact.Definition, pendingCopy.Definition),
            "Copy preserves the immutable definition.");
        AssertPayloadEqual(sourceFact.Payload, pendingCopy.Payload,
            "Copy preserves the opaque payload.");
        Equal(sourceFact.Lifetime, pendingCopy.Lifetime, "Copy preserves lifetime.");
        Equal(sourceFact.SourceId, pendingCopy.SourceId, "Copy preserves original SourceId.");
        Assert(sourceFact.OccurrenceId != pendingCopy.OccurrenceId,
            "Copy allocates a new recipient occurrence.");
        Equal("copy-recipient.local", pendingCopy.OccurrenceId.StoreId,
            "The delivered occurrence belongs to the recipient store.");
        Equal(2L, pendingCopy.IssuedTick,
            "The recipient occurrence records the recipient's current boundary.");
        Equal(-1L, pendingCopy.ActivatedTick,
            "The staged recipient occurrence is not active yet.");

        Assert(ContainsOccurrence(sourceOne, sourceFact.OccurrenceId),
            "Copy does not alter the source's current snapshot.");
        Assert(!recipientTwo.Contains(definition.Id),
            "Copy does not alter the recipient's current snapshot.");
        Assert(!source.RemoveKey(pendingCopy),
            "The source has no mutation authority over the recipient occurrence.");

        KLEPKeySnapshot recipientThree = recipient.Tick().KeySnapshot;
        KLEPKeyFact activeCopy = Exact(recipientThree, pendingCopy.OccurrenceId);
        Equal(3L, activeCopy.ActivatedTick,
            "The delivered fact activates at the recipient's next boundary.");
        AssertPayloadEqual(sourceFact.Payload, activeCopy.Payload,
            "Activation does not reinterpret the copied payload.");

        KLEPKeySnapshot sourceTwo = source.Tick().KeySnapshot;
        Assert(ContainsOccurrence(sourceTwo, sourceFact.OccurrenceId),
            "The exact source occurrence remains after Copy.");
        Assert(recipient.RemoveKey(activeCopy),
            "The recipient owns mutation authority over its delivered occurrence.");
    }

    private static void GiveMovesOnlyTheExactOccurrence()
    {
        var signal = Definition(
            new KLEPKeyId("key.exchange-give"),
            "Exchange duplicate",
            KLEPKeyLifetime.Persistent);
        var donor = new KLEPNeuron("give-donor");
        var recipient = new KLEPNeuron("give-recipient");
        KLEPKeyFact firstPending = donor.InitializeKey(
            signal, Payload(("ordinal", (KLEPKeyValue)1)), sourceId: "first-source");
        KLEPKeyFact secondPending = donor.InitializeKey(
            signal, Payload(("ordinal", (KLEPKeyValue)2)), sourceId: "second-source");
        recipient.InitializeKey(
            signal, Payload(("ordinal", (KLEPKeyValue)3)), sourceId: "recipient-source");

        KLEPKeySnapshot donorOne = donor.Tick().KeySnapshot;
        KLEPKeySnapshot recipientOne = recipient.Tick().KeySnapshot;
        KLEPKeyFact exactFirst = Exact(donorOne, firstPending.OccurrenceId);
        KLEPKeyFact exactSecond = Exact(donorOne, secondPending.OccurrenceId);

        KLEPKeyExchangeResult result = KLEPKeyExchange.GiveKey(
            "exchange.give", donor, exactFirst, recipient);
        Assert(result.Succeeded, "Give accepts an exact activated Persistent Local occurrence.");
        Equal(KLEPKeyExchangeKind.Give, result.Kind, "Give reports its operation kind.");
        Equal(1, result.Deliveries.Count, "Give stages one exact delivery.");
        Equal(exactFirst.OccurrenceId, result.Deliveries[0].SourceOccurrenceId,
            "Give selects the requested occurrence rather than a same-ID sibling.");
        Equal(2, donorOne.FindAll(signal.Id).Count,
            "Give staging does not rewrite the donor's current snapshot.");
        Equal(1, recipientOne.FindAll(signal.Id).Count,
            "Give staging does not rewrite the recipient's current snapshot.");

        KLEPKeySnapshot recipientTwo = recipient.Tick().KeySnapshot;
        Equal(2, recipientTwo.FindAll(signal.Id).Count,
            "A delivered occurrence coexists with the recipient's same-ID occurrence.");
        KLEPKeyFact delivered = Exact(
            recipientTwo, result.Deliveries[0].RecipientFact.OccurrenceId);
        AssertPayloadEqual(exactFirst.Payload, delivered.Payload,
            "Give transports only the selected occurrence's opaque payload.");
        Equal("first-source", delivered.SourceId, "Give preserves the selected SourceId.");

        KLEPKeySnapshot donorTwo = donor.Tick().KeySnapshot;
        Equal(1, donorTwo.FindAll(signal.Id).Count,
            "Give removes one occurrence rather than every same-ID occurrence.");
        Assert(!ContainsOccurrence(donorTwo, exactFirst.OccurrenceId),
            "The selected donor occurrence is removed at its next boundary.");
        Assert(ContainsOccurrence(donorTwo, exactSecond.OccurrenceId),
            "The donor's unselected sibling remains.");
    }

    private static void TakeRequiresTheExactRecipientGrant()
    {
        var definition = Definition(
            new KLEPKeyId("key.exchange-take"),
            "Granted fact",
            KLEPKeyLifetime.Persistent);
        var donor = new KLEPNeuron("take-donor");
        var recipient = new KLEPNeuron("take-recipient");
        var intruder = new KLEPNeuron("take-intruder");
        donor.InitializeKey(
            definition, Payload(("value", (KLEPKeyValue)17)), sourceId: "grant-source");
        KLEPKeySnapshot donorOne = donor.Tick().KeySnapshot;
        KLEPKeyFact sourceFact = First(donorOne, definition.Id);

        KLEPKeyTakeGrant grant = KLEPKeyExchange.CreateTakeGrant(
            "grant.take", donor, sourceFact, recipient);
        Equal("grant.take", grant.GrantId, "Take grant preserves its stable ID.");
        Equal(donor.StableId, grant.DonorId, "Take grant identifies the donor.");
        Equal(recipient.StableId, grant.RecipientId, "Take grant identifies its recipient.");
        Equal(sourceFact.KeyId, grant.KeyId, "Take grant identifies the stable Key.");
        Equal(sourceFact.OccurrenceId, grant.SourceOccurrenceId,
            "Take grant binds one exact occurrence.");

        KLEPKeyExchangeResult denied = KLEPKeyExchange.TakeKey(
            "exchange.take-denied", grant, intruder);
        Assert(!denied.Succeeded, "A Take by a different recipient is denied.");
        Equal(KLEPKeyExchangeFailure.InvalidTakeGrant, denied.Failure,
            "The denied Take reports an invalid grant.");
        Equal(0, denied.Deliveries.Count, "A denied Take stages no delivery.");
        Assert(ContainsOccurrence(donorOne, sourceFact.OccurrenceId),
            "A denied Take does not alter the donor's current snapshot.");

        KLEPKeyExchangeResult copyAfterDenial = KLEPKeyExchange.CopyKey(
            "exchange.after-denial", donor, sourceFact, intruder);
        Assert(copyAfterDenial.Succeeded,
            "The denied Take leaves the exact source fact available.");
        Equal(1L, copyAfterDenial.Deliveries[0].RecipientFact.OccurrenceId.Sequence,
            "A denied Take does not burn the attempted recipient's occurrence sequence.");

        KLEPKeyExchangeResult taken = KLEPKeyExchange.TakeKey(
            "exchange.take", grant, recipient);
        Assert(taken.Succeeded, "The grant-bound recipient can Take the exact fact.");
        Equal(KLEPKeyExchangeKind.Take, taken.Kind, "Take reports its operation kind.");
        Equal(1L, taken.Deliveries[0].RecipientFact.OccurrenceId.Sequence,
            "The authorized recipient receives its first occurrence without a sequence gap.");

        KLEPKeyExchangeResult replay = KLEPKeyExchange.TakeKey(
            "exchange.take-replay", grant, recipient);
        Assert(!replay.Succeeded, "A grant cannot move an occurrence already staged for removal.");
        Equal(KLEPKeyExchangeFailure.FactNotOwnedOrAvailable, replay.Failure,
            "A stale Take grant reports that its exact occurrence is unavailable.");

        Assert(!donor.Tick().KeySnapshot.Contains(definition.Id),
            "A successful Take removes the donor occurrence at the next boundary.");
        KLEPKeyFact recipientFact = First(recipient.Tick().KeySnapshot, definition.Id);
        AssertPayloadEqual(sourceFact.Payload, recipientFact.Payload,
            "Take preserves the opaque payload.");
        Assert(intruder.Tick().KeySnapshot.Contains(definition.Id),
            "The separate post-denial Copy activates normally.");
    }

    private static void TradeStagesBothLegsOrNeither()
    {
        var leftDefinition = Definition(
            new KLEPKeyId("key.trade-left"), "Trade left", KLEPKeyLifetime.Persistent);
        var rightDefinition = Definition(
            new KLEPKeyId("key.trade-right"), "Trade right", KLEPKeyLifetime.Persistent);
        var left = new KLEPNeuron("trade-left");
        var right = new KLEPNeuron("trade-right");
        left.InitializeKey(
            leftDefinition, Payload(("side", (KLEPKeyValue)"left")), sourceId: "left-source");
        right.InitializeKey(
            rightDefinition, Payload(("side", (KLEPKeyValue)"right")), sourceId: "right-source");
        KLEPKeySnapshot leftOne = left.Tick().KeySnapshot;
        KLEPKeySnapshot rightOne = right.Tick().KeySnapshot;
        KLEPKeyFact leftFact = First(leftOne, leftDefinition.Id);
        KLEPKeyFact rightFact = First(rightOne, rightDefinition.Id);

        KLEPKeyExchangeResult trade = KLEPKeyExchange.TradeKeys(
            "exchange.trade", left, leftFact, right, rightFact);
        Assert(trade.Succeeded, "Trade accepts two exact Persistent Local occurrences.");
        Equal(KLEPKeyExchangeKind.Trade, trade.Kind, "Trade reports its operation kind.");
        Equal(2, trade.Deliveries.Count, "Trade stages both delivery legs.");
        Assert(leftOne.Contains(leftDefinition.Id) && !leftOne.Contains(rightDefinition.Id),
            "Trade staging does not alter the left current snapshot.");
        Assert(rightOne.Contains(rightDefinition.Id) && !rightOne.Contains(leftDefinition.Id),
            "Trade staging does not alter the right current snapshot.");

        KLEPKeySnapshot leftTwo = left.Tick().KeySnapshot;
        Assert(!leftTwo.Contains(leftDefinition.Id) && leftTwo.Contains(rightDefinition.Id),
            "The left store publishes its removal and delivery at its next boundary.");
        Assert(rightOne.Contains(rightDefinition.Id),
            "The right's older snapshot is unchanged after the left advances first.");
        KLEPKeySnapshot rightTwo = right.Tick().KeySnapshot;
        Assert(!rightTwo.Contains(rightDefinition.Id) && rightTwo.Contains(leftDefinition.Id),
            "The right store independently publishes its staged Trade leg.");
        AssertPayloadEqual(rightFact.Payload, First(leftTwo, rightDefinition.Id).Payload,
            "The left recipient receives the opaque right payload.");
        AssertPayloadEqual(leftFact.Payload, First(rightTwo, leftDefinition.Id).Payload,
            "The right recipient receives the opaque left payload.");

        var invalidLeft = new KLEPNeuron("invalid-trade-left");
        var invalidRight = new KLEPNeuron("invalid-trade-right");
        var outsider = new KLEPNeuron("invalid-trade-outsider");
        invalidLeft.InitializeKey(leftDefinition, sourceId: "invalid-left-source");
        invalidRight.InitializeKey(rightDefinition, sourceId: "invalid-right-source");
        outsider.InitializeKey(rightDefinition, sourceId: "outsider-source");
        KLEPKeySnapshot invalidLeftOne = invalidLeft.Tick().KeySnapshot;
        KLEPKeySnapshot invalidRightOne = invalidRight.Tick().KeySnapshot;
        KLEPKeySnapshot outsiderOne = outsider.Tick().KeySnapshot;
        KLEPKeyFact invalidLeftFact = First(invalidLeftOne, leftDefinition.Id);
        KLEPKeyFact foreignRightFact = First(outsiderOne, rightDefinition.Id);

        KLEPKeyExchangeResult invalidTrade = KLEPKeyExchange.TradeKeys(
            "exchange.trade-invalid",
            invalidLeft,
            invalidLeftFact,
            invalidRight,
            foreignRightFact);
        Assert(!invalidTrade.Succeeded, "Trade rejects a leg not owned by its declared donor.");
        Equal(KLEPKeyExchangeFailure.FactNotOwnedOrAvailable, invalidTrade.Failure,
            "The invalid Trade reports exact-occurrence ownership failure.");
        Equal(0, invalidTrade.Deliveries.Count, "An invalid Trade reports no partial delivery.");
        Assert(ContainsOccurrence(invalidLeftOne, invalidLeftFact.OccurrenceId),
            "An invalid second leg does not stage the valid first removal.");
        Assert(invalidRightOne.Contains(rightDefinition.Id),
            "An invalid Trade does not alter the other participant's current snapshot.");

        KLEPKeyExchangeResult copyAfterFailure = KLEPKeyExchange.CopyKey(
            "exchange.trade-followup", invalidLeft, invalidLeftFact, invalidRight);
        Assert(copyAfterFailure.Succeeded,
            "The valid first fact remains available after an all-or-none Trade failure.");
        Equal(2L, copyAfterFailure.Deliveries[0].RecipientFact.OccurrenceId.Sequence,
            "A failed Trade does not burn the recipient's next sequence.");
        Assert(ContainsOccurrence(
                invalidLeft.Tick().KeySnapshot, invalidLeftFact.OccurrenceId),
            "The failed Trade did not stage a hidden source removal.");
        Equal(2, invalidRight.Tick().KeySnapshot.Facts.Count,
            "Only the explicit follow-up Copy adds a recipient occurrence.");
    }

    private static void RequestDoesNotRevealOrTransferFacts()
    {
        var definition = Definition(
            new KLEPKeyId("key.exchange-request"),
            "Requested fact",
            KLEPKeyLifetime.Persistent);
        var owner = new KLEPNeuron("request-owner");
        var requester = new KLEPNeuron("request-requester");
        owner.InitializeKey(
            definition, Payload(("private", (KLEPKeyValue)"opaque")), sourceId: "request-source");
        KLEPKeySnapshot ownerOne = owner.Tick().KeySnapshot;
        KLEPKeyFact ownerFact = First(ownerOne, definition.Id);

        KLEPKeyRequest request = KLEPKeyExchange.RequestKey(
            "request.give",
            requester,
            owner,
            definition.Id,
            KLEPKeyRequestKind.Give);
        Equal("request.give", request.RequestId, "Request preserves its stable trace ID.");
        Equal(requester.StableId, request.RequesterId, "Request identifies its requester.");
        Equal(owner.StableId, request.OwnerId, "Request identifies its addressed owner.");
        Equal(definition.Id, request.KeyId, "Request identifies only the stable Key ID.");
        Equal(KLEPKeyRequestKind.Give, request.RequestedKind,
            "Request records the requested response kind without performing it.");
        foreach (System.Reflection.PropertyInfo property in typeof(KLEPKeyRequest).GetProperties())
        {
            Assert(property.PropertyType != typeof(KLEPKeyFact),
                "A Request exposes no Key fact or mutation authority.");
        }

        Assert(ContainsOccurrence(ownerOne, ownerFact.OccurrenceId),
            "Request does not remove or replace the owner's current fact.");
        Assert(!requester.LastTrace.KeySnapshot.Contains(definition.Id),
            "Request does not reveal a fact in the requester's current snapshot.");

        KLEPKeyExchangeResult explicitResponse = KLEPKeyExchange.CopyKey(
            "exchange.request-response", owner, ownerFact, requester);
        Assert(explicitResponse.Succeeded, "A separate explicit response can follow a Request.");
        Equal(1L, explicitResponse.Deliveries[0].RecipientFact.OccurrenceId.Sequence,
            "Request itself does not allocate or burn a recipient occurrence.");
        Assert(ContainsOccurrence(owner.Tick().KeySnapshot, ownerFact.OccurrenceId),
            "Request plus Copy retains the owner's exact fact.");
        Assert(requester.Tick().KeySnapshot.Contains(definition.Id),
            "Only the explicit response becomes visible at the requester boundary.");
    }

    private static void ExchangesRejectGlobalAndOneCycleFacts()
    {
        var pulse = Definition(
            new KLEPKeyId("key.exchange-pulse"), "Exchange pulse", KLEPKeyLifetime.OneCycle);
        var anchor = Definition(
            new KLEPKeyId("key.exchange-anchor"), "Exchange anchor", KLEPKeyLifetime.Persistent);
        var lifetimeSource = new KLEPNeuron("lifetime-source");
        var lifetimeRecipient = new KLEPNeuron("lifetime-recipient");
        lifetimeSource.InitializeKey(pulse, sourceId: "pulse-source");
        lifetimeSource.InitializeKey(anchor, sourceId: "anchor-source");
        KLEPKeySnapshot lifetimeOne = lifetimeSource.Tick().KeySnapshot;
        KLEPKeyFact pulseFact = First(lifetimeOne, pulse.Id);
        KLEPKeyFact anchorFact = First(lifetimeOne, anchor.Id);

        KLEPKeyExchangeResult rejectedPulse = KLEPKeyExchange.GiveKey(
            "exchange.reject-pulse", lifetimeSource, pulseFact, lifetimeRecipient);
        Assert(!rejectedPulse.Succeeded, "Give rejects a OneCycle occurrence.");
        Equal(KLEPKeyExchangeFailure.UnsupportedLifetime, rejectedPulse.Failure,
            "OneCycle rejection reports unsupported lifetime.");
        Equal(0, rejectedPulse.Deliveries.Count, "OneCycle rejection stages no delivery.");
        Assert(ContainsOccurrence(lifetimeOne, pulseFact.OccurrenceId),
            "OneCycle rejection does not mutate the current source snapshot.");

        KLEPKeyExchangeResult anchorCopy = KLEPKeyExchange.CopyKey(
            "exchange.after-pulse", lifetimeSource, anchorFact, lifetimeRecipient);
        Assert(anchorCopy.Succeeded, "A valid exchange can follow a rejected OneCycle Give.");
        Equal(1L, anchorCopy.Deliveries[0].RecipientFact.OccurrenceId.Sequence,
            "Rejected OneCycle exchange does not burn recipient sequence.");
        KLEPKeySnapshot lifetimeRecipientOne = lifetimeRecipient.Tick().KeySnapshot;
        Assert(lifetimeRecipientOne.Contains(anchor.Id) && !lifetimeRecipientOne.Contains(pulse.Id),
            "The recipient publishes only the explicitly valid delivery.");

        var world = new KLEPKeyStore("exchange-world", KLEPKeyScope.Global);
        var scopeSource = new KLEPNeuron("scope-source", world);
        var scopeRecipient = new KLEPNeuron("scope-recipient", world);
        var global = Definition(
            new KLEPKeyId("key.exchange-global"),
            "Exchange global",
            KLEPKeyLifetime.Persistent,
            KLEPKeyScope.Global);
        var local = Definition(
            new KLEPKeyId("key.exchange-local"), "Exchange local", KLEPKeyLifetime.Persistent);
        scopeSource.InitializeKey(global, sourceId: "global-source");
        scopeSource.InitializeKey(local, sourceId: "local-source");
        world.CommitBoundary(1);
        KLEPKeySnapshot scopeSourceOne = scopeSource.Tick().KeySnapshot;
        KLEPKeySnapshot scopeRecipientOne = scopeRecipient.Tick().KeySnapshot;
        KLEPKeyFact globalFact = First(scopeSourceOne, global.Id);
        KLEPKeyFact localFact = First(scopeSourceOne, local.Id);

        KLEPKeyExchangeResult rejectedGlobal = KLEPKeyExchange.CopyKey(
            "exchange.reject-global", scopeSource, globalFact, scopeRecipient);
        Assert(!rejectedGlobal.Succeeded, "Copy rejects a Global occurrence.");
        Equal(KLEPKeyExchangeFailure.UnsupportedScope, rejectedGlobal.Failure,
            "Global rejection reports unsupported scope.");
        Equal(0, rejectedGlobal.Deliveries.Count, "Global rejection stages no delivery.");
        Equal(1, scopeRecipientOne.FindAll(global.Id).Count,
            "Global rejection does not duplicate already shared Global state.");

        KLEPKeyExchangeResult localCopy = KLEPKeyExchange.CopyKey(
            "exchange.after-global", scopeSource, localFact, scopeRecipient);
        Assert(localCopy.Succeeded, "A valid Local Copy can follow a rejected Global Copy.");
        Equal(1L, localCopy.Deliveries[0].RecipientFact.OccurrenceId.Sequence,
            "Rejected Global exchange does not burn the recipient Local sequence.");
        world.CommitBoundary(2);
        KLEPKeySnapshot scopeRecipientTwo = scopeRecipient.Tick().KeySnapshot;
        Assert(scopeRecipientTwo.Contains(local.Id),
            "The later valid Local delivery activates normally.");
        Equal(1, scopeRecipientTwo.FindAll(global.Id).Count,
            "The shared Global occurrence remains singular.");
    }

    private static void ExchangesRejectRecipientCrossScopeIdentityCollisions()
    {
        var clashId = new KLEPKeyId("key.exchange-recipient-scope-clash");
        var localClash = Definition(
            clashId, "Local exchange clash", KLEPKeyLifetime.Persistent);
        var globalClash = Definition(
            clashId,
            "Global exchange clash",
            KLEPKeyLifetime.Persistent,
            KLEPKeyScope.Global);
        var safe = Definition(
            new KLEPKeyId("key.exchange-recipient-safe"),
            "Safe exchange fact",
            KLEPKeyLifetime.Persistent);
        var world = new KLEPKeyStore(
            "exchange-recipient-collision.world", KLEPKeyScope.Global);
        var source = new KLEPNeuron("exchange-recipient-collision.source");
        var recipient = new KLEPNeuron(
            "exchange-recipient-collision.recipient", world);

        world.CreateAndStage(globalClash, sourceId: "world");
        world.CommitBoundary(1);
        source.InitializeKey(localClash, sourceId: "source-clash");
        source.InitializeKey(safe, sourceId: "source-safe");
        recipient.InitializeKey(safe, sourceId: "recipient-safe");
        KLEPKeySnapshot sourceOne = source.Tick().KeySnapshot;
        KLEPKeySnapshot recipientOne = recipient.Tick().KeySnapshot;
        KLEPKeyFact clashFact = First(sourceOne, clashId);
        KLEPKeyFact sourceSafeFact = First(sourceOne, safe.Id);
        KLEPKeyFact recipientSafeFact = First(recipientOne, safe.Id);

        KLEPKeyExchangeResult copy = KLEPKeyExchange.CopyKey(
            "exchange.reject-recipient-clash.copy", source, clashFact, recipient);
        Assert(!copy.Succeeded, "Copy rejects a Local delivery that clashes with recipient Global identity.");
        Equal(KLEPKeyExchangeFailure.CrossScopeIdentityCollision, copy.Failure,
            "Copy reports the recipient cross-scope identity collision.");

        KLEPKeyExchangeResult give = KLEPKeyExchange.GiveKey(
            "exchange.reject-recipient-clash.give", source, clashFact, recipient);
        Assert(!give.Succeeded, "Give rejects a Local delivery that clashes with recipient Global identity.");
        Equal(KLEPKeyExchangeFailure.CrossScopeIdentityCollision, give.Failure,
            "Give reports the recipient cross-scope identity collision.");

        KLEPKeyExchangeResult trade = KLEPKeyExchange.TradeKeys(
            "exchange.reject-recipient-clash.trade",
            source,
            clashFact,
            recipient,
            recipientSafeFact);
        Assert(!trade.Succeeded, "Trade rejects atomically when one recipient has a Global identity clash.");
        Equal(KLEPKeyExchangeFailure.CrossScopeIdentityCollision, trade.Failure,
            "Trade reports the recipient cross-scope identity collision.");
        Equal(0, trade.Deliveries.Count, "Rejected Trade stages neither delivery leg.");

        KLEPKeyExchangeResult safeCopy = KLEPKeyExchange.CopyKey(
            "exchange.after-recipient-clash", source, sourceSafeFact, recipient);
        Assert(safeCopy.Succeeded, "A non-clashing Copy succeeds after rejected exchanges.");
        Equal(2L, safeCopy.Deliveries[0].RecipientFact.OccurrenceId.Sequence,
            "Cross-scope rejections do not burn the recipient occurrence sequence.");

        world.CommitBoundary(2);
        KLEPKeySnapshot recipientTwo = recipient.Tick().KeySnapshot;
        Equal(2, recipientTwo.FindAll(safe.Id).Count,
            "The recipient publishes only its existing and valid delivered Local facts.");
        Equal(1, recipientTwo.FindAll(clashId).Count,
            "The recipient snapshot remains unambiguous with only the Global clash ID.");
        KLEPKeySnapshot sourceTwo = source.Tick().KeySnapshot;
        Assert(ContainsOccurrence(sourceTwo, clashFact.OccurrenceId),
            "Rejected Give and Trade do not stage source removal.");
        Assert(ContainsOccurrence(sourceTwo, sourceSafeFact.OccurrenceId),
            "The later valid Copy retains its exact source fact.");
    }

    private static void HealthRemovalEnablesTheDeathLock()
    {
        var health = new KLEPKeyDefinition(
            new KLEPKeyId("key.health-example"),
            "Health",
            defaultLifetime: KLEPKeyLifetime.Persistent,
            defaultPayload: Payload(("hp", (KLEPKeyValue)10)));
        var deathLock = new KLEPLock(
            "lock.death-animation",
            "Run death animation",
            new KLEPNot(new KLEPKeyPresent(health.Id.Value)));
        var neuron = new KLEPNeuron("health-example-neuron");
        neuron.InitializeKey(health, sourceId: "spawn");

        KLEPKeySnapshot cycleOne = neuron.Tick().KeySnapshot;
        Assert(!deathLock.Evaluate(cycleOne).IsSatisfied,
            "Not(Health) is false while Health exists.");
        KLEPKeyFact damaged = neuron.ReplaceKey(
            First(cycleOne, health.Id), Payload(("hp", (KLEPKeyValue)0)), sourceId: "damage");
        Assert(!deathLock.Evaluate(cycleOne).IsSatisfied,
            "Changing HP is staged and cannot alter the current Lock result.");

        KLEPKeySnapshot cycleTwo = neuron.Tick().KeySnapshot;
        KLEPKeyFact zeroHealth = First(cycleTwo, health.Id);
        Equal(damaged.OccurrenceId, zeroHealth.OccurrenceId,
            "Cycle two observes the zero-HP replacement.");
        Assert(zeroHealth.Payload.TryGetInteger("hp", out long hp) && hp <= 0,
            "The consume behavior can deterministically observe HP at zero.");
        Assert(!deathLock.Evaluate(cycleTwo).IsSatisfied,
            "Health still blocks death until the consume behavior removes it.");

        Assert(neuron.RemoveKey(zeroHealth),
            "Consume Health At Zero stages removal of the exact Health occurrence.");
        Assert(!deathLock.Evaluate(cycleTwo).IsSatisfied,
            "Removal cannot change an in-flight snapshot.");
        KLEPKeySnapshot cycleThree = neuron.Tick().KeySnapshot;
        Assert(deathLock.Evaluate(cycleThree).IsSatisfied,
            "At the next boundary, absent Health opens the death-animation Lock.");

        neuron.AddKey(health, Payload(("hp", (KLEPKeyValue)10)), sourceId: "respawn");
        Assert(deathLock.Evaluate(cycleThree).IsSatisfied,
            "A staged respawn cannot rewrite the current snapshot.");
        Assert(!deathLock.Evaluate(neuron.Tick().KeySnapshot).IsSatisfied,
            "Restored Health closes the recomputed Not(Health) Lock.");
    }

    private static void GlobalKeysHaveOneExplicitOwner()
    {
        var world = new KLEPKeyStore("world.keys", KLEPKeyScope.Global);
        var alpha = new KLEPNeuron("global-alpha", world);
        var beta = new KLEPNeuron("global-beta", world);
        var weather = Definition(
            new KLEPKeyId("key.weather.rain"),
            "Rain",
            KLEPKeyLifetime.Persistent,
            KLEPKeyScope.Global);
        KLEPKeyFact rain = alpha.InitializeKey(weather, sourceId: "weather-system");

        Throws<InvalidOperationException>(() => alpha.Tick(),
            "A Neuron refuses to advance an uncommitted Global boundary.");
        Equal(0L, alpha.CycleIndex, "A rejected Global boundary does not advance the Neuron.");
        Equal(-1L, alpha.LocalKeyStore.LastCommittedTick,
            "A rejected Global boundary does not partially commit Local state.");

        world.CommitBoundary(1);
        KLEPKeySnapshot alphaOne = alpha.Tick().KeySnapshot;
        KLEPKeySnapshot betaOne = beta.Tick().KeySnapshot;
        Assert(alphaOne.Contains(weather.Id), "Alpha reads the world-owned Global snapshot.");
        Assert(betaOne.Contains(weather.Id), "Beta reads the same Global snapshot.");
        Equal(rain.OccurrenceId, First(alphaOne, weather.Id).OccurrenceId,
            "Both Neurons observe the same Global occurrence.");
        Equal(rain.OccurrenceId, First(betaOne, weather.Id).OccurrenceId,
            "Global occurrence identity is shared explicitly.");

        var localMarker = Definition(
            new KLEPKeyId("key.local-marker"), "Local Marker", KLEPKeyLifetime.Persistent);
        alpha.AddKey(localMarker);
        world.CommitBoundary(2);
        KLEPKeySnapshot alphaTwo = alpha.Tick().KeySnapshot;
        KLEPKeySnapshot betaTwo = beta.Tick().KeySnapshot;
        Assert(alphaTwo.Contains(localMarker.Id), "Alpha sees its Local Key.");
        Assert(!betaTwo.Contains(localMarker.Id), "Beta never sees Alpha's Local Key.");

        Assert(alpha.RemoveKey(rain), "A Global occurrence can be staged for exact removal.");
        Assert(alphaTwo.Contains(weather.Id) && betaTwo.Contains(weather.Id),
            "Global staging cannot mutate either current snapshot.");
        world.CommitBoundary(3);
        Assert(!alpha.Tick().KeySnapshot.Contains(weather.Id),
            "Alpha sees Global removal at the coordinated boundary.");
        Assert(!beta.Tick().KeySnapshot.Contains(weather.Id),
            "Beta sees the same Global removal at that boundary.");

        var pulse = Definition(
            new KLEPKeyId("key.global-pulse"),
            "Global Pulse",
            KLEPKeyLifetime.OneCycle,
            KLEPKeyScope.Global);
        alpha.AddKey(pulse);
        world.CommitBoundary(4);
        Assert(alpha.Tick().KeySnapshot.Contains(pulse.Id),
            "A Global OneCycle Key is visible to Alpha for its committed boundary.");
        Assert(beta.Tick().KeySnapshot.Contains(pulse.Id),
            "A Global OneCycle Key is visible to Beta for the same boundary.");
        world.CommitBoundary(5);
        Assert(!alpha.Tick().KeySnapshot.Contains(pulse.Id),
            "The world owner expires the Global OneCycle Key next boundary.");
        Assert(!beta.Tick().KeySnapshot.Contains(pulse.Id),
            "Every synchronized Neuron observes identical Global expiry.");

        var otherWorld = new KLEPKeyStore("other-world.keys", KLEPKeyScope.Global);
        var gamma = new KLEPNeuron("global-gamma", otherWorld);
        otherWorld.CommitBoundary(1);
        Assert(!gamma.Tick().KeySnapshot.Contains(weather.Id),
            "Another explicitly injected Global store remains isolated.");
    }

    private static void LateGlobalNeuronsCatchUp()
    {
        var world = new KLEPKeyStore("late-world.keys", KLEPKeyScope.Global);
        var weather = Definition(
            new KLEPKeyId("key.late-world-weather"),
            "Late-world weather",
            KLEPKeyLifetime.Persistent,
            KLEPKeyScope.Global);
        world.CreateAndStage(weather, sourceId: "world-bootstrap");
        world.CommitBoundary(5);

        var local = Definition(
            new KLEPKeyId("key.late-local"), "Late local", KLEPKeyLifetime.Persistent);
        var late = new KLEPNeuron("late-neuron", world);
        KLEPKeyFact localPending = late.InitializeKey(local);
        Equal(5L, late.NextCycleIndex,
            "A late Neuron reports the current world boundary as its next cycle.");
        Throws<InvalidOperationException>(() => late.InitializeKey(weather),
            "A late Neuron cannot retroactively initialize an already-running Global store.");

        KLEPKeySnapshot firstObserved = late.Tick().KeySnapshot;
        Equal(5L, late.CycleIndex, "A late Neuron aligns its first cycle to the world.");
        Assert(firstObserved.Contains(weather.Id), "The late Neuron sees current persistent Globals.");
        Assert(firstObserved.Contains(local.Id), "Its initial Local Key appears on its first cycle.");
        Equal(0L, localPending.IssuedTick, "Late Local initialization was staged before first use.");
        Equal(5L, First(firstObserved, local.Id).ActivatedTick,
            "Late Local initialization records the actual activation boundary.");

        var pulse = Definition(
            new KLEPKeyId("key.late-global-pulse"),
            "Late Global Pulse",
            KLEPKeyLifetime.OneCycle,
            KLEPKeyScope.Global);
        KLEPKeyFact stagedAfterFive = world.CreateAndStage(pulse, sourceId: "after-five");
        Equal(5L, stagedAfterFive.IssuedTick,
            "A Global emission records the world boundary where it was staged.");
        Assert(!firstObserved.Contains(pulse.Id),
            "Staging after boundary five cannot rewrite boundary five.");

        world.CommitBoundary(6);
        KLEPKeySnapshot cycleSix = late.Tick().KeySnapshot;
        Assert(cycleSix.Contains(pulse.Id),
            "The staged Global emission appears at the next world boundary.");
        Equal(6L, First(cycleSix, pulse.Id).ActivatedTick,
            "The Global fact exposes its real activation boundary.");

        world.CommitBoundary(8);
        KLEPKeySnapshot cycleEight = late.Tick().KeySnapshot;
        Equal(8L, late.CycleIndex, "A Neuron can recover after skipping a world boundary.");
        Assert(!cycleEight.Contains(pulse.Id),
            "A skipped OneCycle Global pulse is expired rather than replayed late.");
        Assert(cycleEight.Contains(weather.Id), "Persistent Global state survives the skipped boundary.");
    }

    private static void ValidationRejectsAmbiguousState()
    {
        Throws<ArgumentException>(() => new KLEPKeyId(" "),
            "Empty stable Key IDs are rejected.");
        Throws<ArgumentOutOfRangeException>(() => new KLEPKeyOccurrenceId("store", 0),
            "Occurrence sequences start at one.");
        Throws<ArgumentException>(() => new KLEPKeyDefinition(default, "Invalid"),
            "A default KeyId cannot become a definition.");
        Throws<ArgumentOutOfRangeException>(() => new KLEPKeyDefinition(
            new KLEPKeyId("key.bad-scope"), "Bad", scope: (KLEPKeyScope)999),
            "Undefined scope values are rejected.");
        Throws<ArgumentOutOfRangeException>(() => new KLEPKeyDefinition(
            new KLEPKeyId("key.bad-life"), "Bad", defaultLifetime: (KLEPKeyLifetime)999),
            "Undefined lifetime values are rejected.");
        Throws<ArgumentOutOfRangeException>(() => KLEPKeyValue.FromNumber(double.NaN),
            "NaN payload numbers are rejected.");
        Throws<ArgumentOutOfRangeException>(() => KLEPKeyValue.FromNumber(double.PositiveInfinity),
            "Infinite payload numbers are rejected.");
        Equal(0, new KLEPKeyPayload().Count,
            "A parameterless payload remains the valid empty payload.");
        Throws<ArgumentException>(() => new KLEPKeyPayload(new[]
        {
            new KeyValuePair<string, KLEPKeyValue>(
                "unsupported-none", default)
        }), "The unsupported None payload value is rejected at construction.");
        Throws<ArgumentException>(() => new KLEPKeyPayload(new[]
        {
            new KeyValuePair<string, KLEPKeyValue>("duplicate", 1),
            new KeyValuePair<string, KLEPKeyValue>("duplicate", 2)
        }), "Duplicate payload field names are rejected.");

        var localDefinition = Definition(new KLEPKeyId("key.local"), "Local");
        var globalDefinition = Definition(
            new KLEPKeyId("key.global"), "Global", scope: KLEPKeyScope.Global);
        var localStore = new KLEPKeyStore("validation.local", KLEPKeyScope.Local);
        var globalStore = new KLEPKeyStore("validation.global", KLEPKeyScope.Global);
        Throws<InvalidOperationException>(() => localStore.CreateAndStage(globalDefinition),
            "A Global definition cannot enter a Local store.");
        Throws<InvalidOperationException>(() => globalStore.CreateAndStage(localDefinition),
            "A Local definition cannot enter a Global store.");
        Throws<ArgumentException>(() => new KLEPNeuron("bad-global", localStore),
            "A Neuron rejects a Local store injected as its Global store.");
        Throws<InvalidOperationException>(() => new KLEPNeuron("no-global").AddKey(globalDefinition),
            "A Global Key requires an explicitly injected Global store.");

        localStore.CommitBoundary(1);
        localStore.CreateAndStage(localDefinition);
        localStore.CommitBoundary(1);
        var sameBoundary = new KLEPKeySnapshot(1, localStore);
        Assert(!sameBoundary.Contains(localDefinition.Id),
            "Recommitting one boundary does not leak newly staged state into it.");
        localStore.CommitBoundary(2);
        KLEPKeySnapshot secondBoundary = new KLEPKeySnapshot(2, localStore);
        Assert(secondBoundary.Contains(localDefinition.Id),
            "State staged after a boundary appears at the following boundary.");
        Equal(1L, First(secondBoundary, localDefinition.Id).IssuedTick,
            "The store derives issued time from its committed boundary.");
        Equal(2L, First(secondBoundary, localDefinition.Id).ActivatedTick,
            "The visible fact records its activation boundary.");
        Throws<InvalidOperationException>(() => localStore.CommitBoundary(1),
            "A store cannot move backward in deterministic time.");

        var tampered = new KLEPNeuron("tampered-local");
        tampered.LocalKeyStore.CommitBoundary(2);
        Throws<InvalidOperationException>(() => tampered.Tick(),
            "A Neuron detects external advancement of its Local store.");
        Equal(0L, tampered.CycleIndex,
            "A rejected Local boundary does not partially advance CycleIndex.");
        Equal(0L, tampered.LastTrace.CycleIndex,
            "A rejected Local boundary does not publish a mismatched trace.");

        var clashingLocalStore = new KLEPKeyStore("clash.local", KLEPKeyScope.Local);
        var clashingGlobalStore = new KLEPKeyStore("clash.global", KLEPKeyScope.Global);
        var clashId = new KLEPKeyId("key.scope-clash");
        clashingLocalStore.CreateAndStage(Definition(clashId, "Local clash"));
        clashingGlobalStore.CreateAndStage(Definition(
            clashId, "Global clash", scope: KLEPKeyScope.Global));
        clashingLocalStore.CommitBoundary(1);
        clashingGlobalStore.CommitBoundary(1);
        Throws<InvalidOperationException>(() =>
            new KLEPKeySnapshot(1, clashingLocalStore, clashingGlobalStore),
            "One stable Key ID cannot be visible with conflicting scopes.");
    }

    private static void BatchInitializationIsAtomic()
    {
        var first = Definition(
            new KLEPKeyId("key.batch-first"), "Batch first", KLEPKeyLifetime.Persistent);
        var neuron = new KLEPNeuron("batch-neuron");
        Throws<ArgumentNullException>(() => neuron.InitializeKeys(new[] { first, null }),
            "Batch initialization validates the complete input before staging.");
        Equal(0, neuron.Tick().KeySnapshot.Facts.Count,
            "A failed batch leaves no partially initialized Keys.");
    }

    private static void RepeatedRunsAreByteIdentical()
    {
        string expected = RunCanonicalScenario();
        for (int run = 0; run < 100; run++)
        {
            Equal(expected, RunCanonicalScenario(),
                $"Deterministic run {run.ToString(CultureInfo.InvariantCulture)} matches.");
        }
    }

    private static void ExchangeRunsAreByteIdentical()
    {
        string expected = RunCanonicalExchangeScenario();
        for (int run = 0; run < 100; run++)
        {
            Equal(expected, RunCanonicalExchangeScenario(),
                $"Deterministic exchange run {run.ToString(CultureInfo.InvariantCulture)} matches.");
        }
    }

    private static string RunCanonicalScenario()
    {
        var neuron = new KLEPNeuron("repeatable-neuron");
        var zulu = Definition(
            new KLEPKeyId("key.zulu"), "Zulu", KLEPKeyLifetime.Persistent);
        var alpha = Definition(
            new KLEPKeyId("key.alpha"), "Alpha", KLEPKeyLifetime.Persistent);
        var middle = Definition(
            new KLEPKeyId("key.middle"), "Middle", KLEPKeyLifetime.Persistent);

        neuron.InitializeKey(zulu, Payload(("z", (KLEPKeyValue)3), ("a", (KLEPKeyValue)1)));
        neuron.InitializeKey(alpha, sourceId: "first-alpha");
        neuron.InitializeKey(middle, Payload(("value", (KLEPKeyValue)2)));
        neuron.InitializeKey(alpha, sourceId: "second-alpha");
        KLEPKeySnapshot snapshot = neuron.Tick().KeySnapshot;

        var expression = new KLEPAll(
            new KLEPKeyPresent(alpha.Id.Value),
            new KLEPAny(
                new KLEPKeyPresent(middle.Id.Value),
                new KLEPKeyPresent("key.absent")),
            new KLEPNot(new KLEPKeyPresent("key.forbidden")));
        KLEPLockEvaluation evaluation = new KLEPLock(
            "lock.repeatable", "Repeatable", expression).Evaluate(snapshot);

        var text = new StringBuilder();
        text.Append(snapshot.Tick).Append('|');
        foreach (KLEPKeyFact fact in snapshot.Facts)
        {
            text.Append(fact.KeyId.Value).Append('@')
                .Append(fact.OccurrenceId).Append('@')
                .Append(fact.SourceId).Append('{');
            foreach (KLEPKeyField field in fact.Payload.Fields)
            {
                text.Append(field.Name).Append('=')
                    .Append(field.Value.Kind).Append(':')
                    .Append(field.Value).Append(';');
            }

            text.Append("}|");
        }

        text.Append(evaluation.IsSatisfied).Append('|');
        foreach (KLEPLockExpressionResult result in evaluation.Results)
        {
            text.Append(result.Path).Append(':')
                .Append(result.Kind).Append(':')
                .Append(result.StableKeyId).Append(':')
                .Append(result.IsSatisfied).Append('|');
        }

        return text.ToString();
    }

    private static string RunCanonicalExchangeScenario()
    {
        var leftDefinition = Definition(
            new KLEPKeyId("key.repeatable-exchange-left"),
            "Repeatable exchange left",
            KLEPKeyLifetime.Persistent);
        var rightDefinition = Definition(
            new KLEPKeyId("key.repeatable-exchange-right"),
            "Repeatable exchange right",
            KLEPKeyLifetime.Persistent);
        var left = new KLEPNeuron("repeatable-exchange-left");
        var right = new KLEPNeuron("repeatable-exchange-right");
        left.InitializeKey(
            leftDefinition,
            Payload(
                ("flag", (KLEPKeyValue)true),
                ("number", (KLEPKeyValue)11),
                ("text", (KLEPKeyValue)"left")),
            sourceId: "repeatable-left-source");
        right.InitializeKey(
            rightDefinition,
            Payload(
                ("flag", (KLEPKeyValue)false),
                ("number", (KLEPKeyValue)22),
                ("text", (KLEPKeyValue)"right")),
            sourceId: "repeatable-right-source");
        KLEPKeyFact leftFact = First(left.Tick().KeySnapshot, leftDefinition.Id);
        KLEPKeyFact rightFact = First(right.Tick().KeySnapshot, rightDefinition.Id);

        KLEPKeyExchangeResult trade = KLEPKeyExchange.TradeKeys(
            "exchange.repeatable", left, leftFact, right, rightFact);
        Assert(trade.Succeeded, "The canonical exchange scenario must stage successfully.");
        KLEPKeySnapshot leftResult = left.Tick().KeySnapshot;
        KLEPKeySnapshot rightResult = right.Tick().KeySnapshot;

        var text = new StringBuilder();
        text.Append(trade.ExchangeId).Append('|')
            .Append(trade.Kind).Append('|')
            .Append(trade.Failure).Append('|');
        foreach (KLEPKeyExchangeDelivery delivery in trade.Deliveries)
        {
            text.Append(delivery.SourceNeuronId).Append('@')
                .Append(delivery.SourceOccurrenceId).Append('>')
                .Append(delivery.RecipientNeuronId).Append('@');
            AppendFact(text, delivery.RecipientFact);
        }

        AppendSnapshot(text, leftResult);
        AppendSnapshot(text, rightResult);
        return text.ToString();
    }

    private static void AppendSnapshot(StringBuilder text, KLEPKeySnapshot snapshot)
    {
        text.Append("snapshot[")
            .Append(snapshot.Tick).Append(':')
            .Append(snapshot.WaveIndex).Append(']');
        foreach (KLEPKeyFact fact in snapshot.Facts)
        {
            AppendFact(text, fact);
        }
    }

    private static void AppendFact(StringBuilder text, KLEPKeyFact fact)
    {
        text.Append(fact.KeyId.Value).Append('@')
            .Append(fact.OccurrenceId).Append('@')
            .Append(fact.SourceId).Append('@')
            .Append(fact.IssuedTick).Append('@')
            .Append(fact.ActivatedTick).Append('{');
        foreach (KLEPKeyField field in fact.Payload.Fields)
        {
            text.Append(field.Name).Append('=')
                .Append(field.Value.Kind).Append(':')
                .Append(field.Value).Append(';');
        }

        text.Append("}|");
    }

    private static KLEPKeyDefinition Definition(
        KLEPKeyId id,
        string displayName,
        KLEPKeyLifetime lifetime = KLEPKeyLifetime.OneCycle,
        KLEPKeyScope scope = KLEPKeyScope.Local)
    {
        return new KLEPKeyDefinition(
            id,
            displayName,
            scope: scope,
            defaultLifetime: lifetime);
    }

    private static KLEPKeyPayload Payload(
        params (string Name, KLEPKeyValue Value)[] fields)
    {
        var values = new List<KeyValuePair<string, KLEPKeyValue>>(fields.Length);
        foreach ((string Name, KLEPKeyValue Value) field in fields)
        {
            values.Add(new KeyValuePair<string, KLEPKeyValue>(field.Name, field.Value));
        }

        return new KLEPKeyPayload(values);
    }

    private static KLEPKeyFact Exact(
        KLEPKeySnapshot snapshot,
        KLEPKeyOccurrenceId occurrenceId)
    {
        foreach (KLEPKeyFact fact in snapshot.Facts)
        {
            if (fact.OccurrenceId == occurrenceId)
            {
                return fact;
            }
        }

        throw new InvalidOperationException(
            $"Expected Key occurrence '{occurrenceId}' in snapshot {snapshot.Tick}.");
    }

    private static bool ContainsOccurrence(
        KLEPKeySnapshot snapshot,
        KLEPKeyOccurrenceId occurrenceId)
    {
        foreach (KLEPKeyFact fact in snapshot.Facts)
        {
            if (fact.OccurrenceId == occurrenceId)
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertPayloadEqual(
        KLEPKeyPayload expected,
        KLEPKeyPayload actual,
        string message)
    {
        Equal(expected.Fields.Count, actual.Fields.Count, $"{message} Field count differs.");
        for (int index = 0; index < expected.Fields.Count; index++)
        {
            Equal(expected.Fields[index].Name, actual.Fields[index].Name,
                $"{message} Field {index.ToString(CultureInfo.InvariantCulture)} name differs.");
            Equal(expected.Fields[index].Value, actual.Fields[index].Value,
                $"{message} Field {expected.Fields[index].Name} value differs.");
        }
    }

    private static KLEPKeyFact First(KLEPKeySnapshot snapshot, KLEPKeyId id)
    {
        Assert(snapshot.TryGetFirst(id, out KLEPKeyFact fact), $"Expected Key '{id}'.");
        return fact;
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        assertions++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message} Expected '{expected}', received '{actual}'.");
        }
    }

    private static TException Throws<TException>(Action action, string message)
        where TException : Exception
    {
        assertions++;
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(
            $"{message} Expected {typeof(TException).Name}.");
    }
}
