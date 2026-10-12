#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class PrecioListaMaterialVm : INotifyPropertyChanged
{
    // Entrada del banco de precios global. Null en los materiales que existen SOLO en el proyecto:
    // esos no tienen —ni deben tener— fila en el banco compartido entre proyectos.
    private readonly PriceBankEntry? bankEntry;

    // Material del catálogo del .qce. Referencia directa en los materiales solo-proyecto (para que
    // renombrar el código no los desenganche); en los del banco se resuelve por código al guardar.
    private readonly MaterialRow? materialRow;

    private readonly BudgetProjectSession session;

    /// <summary>
    /// Material que vive únicamente en el proyecto. El alta de esta pantalla lo crea así: son
    /// materiales de ESTE proyecto (un tablero, una luminaria puntual), no catálogo reutilizable, y
    /// no deben engordar el banco de precios compartido entre todos los proyectos.
    /// </summary>
    public bool EsSoloProyecto { get; }

    private string codigo = "";

    private string descripcion = "";

    private string unidad = "";

    private decimal matClp;

    private decimal moClp;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Codigo
    {
        get
        {
            return codigo;
        }
        set
        {
            codigo = value ?? "";
            OnPropertyChanged("Codigo");
        }
    }

    public string Descripcion
    {
        get
        {
            return descripcion;
        }
        set
        {
            descripcion = value ?? "";
            OnPropertyChanged("Descripcion");
        }
    }

    public string Unidad
    {
        get
        {
            return unidad;
        }
        set
        {
            unidad = value ?? "";
            OnPropertyChanged("Unidad");
        }
    }

    public string Categoria { get; set; } = "";

    public decimal MatClp
    {
        get
        {
            return matClp;
        }
        set
        {
            matClp = value;
            NotifyPriceChanged();
        }
    }

    public decimal MoClp
    {
        get
        {
            return moClp;
        }
        set
        {
            moClp = value;
            NotifyPriceChanged();
        }
    }

    private void NotifyPriceChanged()
    {
        OnPropertyChanged("MatClp");
        OnPropertyChanged("MoClp");
        OnPropertyChanged("SinPrecio");
        OnPropertyChanged("EstadoDisplay");
        OnPropertyChanged("EstadoBadgeBackground");
        OnPropertyChanged("EstadoBadgeBorder");
        OnPropertyChanged("EstadoBadgeForeground");
        OnPropertyChanged("EstadoDotBrush");
    }

    public string Proveedor { get; set; } = "";

    public DateTime FechaPrecio { get; set; } = DateTime.Today;

    public bool SinPrecio
    {
        get
        {
            if (MatClp == 0m)
            {
                return MoClp == 0m;
            }
            return false;
        }
    }

    public int AntiguedadDias
    {
        get
        {
            if (!(FechaPrecio == default(DateTime)))
            {
                return Math.Max(0, (DateTime.Today - FechaPrecio.Date).Days);
            }
            return 0;
        }
    }

    public string VigenciaLabel
    {
        get
        {
            if (AntiguedadDias > 90)
            {
                if (AntiguedadDias > 180)
                {
                    return "Vencido";
                }
                return "Revisar";
            }
            return "Vigente";
        }
    }

    public string VigenciaColor
    {
        get
        {
            if (AntiguedadDias > 90)
            {
                if (AntiguedadDias > 180)
                {
                    return "Danger";
                }
                return "Amber";
            }
            return "Success";
        }
    }

    public string EstadoDisplay
    {
        get
        {
            if (SinPrecio) return "Sin precio";
            return VigenciaLabel;
        }
    }

    public string EstadoBadgeBackground
    {
        get
        {
            if (SinPrecio) return "#FFFBEB";
            return VigenciaColor switch
            {
                "Success" => "#ECFDF5",
                "Amber" => "#FFFBEB",
                _ => "#FEF2F2"
            };
        }
    }

    public string EstadoBadgeBorder
    {
        get
        {
            if (SinPrecio) return "#FDE68A";
            return VigenciaColor switch
            {
                "Success" => "#A7F3D0",
                "Amber" => "#FDE68A",
                _ => "#FECACA"
            };
        }
    }

    public string EstadoBadgeForeground
    {
        get
        {
            if (SinPrecio) return "#B45309";
            return VigenciaColor switch
            {
                "Success" => "#047857",
                "Amber" => "#B45309",
                _ => "#B91C1C"
            };
        }
    }

    public string EstadoDotBrush
    {
        get
        {
            if (SinPrecio) return "#F59E0B";
            return VigenciaColor switch
            {
                "Success" => "#10B981",
                "Amber" => "#F59E0B",
                _ => "#EF4444"
            };
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PrecioListaMaterialVm(PriceBankEntry entry, BudgetProjectSession session)
        : this(session, esSoloProyecto: false, materialRow: null)
    {
        bankEntry = entry;
        Codigo = entry.Codigo;
        Descripcion = entry.Descripcion;
        Unidad = entry.Unidad;
        Categoria = entry.Categoria;
        MatClp = entry.MatClp;
        MoClp = entry.MoClp;
        Proveedor = entry.Proveedor;
        FechaPrecio = entry.FechaPrecio;
    }

    /// <summary>
    /// Material del catálogo del proyecto que no está en el banco de precios global.
    /// </summary>
    public PrecioListaMaterialVm(MaterialRow material, BudgetProjectSession session)
        : this(session, esSoloProyecto: true, materialRow: material)
    {
        Codigo = material.Codigo;
        Descripcion = material.Descripcion;
        Unidad = material.Unidad;
        Categoria = material.Categoria;
        MatClp = material.MatClp;
        MoClp = material.MoClp;
        Proveedor = material.Proveedor;
        FechaPrecio = material.FechaPrecio;
    }

    private PrecioListaMaterialVm(BudgetProjectSession session, bool esSoloProyecto, MaterialRow? materialRow)
    {
        this.session = session;
        EsSoloProyecto = esSoloProyecto;
        this.materialRow = materialRow;
    }

    /// <summary>
    /// Guarda la fila donde corresponde: siempre en el catálogo del proyecto y, solo si la fila
    /// viene del banco de precios, también en el banco global. Un material nacido en este proyecto
    /// no se propaga al banco compartido.
    /// </summary>
    public void Guardar(IPriceBankStore store, decimal valorUfClp)
    {
        // materialRow directo cuando la fila es del proyecto; si no, se busca por código, que es el
        // caso de los materiales del banco que además existen en el catálogo del .qce.
        MaterialRow? fila = materialRow
            ?? session.Materiales.FirstOrDefault((MaterialRow x) => CodeHelper.Match(x.Codigo, Codigo));
        if (fila != null)
        {
            fila.Codigo = Codigo;
            fila.Descripcion = Descripcion;
            fila.Unidad = Unidad;
            fila.MatClp = MatClp;
            fila.MoClp = MoClp;
            fila.Proveedor = Proveedor;
            fila.FechaPrecio = DateTime.Today;
            fila.UltimaActualizacionPrecio = AppClock.Now;
            fila.RecalcularUf(valorUfClp);
        }

        if (EsSoloProyecto || bankEntry == null)
            return;

        bankEntry.Codigo = Codigo;
        bankEntry.Descripcion = Descripcion;
        bankEntry.Unidad = Unidad;
        bankEntry.MatClp = MatClp;
        bankEntry.MoClp = MoClp;
        bankEntry.FechaPrecio = DateTime.Today;
        bankEntry.Proveedor = Proveedor;
        bankEntry.RecalcularUf(valorUfClp);
        store.Upsert(bankEntry);
    }

    private void OnPropertyChanged(string prop)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
