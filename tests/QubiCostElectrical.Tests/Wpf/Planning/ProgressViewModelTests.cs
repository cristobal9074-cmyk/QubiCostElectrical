using System;
using Xunit;
using QubiCostElectrical.Wpf.Features.ProgressWorkspace;

namespace QubiCostElectrical.Tests.Wpf.Planning;

public sealed class ProgressViewModelTests
{
    [Fact]
    public void Editor_validates_percent_range()
    {
        var vm = new ActivityProgressEditorViewModel { PercentComplete = 150 };
        vm.ShowImmediateValidationHint();
        Assert.Contains("0 y 100", vm.ValidationMessage);
    }

    [Fact]
    public void Editor_validates_finish_without_start()
    {
        var vm = new ActivityProgressEditorViewModel { ActualFinish = DateTime.Today };
        vm.ShowImmediateValidationHint();
        Assert.Contains("término real sin inicio", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Editor_validates_finish_before_start()
    {
        var vm = new ActivityProgressEditorViewModel
        {
            ActualStart = DateTime.Today,
            ActualFinish = DateTime.Today.AddDays(-1)
        };
        vm.ShowImmediateValidationHint();
        Assert.Contains("anterior", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Editor_to_update_maps_fields()
    {
        var sid = ScheduleId.New();
        var aid = ActivityId.New();
        var vm = new ActivityProgressEditorViewModel
        {
            ActualStart = new DateTime(2026, 1, 2),
            PercentComplete = 25,
            ExecutedQuantity = 10,
            ActualHours = 5,
            ActualCost = 100,
            Comment = "ok",
            Evidence = "doc1",
            DelayReason = "Weather"
        };
        var upd = vm.ToUpdate(sid, aid, new DateTime(2026, 1, 15), "tester");
        Assert.Equal(25m, upd.PercentComplete);
        Assert.Equal(10m, upd.ExecutedQuantity);
        Assert.Equal(DelayReasonCode.Weather, upd.DelayReason);
        Assert.Equal("doc1", upd.EvidenceReference);
    }

    [Fact]
    public void Export_helper_writes_csv_without_recalc()
    {
        var rows = new[]
        {
            new ProgressRowVm
            {
                Code = "A1", Name = "Act", Status = "InProgress", PercentComplete = 25,
                PhysicalProgress = 25, FinancialProgress = 10, IsCritical = false
            }
        };
        var path = ProgressExportHelper.ExportSummaryCsv(rows, "resumen", new DateTime(2026, 1, 15));
        Assert.True(System.IO.File.Exists(path));
        var text = System.IO.File.ReadAllText(path);
        Assert.Contains("A1", text);
        Assert.Contains("FechaEstado", text);
        try { System.IO.File.Delete(path); } catch { }
    }

    [Fact]
    public void ProgressRow_layer_label_mentions_baseline_plan_actual_forecast()
    {
        var row = new ProgressRowVm();
        Assert.Contains("BL", row.LayerLabel);
        Assert.Contains("Forecast", row.LayerLabel);
    }
}
