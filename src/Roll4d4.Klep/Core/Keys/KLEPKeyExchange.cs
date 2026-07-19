using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Roll4d4.Klep.Core
{
    public enum KLEPKeyExchangeKind
    {
        Copy,
        Give,
        Take,
        Trade
    }

    public enum KLEPKeyExchangeFailure
    {
        None,
        ParticipantsNotDistinct,
        FactNotActivated,
        FactNotOwnedOrAvailable,
        UnsupportedScope,
        UnsupportedLifetime,
        DestinationSequenceExhausted,
        InvalidTakeGrant,
        StoreChangedDuringPreparation,
        PreparationFailed,
        CrossScopeIdentityCollision
    }

    public enum KLEPKeyRequestKind
    {
        Copy,
        Give
    }

    public sealed class KLEPKeyExchangeDelivery
    {
        internal KLEPKeyExchangeDelivery(
            KLEPNeuron source,
            KLEPKeyFact sourceFact,
            KLEPNeuron recipient,
            KLEPKeyFact recipientFact)
        {
            SourceNeuronId = source.StableId;
            SourceOccurrenceId = sourceFact.OccurrenceId;
            RecipientNeuronId = recipient.StableId;
            RecipientFact = recipientFact;
        }

        public string SourceNeuronId { get; }
        public KLEPKeyOccurrenceId SourceOccurrenceId { get; }
        public string RecipientNeuronId { get; }
        public KLEPKeyFact RecipientFact { get; }
    }

    public sealed class KLEPKeyExchangeResult
    {
        internal KLEPKeyExchangeResult(
            string exchangeId,
            KLEPKeyExchangeKind kind,
            KLEPKeyExchangeFailure failure,
            string explanation,
            IReadOnlyList<KLEPKeyExchangeDelivery> deliveries)
        {
            ExchangeId = exchangeId;
            Kind = kind;
            Failure = failure;
            Explanation = explanation ?? string.Empty;
            Deliveries = deliveries ?? Array.Empty<KLEPKeyExchangeDelivery>();
        }

        public string ExchangeId { get; }
        public KLEPKeyExchangeKind Kind { get; }
        public bool Succeeded => Failure == KLEPKeyExchangeFailure.None;
        public KLEPKeyExchangeFailure Failure { get; }
        public string Explanation { get; }
        public IReadOnlyList<KLEPKeyExchangeDelivery> Deliveries { get; }
    }

    public sealed class KLEPKeyTakeGrant
    {
        internal KLEPKeyTakeGrant(
            string grantId,
            KLEPNeuron donor,
            KLEPKeyFact exactFact,
            KLEPNeuron recipient)
        {
            GrantId = grantId;
            DonorId = donor.StableId;
            RecipientId = recipient.StableId;
            KeyId = exactFact.KeyId;
            SourceOccurrenceId = exactFact.OccurrenceId;
            Donor = donor;
            ExactFact = exactFact;
            Recipient = recipient;
        }

        public string GrantId { get; }
        public string DonorId { get; }
        public string RecipientId { get; }
        public KLEPKeyId KeyId { get; }
        public KLEPKeyOccurrenceId SourceOccurrenceId { get; }

        internal KLEPNeuron Donor { get; }
        internal KLEPKeyFact ExactFact { get; }
        internal KLEPNeuron Recipient { get; }
    }

    public sealed class KLEPKeyRequest
    {
        internal KLEPKeyRequest(
            string requestId,
            string requesterId,
            string ownerId,
            KLEPKeyId keyId,
            KLEPKeyRequestKind requestedKind)
        {
            RequestId = requestId;
            RequesterId = requesterId;
            OwnerId = ownerId;
            KeyId = keyId;
            RequestedKind = requestedKind;
        }

        public string RequestId { get; }
        public string RequesterId { get; }
        public string OwnerId { get; }
        public KLEPKeyId KeyId { get; }
        public KLEPKeyRequestKind RequestedKind { get; }
    }

    /// <summary>
    /// Deterministic, exact-occurrence operations between Neuron-owned Local
    /// Key stores. Payloads are transported opaquely and are never interpreted.
    /// Give and Trade are atomic at staging time; each Neuron observes the
    /// staged result at its own next top-level Local boundary. These mutation
    /// methods form a trusted host/coordinator API, not a security boundary
    /// between arbitrary in-process callers.
    /// </summary>
    public static class KLEPKeyExchange
    {
        public static KLEPKeyExchangeResult CopyKey(
            string exchangeId,
            KLEPNeuron source,
            KLEPKeyFact exactFact,
            KLEPNeuron recipient)
        {
            ValidateCommonArguments(exchangeId, source, exactFact, recipient);
            KLEPKeyExchangeResult rejected = ValidateTransfer(
                exchangeId, KLEPKeyExchangeKind.Copy, source, exactFact, recipient);
            if (rejected != null)
            {
                return rejected;
            }

            KLEPKeyStore recipientStore = recipient.LocalKeyStore;
            if (!recipientStore.CanPrepareExchangeDeliveries(1))
            {
                return Failure(
                    exchangeId,
                    KLEPKeyExchangeKind.Copy,
                    KLEPKeyExchangeFailure.DestinationSequenceExhausted,
                    $"Recipient '{recipient.StableId}' cannot allocate another Key occurrence.");
            }

            try
            {
                KLEPKeyStorePreparedBatch recipientBatch = recipientStore.PrepareExchangeBatch(
                    Array.Empty<KLEPKeyFact>(),
                    new[] { exactFact });
                if (!recipientStore.CanApplyExchangeBatch(recipientBatch))
                {
                    return StoreChanged(exchangeId, KLEPKeyExchangeKind.Copy);
                }

                recipientStore.ApplyExchangeBatch(recipientBatch);
                return Success(
                    exchangeId,
                    KLEPKeyExchangeKind.Copy,
                    new KLEPKeyExchangeDelivery(
                        source, exactFact, recipient, recipientBatch.Additions[0]));
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException)
            {
                return PreparationFailure(exchangeId, KLEPKeyExchangeKind.Copy, exception);
            }
        }

        public static KLEPKeyExchangeResult GiveKey(
            string exchangeId,
            KLEPNeuron donor,
            KLEPKeyFact exactFact,
            KLEPNeuron recipient)
        {
            ValidateCommonArguments(exchangeId, donor, exactFact, recipient);
            return MoveKey(
                exchangeId,
                KLEPKeyExchangeKind.Give,
                donor,
                exactFact,
                recipient);
        }

        public static KLEPKeyTakeGrant CreateTakeGrant(
            string grantId,
            KLEPNeuron donor,
            KLEPKeyFact exactFact,
            KLEPNeuron recipient)
        {
            RequireId(grantId, nameof(grantId));
            if (donor == null)
            {
                throw new ArgumentNullException(nameof(donor));
            }

            if (exactFact == null)
            {
                throw new ArgumentNullException(nameof(exactFact));
            }

            if (recipient == null)
            {
                throw new ArgumentNullException(nameof(recipient));
            }

            KLEPKeyExchangeResult rejected = ValidateTransfer(
                grantId, KLEPKeyExchangeKind.Take, donor, exactFact, recipient);
            if (rejected != null)
            {
                throw new InvalidOperationException(rejected.Explanation);
            }

            return new KLEPKeyTakeGrant(grantId, donor, exactFact, recipient);
        }

        public static KLEPKeyExchangeResult TakeKey(
            string exchangeId,
            KLEPKeyTakeGrant grant,
            KLEPNeuron recipient)
        {
            RequireId(exchangeId, nameof(exchangeId));
            if (grant == null)
            {
                throw new ArgumentNullException(nameof(grant));
            }

            if (recipient == null)
            {
                throw new ArgumentNullException(nameof(recipient));
            }

            if (!ReferenceEquals(grant.Recipient, recipient))
            {
                return Failure(
                    exchangeId,
                    KLEPKeyExchangeKind.Take,
                    KLEPKeyExchangeFailure.InvalidTakeGrant,
                    $"Take grant '{grant.GrantId}' was issued to '{grant.RecipientId}', " +
                    $"not '{recipient.StableId}'.");
            }

            return MoveKey(
                exchangeId,
                KLEPKeyExchangeKind.Take,
                grant.Donor,
                grant.ExactFact,
                recipient);
        }

        public static KLEPKeyExchangeResult TradeKeys(
            string exchangeId,
            KLEPNeuron left,
            KLEPKeyFact exactLeftFact,
            KLEPNeuron right,
            KLEPKeyFact exactRightFact)
        {
            RequireId(exchangeId, nameof(exchangeId));
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (exactLeftFact == null)
            {
                throw new ArgumentNullException(nameof(exactLeftFact));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (exactRightFact == null)
            {
                throw new ArgumentNullException(nameof(exactRightFact));
            }

            KLEPKeyExchangeResult leftRejected = ValidateTransfer(
                exchangeId, KLEPKeyExchangeKind.Trade, left, exactLeftFact, right);
            if (leftRejected != null)
            {
                return leftRejected;
            }

            KLEPKeyExchangeResult rightRejected = ValidateTransfer(
                exchangeId, KLEPKeyExchangeKind.Trade, right, exactRightFact, left);
            if (rightRejected != null)
            {
                return rightRejected;
            }

            KLEPKeyStore leftStore = left.LocalKeyStore;
            KLEPKeyStore rightStore = right.LocalKeyStore;
            if (!leftStore.CanPrepareExchangeDeliveries(1) ||
                !rightStore.CanPrepareExchangeDeliveries(1))
            {
                return Failure(
                    exchangeId,
                    KLEPKeyExchangeKind.Trade,
                    KLEPKeyExchangeFailure.DestinationSequenceExhausted,
                    "A Trade participant cannot allocate its delivered Key occurrence.");
            }

            try
            {
                KLEPKeyStorePreparedBatch leftBatch = leftStore.PrepareExchangeBatch(
                    new[] { exactLeftFact },
                    new[] { exactRightFact });
                KLEPKeyStorePreparedBatch rightBatch = rightStore.PrepareExchangeBatch(
                    new[] { exactRightFact },
                    new[] { exactLeftFact });
                if (!leftStore.CanApplyExchangeBatch(leftBatch) ||
                    !rightStore.CanApplyExchangeBatch(rightBatch))
                {
                    return StoreChanged(exchangeId, KLEPKeyExchangeKind.Trade);
                }

                leftStore.ApplyExchangeBatch(leftBatch);
                rightStore.ApplyExchangeBatch(rightBatch);
                return Success(
                    exchangeId,
                    KLEPKeyExchangeKind.Trade,
                    new KLEPKeyExchangeDelivery(
                        right, exactRightFact, left, leftBatch.Additions[0]),
                    new KLEPKeyExchangeDelivery(
                        left, exactLeftFact, right, rightBatch.Additions[0]));
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException)
            {
                return PreparationFailure(exchangeId, KLEPKeyExchangeKind.Trade, exception);
            }
        }

        public static KLEPKeyRequest RequestKey(
            string requestId,
            KLEPNeuron requester,
            KLEPNeuron owner,
            KLEPKeyId keyId,
            KLEPKeyRequestKind requestedKind = KLEPKeyRequestKind.Copy)
        {
            RequireId(requestId, nameof(requestId));
            if (requester == null)
            {
                throw new ArgumentNullException(nameof(requester));
            }

            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (string.IsNullOrWhiteSpace(keyId.Value))
            {
                throw new ArgumentException("A valid requested Key ID is required.", nameof(keyId));
            }

            if (!Enum.IsDefined(typeof(KLEPKeyRequestKind), requestedKind))
            {
                throw new ArgumentOutOfRangeException(nameof(requestedKind));
            }

            if (!AreDistinct(requester, owner))
            {
                throw new InvalidOperationException(
                    "A Key request requires distinct requester and owner Neurons.");
            }

            return new KLEPKeyRequest(
                requestId,
                requester.StableId,
                owner.StableId,
                keyId,
                requestedKind);
        }

        private static KLEPKeyExchangeResult MoveKey(
            string exchangeId,
            KLEPKeyExchangeKind kind,
            KLEPNeuron donor,
            KLEPKeyFact exactFact,
            KLEPNeuron recipient)
        {
            KLEPKeyExchangeResult rejected = ValidateTransfer(
                exchangeId, kind, donor, exactFact, recipient);
            if (rejected != null)
            {
                return rejected;
            }

            KLEPKeyStore donorStore = donor.LocalKeyStore;
            KLEPKeyStore recipientStore = recipient.LocalKeyStore;
            if (!recipientStore.CanPrepareExchangeDeliveries(1))
            {
                return Failure(
                    exchangeId,
                    kind,
                    KLEPKeyExchangeFailure.DestinationSequenceExhausted,
                    $"Recipient '{recipient.StableId}' cannot allocate another Key occurrence.");
            }

            try
            {
                KLEPKeyStorePreparedBatch donorBatch = donorStore.PrepareExchangeBatch(
                    new[] { exactFact },
                    Array.Empty<KLEPKeyFact>());
                KLEPKeyStorePreparedBatch recipientBatch = recipientStore.PrepareExchangeBatch(
                    Array.Empty<KLEPKeyFact>(),
                    new[] { exactFact });
                if (!donorStore.CanApplyExchangeBatch(donorBatch) ||
                    !recipientStore.CanApplyExchangeBatch(recipientBatch))
                {
                    return StoreChanged(exchangeId, kind);
                }

                donorStore.ApplyExchangeBatch(donorBatch);
                recipientStore.ApplyExchangeBatch(recipientBatch);
                return Success(
                    exchangeId,
                    kind,
                    new KLEPKeyExchangeDelivery(
                        donor, exactFact, recipient, recipientBatch.Additions[0]));
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException)
            {
                return PreparationFailure(exchangeId, kind, exception);
            }
        }

        private static KLEPKeyExchangeResult ValidateTransfer(
            string exchangeId,
            KLEPKeyExchangeKind kind,
            KLEPNeuron source,
            KLEPKeyFact exactFact,
            KLEPNeuron recipient)
        {
            if (!AreDistinct(source, recipient))
            {
                return Failure(
                    exchangeId,
                    kind,
                    KLEPKeyExchangeFailure.ParticipantsNotDistinct,
                    "A Key exchange requires distinct, uniquely identified Neurons.");
            }

            if (exactFact.Scope != KLEPKeyScope.Local)
            {
                return Failure(
                    exchangeId,
                    kind,
                    KLEPKeyExchangeFailure.UnsupportedScope,
                    $"Key occurrence '{exactFact.OccurrenceId}' is {exactFact.Scope}; " +
                    "V1 exchanges accept only Local facts.");
            }

            if (exactFact.Lifetime != KLEPKeyLifetime.Persistent)
            {
                return Failure(
                    exchangeId,
                    kind,
                    KLEPKeyExchangeFailure.UnsupportedLifetime,
                    $"Key occurrence '{exactFact.OccurrenceId}' is {exactFact.Lifetime}; " +
                    "V1 exchanges accept only Persistent facts.");
            }

            if (!exactFact.IsActivated)
            {
                return Failure(
                    exchangeId,
                    kind,
                    KLEPKeyExchangeFailure.FactNotActivated,
                    $"Key occurrence '{exactFact.OccurrenceId}' is not activated.");
            }

            if (!source.LocalKeyStore.OwnsAvailableActivatedFact(exactFact))
            {
                return Failure(
                    exchangeId,
                    kind,
                    KLEPKeyExchangeFailure.FactNotOwnedOrAvailable,
                    $"Neuron '{source.StableId}' does not own available occurrence " +
                    $"'{exactFact.OccurrenceId}'.");
            }

            KLEPKeyStore recipientGlobalStore = recipient.GlobalKeyStore;
            if (recipientGlobalStore != null &&
                recipientGlobalStore.HasVisibleOrPendingAddition(exactFact.KeyId))
            {
                return Failure(
                    exchangeId,
                    kind,
                    KLEPKeyExchangeFailure.CrossScopeIdentityCollision,
                    $"Recipient '{recipient.StableId}' cannot receive Local Key " +
                    $"'{exactFact.KeyId}' while the same stable Key ID is visible " +
                    "or pending in its Global store.");
            }

            return null;
        }

        private static bool AreDistinct(KLEPNeuron left, KLEPNeuron right)
        {
            return !ReferenceEquals(left, right) &&
                !StringComparer.Ordinal.Equals(left.StableId, right.StableId);
        }

        private static void ValidateCommonArguments(
            string exchangeId,
            KLEPNeuron source,
            KLEPKeyFact exactFact,
            KLEPNeuron recipient)
        {
            RequireId(exchangeId, nameof(exchangeId));
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (exactFact == null)
            {
                throw new ArgumentNullException(nameof(exactFact));
            }

            if (recipient == null)
            {
                throw new ArgumentNullException(nameof(recipient));
            }
        }

        private static void RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty stable operation ID is required.", parameterName);
            }
        }

        private static KLEPKeyExchangeResult Success(
            string exchangeId,
            KLEPKeyExchangeKind kind,
            params KLEPKeyExchangeDelivery[] deliveries)
        {
            return new KLEPKeyExchangeResult(
                exchangeId,
                kind,
                KLEPKeyExchangeFailure.None,
                $"{kind} exchange '{exchangeId}' was staged successfully.",
                new ReadOnlyCollection<KLEPKeyExchangeDelivery>(
                    new List<KLEPKeyExchangeDelivery>(deliveries)));
        }

        private static KLEPKeyExchangeResult Failure(
            string exchangeId,
            KLEPKeyExchangeKind kind,
            KLEPKeyExchangeFailure failure,
            string explanation)
        {
            return new KLEPKeyExchangeResult(
                exchangeId,
                kind,
                failure,
                explanation,
                Array.Empty<KLEPKeyExchangeDelivery>());
        }

        private static KLEPKeyExchangeResult StoreChanged(
            string exchangeId,
            KLEPKeyExchangeKind kind)
        {
            return Failure(
                exchangeId,
                kind,
                KLEPKeyExchangeFailure.StoreChangedDuringPreparation,
                "A participant Key store changed while the exchange was being prepared; " +
                "nothing was staged.");
        }

        private static KLEPKeyExchangeResult PreparationFailure(
            string exchangeId,
            KLEPKeyExchangeKind kind,
            Exception exception)
        {
            return Failure(
                exchangeId,
                kind,
                KLEPKeyExchangeFailure.PreparationFailed,
                $"The exchange could not be prepared: {exception.Message}");
        }
    }
}
