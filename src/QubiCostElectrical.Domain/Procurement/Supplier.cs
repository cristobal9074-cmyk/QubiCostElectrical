using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class Supplier
{
    public SupplierId Id { get; init; } = null!;
    public string Code { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Specialties { get; set; } = string.Empty;
    public string CategoriesJson { get; set; } = "[]";
    public string AcceptedCurrenciesJson { get; set; } = "[\"CLP\"]";
    public string UsualPaymentTerms { get; set; } = string.Empty;
    public int UsualLeadTimeDays { get; set; }
    public SupplierStatus Status { get; set; } = SupplierStatus.Prospect;
    public string Remarks { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");
    public string CompatibilityMetadata { get; set; } = string.Empty;
}

public sealed class SupplierContact
{
    public string ContactId { get; init; } = null!;
    public SupplierId SupplierId { get; init; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsCommercial { get; set; }
    public bool IsTechnical { get; set; }
    public bool IsDispatch { get; set; }
    public bool IsActive { get; set; } = true;
    public string Remarks { get; set; } = string.Empty;
}

public sealed class SupplyCategory
{
    public string CategoryId { get; init; } = null!;
    public SupplyCategoryKind Kind { get; init; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class SupplyCategoryCatalog
{
    public static IReadOnlyList<SupplyCategory> Defaults { get; } = new[]
    {
        Cat("MAT", "Materiales", SupplyCategoryKind.Materials),
        Cat("EQ", "Equipos", SupplyCategoryKind.Equipment),
        Cat("HER", "Herramientas", SupplyCategoryKind.Tools),
        Cat("SUB", "Subcontratos", SupplyCategoryKind.Subcontracts),
        Cat("TRA", "Transporte", SupplyCategoryKind.Transport),
        Cat("SER", "Servicios", SupplyCategoryKind.Services),
        Cat("ARR", "Arriendo", SupplyCategoryKind.Rental),
        Cat("ING", "Ingeniería", SupplyCategoryKind.Engineering),
        Cat("OTR", "Otros", SupplyCategoryKind.Other)
    };

    private static SupplyCategory Cat(string code, string name, SupplyCategoryKind kind) =>
        new() { CategoryId = code, Code = code, Name = name, Kind = kind };
}
