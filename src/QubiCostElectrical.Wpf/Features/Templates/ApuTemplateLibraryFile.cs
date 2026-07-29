using System;
using System.Collections.Generic;

namespace QubiCostElectrical.Wpf.Features.Templates;

internal sealed class ApuTemplateLibraryFile
{
    public string Format { get; set; } = "QubiCostElectrical.ApuTemplateLibrary";
    public int Version { get; set; } = 1;
    public DateTime ExportedAt { get; set; } = AppClock.Now;
    public List<ApuTemplateRow> Templates { get; set; } = new();
}
