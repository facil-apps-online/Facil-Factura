namespace Fel.Core.Entities;

public enum CertificateEnvironment
{
    Sandbox = 1,
    Production = 2
}

public enum CertificateStatus
{
    Current = 1,
    Future = 2,
    Retired = 3,
    Revoked = 4,
    Invalid = 5,
    Expired = 6
}

public enum CertificateRequestType
{
    Initial = 1,
    Renewal = 2,
    Replacement = 3
}

public enum CertificateRequestStatus
{
    Draft = 1,
    Submitted = 2,
    WaitingForIdentity = 3,
    IdentityReview = 4,
    DocumentsRequired = 5,
    DocumentsSubmitted = 6,
    ProviderReview = 7,
    CertificateProcessing = 8,
    ReadyToDownload = 9,
    Downloaded = 10,
    Installing = 11,
    InstalledPendingActivation = 12,
    Active = 13,
    Rejected = 14,
    Failed = 15,
    RevocationRequested = 16,
    Revoked = 17,
    Expired = 18
}

public enum CertificateChargeType
{
    Initial = 1,
    Renewal = 2,
    Replacement = 3,
    Revocation = 4
}

public enum CertificateChargeStatus
{
    Pending = 1,
    Accrued = 2,
    Invoiced = 3,
    Voided = 4,
    Refunded = 5
}

public enum CertificateEventType
{
    RequestCreated = 1,
    ProviderStatusChanged = 2,
    KycLinkGenerated = 3,
    DocumentRequired = 4,
    DocumentUploaded = 5,
    P7bDownloaded = 6,
    Installed = 7,
    Activated = 8,
    Retired = 9,
    RenewalScheduled = 10,
    ChargeCreated = 11,
    Failed = 12,
    RevocationRequested = 13,
    Revoked = 14
}
