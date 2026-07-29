namespace QubiCostElectrical;

public sealed record ProjectReceptionId(string Value) { public static ProjectReceptionId New() => new(Guid.NewGuid().ToString("N")); public static ProjectReceptionId From(string value) => new(value); }
public sealed record ReceptionInspectionId(string Value) { public static ReceptionInspectionId New() => new(Guid.NewGuid().ToString("N")); public static ReceptionInspectionId From(string value) => new(value); }
public sealed record ReceptionObservationId(string Value) { public static ReceptionObservationId New() => new(Guid.NewGuid().ToString("N")); public static ReceptionObservationId From(string value) => new(value); }
public sealed record ReceptionRecordId(string Value) { public static ReceptionRecordId New() => new(Guid.NewGuid().ToString("N")); public static ReceptionRecordId From(string value) => new(value); }
public sealed record HandoverRecordId(string Value) { public static HandoverRecordId New() => new(Guid.NewGuid().ToString("N")); public static HandoverRecordId From(string value) => new(value); }
public sealed record AcceptanceCertificateId(string Value) { public static AcceptanceCertificateId New() => new(Guid.NewGuid().ToString("N")); public static AcceptanceCertificateId From(string value) => new(value); }
public sealed record AsBuiltDeliverableId(string Value) { public static AsBuiltDeliverableId New() => new(Guid.NewGuid().ToString("N")); public static AsBuiltDeliverableId From(string value) => new(value); }
public sealed record TrainingRecordId(string Value) { public static TrainingRecordId New() => new(Guid.NewGuid().ToString("N")); public static TrainingRecordId From(string value) => new(value); }
public sealed record GuaranteeReleaseId(string Value) { public static GuaranteeReleaseId New() => new(Guid.NewGuid().ToString("N")); public static GuaranteeReleaseId From(string value) => new(value); }
public sealed record RetentionReleaseId(string Value) { public static RetentionReleaseId New() => new(Guid.NewGuid().ToString("N")); public static RetentionReleaseId From(string value) => new(value); }
public sealed record InventoryCloseoutId(string Value) { public static InventoryCloseoutId New() => new(Guid.NewGuid().ToString("N")); public static InventoryCloseoutId From(string value) => new(value); }
public sealed record SurplusDisposalId(string Value) { public static SurplusDisposalId New() => new(Guid.NewGuid().ToString("N")); public static SurplusDisposalId From(string value) => new(value); }
public sealed record LessonLearnedId(string Value) { public static LessonLearnedId New() => new(Guid.NewGuid().ToString("N")); public static LessonLearnedId From(string value) => new(value); }
public sealed record LessonEvidenceId(string Value) { public static LessonEvidenceId New() => new(Guid.NewGuid().ToString("N")); public static LessonEvidenceId From(string value) => new(value); }
public sealed record FinalSnapshotId(string Value) { public static FinalSnapshotId New() => new(Guid.NewGuid().ToString("N")); public static FinalSnapshotId From(string value) => new(value); }
public sealed record ProjectArchiveManifestId(string Value) { public static ProjectArchiveManifestId New() => new(Guid.NewGuid().ToString("N")); public static ProjectArchiveManifestId From(string value) => new(value); }
public sealed record ProjectFinalResultId(string Value) { public static ProjectFinalResultId New() => new(Guid.NewGuid().ToString("N")); public static ProjectFinalResultId From(string value) => new(value); }
