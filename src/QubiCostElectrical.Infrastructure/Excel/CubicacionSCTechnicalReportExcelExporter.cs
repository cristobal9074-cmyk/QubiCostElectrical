using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace QubiCostElectrical;

public sealed partial class CubicacionSCTechnicalReportExcelExporter : ICubicacionSCReportExcelExporter
{
    private readonly ICurrencyConversionService currencyConversion;
}
