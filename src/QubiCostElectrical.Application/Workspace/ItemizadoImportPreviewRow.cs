using System;

namespace QubiCostElectrical;

/// <summary>Fila de vista previa de importación/pega con estado de validación (Application).</summary>
public sealed class ItemizadoImportPreviewRow
{
    private readonly ItemizadoRow source;
    private bool duplicateWbs;

    public ItemizadoImportPreviewRow(ItemizadoRow source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        Item = source.Item ?? "";
        Descripcion = source.Descripcion ?? "";
        Unidad = source.Unidad ?? "";
        Cantidad = source.Cantidad;
        EsTitulo = source.EsTitulo;
        Revalidate();
    }

    public ItemizadoRow Source => source;
    public bool EsTitulo { get; }

    public string Item { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public decimal Cantidad { get; set; }

    public bool IsRejected { get; private set; }
    public string RejectionReason { get; private set; } = "";

    public void SetDuplicateWbs(bool value)
    {
        duplicateWbs = value;
        Revalidate();
    }

    public void Revalidate()
    {
        if (EsTitulo)
        {
            IsRejected = false;
            RejectionReason = "";
            return;
        }

        if (duplicateWbs && !string.IsNullOrWhiteSpace(Item))
        {
            IsRejected = true;
            RejectionReason = "Código WBS duplicado";
        }
        else if (string.IsNullOrWhiteSpace(Descripcion))
        {
            IsRejected = true;
            RejectionReason = "Descripción requerida";
        }
        else if (string.IsNullOrWhiteSpace(Item))
        {
            IsRejected = false;
            RejectionReason = "Sin código — se importará igual";
        }
        else if (Cantidad < 0)
        {
            IsRejected = true;
            RejectionReason = "Cantidad no puede ser < 0";
        }
        else
        {
            IsRejected = false;
            RejectionReason = "";
        }
    }

    public void FixEmptyCode()
    {
        if (string.IsNullOrWhiteSpace(Item))
            Item = "(sin código)";
        Revalidate();
    }

    public void FixQuantity()
    {
        if (Cantidad < 0)
            Cantidad = 0;
        Revalidate();
    }

    public ItemizadoRow ToRow() => new()
    {
        PartidaId = source.PartidaId == Guid.Empty ? Guid.NewGuid() : source.PartidaId,
        Id = source.Id,
        Item = Item,
        WBSCode = ItemizadoHierarchyRules.NormalizeIdFromItem(Item),
        Descripcion = Descripcion,
        Unidad = Unidad,
        Cantidad = Cantidad,
        EsTitulo = EsTitulo,
        ParentId = source.ParentId,
        NivelJerarquico = source.NivelJerarquico,
        ExcluidoCubicacion = source.ExcluidoCubicacion,
        Observaciones = source.Observaciones,
    };
}
