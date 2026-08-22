// TODO(null-safety): archivo proveniente de decompilación; nulabilidad pendiente de revisión manual.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8619, CS8625, CS8622, CS4014
#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class PrecioListaMaterialVm : INotifyPropertyChanged
{
	private readonly PriceBankEntry bankEntry;

	private readonly BudgetProjectSession session;

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
			OnPropertyChanged("MatClp");
			OnPropertyChanged("SinPrecio");
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
			OnPropertyChanged("MoClp");
		}
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

	public event PropertyChangedEventHandler? PropertyChanged;

	public PrecioListaMaterialVm(PriceBankEntry entry, BudgetProjectSession session)
	{
		bankEntry = entry;
		this.session = session;
		Codigo = entry.Codigo;
		Descripcion = entry.Descripcion;
		Unidad = entry.Unidad;
		Categoria = entry.Categoria;
		MatClp = entry.MatClp;
		MoClp = entry.MoClp;
		Proveedor = entry.Proveedor;
		FechaPrecio = entry.FechaPrecio;
	}

	public void SyncToBank(IPriceBankStore store, decimal valorUfClp)
	{
		bankEntry.Codigo = Codigo;
		bankEntry.Descripcion = Descripcion;
		bankEntry.Unidad = Unidad;
		bankEntry.MatClp = MatClp;
		bankEntry.MoClp = MoClp;
		bankEntry.FechaPrecio = DateTime.Today;
		bankEntry.Proveedor = Proveedor;
		bankEntry.RecalcularUf(valorUfClp);
		store.Upsert(bankEntry);
		MaterialRow materialRow = session.Materiales.FirstOrDefault((MaterialRow x) => string.Equals(x.Codigo, Codigo, StringComparison.OrdinalIgnoreCase));
		if (materialRow != null)
		{
			materialRow.Codigo = Codigo;
			materialRow.Descripcion = Descripcion;
			materialRow.Unidad = Unidad;
			materialRow.MatClp = MatClp;
			materialRow.MoClp = MoClp;
			materialRow.Proveedor = Proveedor;
			materialRow.FechaPrecio = DateTime.Today;
			materialRow.UltimaActualizacionPrecio = AppClock.Now;
			materialRow.RecalcularUf(valorUfClp);
		}
	}

	private void OnPropertyChanged(string prop)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
	}
}
