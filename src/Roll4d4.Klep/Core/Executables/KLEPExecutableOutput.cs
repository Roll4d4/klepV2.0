using System;

namespace Roll4d4.Klep.Core
{
    public enum KLEPExecutableOutputKind
    {
        Add,
        Remove,
        Replace
    }

    /// <summary>
    /// One immutable request produced by an Executable. It describes intent;
    /// only the owning Neuron may apply it to a KeyStore at an approved barrier.
    /// </summary>
    public sealed class KLEPExecutableOutput
    {
        private KLEPExecutableOutput(
            KLEPExecutableOutputKind kind,
            KLEPKeyDefinition definition,
            KLEPKeyFact target,
            KLEPKeyPayload payload,
            string sourceExecutableId)
        {
            Kind = kind;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Target = target;
            Payload = payload;
            SourceExecutableId = sourceExecutableId ?? string.Empty;
        }

        public KLEPExecutableOutputKind Kind { get; }
        public KLEPKeyDefinition Definition { get; }
        public KLEPKeyId KeyId => Definition.Id;
        public KLEPKeyScope Scope => Definition.Scope;
        public KLEPKeyFact Target { get; }
        public KLEPKeyPayload Payload { get; }
        public string SourceExecutableId { get; }

        public static KLEPExecutableOutput Add(
            KLEPKeyDefinition definition,
            KLEPKeyPayload payload = null)
        {
            return new KLEPExecutableOutput(
                KLEPExecutableOutputKind.Add,
                definition ?? throw new ArgumentNullException(nameof(definition)),
                null,
                payload,
                string.Empty);
        }

        public static KLEPExecutableOutput Remove(KLEPKeyFact target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return new KLEPExecutableOutput(
                KLEPExecutableOutputKind.Remove,
                target.Definition,
                target,
                null,
                string.Empty);
        }

        public static KLEPExecutableOutput Replace(
            KLEPKeyFact target,
            KLEPKeyPayload payload)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return new KLEPExecutableOutput(
                KLEPExecutableOutputKind.Replace,
                target.Definition,
                target,
                payload,
                string.Empty);
        }

        internal KLEPExecutableOutput BindSource(string executableStableId)
        {
            if (string.IsNullOrWhiteSpace(executableStableId))
            {
                throw new ArgumentException(
                    "A non-empty source Executable ID is required.",
                    nameof(executableStableId));
            }

            if (!string.IsNullOrEmpty(SourceExecutableId))
            {
                throw new InvalidOperationException(
                    "An emitted Key operation already has a source Executable.");
            }

            return new KLEPExecutableOutput(
                Kind,
                Definition,
                Target,
                Payload,
                executableStableId);
        }
    }
}
