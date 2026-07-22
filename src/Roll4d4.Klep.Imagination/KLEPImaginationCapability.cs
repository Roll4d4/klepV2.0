using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Imagination
{
    public enum KLEPImaginationValueKind
    {
        Boolean,
        Integer,
        Number,
        Text
    }

    /// <summary>
    /// One closed, immutable argument value admitted by the Imagination
    /// compiler. It intentionally mirrors the portable Key payload primitives
    /// without turning model arguments into Keys.
    /// </summary>
    public sealed class KLEPImaginationValue :
        IEquatable<KLEPImaginationValue>
    {
        private readonly object value;

        private KLEPImaginationValue(
            KLEPImaginationValueKind kind,
            object value)
        {
            Kind = kind;
            this.value = value;
        }

        public KLEPImaginationValueKind Kind { get; }

        public static KLEPImaginationValue FromBoolean(bool value)
        {
            return new KLEPImaginationValue(
                KLEPImaginationValueKind.Boolean,
                value);
        }

        public static KLEPImaginationValue FromInteger(long value)
        {
            return new KLEPImaginationValue(
                KLEPImaginationValueKind.Integer,
                value);
        }

        public static KLEPImaginationValue FromNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Imagination numbers must be finite.");
            }

            return new KLEPImaginationValue(
                KLEPImaginationValueKind.Number,
                value);
        }

        public static KLEPImaginationValue FromText(string value)
        {
            return new KLEPImaginationValue(
                KLEPImaginationValueKind.Text,
                value ?? throw new ArgumentNullException(nameof(value)));
        }

        public bool AsBoolean()
        {
            RequireKind(KLEPImaginationValueKind.Boolean);
            return (bool)value;
        }

        public long AsInteger()
        {
            RequireKind(KLEPImaginationValueKind.Integer);
            return (long)value;
        }

        public double AsNumber()
        {
            RequireKind(KLEPImaginationValueKind.Number);
            return (double)value;
        }

        public string AsText()
        {
            RequireKind(KLEPImaginationValueKind.Text);
            return (string)value;
        }

        public bool Equals(KLEPImaginationValue other)
        {
            return other != null &&
                   Kind == other.Kind &&
                   Equals(value, other.value);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as KLEPImaginationValue);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ value.GetHashCode();
            }
        }

        public override string ToString()
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        internal string CanonicalSignature()
        {
            switch (Kind)
            {
                case KLEPImaginationValueKind.Boolean:
                    return AsBoolean() ? "b:1" : "b:0";
                case KLEPImaginationValueKind.Integer:
                    return "i:" + AsInteger().ToString(
                        CultureInfo.InvariantCulture);
                case KLEPImaginationValueKind.Number:
                    return "n:" + AsNumber().ToString(
                        "R",
                        CultureInfo.InvariantCulture);
                case KLEPImaginationValueKind.Text:
                    return "t:" + AsText();
                default:
                    throw new InvalidOperationException(
                        "Unsupported Imagination value kind.");
            }
        }

        private void RequireKind(KLEPImaginationValueKind expected)
        {
            if (Kind != expected)
            {
                throw new InvalidOperationException(
                    $"Imagination value is {Kind}, not {expected}.");
            }
        }
    }

    /// <summary>
    /// Trusted, project-authored validation for one model-bindable argument.
    /// V1 arguments are all required; there are no hidden defaults.
    /// </summary>
    public sealed class KLEPImaginationParameterDefinition
    {
        private readonly ReadOnlyCollection<string> allowedTextValues;

        public KLEPImaginationParameterDefinition(
            string name,
            KLEPImaginationValueKind kind,
            long? minimumInteger = null,
            long? maximumInteger = null,
            double? minimumNumber = null,
            double? maximumNumber = null,
            int maximumTextLength = 512,
            IEnumerable<string> allowedTexts = null)
        {
            Name = RequireId(name, nameof(name));
            if (!Enum.IsDefined(typeof(KLEPImaginationValueKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (maximumTextLength < 1 || maximumTextLength > 4096)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumTextLength));
            }

            ValidateFinite(minimumNumber, nameof(minimumNumber));
            ValidateFinite(maximumNumber, nameof(maximumNumber));
            if (minimumInteger.HasValue && maximumInteger.HasValue &&
                minimumInteger.Value > maximumInteger.Value)
            {
                throw new ArgumentException(
                    "The minimum integer cannot exceed the maximum integer.");
            }

            if (minimumNumber.HasValue && maximumNumber.HasValue &&
                minimumNumber.Value > maximumNumber.Value)
            {
                throw new ArgumentException(
                    "The minimum number cannot exceed the maximum number.");
            }

            if (kind != KLEPImaginationValueKind.Integer &&
                (minimumInteger.HasValue || maximumInteger.HasValue))
            {
                throw new ArgumentException(
                    "Integer bounds require an Integer parameter.");
            }

            if (kind != KLEPImaginationValueKind.Number &&
                (minimumNumber.HasValue || maximumNumber.HasValue))
            {
                throw new ArgumentException(
                    "Number bounds require a Number parameter.");
            }

            Kind = kind;
            MinimumInteger = minimumInteger;
            MaximumInteger = maximumInteger;
            MinimumNumber = minimumNumber;
            MaximumNumber = maximumNumber;
            MaximumTextLength = maximumTextLength;

            var allowed = new List<string>();
            if (allowedTexts != null)
            {
                foreach (string item in allowedTexts)
                {
                    if (item == null)
                    {
                        throw new ArgumentException(
                            "Allowed text values cannot contain null.",
                            nameof(allowedTexts));
                    }

                    if (item.Length > maximumTextLength)
                    {
                        throw new ArgumentException(
                            $"Allowed text '{item}' exceeds the maximum length.",
                            nameof(allowedTexts));
                    }

                    allowed.Add(item);
                }
            }

            if (kind != KLEPImaginationValueKind.Text && allowed.Count != 0)
            {
                throw new ArgumentException(
                    "Allowed text values require a Text parameter.",
                    nameof(allowedTexts));
            }

            allowed.Sort(StringComparer.Ordinal);
            for (int index = 1; index < allowed.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(
                        allowed[index - 1],
                        allowed[index]))
                {
                    throw new ArgumentException(
                        $"Allowed text '{allowed[index]}' is duplicated.",
                        nameof(allowedTexts));
                }
            }

            allowedTextValues = new ReadOnlyCollection<string>(allowed);
        }

        public string Name { get; }
        public KLEPImaginationValueKind Kind { get; }
        public long? MinimumInteger { get; }
        public long? MaximumInteger { get; }
        public double? MinimumNumber { get; }
        public double? MaximumNumber { get; }
        public int MaximumTextLength { get; }
        public IReadOnlyList<string> AllowedTextValues => allowedTextValues;

        public void Validate(KLEPImaginationValue candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (candidate.Kind != Kind)
            {
                throw new ArgumentException(
                    $"Argument '{Name}' requires {Kind}, not {candidate.Kind}.",
                    nameof(candidate));
            }

            switch (Kind)
            {
                case KLEPImaginationValueKind.Integer:
                    long integer = candidate.AsInteger();
                    if (MinimumInteger.HasValue &&
                        integer < MinimumInteger.Value)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(candidate),
                            $"Argument '{Name}' is below {MinimumInteger.Value}.");
                    }

                    if (MaximumInteger.HasValue &&
                        integer > MaximumInteger.Value)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(candidate),
                            $"Argument '{Name}' exceeds {MaximumInteger.Value}.");
                    }

                    break;
                case KLEPImaginationValueKind.Number:
                    double number = candidate.AsNumber();
                    if (MinimumNumber.HasValue && number < MinimumNumber.Value)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(candidate),
                            $"Argument '{Name}' is below {MinimumNumber.Value}.");
                    }

                    if (MaximumNumber.HasValue && number > MaximumNumber.Value)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(candidate),
                            $"Argument '{Name}' exceeds {MaximumNumber.Value}.");
                    }

                    break;
                case KLEPImaginationValueKind.Text:
                    string text = candidate.AsText();
                    if (text.Length > MaximumTextLength)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(candidate),
                            $"Argument '{Name}' exceeds {MaximumTextLength} characters.");
                    }

                    if (allowedTextValues.Count != 0 &&
                        !ContainsAllowedText(text))
                    {
                        throw new ArgumentException(
                            $"Argument '{Name}' contains an unapproved text value.",
                            nameof(candidate));
                    }

                    break;
            }
        }

        internal string CanonicalSignature()
        {
            var builder = new StringBuilder();
            builder.Append(Name).Append('|').Append((int)Kind).Append('|');
            builder.Append(MinimumInteger.HasValue
                ? MinimumInteger.Value.ToString(CultureInfo.InvariantCulture)
                : "-");
            builder.Append('|');
            builder.Append(MaximumInteger.HasValue
                ? MaximumInteger.Value.ToString(CultureInfo.InvariantCulture)
                : "-");
            builder.Append('|');
            builder.Append(MinimumNumber.HasValue
                ? MinimumNumber.Value.ToString("R", CultureInfo.InvariantCulture)
                : "-");
            builder.Append('|');
            builder.Append(MaximumNumber.HasValue
                ? MaximumNumber.Value.ToString("R", CultureInfo.InvariantCulture)
                : "-");
            builder.Append('|').Append(MaximumTextLength);
            foreach (string item in allowedTextValues)
            {
                builder.Append('|').Append(item);
            }

            return builder.ToString();
        }

        private bool ContainsAllowedText(string value)
        {
            int low = 0;
            int high = allowedTextValues.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = StringComparer.Ordinal.Compare(
                    allowedTextValues[middle],
                    value);
                if (comparison == 0)
                {
                    return true;
                }

                if (comparison < 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return false;
        }

        private static void ValidateFinite(double? value, string name)
        {
            if (value.HasValue &&
                (double.IsNaN(value.Value) || double.IsInfinity(value.Value)))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static string RequireId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty parameter name is required.",
                    name);
            }

            return value;
        }
    }

    public abstract class KLEPImaginationCapabilityRuntime
    {
        private object materializationOwner;

        public virtual void Enter(KLEPImaginationCapabilityContext context)
        {
        }

        public abstract KLEPImaginationCapabilityResult Tick(
            KLEPImaginationCapabilityContext context);

        public virtual void Exit(KLEPImaginationCapabilityExitContext context)
        {
        }

        public virtual void Cleanup(
            KLEPImaginationCapabilityExitContext context)
        {
        }

        internal void ClaimMaterialization(object owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (materializationOwner != null)
            {
                throw new InvalidOperationException(
                    "A capability runtime instance cannot be shared by two " +
                    "materialized Executables.");
            }

            materializationOwner = owner;
        }
    }

    public interface IKLEPImaginationCapabilityRuntimeFactory
    {
        KLEPImaginationCapabilityRuntime CreateRuntime(
            IReadOnlyDictionary<string, KLEPImaginationValue> arguments);
    }

    /// <summary>
    /// Trusted descriptor data. The model never supplies or alters this object.
    /// </summary>
    public sealed class KLEPImaginationCapabilityDescriptor
    {
        private readonly ReadOnlyCollection<KLEPImaginationParameterDefinition>
            parameters;
        private readonly ReadOnlyDictionary<
            string,
            KLEPImaginationParameterDefinition> parametersByName;

        public KLEPImaginationCapabilityDescriptor(
            string stableId,
            string version,
            string descriptorFingerprint,
            KLEPExecutableDefinition definitionTemplate,
            IEnumerable<KLEPImaginationParameterDefinition> argumentSchema = null)
        {
            StableId = RequireId(stableId, nameof(stableId));
            Version = RequireId(version, nameof(version));
            DescriptorFingerprint = RequireId(
                descriptorFingerprint,
                nameof(descriptorFingerprint));
            DefinitionTemplate = definitionTemplate ??
                throw new ArgumentNullException(nameof(definitionTemplate));

            if (definitionTemplate.Kind == KLEPExecutableKind.Goal ||
                definitionTemplate.ExecutionMode != KLEPExecutionMode.Solo)
            {
                throw new ArgumentException(
                    "Imagination V1 capabilities must materialize root Solo " +
                    "non-Goal Executables.",
                    nameof(definitionTemplate));
            }

            var copy = new List<KLEPImaginationParameterDefinition>();
            if (argumentSchema != null)
            {
                foreach (KLEPImaginationParameterDefinition parameter in
                         argumentSchema)
                {
                    copy.Add(parameter ?? throw new ArgumentException(
                        "A capability argument schema cannot contain null.",
                        nameof(argumentSchema)));
                }
            }

            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.Name,
                right.Name));
            var byName = new Dictionary<
                string,
                KLEPImaginationParameterDefinition>(StringComparer.Ordinal);
            foreach (KLEPImaginationParameterDefinition parameter in copy)
            {
                if (byName.ContainsKey(parameter.Name))
                {
                    throw new ArgumentException(
                        $"Capability argument '{parameter.Name}' is duplicated.",
                        nameof(argumentSchema));
                }

                byName.Add(parameter.Name, parameter);
            }

            parameters = new ReadOnlyCollection<
                KLEPImaginationParameterDefinition>(copy);
            parametersByName = new ReadOnlyDictionary<
                string,
                KLEPImaginationParameterDefinition>(byName);
        }

        public string StableId { get; }
        public string Version { get; }
        public string DescriptorFingerprint { get; }
        public KLEPExecutableDefinition DefinitionTemplate { get; }
        public IReadOnlyList<KLEPImaginationParameterDefinition> Parameters =>
            parameters;

        public bool TryGetParameter(
            string name,
            out KLEPImaginationParameterDefinition parameter)
        {
            return parametersByName.TryGetValue(name, out parameter);
        }

        internal string CanonicalSignature()
        {
            var builder = new StringBuilder();
            builder.Append(StableId).Append('|')
                .Append(Version).Append('|')
                .Append(DescriptorFingerprint).Append('|')
                .Append((int)DefinitionTemplate.Kind).Append('|')
                .Append((int)DefinitionTemplate.ExecutionMode).Append('|')
                .Append(DefinitionTemplate.BaseAttractiveness.ToString(
                    "R",
                    CultureInfo.InvariantCulture));
            foreach (KLEPImaginationParameterDefinition parameter in parameters)
            {
                builder.Append("||").Append(parameter.CanonicalSignature());
            }

            return builder.ToString();
        }

        private static string RequireId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable identity is required.",
                    name);
            }

            return value;
        }
    }

    public sealed class KLEPImaginationCapabilityRegistration
    {
        public KLEPImaginationCapabilityRegistration(
            KLEPImaginationCapabilityDescriptor descriptor,
            IKLEPImaginationCapabilityRuntimeFactory runtimeFactory)
        {
            Descriptor = descriptor ??
                throw new ArgumentNullException(nameof(descriptor));
            RuntimeFactory = runtimeFactory ??
                throw new ArgumentNullException(nameof(runtimeFactory));
        }

        public KLEPImaginationCapabilityDescriptor Descriptor { get; }
        public IKLEPImaginationCapabilityRuntimeFactory RuntimeFactory { get; }
    }

    public interface IKLEPImaginationCapabilityCatalog
    {
        string Fingerprint { get; }

        bool TryResolve(
            string capabilityId,
            string version,
            out KLEPImaginationCapabilityRegistration registration);
    }

    public sealed class KLEPImaginationCapabilityCatalog :
        IKLEPImaginationCapabilityCatalog
    {
        private readonly ReadOnlyDictionary<
            string,
            KLEPImaginationCapabilityRegistration> registrations;

        public KLEPImaginationCapabilityCatalog(
            IEnumerable<KLEPImaginationCapabilityRegistration> capabilities)
        {
            if (capabilities == null)
            {
                throw new ArgumentNullException(nameof(capabilities));
            }

            var ordered = new List<KLEPImaginationCapabilityRegistration>();
            foreach (KLEPImaginationCapabilityRegistration registration in
                     capabilities)
            {
                ordered.Add(registration ?? throw new ArgumentException(
                    "A capability catalog cannot contain null.",
                    nameof(capabilities)));
            }

            ordered.Sort((left, right) => CompareDescriptors(
                left.Descriptor,
                right.Descriptor));
            var byKey = new Dictionary<
                string,
                KLEPImaginationCapabilityRegistration>(StringComparer.Ordinal);
            var fingerprintSource = new StringBuilder();
            foreach (KLEPImaginationCapabilityRegistration registration in ordered)
            {
                string key = MakeKey(
                    registration.Descriptor.StableId,
                    registration.Descriptor.Version);
                if (byKey.ContainsKey(key))
                {
                    throw new ArgumentException(
                        $"Capability '{registration.Descriptor.StableId}' " +
                        $"version '{registration.Descriptor.Version}' is duplicated.",
                        nameof(capabilities));
                }

                byKey.Add(key, registration);
                fingerprintSource.Append(registration.Descriptor.CanonicalSignature())
                    .Append('\n');
            }

            registrations = new ReadOnlyDictionary<
                string,
                KLEPImaginationCapabilityRegistration>(byKey);
            Fingerprint = KLEPImaginationHash.Compute(
                fingerprintSource.ToString());
        }

        public string Fingerprint { get; }

        public bool TryResolve(
            string capabilityId,
            string version,
            out KLEPImaginationCapabilityRegistration registration)
        {
            if (string.IsNullOrWhiteSpace(capabilityId) ||
                string.IsNullOrWhiteSpace(version))
            {
                registration = null;
                return false;
            }

            return registrations.TryGetValue(
                MakeKey(capabilityId, version),
                out registration);
        }

        private static int CompareDescriptors(
            KLEPImaginationCapabilityDescriptor left,
            KLEPImaginationCapabilityDescriptor right)
        {
            int byId = StringComparer.Ordinal.Compare(
                left.StableId,
                right.StableId);
            return byId != 0
                ? byId
                : StringComparer.Ordinal.Compare(left.Version, right.Version);
        }

        private static string MakeKey(string capabilityId, string version)
        {
            return capabilityId + "\u001f" + version;
        }
    }

    internal static class KLEPImaginationHash
    {
        internal static string Compute(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(bytes);
            }

            var builder = new StringBuilder(hash.Length * 2 + 7);
            builder.Append("sha256:");
            for (int index = 0; index < hash.Length; index++)
            {
                builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
