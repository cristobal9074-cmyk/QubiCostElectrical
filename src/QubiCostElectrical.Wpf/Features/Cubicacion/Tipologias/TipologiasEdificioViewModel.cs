using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion.Tipologias;

public sealed class TipologiaRecursoItemVm : ViewModelBase
{
    private string codigo = "";
    private string descripcion = "";
    private string unidad = "un";
    private string categoria = "Artefactos";
    private decimal cantidadPorDepto = 1m;
    private decimal matClp;
    private decimal moClp;
    private string partidaCodigo = "";

    public string Id { get; }

    public string Codigo
    {
        get => codigo;
        set => SetProperty(ref codigo, value);
    }

    public string Descripcion
    {
        get => descripcion;
        set => SetProperty(ref descripcion, value);
    }

    public string Unidad
    {
        get => unidad;
        set => SetProperty(ref unidad, value);
    }

    public string Categoria
    {
        get => categoria;
        set => SetProperty(ref categoria, value);
    }

    public decimal CantidadPorDepto
    {
        get => cantidadPorDepto;
        set
        {
            if (SetProperty(ref cantidadPorDepto, value))
            {
                OnPropertyChanged(nameof(TotalMatClp));
                OnPropertyChanged(nameof(TotalMoClp));
                OnPropertyChanged(nameof(TotalClp));
            }
        }
    }

    public decimal MatClp
    {
        get => matClp;
        set
        {
            if (SetProperty(ref matClp, value))
            {
                OnPropertyChanged(nameof(TotalMatClp));
                OnPropertyChanged(nameof(TotalClp));
            }
        }
    }

    public decimal MoClp
    {
        get => moClp;
        set
        {
            if (SetProperty(ref moClp, value))
            {
                OnPropertyChanged(nameof(TotalMoClp));
                OnPropertyChanged(nameof(TotalClp));
            }
        }
    }

    public string PartidaCodigo
    {
        get => partidaCodigo;
        set => SetProperty(ref partidaCodigo, value);
    }

    public decimal TotalMatClp => CantidadPorDepto * MatClp;
    public decimal TotalMoClp => CantidadPorDepto * MoClp;
    public decimal TotalClp => TotalMatClp + TotalMoClp;

    public TipologiaRecursoItemVm(TipologiaRecursoItem item)
    {
        Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
        codigo = item.Codigo;
        descripcion = item.Descripcion;
        unidad = item.Unidad;
        categoria = item.Categoria;
        cantidadPorDepto = item.CantidadPorDepto;
        matClp = item.MatClp;
        moClp = item.MoClp;
        partidaCodigo = item.PartidaCodigo;
    }

    public TipologiaRecursoItem ToDomain() => new()
    {
        Id = Id,
        Codigo = Codigo,
        Descripcion = Descripcion,
        Unidad = Unidad,
        Categoria = Categoria,
        CantidadPorDepto = CantidadPorDepto,
        MatClp = MatClp,
        MoClp = MoClp,
        PartidaCodigo = PartidaCodigo
    };
}

public sealed class TipologiaVm : ViewModelBase
{
    private string codigo = "";
    private string nombre = "";
    private string descripcion = "";
    private decimal superficieM2;

    public string Id { get; }
    public ObservableCollection<TipologiaRecursoItemVm> Items { get; } = new();

    public string Codigo
    {
        get => codigo;
        set => SetProperty(ref codigo, value);
    }

    public string Nombre
    {
        get => nombre;
        set => SetProperty(ref nombre, value);
    }

    public string Descripcion
    {
        get => descripcion;
        set => SetProperty(ref descripcion, value);
    }

    public decimal SuperficieM2
    {
        get => superficieM2;
        set => SetProperty(ref superficieM2, value);
    }

    public int TotalItems => Items.Count;
    public decimal CostoDirectoUnitarioClp => Items.Sum(i => i.TotalClp);

    public void NotifyTotalsChanged()
    {
        OnPropertyChanged(nameof(TotalItems));
        OnPropertyChanged(nameof(CostoDirectoUnitarioClp));
    }

    public TipologiaVm(TipologiaDefinicion def)
    {
        Id = string.IsNullOrWhiteSpace(def.Id) ? Guid.NewGuid().ToString("N") : def.Id;
        codigo = def.Codigo;
        nombre = def.Nombre;
        descripcion = def.Descripcion;
        superficieM2 = def.SuperficieM2;
        foreach (var item in def.Items ?? new List<TipologiaRecursoItem>())
        {
            Items.Add(new TipologiaRecursoItemVm(item));
        }
    }

    public TipologiaDefinicion ToDomain() => new()
    {
        Id = Id,
        Codigo = Codigo,
        Nombre = Nombre,
        Descripcion = Descripcion,
        SuperficieM2 = SuperficieM2,
        Items = Items.Select(i => i.ToDomain()).ToList()
    };
}

public sealed class DistribucionPisoVm : ViewModelBase
{
    private string zonaNombre = "";
    private int cantidadDeptos;

