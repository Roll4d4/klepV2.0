using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Roll4d4.Klep.Core
{
    public enum KLEPKeyScope
    {
        Local,
        Global
    }

    public enum KLEPKeyLifetime
    {
        OneCycle,
        Persistent
    }

    public readonly struct KLEPKeyId : IEquatable<KLEPKeyId>, IComparable<KLEPKeyId>
    {
        public KLEPKeyId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty stable Key ID is required.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }
        public int CompareTo(KLEPKeyId other) =>
            StringComparer.Ordinal.Compare(Value, other.Value);
        public bool Equals(KLEPKeyId other) =>
            StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is KLEPKeyId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(KLEPKeyId left, KLEPKeyId right) => left.Equals(right);
        public static bool operator !=(KLEPKeyId left, KLEPKeyId right) => !left.Equals(right);
    }

    public readonly struct KLEPKeyOccurrenceId :
        IEquatable<KLEPKeyOccurrenceId>, IComparable<KLEPKeyOccurrenceId>
    {
        public KLEPKeyOccurrenceId(string storeId, long sequence)
        {
            if (string.IsNullOrWhiteSpace(storeId))
            {
                throw new ArgumentException("A non-empty KeyStore ID is required.", nameof(storeId));
            }

            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            StoreId = storeId;
            Sequence = sequence;
        }

        public string StoreId { get; }
        public long Sequence { get; }

        public int CompareTo(KLEPKeyOccurrenceId other)
        {
            int storeComparison = StringComparer.Ordinal.Compare(StoreId, other.StoreId);
            return storeComparison != 0 ? storeComparison : Sequence.CompareTo(other.Sequence);
        }

        public bool Equals(KLEPKeyOccurrenceId other) =>
            Sequence == other.Sequence && StringComparer.Ordinal.Equals(StoreId, other.StoreId);
        public override bool Equals(object obj) =>
            obj is KLEPKeyOccurrenceId other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(StoreId ?? string.Empty) * 397) ^
                    Sequence.GetHashCode();
            }
        }

        public override string ToString() => $"{StoreId}:{Sequence.ToString(CultureInfo.InvariantCulture)}";
        public static bool operator ==(KLEPKeyOccurrenceId left, KLEPKeyOccurrenceId right) =>
            left.Equals(right);
        public static bool operator !=(KLEPKeyOccurrenceId left, KLEPKeyOccurrenceId right) =>
            !left.Equals(right);
    }

    public enum KLEPKeyValueKind
    {
        None,
        Boolean,
        Integer,
        Number,
        Text
    }

    public readonly struct KLEPKeyValue : IEquatable<KLEPKeyValue>
    {
        private readonly object value;

        private KLEPKeyValue(KLEPKeyValueKind kind, object value)
        {
            Kind = kind;
            this.value = value;
        }

        public KLEPKeyValueKind Kind { get; }
        public static KLEPKeyValue FromBoolean(bool value) =>
            new KLEPKeyValue(KLEPKeyValueKind.Boolean, value);
        public static KLEPKeyValue FromInteger(long value) =>
            new KLEPKeyValue(KLEPKeyValueKind.Integer, value);
        public static KLEPKeyValue FromNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return new KLEPKeyValue(KLEPKeyValueKind.Number, value);
        }

        public static KLEPKeyValue FromText(string value) =>
            new KLEPKeyValue(KLEPKeyValueKind.Text, value ?? string.Empty);

        public bool TryGetBoolean(out bool result)
        {
            result = Kind == KLEPKeyValueKind.Boolean && value is bool boolean ? boolean : default;
            return Kind == KLEPKeyValueKind.Boolean;
        }

        public bool TryGetInteger(out long result)
        {
            result = Kind == KLEPKeyValueKind.Integer && value is long integer ? integer : default;
            return Kind == KLEPKeyValueKind.Integer;
        }

        public bool TryGetNumber(out double result)
        {
            if (Kind == KLEPKeyValueKind.Number && value is double number)
            {
                result = number;
                return true;
            }

            if (Kind == KLEPKeyValueKind.Integer && value is long integer)
            {
                result = integer;
                return true;
            }

            result = default;
            return false;
        }

        public bool TryGetText(out string result)
        {
            result = Kind == KLEPKeyValueKind.Text ? value as string ?? string.Empty : null;
            return Kind == KLEPKeyValueKind.Text;
        }

        public bool Equals(KLEPKeyValue other) => Kind == other.Kind && Equals(value, other.value);
        public override bool Equals(object obj) => obj is KLEPKeyValue other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ (value == null ? 0 : value.GetHashCode());
            }
        }

        public override string ToString() => Convert.ToString(value, CultureInfo.InvariantCulture);
        public static implicit operator KLEPKeyValue(bool value) => FromBoolean(value);
        public static implicit operator KLEPKeyValue(int value) => FromInteger(value);
        public static implicit operator KLEPKeyValue(long value) => FromInteger(value);
        public static implicit operator KLEPKeyValue(float value) => FromNumber(value);
        public static implicit operator KLEPKeyValue(double value) => FromNumber(value);
        public static implicit operator KLEPKeyValue(string value) => FromText(value);
    }

    public readonly struct KLEPKeyField
    {
        public KLEPKeyField(string name, KLEPKeyValue value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A non-empty payload field name is required.", nameof(name));
            }

            Name = name;
            Value = value;
        }

        public string Name { get; }
        public KLEPKeyValue Value { get; }
    }

    public sealed class KLEPKeyPayload
    {
        private readonly ReadOnlyDictionary<string, KLEPKeyValue> valuesByName;
        private readonly ReadOnlyCollection<KLEPKeyField> fields;

        public KLEPKeyPayload(IEnumerable<KeyValuePair<string, KLEPKeyValue>> values = null)
        {
            var copy = new Dictionary<string, KLEPKeyValue>(StringComparer.Ordinal);
            if (values != null)
            {
                foreach (KeyValuePair<string, KLEPKeyValue> pair in values)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        throw new ArgumentException("Payload field names cannot be empty.", nameof(values));
                    }

                    if (pair.Value.Kind != KLEPKeyValueKind.Boolean &&
                        pair.Value.Kind != KLEPKeyValueKind.Integer &&
                        pair.Value.Kind != KLEPKeyValueKind.Number &&
                        pair.Value.Kind != KLEPKeyValueKind.Text)
                    {
                        throw new ArgumentException(
                            "Payload fields support only Boolean, Int64, finite Double, and Text values.",
                            nameof(values));
                    }

                    copy.Add(pair.Key, pair.Value);
                }
            }

            var orderedNames = new List<string>(copy.Keys);
            orderedNames.Sort(StringComparer.Ordinal);

            var orderedFields = new List<KLEPKeyField>(orderedNames.Count);
            foreach (string name in orderedNames)
            {
                orderedFields.Add(new KLEPKeyField(name, copy[name]));
            }

            valuesByName = new ReadOnlyDictionary<string, KLEPKeyValue>(copy);
            fields = new ReadOnlyCollection<KLEPKeyField>(orderedFields);
        }

        public static KLEPKeyPayload Empty { get; } = new KLEPKeyPayload();
        public IReadOnlyList<KLEPKeyField> Fields => fields;
        public int Count => fields.Count;
        public bool TryGetValue(string field, out KLEPKeyValue value) =>
            valuesByName.TryGetValue(field, out value);
        public bool TryGetBoolean(string field, out bool value)
        {
            if (valuesByName.TryGetValue(field, out KLEPKeyValue found))
            {
                return found.TryGetBoolean(out value);
            }

            value = default;
            return false;
        }

        public bool TryGetInteger(string field, out long value)
        {
            if (valuesByName.TryGetValue(field, out KLEPKeyValue found))
            {
                return found.TryGetInteger(out value);
            }

            value = default;
            return false;
        }

        public bool TryGetNumber(string field, out double value)
        {
            if (valuesByName.TryGetValue(field, out KLEPKeyValue found))
            {
                return found.TryGetNumber(out value);
            }

            value = default;
            return false;
        }

        public bool TryGetText(string field, out string value)
        {
            if (valuesByName.TryGetValue(field, out KLEPKeyValue found))
            {
                return found.TryGetText(out value);
            }

            value = null;
            return false;
        }

        public KLEPKeyPayload Merge(KLEPKeyPayload overrides)
        {
            if (overrides == null || overrides.Count == 0)
            {
                return this;
            }

            var merged = new Dictionary<string, KLEPKeyValue>(valuesByName, StringComparer.Ordinal);
            foreach (KLEPKeyField field in overrides.fields)
            {
                merged[field.Name] = field.Value;
            }

            return new KLEPKeyPayload(merged);
        }
    }

    public sealed class KLEPKeyDefinition
    {
        public KLEPKeyDefinition(
            KLEPKeyId id,
            string displayName,
            string description = "",
            KLEPKeyScope scope = KLEPKeyScope.Local,
            KLEPKeyLifetime defaultLifetime = KLEPKeyLifetime.OneCycle,
            float baseAttractiveness = 0f,
            KLEPKeyPayload defaultPayload = null)
        {
            if (string.IsNullOrWhiteSpace(id.Value))
            {
                throw new ArgumentException("A valid Key ID is required.", nameof(id));
            }

            if (float.IsNaN(baseAttractiveness) || float.IsInfinity(baseAttractiveness))
            {
                throw new ArgumentOutOfRangeException(nameof(baseAttractiveness));
            }

            if (!Enum.IsDefined(typeof(KLEPKeyScope), scope))
            {
                throw new ArgumentOutOfRangeException(nameof(scope));
            }

            if (!Enum.IsDefined(typeof(KLEPKeyLifetime), defaultLifetime))
            {
                throw new ArgumentOutOfRangeException(nameof(defaultLifetime));
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Value : displayName;
            Description = description ?? string.Empty;
            Scope = scope;
            DefaultLifetime = defaultLifetime;
            BaseAttractiveness = baseAttractiveness;
            DefaultPayload = defaultPayload ?? KLEPKeyPayload.Empty;
        }

        public KLEPKeyId Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public KLEPKeyScope Scope { get; }
        public KLEPKeyLifetime DefaultLifetime { get; }
        public float BaseAttractiveness { get; }
        public KLEPKeyPayload DefaultPayload { get; }
    }

    public sealed class KLEPKeyFact
    {
        internal KLEPKeyFact(
            object ownerToken,
            KLEPKeyOccurrenceId occurrenceId,
            KLEPKeyDefinition definition,
            KLEPKeyPayload payload,
            KLEPKeyLifetime lifetime,
            long issuedTick,
            string sourceId,
            long activatedTick = -1)
        {
            this.ownerToken = ownerToken ?? throw new ArgumentNullException(nameof(ownerToken));
            OccurrenceId = occurrenceId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Payload = definition.DefaultPayload.Merge(payload);
            Lifetime = lifetime;
            IssuedTick = issuedTick;
            SourceId = sourceId ?? string.Empty;
            ActivatedTick = activatedTick;
        }

        private readonly object ownerToken;

        public KLEPKeyOccurrenceId OccurrenceId { get; }
        public KLEPKeyDefinition Definition { get; }
        public KLEPKeyId KeyId => Definition.Id;
        public KLEPKeyScope Scope => Definition.Scope;
        public KLEPKeyPayload Payload { get; }
        public KLEPKeyLifetime Lifetime { get; }
        // IssuedTick is when the occurrence was staged. ActivatedTick is the
        // boundary where it first became visible; -1 means it is still pending.
        public long IssuedTick { get; }
        public long ActivatedTick { get; }
        public bool IsActivated => ActivatedTick >= 0;
        public string SourceId { get; }

        internal bool IsOwnedBy(object candidateOwnerToken) =>
            ReferenceEquals(ownerToken, candidateOwnerToken);

        internal KLEPKeyFact Activate(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }

            if (IsActivated)
            {
                throw new InvalidOperationException(
                    $"Key occurrence '{OccurrenceId}' is already active at tick {ActivatedTick}.");
            }

            return new KLEPKeyFact(
                ownerToken,
                OccurrenceId,
                Definition,
                Payload,
                Lifetime,
                IssuedTick,
                SourceId,
                tick);
        }
    }
}
