using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class LessonLearnedService : ILessonLearnedService
{
    private readonly ILessonLearnedRepository _repo;
    public LessonLearnedService(ILessonLearnedRepository repo) => _repo = repo;

    public async Task<LessonLearned> CreateAsync(string closeoutId, string title, LessonCategory category, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var lesson = new LessonLearned
        {
            Id = LessonLearnedId.New(), CloseoutId = closeoutId,
            Title = title, Category = category, Status = LessonStatus.Draft,
            Author = actor, CreatedBy = actor, CreatedUtc = now,
            ModifiedBy = actor, ModifiedUtc = now,
            Version = 1, RowVersion = Guid.NewGuid().ToString("N")
        };
        await _repo.SaveAsync(lesson, ct);
        return lesson;
    }

    public async Task ApproveAsync(LessonLearnedId id, string actor, CancellationToken ct)
    {
        var lesson = await _repo.GetByIdAsync(id, ct);
        if (lesson == null) throw new InvalidOperationException("Lección no encontrada.");
        lesson.Status = LessonStatus.Approved;
        lesson.Reviewer = actor;
        lesson.ModifiedBy = actor;
        lesson.ModifiedUtc = DateTime.UtcNow.ToString("O");
        await _repo.SaveAsync(lesson, ct);
    }

    public async Task PublishAsync(LessonLearnedId id, string actor, CancellationToken ct)
    {
        var lesson = await _repo.GetByIdAsync(id, ct);
        if (lesson == null) throw new InvalidOperationException("Lección no encontrada.");
        lesson.Status = LessonStatus.Published;
        lesson.ModifiedBy = actor;
        lesson.ModifiedUtc = DateTime.UtcNow.ToString("O");
        await _repo.SaveAsync(lesson, ct);
    }

    public Task<List<LessonLearned>> ListAsync(string? closeoutId, CancellationToken ct) => _repo.ListAsync(closeoutId, ct);
}