    public string Id { get; }
    public string ZonaId { get; set; }
    public string TipologiaId { get; set; }
    public string TipologiaNombre { get; set; } = "";

    public string ZonaNombre
    {
        get => zonaNombre;
        set => SetProperty(ref zonaNombre, value);
    }

    public int CantidadDeptos
    {
        get => cantidadDeptos;
        set => SetProperty(ref cantidadDeptos, Math.Max(0, value));
    }

    public DistribucionPisoVm(TipologiaDistribucionPiso dist, string tipologiaNombre = "")
    {
        Id = string.IsNullOrWhiteSpace(dist.Id) ? Guid.NewGuid().ToString("N") : dist.Id;
        ZonaId = dist.ZonaId;
        zonaNombre = dist.ZonaNombre;
        TipologiaId = dist.TipologiaId;
        cantidadDeptos = dist.CantidadDeptos;
        TipologiaNombre = tipologiaNombre;
    }

    public TipologiaDistribucionPiso ToDomain() => new()
    {
        Id = Id,
        ZonaId = ZonaId,
        ZonaNombre = ZonaNombre,
        TipologiaId = TipologiaId,
        CantidadDeptos = CantidadDeptos
    };
}

public sealed class TipologiasEdificioViewModel : ViewModelBase
{
    private TipologiaVm? selectedTipologia;
    private TipologiaRecursoItemVm? selectedItem;
    private int selectedTab = 0;
    private readonly decimal valorUf;

    public ObservableCollection<TipologiaVm> Tipologias { get; } = new();
    public ObservableCollection<DistribucionPisoVm> Distribucion { get; } = new();
    public ObservableCollection<MaterialConsolidadoTipologiaDto> Consolidados { get; } = new();

    public TipologiaVm? SelectedTipologia
    {
        get => selectedTipologia;
        set
        {
            if (SetProperty(ref selectedTipologia, value))
            {
                OnPropertyChanged(nameof(HasSelectedTipologia));
            }
        }
    }

    public bool HasSelectedTipologia => SelectedTipologia != null;

    public TipologiaRecursoItemVm? SelectedItem
    {
        get => selectedItem;
        set => SetProperty(ref selectedItem, value);
    }

    public int SelectedTab
    {
        get => selectedTab;
        set
        {
            if (SetProperty(ref selectedTab, value) && value == 2)
            {
                RecalcularConsolidado();
            }
        }
    }

    public int TotalDepartamentos => Distribucion.Sum(d => Math.Max(0, d.CantidadDeptos));
    public int TotalTipologiasCount => Tipologias.Count;
    public decimal TotalMaterialesClp => Consolidados.Sum(c => c.TotalMatClp);
    public decimal TotalManoObraClp => Consolidados.Sum(c => c.TotalMoClp);
    public decimal TotalCostoClp => TotalMaterialesClp + TotalManoObraClp;
    public decimal TotalCostoUf => valorUf > 0m ? Math.Round(TotalCostoClp / valorUf, 3) : 0m;

    public ICommand AgregarTipologiaCommand { get; }
    public ICommand EliminarTipologiaCommand { get; }
    public ICommand AgregarInsumoCommand { get; }
    public ICommand EliminarInsumoCommand { get; }
    public ICommand GenerarPisosCommand { get; }
    public ICommand RecalcularCommand { get; }

    public TipologiasEdificioViewModel(TipologiasEdificioData? initialData, decimal valorUfClp = 38500m)
    {
        valorUf = valorUfClp > 0m ? valorUfClp : PriceConversion.DefaultValorUfClp;

        var data = initialData ?? TipologiasEdificioData.Default();
        foreach (var tip in data.Tipologias ?? new List<TipologiaDefinicion>())
        {
            Tipologias.Add(new TipologiaVm(tip));
        }

        var tipNames = Tipologias.ToDictionary(t => t.Id, t => $"{t.Codigo} - {t.Nombre}");
        foreach (var dist in data.Distribucion ?? new List<TipologiaDistribucionPiso>())
        {
            tipNames.TryGetValue(dist.TipologiaId, out var name);
            Distribucion.Add(new DistribucionPisoVm(dist, name ?? "Tipología"));
        }

        if (Tipologias.Count > 0)
            SelectedTipologia = Tipologias[0];

        AgregarTipologiaCommand = new RelayCommand(AgregarTipologia);
        EliminarTipologiaCommand = new RelayCommand(EliminarTipologia, () => HasSelectedTipologia);
        AgregarInsumoCommand = new RelayCommand(AgregarInsumo, () => HasSelectedTipologia);
        EliminarInsumoCommand = new RelayCommand(EliminarInsumo, () => SelectedItem != null);
        GenerarPisosCommand = new RelayCommand(GenerarPisosDefault);
        RecalcularCommand = new RelayCommand(RecalcularConsolidado);

        RecalcularConsolidado();
    }

