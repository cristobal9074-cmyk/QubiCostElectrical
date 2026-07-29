using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class HandoverService : IHandoverService
{
    private readonly IReceptionRecordRepository _recordRepo;
    private readonly IAsBuiltRepository _asBuiltRepo;
    public HandoverService(IReceptionRecordRepository recordRepo, IAsBuiltRepository asBuiltRepo)
    { _recordRepo = recordRepo; _asBuiltRepo = asBuiltRepo; }

    public async Task<HandoverRecord> CreateHandoverAsync(string closeoutId, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var record = new HandoverRecord
        {
            Id = HandoverRecordId.New(), CloseoutId = closeoutId,
            HandoverDate = now, Status = "Draft",
            Version = 1, CreatedBy = actor, CreatedUtc = now
        };
        await _recordRepo.SaveHandoverAsync(record, ct);
        return record;
    }

    public async Task AcceptCertificateAsync(AcceptanceCertificateId id, string actor, CancellationToken ct)
    {
        var cert = new AcceptanceCertificate
        {
            Id = id, IssuedBy = actor,
            CertificateDate = DateTime.UtcNow.ToString("O"),
            CreatedBy = actor, CreatedUtc = DateTime.UtcNow.ToString("O")
        };
        await _recordRepo.SaveCertificateAsync(cert, ct);
    }

    public Task<List<AsBuiltDeliverable>> ListAsBuiltAsync(string closeoutId, CancellationToken ct) => _asBuiltRepo.ListAsync(closeoutId, ct);

    public async Task<TrainingRecord> RecordTrainingAsync(string closeoutId, string topic, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var record = new TrainingRecord
        {
            Id = TrainingRecordId.New(), CloseoutId = closeoutId,
            Topic = topic, Instructor = actor, TrainingDate = now,
            CreatedBy = actor, CreatedUtc = now
        };
        await _asBuiltRepo.SaveTrainingAsync(record, ct);
        return record;
    }
}
