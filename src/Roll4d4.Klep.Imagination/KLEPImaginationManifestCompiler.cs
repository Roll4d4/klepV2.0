using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Imagination
{
    /// <summary>
    /// Strict, engine-free compiler for the two approved Imagination channels.
    /// Strong JSON derives a complete definition from trusted descriptor data;
    /// Weak JSON remains immutable non-runnable evidence.
    /// </summary>
    public sealed class KLEPImaginationManifestCompiler
    {
        public const string StrongSchema = "klep.imagination.strong.v1";
        public const string WeakSchema = "klep.imagination.conjecture.v1";
        public const int MaximumDocumentBytes = 16 * 1024;

        public KLEPImaginationManifest CompileStrong(
            string json,
            IKLEPImaginationCapabilityCatalog capabilityCatalog)
        {
            if (capabilityCatalog == null)
            {
                throw new ArgumentNullException(nameof(capabilityCatalog));
            }

            using (JsonDocument document = ParseDocument(json))
            {
                Dictionary<string, JsonElement> root = ReadOpenObject(
                    document.RootElement,
                    "$");
                if (!root.ContainsKey("schema"))
                {
                    throw Reject(
                        KLEPImaginationRejectionCode.MissingProperty,
                        "Required property '$.schema' is missing.");
                }

                string schema = ReadRequiredString(
                    root,
                    "schema",
                    "$",
                    64);
                if (StringComparer.Ordinal.Equals(schema, WeakSchema))
                {
                    throw Reject(
                        KLEPImaginationRejectionCode.WeakConjectureNotRunnable,
                        "A Weak Conjecture cannot be compiled or materialized.");
                }

                if (!StringComparer.Ordinal.Equals(schema, StrongSchema))
                {
                    throw Reject(
                        KLEPImaginationRejectionCode.UnsupportedSchema,
                        $"Unsupported Imagination schema '{schema}'.");
                }

                root = ReadClosedObject(
                    document.RootElement,
                    "$",
                    "schema",
                    "requestFingerprint",
                    "displayName",
                    "capability",
                    "hypothesis");

                string requestFingerprint = ReadRequiredString(
                    root,
                    "requestFingerprint",
                    "$",
                    256);
                string displayName = ReadRequiredString(
                    root,
                    "displayName",
                    "$",
                    120);

                Dictionary<string, JsonElement> capability = ReadClosedObject(
                    root["capability"],
                    "$.capability",
                    "id",
                    "version",
                    "arguments");
                string capabilityId = ReadRequiredString(
                    capability,
                    "id",
                    "$.capability",
                    200);
                string capabilityVersion = ReadRequiredString(
                    capability,
                    "version",
                    "$.capability",
                    80);
                if (!capabilityCatalog.TryResolve(
                        capabilityId,
                        capabilityVersion,
                        out KLEPImaginationCapabilityRegistration registration))
                {
                    throw Reject(
                        KLEPImaginationRejectionCode.UnknownCapability,
                        $"Capability '{capabilityId}' version " +
                        $"'{capabilityVersion}' is not admitted.");
                }

                Dictionary<string, JsonElement> argumentElements =
                    ReadOpenObject(
                        capability["arguments"],
                        "$.capability.arguments");
                Dictionary<string, KLEPImaginationValue> arguments =
                    CompileArguments(
                        registration.Descriptor,
                        argumentElements);

                Dictionary<string, JsonElement> hypothesis = ReadClosedObject(
                    root["hypothesis"],
                    "$.hypothesis",
                    "targetKey",
                    "explanation");
                string targetKey = ReadRequiredString(
                    hypothesis,
                    "targetKey",
                    "$.hypothesis",
                    256);
                string explanation = ReadRequiredString(
                    hypothesis,
                    "explanation",
                    "$.hypothesis",
                    1200);

                string canonical = WriteCanonicalStrong(
                    requestFingerprint,
                    displayName,
                    registration.Descriptor,
                    arguments,
                    targetKey,
                    explanation);
                string proposalFingerprint =
                    KLEPImaginationHash.Compute(canonical);
                string executableId = "imagined." +
                    proposalFingerprint.Substring("sha256:".Length);
                KLEPExecutableDefinition template =
                    registration.Descriptor.DefinitionTemplate;
                var definition = new KLEPExecutableDefinition(
                    executableId,
                    displayName,
                    template.Kind,
                    template.ValidationLocks,
                    template.ExecutionLocks,
                    template.BaseAttractiveness,
                    template.ExecutionMode,
                    template.DeclaredOutputs);

                return new KLEPImaginationManifest(
                    requestFingerprint,
                    displayName,
                    targetKey,
                    explanation,
                    proposalFingerprint,
                    canonical,
                    capabilityCatalog.Fingerprint,
                    registration.Descriptor,
                    arguments,
                    definition);
            }
        }

        public KLEPImaginationConjecture ParseWeakConjecture(string json)
        {
            using (JsonDocument document = ParseDocument(json))
            {
                Dictionary<string, JsonElement> root = ReadClosedObject(
                    document.RootElement,
                    "$",
                    "schema",
                    "requestFingerprint",
                    "title",
                    "conjecture",
                    "details");
                string schema = ReadRequiredString(root, "schema", "$", 64);
                if (!StringComparer.Ordinal.Equals(schema, WeakSchema))
                {
                    throw Reject(
                        KLEPImaginationRejectionCode.UnsupportedSchema,
                        $"Schema '{schema}' is not a Weak Conjecture.");
                }

                string requestFingerprint = ReadRequiredString(
                    root,
                    "requestFingerprint",
                    "$",
                    256);
                string title = ReadRequiredString(
                    root,
                    "title",
                    "$",
                    120);
                string conjecture = ReadRequiredString(
                    root,
                    "conjecture",
                    "$",
                    4000);

                string canonical = WriteCanonicalWeak(
                    requestFingerprint,
                    title,
                    conjecture,
                    root["details"]);
                return new KLEPImaginationConjecture(
                    requestFingerprint,
                    title,
                    conjecture,
                    canonical,
                    KLEPImaginationHash.Compute(canonical));
            }
        }

        private static Dictionary<string, KLEPImaginationValue>
            CompileArguments(
                KLEPImaginationCapabilityDescriptor descriptor,
                IDictionary<string, JsonElement> elements)
        {
            if (elements.Count != descriptor.Parameters.Count)
            {
                throw Reject(
                    KLEPImaginationRejectionCode.InvalidArgument,
                    $"Capability '{descriptor.StableId}' requires exactly " +
                    $"{descriptor.Parameters.Count} arguments, but the proposal " +
                    $"contains {elements.Count}.");
            }

            var result = new Dictionary<string, KLEPImaginationValue>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, JsonElement> pair in elements)
            {
                if (!descriptor.TryGetParameter(
                        pair.Key,
                        out KLEPImaginationParameterDefinition parameter))
                {
                    throw Reject(
                        KLEPImaginationRejectionCode.InvalidArgument,
                        $"Capability '{descriptor.StableId}' has no argument " +
                        $"named '{pair.Key}'.");
                }

                KLEPImaginationValue value = ReadArgumentValue(
                    pair.Value,
                    parameter);
                try
                {
                    parameter.Validate(value);
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is InvalidOperationException)
                {
                    throw new KLEPImaginationRejectedException(
                        KLEPImaginationRejectionCode.InvalidArgument,
                        $"Argument '{pair.Key}' was rejected: {exception.Message}",
                        exception);
                }

                result.Add(pair.Key, value);
            }

            foreach (KLEPImaginationParameterDefinition parameter in
                     descriptor.Parameters)
            {
                if (!result.ContainsKey(parameter.Name))
                {
                    throw Reject(
                        KLEPImaginationRejectionCode.InvalidArgument,
                        $"Required capability argument '{parameter.Name}' is missing.");
                }
            }

            return result;
        }

        private static KLEPImaginationValue ReadArgumentValue(
            JsonElement element,
            KLEPImaginationParameterDefinition parameter)
        {
            switch (parameter.Kind)
            {
                case KLEPImaginationValueKind.Boolean:
                    if (element.ValueKind != JsonValueKind.True &&
                        element.ValueKind != JsonValueKind.False)
                    {
                        break;
                    }

                    return KLEPImaginationValue.FromBoolean(
                        element.GetBoolean());
                case KLEPImaginationValueKind.Integer:
                    if (element.ValueKind != JsonValueKind.Number ||
                        !element.TryGetInt64(out long integer))
                    {
                        break;
                    }

                    return KLEPImaginationValue.FromInteger(integer);
                case KLEPImaginationValueKind.Number:
                    if (element.ValueKind != JsonValueKind.Number ||
                        !element.TryGetDouble(out double number) ||
                        double.IsNaN(number) ||
                        double.IsInfinity(number))
                    {
                        break;
                    }

                    return KLEPImaginationValue.FromNumber(number);
                case KLEPImaginationValueKind.Text:
                    if (element.ValueKind != JsonValueKind.String)
                    {
                        break;
                    }

                    return KLEPImaginationValue.FromText(
                        element.GetString() ?? string.Empty);
            }

            throw Reject(
                KLEPImaginationRejectionCode.InvalidArgument,
                $"Argument '{parameter.Name}' is not a valid {parameter.Kind}.");
        }

        private static JsonDocument ParseDocument(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (Encoding.UTF8.GetByteCount(json) > MaximumDocumentBytes)
            {
                throw Reject(
                    KLEPImaginationRejectionCode.DocumentTooLarge,
                    $"An Imagination proposal may not exceed " +
                    $"{MaximumDocumentBytes} UTF-8 bytes.");
            }

            try
            {
                return JsonDocument.Parse(
                    json,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = 16
                    });
            }
            catch (JsonException exception)
            {
                throw new KLEPImaginationRejectedException(
                    KLEPImaginationRejectionCode.MalformedJson,
                    "The Imagination proposal is not valid bounded JSON.",
                    exception);
            }
        }

        private static Dictionary<string, JsonElement> ReadClosedObject(
            JsonElement element,
            string path,
            params string[] requiredProperties)
        {
            Dictionary<string, JsonElement> result = ReadOpenObject(
                element,
                path);
            var allowed = new HashSet<string>(
                requiredProperties,
                StringComparer.Ordinal);
            foreach (string name in result.Keys)
            {
                if (!allowed.Contains(name))
                {
                    throw Reject(
                        KLEPImaginationRejectionCode.UnknownProperty,
                        $"Unknown property '{path}.{name}'.");
                }
            }

            foreach (string name in requiredProperties)
            {
                if (!result.ContainsKey(name))
                {
                    throw Reject(
                        KLEPImaginationRejectionCode.MissingProperty,
                        $"Required property '{path}.{name}' is missing.");
                }
            }

            return result;
        }

        private static Dictionary<string, JsonElement> ReadOpenObject(
            JsonElement element,
            string path)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw Reject(
                    KLEPImaginationRejectionCode.InvalidValue,
                    $"'{path}' must be an object.");
            }

            var result = new Dictionary<string, JsonElement>(
                StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (result.ContainsKey(property.Name))
                {
                    throw Reject(
                        KLEPImaginationRejectionCode.DuplicateProperty,
                        $"Property '{path}.{property.Name}' is duplicated.");
                }

                result.Add(property.Name, property.Value);
            }

            return result;
        }

        private static string ReadRequiredString(
            IDictionary<string, JsonElement> properties,
            string name,
            string path,
            int maximumLength)
        {
            JsonElement element = properties[name];
            if (element.ValueKind != JsonValueKind.String)
            {
                throw Reject(
                    KLEPImaginationRejectionCode.InvalidValue,
                    $"'{path}.{name}' must be text.");
            }

            string value = element.GetString();
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > maximumLength)
            {
                throw Reject(
                    KLEPImaginationRejectionCode.InvalidValue,
                    $"'{path}.{name}' must contain 1-{maximumLength} characters.");
            }

            return value;
        }

        private static string WriteCanonicalStrong(
            string requestFingerprint,
            string displayName,
            KLEPImaginationCapabilityDescriptor descriptor,
            IDictionary<string, KLEPImaginationValue> arguments,
            string targetKey,
            string explanation)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteString("schema", StrongSchema);
                    writer.WriteString("requestFingerprint", requestFingerprint);
                    writer.WriteString("displayName", displayName);
                    writer.WritePropertyName("capability");
                    writer.WriteStartObject();
                    writer.WriteString("id", descriptor.StableId);
                    writer.WriteString("version", descriptor.Version);
                    writer.WritePropertyName("arguments");
                    writer.WriteStartObject();
                    foreach (KLEPImaginationParameterDefinition parameter in
                             descriptor.Parameters)
                    {
                        writer.WritePropertyName(parameter.Name);
                        WriteValue(writer, arguments[parameter.Name]);
                    }

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WritePropertyName("hypothesis");
                    writer.WriteStartObject();
                    writer.WriteString("targetKey", targetKey);
                    writer.WriteString("explanation", explanation);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static string WriteCanonicalWeak(
            string requestFingerprint,
            string title,
            string conjecture,
            JsonElement details)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteString("schema", WeakSchema);
                    writer.WriteString("requestFingerprint", requestFingerprint);
                    writer.WriteString("title", title);
                    writer.WriteString("conjecture", conjecture);
                    writer.WritePropertyName("details");
                    WriteCanonicalElement(writer, details, "$.details");
                    writer.WriteEndObject();
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static void WriteValue(
            Utf8JsonWriter writer,
            KLEPImaginationValue value)
        {
            switch (value.Kind)
            {
                case KLEPImaginationValueKind.Boolean:
                    writer.WriteBooleanValue(value.AsBoolean());
                    break;
                case KLEPImaginationValueKind.Integer:
                    writer.WriteNumberValue(value.AsInteger());
                    break;
                case KLEPImaginationValueKind.Number:
                    writer.WriteNumberValue(value.AsNumber());
                    break;
                case KLEPImaginationValueKind.Text:
                    writer.WriteStringValue(value.AsText());
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unsupported Imagination value kind.");
            }
        }

        private static void WriteCanonicalElement(
            Utf8JsonWriter writer,
            JsonElement element,
            string path)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    Dictionary<string, JsonElement> properties =
                        ReadOpenObject(element, path);
                    var names = new List<string>(properties.Keys);
                    names.Sort(StringComparer.Ordinal);
                    writer.WriteStartObject();
                    foreach (string name in names)
                    {
                        writer.WritePropertyName(name);
                        WriteCanonicalElement(
                            writer,
                            properties[name],
                            path + "." + name);
                    }

                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    int index = 0;
                    foreach (JsonElement child in element.EnumerateArray())
                    {
                        WriteCanonicalElement(
                            writer,
                            child,
                            path + "[" + index.ToString(
                                CultureInfo.InvariantCulture) + "]");
                        index++;
                    }

                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;
                case JsonValueKind.Number:
                    if (!element.TryGetDouble(out double number) ||
                        double.IsNaN(number) ||
                        double.IsInfinity(number))
                    {
                        throw Reject(
                            KLEPImaginationRejectionCode.InvalidValue,
                            $"'{path}' contains a non-finite or unsupported number.");
                    }

                    writer.WriteNumberValue(number);
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    writer.WriteBooleanValue(element.GetBoolean());
                    break;
                case JsonValueKind.Null:
                    writer.WriteNullValue();
                    break;
                default:
                    throw Reject(
                        KLEPImaginationRejectionCode.InvalidValue,
                        $"'{path}' contains an unsupported JSON token.");
            }
        }

        private static KLEPImaginationRejectedException Reject(
            KLEPImaginationRejectionCode code,
            string message)
        {
            return new KLEPImaginationRejectedException(code, message);
        }
    }
}
