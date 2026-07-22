using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Imagination
{
    public enum KLEPImaginationProposalKind
    {
        StrongManifest,
        WeakConjecture
    }

    public enum KLEPImaginationRejectionCode
    {
        DocumentTooLarge,
        MalformedJson,
        UnsupportedSchema,
        MissingProperty,
        UnknownProperty,
        DuplicateProperty,
        InvalidValue,
        UnknownCapability,
        InvalidArgument,
        WeakConjectureNotRunnable,
        StaleCapabilityBinding,
        InvalidCapabilityResult
    }

    public sealed class KLEPImaginationRejectedException : Exception
    {
        public KLEPImaginationRejectedException(
            KLEPImaginationRejectionCode code,
            string message,
            Exception innerException = null)
            : base(message, innerException)
        {
            Code = code;
        }

        public KLEPImaginationRejectionCode Code { get; }
    }

    /// <summary>
    /// Immutable evidence supplied to a model adapter. It contains no Neuron,
    /// Agent, mutable catalog, or callback.
    /// </summary>
    public sealed class KLEPImaginationRequest
    {
        private readonly ReadOnlyCollection<string> availableCapabilityBindings;

        public KLEPImaginationRequest(
            string requestFingerprint,
            string acceptedCatalogFingerprint,
            string capabilityCatalogFingerprint,
            string targetKeyId,
            string noSolutionExplanation,
            IEnumerable<string> capabilityBindings)
        {
            RequestFingerprint = Require(
                requestFingerprint,
                nameof(requestFingerprint));
            AcceptedCatalogFingerprint = Require(
                acceptedCatalogFingerprint,
                nameof(acceptedCatalogFingerprint));
            CapabilityCatalogFingerprint = Require(
                capabilityCatalogFingerprint,
                nameof(capabilityCatalogFingerprint));
            TargetKeyId = Require(targetKeyId, nameof(targetKeyId));
            NoSolutionExplanation = noSolutionExplanation ?? string.Empty;

            var copy = new List<string>();
            if (capabilityBindings != null)
            {
                foreach (string binding in capabilityBindings)
                {
                    copy.Add(Require(binding, nameof(capabilityBindings)));
                }
            }

            copy.Sort(StringComparer.Ordinal);
            availableCapabilityBindings = new ReadOnlyCollection<string>(copy);
        }

        public string RequestFingerprint { get; }
        public string AcceptedCatalogFingerprint { get; }
        public string CapabilityCatalogFingerprint { get; }
        public string TargetKeyId { get; }
        public string NoSolutionExplanation { get; }
        public IReadOnlyList<string> AvailableCapabilityBindings =>
            availableCapabilityBindings;

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty Imagination request value is required.",
                    name);
            }

            return value;
        }
    }

    /// <summary>
    /// Optional adapters may be local or remote. They return text only and are
    /// never invoked by KLEPAgent.Tick.
    /// </summary>
    public interface IKLEPImaginationModelAdapter
    {
        string StableAdapterId { get; }

        Task<string> ProposeAsync(
            KLEPImaginationRequest request,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Non-runnable proposal evidence. There is deliberately no conversion or
    /// materialization API on this type.
    /// </summary>
    public sealed class KLEPImaginationConjecture
    {
        internal KLEPImaginationConjecture(
            string requestFingerprint,
            string title,
            string conjecture,
            string canonicalJson,
            string fingerprint)
        {
            RequestFingerprint = requestFingerprint;
            Title = title;
            Text = conjecture;
            CanonicalJson = canonicalJson;
            Fingerprint = fingerprint;
        }

        public KLEPImaginationProposalKind Kind =>
            KLEPImaginationProposalKind.WeakConjecture;
        public string RequestFingerprint { get; }
        public string Title { get; }
        public string Text { get; }
        public string CanonicalJson { get; }
        public string Fingerprint { get; }
    }

    public sealed class KLEPImaginationManifest
    {
        private readonly ReadOnlyDictionary<
            string,
            KLEPImaginationValue> arguments;

        internal KLEPImaginationManifest(
            string requestFingerprint,
            string displayName,
            string targetKeyId,
            string explanation,
            string proposalFingerprint,
            string canonicalJson,
            string capabilityCatalogFingerprint,
            KLEPImaginationCapabilityDescriptor descriptor,
            IDictionary<string, KLEPImaginationValue> validatedArguments,
            KLEPExecutableDefinition definition)
        {
            RequestFingerprint = requestFingerprint;
            DisplayName = displayName;
            TargetKeyId = targetKeyId;
            Explanation = explanation;
            ProposalFingerprint = proposalFingerprint;
            CanonicalJson = canonicalJson;
            CapabilityCatalogFingerprint = capabilityCatalogFingerprint;
            CapabilityId = descriptor.StableId;
            CapabilityVersion = descriptor.Version;
            CapabilityDescriptorFingerprint =
                descriptor.DescriptorFingerprint;
            Definition = definition;
            arguments = new ReadOnlyDictionary<
                string,
                KLEPImaginationValue>(
                    new Dictionary<string, KLEPImaginationValue>(
                        validatedArguments,
                        StringComparer.Ordinal));
        }

        public KLEPImaginationProposalKind Kind =>
            KLEPImaginationProposalKind.StrongManifest;
        public string RequestFingerprint { get; }
        public string DisplayName { get; }
        public string TargetKeyId { get; }
        public string Explanation { get; }
        public string ProposalFingerprint { get; }
        public string CanonicalJson { get; }
        public string CapabilityCatalogFingerprint { get; }
        public string CapabilityId { get; }
        public string CapabilityVersion { get; }
        public string CapabilityDescriptorFingerprint { get; }
        public IReadOnlyDictionary<string, KLEPImaginationValue> Arguments =>
            arguments;
        public KLEPExecutableDefinition Definition { get; }
        public string ExecutableStableId => Definition.StableId;
    }
}
