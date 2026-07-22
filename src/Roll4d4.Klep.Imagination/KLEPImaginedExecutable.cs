using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Imagination
{
    public sealed class KLEPImaginationCapabilityContext
    {
        internal KLEPImaginationCapabilityContext(
            KLEPExecutionContext context,
            IReadOnlyDictionary<string, KLEPImaginationValue> arguments)
        {
            ExecutableStableId = context.ExecutableStableId;
            CycleIndex = context.CycleIndex;
            WaveIndex = context.WaveIndex;
            RunIndex = context.RunIndex;
            Keys = context.Keys;
            Arguments = arguments;
        }

        public string ExecutableStableId { get; }
        public long CycleIndex { get; }
        public int WaveIndex { get; }
        public long RunIndex { get; }
        public KLEPKeySnapshot Keys { get; }
        public IReadOnlyDictionary<string, KLEPImaginationValue> Arguments
        {
            get;
        }
    }

    public sealed class KLEPImaginationCapabilityExitContext
    {
        internal KLEPImaginationCapabilityExitContext(
            KLEPExecutableExitContext context,
            IReadOnlyDictionary<string, KLEPImaginationValue> arguments)
        {
            ExecutableStableId = context.ExecutableStableId;
            CycleIndex = context.CycleIndex;
            WaveIndex = context.WaveIndex;
            RunIndex = context.RunIndex;
            Keys = context.Keys;
            TerminalState = context.TerminalState;
            Reason = context.Reason;
            Arguments = arguments;
        }

        public string ExecutableStableId { get; }
        public long CycleIndex { get; }
        public int WaveIndex { get; }
        public long RunIndex { get; }
        public KLEPKeySnapshot Keys { get; }
        public KLEPExecutableState TerminalState { get; }
        public KLEPExecutableExitReason Reason { get; }
        public IReadOnlyDictionary<string, KLEPImaginationValue> Arguments
        {
            get;
        }
    }

    /// <summary>
    /// Capability-authored intent data. The generic wrapper later binds this
    /// payload to exact Executable/cycle/run identity.
    /// </summary>
    public sealed class KLEPImaginationIntentPayload
    {
        private readonly ReadOnlyDictionary<
            string,
            KLEPImaginationValue> fields;

        public KLEPImaginationIntentPayload(
            IEnumerable<KeyValuePair<string, KLEPImaginationValue>> values = null)
        {
            var copy = new Dictionary<string, KLEPImaginationValue>(
                StringComparer.Ordinal);
            if (values != null)
            {
                foreach (KeyValuePair<string, KLEPImaginationValue> pair in values)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 128)
                    {
                        throw new ArgumentException(
                            "Intent field names require 1-128 characters.",
                            nameof(values));
                    }

                    copy.Add(
                        pair.Key,
                        pair.Value ?? throw new ArgumentException(
                            "Intent values cannot be null.",
                            nameof(values)));
                }
            }

            if (copy.Count > 64)
            {
                throw new ArgumentException(
                    "An Imagination intent may contain at most 64 fields.",
                    nameof(values));
            }

            fields = new ReadOnlyDictionary<string, KLEPImaginationValue>(copy);
        }

        public IReadOnlyDictionary<string, KLEPImaginationValue> Fields =>
            fields;
    }

    public sealed class KLEPImaginationIntent
    {
        internal KLEPImaginationIntent(
            KLEPImaginationManifest manifest,
            long cycleIndex,
            long runIndex,
            KLEPImaginationIntentPayload payload)
        {
            CapabilityId = manifest.CapabilityId;
            CapabilityVersion = manifest.CapabilityVersion;
            ExecutableStableId = manifest.ExecutableStableId;
            ProposalFingerprint = manifest.ProposalFingerprint;
            CycleIndex = cycleIndex;
            RunIndex = runIndex;
            Payload = payload;
        }

        public string CapabilityId { get; }
        public string CapabilityVersion { get; }
        public string ExecutableStableId { get; }
        public string ProposalFingerprint { get; }
        public long CycleIndex { get; }
        public long RunIndex { get; }
        public KLEPImaginationIntentPayload Payload { get; }
    }

    public sealed class KLEPImaginationSuccessfulOutput
    {
        public KLEPImaginationSuccessfulOutput(
            KLEPKeyId keyId,
            KLEPKeyPayload payload = null)
        {
            if (string.IsNullOrWhiteSpace(keyId.Value))
            {
                throw new ArgumentException(
                    "A successful output requires a stable Key ID.",
                    nameof(keyId));
            }

            KeyId = keyId;
            Payload = payload ?? KLEPKeyPayload.Empty;
        }

        public KLEPKeyId KeyId { get; }
        public KLEPKeyPayload Payload { get; }
    }

    public sealed class KLEPImaginationCapabilityResult
    {
        private readonly ReadOnlyCollection<KLEPImaginationSuccessfulOutput>
            successfulOutputs;

        private KLEPImaginationCapabilityResult(
            KLEPExecutableTickStatus status,
            KLEPImaginationIntentPayload intent,
            IEnumerable<KLEPImaginationSuccessfulOutput> outputs)
        {
            if (!Enum.IsDefined(typeof(KLEPExecutableTickStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            var copy = new List<KLEPImaginationSuccessfulOutput>();
            if (outputs != null)
            {
                foreach (KLEPImaginationSuccessfulOutput output in outputs)
                {
                    copy.Add(output ?? throw new ArgumentException(
                        "A capability result cannot contain a null output.",
                        nameof(outputs)));
                }
            }

            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.KeyId.Value,
                right.KeyId.Value));
            for (int index = 1; index < copy.Count; index++)
            {
                if (copy[index - 1].KeyId == copy[index].KeyId)
                {
                    throw new ArgumentException(
                        $"Successful output '{copy[index].KeyId}' is duplicated.",
                        nameof(outputs));
                }
            }

            if (status != KLEPExecutableTickStatus.Succeeded && copy.Count != 0)
            {
                throw new ArgumentException(
                    "Only a Succeeded capability result may contain outputs.",
                    nameof(outputs));
            }

            if (status == KLEPExecutableTickStatus.Failed && intent != null)
            {
                throw new ArgumentException(
                    "A Failed capability result cannot publish a host intent.",
                    nameof(intent));
            }

            Status = status;
            Intent = intent;
            successfulOutputs = new ReadOnlyCollection<
                KLEPImaginationSuccessfulOutput>(copy);
        }

        public KLEPExecutableTickStatus Status { get; }
        public KLEPImaginationIntentPayload Intent { get; }
        public IReadOnlyList<KLEPImaginationSuccessfulOutput> SuccessfulOutputs =>
            successfulOutputs;

        public static KLEPImaginationCapabilityResult Running(
            KLEPImaginationIntentPayload intent = null)
        {
            return new KLEPImaginationCapabilityResult(
                KLEPExecutableTickStatus.Running,
                intent,
                null);
        }

        public static KLEPImaginationCapabilityResult Succeeded(
            IEnumerable<KLEPImaginationSuccessfulOutput> outputs = null,
            KLEPImaginationIntentPayload intent = null)
        {
            return new KLEPImaginationCapabilityResult(
                KLEPExecutableTickStatus.Succeeded,
                intent,
                outputs);
        }

        public static KLEPImaginationCapabilityResult Failed()
        {
            return new KLEPImaginationCapabilityResult(
                KLEPExecutableTickStatus.Failed,
                null,
                null);
        }
    }

    public sealed class KLEPImaginedExecutable : KLEPExecutableBase
    {
        private readonly KLEPImaginationManifest manifest;
        private readonly KLEPImaginationCapabilityRuntime capabilityRuntime;

        internal KLEPImaginedExecutable(
            KLEPImaginationManifest manifest,
            KLEPImaginationCapabilityRuntime capabilityRuntime)
            : base((manifest ?? throw new ArgumentNullException(nameof(manifest)))
                .Definition)
        {
            this.manifest = manifest;
            this.capabilityRuntime = capabilityRuntime ??
                throw new ArgumentNullException(nameof(capabilityRuntime));
            capabilityRuntime.ClaimMaterialization(this);
        }

        public KLEPImaginationManifest Manifest => manifest;
        public KLEPImaginationIntent LastIntent { get; private set; }

        public bool TryGetIntent(
            long cycleIndex,
            out KLEPImaginationIntent intent)
        {
            intent = LastIntent != null && LastIntent.CycleIndex == cycleIndex
                ? LastIntent
                : null;
            return intent != null;
        }

        protected override void OnEnter(KLEPExecutionContext context)
        {
            LastIntent = null;
            capabilityRuntime.Enter(new KLEPImaginationCapabilityContext(
                context,
                manifest.Arguments));
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            KLEPImaginationCapabilityResult result = capabilityRuntime.Tick(
                new KLEPImaginationCapabilityContext(
                    context,
                    manifest.Arguments));
            if (result == null)
            {
                throw InvalidResult(
                    "The capability runtime returned no result.");
            }

            if (result.Intent != null)
            {
                LastIntent = new KLEPImaginationIntent(
                    manifest,
                    context.CycleIndex,
                    context.RunIndex,
                    result.Intent);
            }

            if (result.Status == KLEPExecutableTickStatus.Succeeded)
            {
                EmitSuccessfulOutputs(context, result.SuccessfulOutputs);
            }
            else if (result.SuccessfulOutputs.Count != 0)
            {
                throw InvalidResult(
                    "A non-successful capability result contained outputs.");
            }

            return result.Status;
        }

        protected override void OnExit(KLEPExecutableExitContext context)
        {
            capabilityRuntime.Exit(new KLEPImaginationCapabilityExitContext(
                context,
                manifest.Arguments));
        }

        protected override void OnCleanup(KLEPExecutableExitContext context)
        {
            capabilityRuntime.Cleanup(
                new KLEPImaginationCapabilityExitContext(
                    context,
                    manifest.Arguments));
        }

        private void EmitSuccessfulOutputs(
            KLEPExecutionContext context,
            IReadOnlyList<KLEPImaginationSuccessfulOutput> outputs)
        {
            if (outputs.Count != DeclaredOutputs.Count)
            {
                throw InvalidResult(
                    $"Capability success supplied {outputs.Count} outputs, " +
                    $"but descriptor '{manifest.CapabilityId}' guarantees " +
                    $"{DeclaredOutputs.Count}.");
            }

            var byId = new Dictionary<
                KLEPKeyId,
                KLEPImaginationSuccessfulOutput>();
            foreach (KLEPImaginationSuccessfulOutput output in outputs)
            {
                if (byId.ContainsKey(output.KeyId))
                {
                    throw InvalidResult(
                        $"Capability output '{output.KeyId}' is duplicated.");
                }

                byId.Add(output.KeyId, output);
            }

            foreach (KLEPKeyDefinition definition in DeclaredOutputs)
            {
                if (!byId.TryGetValue(
                        definition.Id,
                        out KLEPImaginationSuccessfulOutput output))
                {
                    throw InvalidResult(
                        $"Capability success omitted guaranteed Key " +
                        $"'{definition.Id}'.");
                }

                context.Add(definition, output.Payload);
                byId.Remove(definition.Id);
            }

            if (byId.Count != 0)
            {
                foreach (KLEPKeyId extra in byId.Keys)
                {
                    throw InvalidResult(
                        $"Capability success supplied undeclared Key '{extra}'.");
                }
            }
        }

        private static KLEPImaginationRejectedException InvalidResult(
            string message)
        {
            return new KLEPImaginationRejectedException(
                KLEPImaginationRejectionCode.InvalidCapabilityResult,
                message);
        }
    }

    public sealed class KLEPImaginationMaterializer
    {
        public KLEPImaginedExecutable Materialize(
            KLEPImaginationManifest manifest,
            IKLEPImaginationCapabilityCatalog capabilityCatalog)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (capabilityCatalog == null)
            {
                throw new ArgumentNullException(nameof(capabilityCatalog));
            }

            if (!StringComparer.Ordinal.Equals(
                    manifest.CapabilityCatalogFingerprint,
                    capabilityCatalog.Fingerprint) ||
                !capabilityCatalog.TryResolve(
                    manifest.CapabilityId,
                    manifest.CapabilityVersion,
                    out KLEPImaginationCapabilityRegistration registration) ||
                !StringComparer.Ordinal.Equals(
                    manifest.CapabilityDescriptorFingerprint,
                    registration.Descriptor.DescriptorFingerprint))
            {
                throw new KLEPImaginationRejectedException(
                    KLEPImaginationRejectionCode.StaleCapabilityBinding,
                    "The Manifest no longer matches the admitted capability " +
                    "catalog and descriptor fingerprints.");
            }

            KLEPImaginationCapabilityRuntime runtime =
                registration.RuntimeFactory.CreateRuntime(manifest.Arguments);
            if (runtime == null)
            {
                throw new KLEPImaginationRejectedException(
                    KLEPImaginationRejectionCode.InvalidCapabilityResult,
                    $"Capability '{manifest.CapabilityId}' created no runtime.");
            }

            return new KLEPImaginedExecutable(manifest, runtime);
        }
    }
}