    private void AgregarTipologia()
    {
        int num = Tipologias.Count + 1;
        var nueva = new TipologiaDefinicion
        {
            Codigo = $"TIPO-{(char)('A' + num - 1)}",
            Nombre = $"Departamento Tipo {(char)('A' + num - 1)}",
            SuperficieM2 = 50m
        };
        // Agregar insumos estándar iniciales
        nueva.Items.Add(new TipologiaRecursoItem { Codigo = "LUM-01", Descripcion = "Centro de Alumbrado", Unidad = "un", Categoria = "Artefactos", CantidadPorDepto = 10m, MatClp = 4500m, MoClp = 2500m });
        nueva.Items.Add(new TipologiaRecursoItem { Codigo = "ENC-01", Descripcion = "Enchufe Doble 10A", Unidad = "un", Categoria = "Artefactos", CantidadPorDepto = 14m, MatClp = 3200m, MoClp = 1800m });
        nueva.Items.Add(new TipologiaRecursoItem { Codigo = "TAB-01", Descripcion = "Tablero TDA Domiciliario", Unidad = "un", Categoria = "Tableros", CantidadPorDepto = 1m, MatClp = 85000m, MoClp = 35000m });
        nueva.Items.Add(new TipologiaRecursoItem { Codigo = "CON-01", Descripcion = "Conductor EVA 2.5 mm2", Unidad = "m", Categoria = "Conductores", CantidadPorDepto = 150m, MatClp = 420m, MoClp = 280m });

        var vm = new TipologiaVm(nueva);
        Tipologias.Add(vm);
        SelectedTipologia = vm;
        OnPropertyChanged(nameof(TotalTipologiasCount));
    }

    private void EliminarTipologia()
    {
        if (SelectedTipologia == null) return;
        string id = SelectedTipologia.Id;
        Tipologias.Remove(SelectedTipologia);

        var toRemove = Distribucion.Where(d => d.TipologiaId == id).ToList();
        foreach (var d in toRemove) Distribucion.Remove(d);

        SelectedTipologia = Tipologias.FirstOrDefault();
        OnPropertyChanged(nameof(TotalTipologiasCount));
        OnPropertyChanged(nameof(TotalDepartamentos));
        RecalcularConsolidado();
    }

    private void AgregarInsumo()
    {
        if (SelectedTipologia == null) return;
        var item = new TipologiaRecursoItem
        {
            Descripcion = "Nuevo punto / recurso",
            Unidad = "un",
            Categoria = "Materiales",
            CantidadPorDepto = 1m
        };
        var vm = new TipologiaRecursoItemVm(item);
        SelectedTipologia.Items.Add(vm);
        SelectedItem = vm;
        SelectedTipologia.NotifyTotalsChanged();
    }

    private void EliminarInsumo()
    {
        if (SelectedTipologia == null || SelectedItem == null) return;
        SelectedTipologia.Items.Remove(SelectedItem);
        SelectedItem = SelectedTipologia.Items.FirstOrDefault();
        SelectedTipologia.NotifyTotalsChanged();
    }

    public void GenerarPisosDefault()
    {
        if (Tipologias.Count == 0) return;
        Distribucion.Clear();

        // Generar 15 pisos residenciales estándar con 4 deptos del primer tipo y 2 del segundo si existe
        var tipo1 = Tipologias[0];
        var tipo2 = Tipologias.Count > 1 ? Tipologias[1] : null;

        for (int p = 1; p <= 15; p++)
        {
            Distribucion.Add(new DistribucionPisoVm(new TipologiaDistribucionPiso
            {
                ZonaId = $"piso-{p}",
                ZonaNombre = $"Piso {p}",
                TipologiaId = tipo1.Id,
                CantidadDeptos = 4
            }, $"{tipo1.Codigo} - {tipo1.Nombre}"));

            if (tipo2 != null)
            {
                Distribucion.Add(new DistribucionPisoVm(new TipologiaDistribucionPiso
                {
                    ZonaId = $"piso-{p}",
                    ZonaNombre = $"Piso {p}",
                    TipologiaId = tipo2.Id,
                    CantidadDeptos = 2
                }, $"{tipo2.Codigo} - {tipo2.Nombre}"));
            }
        }

        OnPropertyChanged(nameof(TotalDepartamentos));
        RecalcularConsolidado();
    }

    public void RecalcularConsolidado()
    {
        Consolidados.Clear();
        var domainData = ToDomain();
        var lista = TipologiasEngine.CalcularConsolidadoMateriales(domainData);
        foreach (var item in lista)
        {
            Consolidados.Add(item);
        }

        OnPropertyChanged(nameof(TotalDepartamentos));
        OnPropertyChanged(nameof(TotalMaterialesClp));
        OnPropertyChanged(nameof(TotalManoObraClp));
        OnPropertyChanged(nameof(TotalCostoClp));
        OnPropertyChanged(nameof(TotalCostoUf));
    }

    public TipologiasEdificioData ToDomain()
    {
        return new TipologiasEdificioData
        {
            Tipologias = Tipologias.Select(t => t.ToDomain()).ToList(),
            Distribucion = Distribucion.Select(d => d.ToDomain()).ToList()
        };
    }
}
