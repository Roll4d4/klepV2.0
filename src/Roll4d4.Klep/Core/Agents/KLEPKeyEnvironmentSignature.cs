using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Roll4d4.Klep.Core
{
    public readonly struct KLEPKeyEnvironmentEntry :
        IEquatable<KLEPKeyEnvironmentEntry>,
        IComparable<KLEPKeyEnvironmentEntry>
    {
        public KLEPKeyEnvironmentEntry(KLEPKeyScope scope, KLEPKeyId keyId)
        {
            if (!Enum.IsDefined(typeof(KLEPKeyScope), scope))
            {
                throw new ArgumentOutOfRangeException(nameof(scope));
            }

            if (string.IsNullOrWhiteSpace(keyId.Value))
            {
                throw new ArgumentException("A valid stable Key ID is required.", nameof(keyId));
            }

            Scope = scope;
            KeyId = keyId;
        }

        public KLEPKeyScope Scope { get; }
        public KLEPKeyId KeyId { get; }

        public int CompareTo(KLEPKeyEnvironmentEntry other)
        {
            int scopeComparison = Scope.CompareTo(other.Scope);
            return scopeComparison != 0
                ? scopeComparison
                : KeyId.CompareTo(other.KeyId);
        }

        public bool Equals(KLEPKeyEnvironmentEntry other) =>
            Scope == other.Scope && KeyId == other.KeyId;
        public override bool Equals(object obj) =>
            obj is KLEPKeyEnvironmentEntry other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Scope * 397) ^ KeyId.GetHashCode();
            }
        }

        public override string ToString() => $"{Scope}:{KeyId}";
    }

    /// <summary>
    /// Canonical Key-presence state used by Agent learning. Payloads and exact
    /// occurrences deliberately do not participate in equality.
    /// </summary>
    public sealed class KLEPKeyEnvironmentSignature :
        IEquatable<KLEPKeyEnvironmentSignature>,
        IComparable<KLEPKeyEnvironmentSignature>
    {
        private readonly ReadOnlyCollection<KLEPKeyEnvironmentEntry> entries;

        private KLEPKeyEnvironmentSignature(
            List<KLEPKeyEnvironmentEntry> entries)
        {
            entries.Sort();
            this.entries = new ReadOnlyCollection<KLEPKeyEnvironmentEntry>(entries);
            CanonicalId = BuildCanonicalId(entries);
        }

        public static KLEPKeyEnvironmentSignature Empty { get; } =
            new KLEPKeyEnvironmentSignature(new List<KLEPKeyEnvironmentEntry>());

        public IReadOnlyList<KLEPKeyEnvironmentEntry> Entries => entries;
        public string CanonicalId { get; }

        public static KLEPKeyEnvironmentSignature FromSnapshot(
            KLEPKeySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var seen = new HashSet<KLEPKeyEnvironmentEntry>();
            var copy = new List<KLEPKeyEnvironmentEntry>();
            foreach (KLEPKeyFact fact in snapshot.Facts)
            {
                var entry = new KLEPKeyEnvironmentEntry(fact.Scope, fact.KeyId);
                if (seen.Add(entry))
                {
                    copy.Add(entry);
                }
            }

            return copy.Count == 0
                ? Empty
                : new KLEPKeyEnvironmentSignature(copy);
        }

        public int CompareTo(KLEPKeyEnvironmentSignature other)
        {
            return other == null
                ? 1
                : StringComparer.Ordinal.Compare(CanonicalId, other.CanonicalId);
        }

        public bool Equals(KLEPKeyEnvironmentSignature other) =>
            other != null &&
            StringComparer.Ordinal.Equals(CanonicalId, other.CanonicalId);
        public override bool Equals(object obj) =>
            obj is KLEPKeyEnvironmentSignature other && Equals(other);
        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(CanonicalId);
        public override string ToString() => CanonicalId;

        private static string BuildCanonicalId(
            IReadOnlyList<KLEPKeyEnvironmentEntry> source)
        {
            if (source.Count == 0)
            {
                return "<empty>";
            }

            var text = new StringBuilder();
            foreach (KLEPKeyEnvironmentEntry entry in source)
            {
                string keyId = entry.KeyId.Value;
                text.Append(entry.Scope == KLEPKeyScope.Local ? 'L' : 'G')
                    .Append(':')
                    .Append(keyId.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(keyId)
                    .Append(';');
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// Canonical evidence identity used only to bind guidance to the visible
    /// Key payloads that supported it. This is deliberately separate from the
    /// presence-only environment signature used by Locks and Agent learning.
    /// Occurrence authority, provenance, and timing metadata do not participate.
    /// </summary>
    public sealed class KLEPGuidanceEvidenceFingerprint :
        IEquatable<KLEPGuidanceEvidenceFingerprint>,
        IComparable<KLEPGuidanceEvidenceFingerprint>
    {
        private KLEPGuidanceEvidenceFingerprint(string canonicalId)
        {
            CanonicalId = canonicalId ?? throw new ArgumentNullException(nameof(canonicalId));
        }

        public static KLEPGuidanceEvidenceFingerprint Empty { get; } =
            new KLEPGuidanceEvidenceFingerprint("<empty>");

        public string CanonicalId { get; }

        public static KLEPGuidanceEvidenceFingerprint FromSnapshot(
            KLEPKeySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.Facts.Count == 0)
            {
                return Empty;
            }

            var factProjections = new List<string>(snapshot.Facts.Count);
            foreach (KLEPKeyFact fact in snapshot.Facts)
            {
                var projection = new StringBuilder();
                projection.Append(fact.Scope == KLEPKeyScope.Local ? 'L' : 'G');
                AppendToken(projection, fact.KeyId.Value);
                projection.Append(fact.Payload.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(':');
                foreach (KLEPKeyField field in fact.Payload.Fields)
                {
                    AppendToken(projection, field.Name);
                    AppendValue(projection, field.Value);
                }

                factProjections.Add(projection.ToString());
            }

            // Duplicate payload occurrences remain visible evidence, but their
            // opaque occurrence IDs and arrival order do not affect identity.
            factProjections.Sort(StringComparer.Ordinal);
            var canonical = new StringBuilder();
            foreach (string projection in factProjections)
            {
                AppendToken(canonical, projection);
            }

            return new KLEPGuidanceEvidenceFingerprint(canonical.ToString());
        }

        public int CompareTo(KLEPGuidanceEvidenceFingerprint other)
        {
            return other == null
                ? 1
                : StringComparer.Ordinal.Compare(CanonicalId, other.CanonicalId);
        }

        public bool Equals(KLEPGuidanceEvidenceFingerprint other) =>
            other != null &&
            StringComparer.Ordinal.Equals(CanonicalId, other.CanonicalId);

        public override bool Equals(object obj) =>
            obj is KLEPGuidanceEvidenceFingerprint other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(CanonicalId);

        public override string ToString() => CanonicalId;

        private static void AppendToken(StringBuilder destination, string value)
        {
            value = value ?? string.Empty;
            destination.Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(value)
                .Append(';');
        }

        private static void AppendValue(
            StringBuilder destination,
            KLEPKeyValue value)
        {
            destination.Append((int)value.Kind).Append(':');
            switch (value.Kind)
            {
                case KLEPKeyValueKind.Boolean:
                    value.TryGetBoolean(out bool boolean);
                    destination.Append(boolean ? '1' : '0').Append(';');
                    return;

                case KLEPKeyValueKind.Integer:
                    value.TryGetInteger(out long integer);
                    destination.Append(integer.ToString(CultureInfo.InvariantCulture))
                        .Append(';');
                    return;

                case KLEPKeyValueKind.Number:
                    value.TryGetNumber(out double number);
                    destination.Append(number == 0d
                            ? "0"
                            : number.ToString("R", CultureInfo.InvariantCulture))
                        .Append(';');
                    return;

                case KLEPKeyValueKind.Text:
                    value.TryGetText(out string text);
                    AppendToken(destination, text);
                    return;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported Key payload value kind '{value.Kind}' reached guidance evidence.");
            }
        }
    }
}
