using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// Passive symbolic state owned by one cognitive unit. A Neuron retains
    /// Keys and the authored root Executable catalog. KLEPAgent owns the
    /// decision runtime that evaluates and fires that catalog.
    /// </summary>
    public sealed class KLEPNeuron
    {
        private static readonly StringComparer IdComparer = StringComparer.Ordinal;
        private readonly Dictionary<string, KLEPExecutableBase> executables =
            new Dictionary<string, KLEPExecutableBase>(IdComparer);
        private readonly Dictionary<string, KLEPExecutableBase> pendingRegistrations =
            new Dictionary<string, KLEPExecutableBase>(IdComparer);
        private readonly Dictionary<string, string> registrationTenureIds =
            new Dictionary<string, string>(IdComparer);
        private readonly Dictionary<string, string> pendingRegistrationTenureIds =
            new Dictionary<string, string>(IdComparer);
        private readonly HashSet<string> pendingRemovals =
            new HashSet<string>(IdComparer);
        private object decisionOwner;
        private bool isAgentTicking;
        private long nextRegistrationTenure;

        public KLEPNeuron(string stableId, KLEPKeyStore globalKeyStore = null)
        {
            ValidateStableId(stableId, nameof(stableId));
            if (globalKeyStore != null && globalKeyStore.Scope != KLEPKeyScope.Global)
            {
                throw new ArgumentException(
                    "An injected shared KeyStore must have Global scope.",
                    nameof(globalKeyStore));
            }

            StableId = stableId;
            LocalKeyStore = new KLEPKeyStore($"{stableId}.local", KLEPKeyScope.Local);
            GlobalKeyStore = globalKeyStore;
        }

        public string StableId { get; }
        internal KLEPKeyStore LocalKeyStore { get; }
        internal KLEPKeyStore GlobalKeyStore { get; }
        internal Dictionary<string, KLEPExecutableBase> ExecutableCatalog =>
            executables;
        internal Dictionary<string, KLEPExecutableBase> PendingRegistrations =>
            pendingRegistrations;
        internal Dictionary<string, string> RegistrationTenureIds =>
            registrationTenureIds;
        internal Dictionary<string, string> PendingRegistrationTenureIds =>
            pendingRegistrationTenureIds;
        internal HashSet<string> PendingRemovals => pendingRemovals;
        public long CycleIndex { get; internal set; }
        public long CatalogRevision { get; private set; }
        public long NextCycleIndex
        {
            get
            {
                long next = CheckedNextCycle(CycleIndex);
                return GlobalKeyStore != null && GlobalKeyStore.LastCommittedTick > next
                    ? GlobalKeyStore.LastCommittedTick
                    : next;
            }
        }

        public void RegisterExecutable(KLEPExecutableBase executable)
        {
            EnsureExternalMutationAllowed();
            if (executable == null)
            {
                throw new ArgumentNullException(nameof(executable));
            }

            ValidateStableId(executable.StableId, nameof(executable));
            if (executable.IsGoalOwned)
            {
                throw new InvalidOperationException(
                    $"Executable '{executable.StableId}' is owned by Goal " +
                    $"'{executable.GoalOwnerId}' and cannot also be registered " +
                    "as a Neuron root.");
            }

            if (executable.IsNeuronOwned &&
                (!executables.TryGetValue(
                    executable.StableId, out KLEPExecutableBase owned) ||
                 !ReferenceEquals(owned, executable)))
            {
                throw new InvalidOperationException(
                    $"Executable '{executable.StableId}' is already registered " +
                    $"by Neuron '{executable.NeuronOwnerId}'.");
            }

            if (pendingRegistrations.TryGetValue(
                    executable.StableId, out KLEPExecutableBase pending))
            {
                if (ReferenceEquals(pending, executable))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Executable ID '{executable.StableId}' is already staged " +
                    "for registration.");
            }

            bool explicitlyRemoved = pendingRemovals.Contains(executable.StableId);
            if (executables.TryGetValue(
                    executable.StableId, out KLEPExecutableBase registered))
            {
                if (ReferenceEquals(registered, executable) && !explicitlyRemoved)
                {
                    return;
                }

                if (!explicitlyRemoved)
                {
                    throw new InvalidOperationException(
                        $"Executable ID '{executable.StableId}' is already registered. " +
                        "Remove it before registering a replacement.");
                }
            }

            // When the ID is explicitly removed, keep both staged changes. The
            // old runtime must unwind before the replacement initializes.
            pendingRegistrations.Add(executable.StableId, executable);
            pendingRegistrationTenureIds.Add(
                executable.StableId,
                AllocateRegistrationTenureId());
        }

        public void RemoveExecutable(string stableId)
        {
            EnsureExternalMutationAllowed();
            ValidateStableId(stableId, nameof(stableId));
            pendingRegistrations.Remove(stableId);
            pendingRegistrationTenureIds.Remove(stableId);
            pendingRemovals.Add(stableId);
        }

        public IReadOnlyList<KLEPExecutableDefinition>
            GetRootExecutableDefinitionsSnapshot()
        {
            var definitions = new List<KLEPExecutableDefinition>(executables.Count);
            foreach (KLEPExecutableBase executable in executables.Values)
            {
                definitions.Add(executable.Definition);
            }

            definitions.Sort((left, right) => IdComparer.Compare(
                left.StableId, right.StableId));
            return new ReadOnlyCollection<KLEPExecutableDefinition>(definitions);
        }

        public KLEPKeyFact InitializeKey(
            KLEPKeyDefinition definition,
            KLEPKeyPayload payload = null,
            KLEPKeyLifetime? lifetime = null,
            string sourceId = "initial")
        {
            ValidateCanInitialize(definition);
            return AddKey(definition, payload, lifetime, sourceId);
        }

        public IReadOnlyList<KLEPKeyFact> InitializeKeys(
            IEnumerable<KLEPKeyDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            if (CycleIndex != 0)
            {
                throw new InvalidOperationException(
                    "Initial keys can only be supplied before the first Tick.");
            }

            var validated = new List<KLEPKeyDefinition>();
            foreach (KLEPKeyDefinition definition in definitions)
            {
                ValidateCanInitialize(definition);
                validated.Add(definition);
            }

            var facts = new List<KLEPKeyFact>(validated.Count);
            foreach (KLEPKeyDefinition definition in validated)
            {
                facts.Add(AddKey(definition, sourceId: "initial"));
            }

            return new ReadOnlyCollection<KLEPKeyFact>(facts);
        }

        public KLEPKeyFact AddKey(
            KLEPKeyDefinition definition,
            KLEPKeyPayload payload = null,
            KLEPKeyLifetime? lifetime = null,
            string sourceId = "")
        {
            EnsureExternalMutationAllowed();
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return GetStore(definition.Scope).CreateAndStage(
                definition, payload, lifetime, sourceId);
        }

        public bool RemoveKey(KLEPKeyFact fact)
        {
            EnsureExternalMutationAllowed();
            if (fact == null)
            {
                throw new ArgumentNullException(nameof(fact));
            }

            return GetStore(fact.Scope).StageRemove(fact);
        }

        public KLEPKeyFact ReplaceKey(
            KLEPKeyFact current,
            KLEPKeyPayload payload,
            KLEPKeyLifetime? lifetime = null,
            string sourceId = "")
        {
            EnsureExternalMutationAllowed();
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            return GetStore(current.Scope).ReplaceAndStage(
                current, payload, lifetime, sourceId);
        }

        internal void ClaimDecisionOwner(object owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (decisionOwner != null && !ReferenceEquals(decisionOwner, owner))
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' already has an Agent decision owner.");
            }

            decisionOwner = owner;
        }

        internal void EnterAgentDecisionBoundary(object owner)
        {
            if (!ReferenceEquals(decisionOwner, owner))
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' may be advanced only by its owning Agent.");
            }

            if (isAgentTicking)
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' already has an active Agent boundary.");
            }

            isAgentTicking = true;
        }

        internal void ExitAgentDecisionBoundary(object owner)
        {
            if (!ReferenceEquals(decisionOwner, owner) || !isAgentTicking)
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' has no matching Agent boundary to release.");
            }

            isAgentTicking = false;
        }

        internal void RecordCatalogMutation()
        {
            if (CatalogRevision == long.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' exhausted its Executable catalog revision.");
            }

            CatalogRevision++;
        }

        internal bool HasPendingCatalogChanges =>
            pendingRegistrations.Count > 0 || pendingRemovals.Count > 0;

        internal long GetProposedCatalogRevision()
        {
            if (!HasPendingCatalogChanges)
            {
                return CatalogRevision;
            }

            if (CatalogRevision == long.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' exhausted its Executable catalog revision.");
            }

            return CatalogRevision + 1;
        }

        internal IReadOnlyList<KLEPExecutableCatalogRoot>
            CaptureProposedCatalogRoots()
        {
            var proposed = new Dictionary<string, KLEPExecutableBase>(
                executables,
                IdComparer);
            var tenures = new Dictionary<string, string>(
                registrationTenureIds,
                IdComparer);

            foreach (string removedId in pendingRemovals)
            {
                proposed.Remove(removedId);
                tenures.Remove(removedId);
            }

            foreach (KeyValuePair<string, KLEPExecutableBase> pending in
                     pendingRegistrations)
            {
                proposed[pending.Key] = pending.Value;
                tenures[pending.Key] = pendingRegistrationTenureIds[pending.Key];
            }

            var stableIds = new List<string>(proposed.Keys);
            stableIds.Sort(IdComparer);
            var roots = new List<KLEPExecutableCatalogRoot>(stableIds.Count);
            foreach (string stableId in stableIds)
            {
                roots.Add(new KLEPExecutableCatalogRoot(
                    proposed[stableId],
                    tenures[stableId]));
            }

            return new ReadOnlyCollection<KLEPExecutableCatalogRoot>(roots);
        }

        internal void AcceptProposedCatalogRevision(long proposedRevision)
        {
            long expected = GetProposedCatalogRevision();
            if (!HasPendingCatalogChanges || proposedRevision != expected)
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' cannot accept catalog revision " +
                    $"{proposedRevision}; expected proposed revision {expected}.");
            }

            CatalogRevision = proposedRevision;
        }

        internal void RejectProposedCatalogChanges()
        {
            pendingRegistrations.Clear();
            pendingRegistrationTenureIds.Clear();
            pendingRemovals.Clear();
        }

        internal string FormatCatalogRevision(long revision)
        {
            return revision.ToString(CultureInfo.InvariantCulture);
        }

        private string AllocateRegistrationTenureId()
        {
            if (nextRegistrationTenure == long.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' exhausted its registration tenure IDs.");
            }

            nextRegistrationTenure++;
            return $"{StableId}.tenure.{nextRegistrationTenure}";
        }

        private void EnsureExternalMutationAllowed()
        {
            if (isAgentTicking)
            {
                throw new InvalidOperationException(
                    "Direct Key mutation is not allowed during KLEPAgent.Tick. " +
                    "Executable callbacks must emit buffered operations through " +
                    "their lifecycle context.");
            }
        }

        private void ValidateCanInitialize(KLEPKeyDefinition definition)
        {
            if (CycleIndex != 0)
            {
                throw new InvalidOperationException(
                    "Initial keys can only be supplied before the first Tick.");
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            KLEPKeyStore store = GetStore(definition.Scope);
            if (store.Scope == KLEPKeyScope.Global && store.LastCommittedTick >= 0)
            {
                throw new InvalidOperationException(
                    $"Global KeyStore '{store.StableId}' has already committed " +
                    $"boundary {store.LastCommittedTick}. Initial Global Keys must " +
                    "be staged before the world begins; use AddKey for later " +
                    "Global emissions.");
            }
        }

        private KLEPKeyStore GetStore(KLEPKeyScope scope)
        {
            if (scope == KLEPKeyScope.Local)
            {
                return LocalKeyStore;
            }

            return GlobalKeyStore ?? throw new InvalidOperationException(
                $"Neuron '{StableId}' requires an explicitly injected Global " +
                "KeyStore before it can add, remove, or replace Global Keys.");
        }

        private static long CheckedNextCycle(long cycleIndex)
        {
            if (cycleIndex == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The Neuron exhausted its cycle counter.");
            }

            return cycleIndex + 1;
        }

        private static void ValidateStableId(string stableId, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException(
                    "A non-empty stable ID is required.", parameterName);
            }
        }
    }
}
