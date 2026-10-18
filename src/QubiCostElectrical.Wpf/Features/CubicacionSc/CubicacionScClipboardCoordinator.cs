using System;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

/// <summary>
/// Coordina las operaciones de portapapeles, clonación, duplicación
/// y herencia de canalización de circuitos SC.
/// </summary>
public sealed class CubicacionScClipboardCoordinator
{
    private CircuitoSCRow? clipboardCircuito;

    public CircuitoSCRow? ClipboardCircuito => clipboardCircuito;
    public bool HasClipboard => clipboardCircuito != null;

    public void ClearClipboard()
    {
        clipboardCircuito = null;
    }

    public string? Copy(CircuitoScRowVm? selectedCircuito)
    {
        if (selectedCircuito == null) return null;
        clipboardCircuito = selectedCircuito.Source.Clone(selectedCircuito.Numero);
        return $"Circuito {selectedCircuito.Numero} copiado · ve a otro circuito y Ctrl+V lo reemplaza (no agrega fila) · Ctrl+D duplica";
    }

    public string? Paste(CircuitoScRowVm? selectedCircuito, CuadroScRowVm? selectedCuadro)
    {
        if (clipboardCircuito == null || selectedCircuito == null) return null;
        int destino = selectedCircuito.Numero;
        selectedCircuito.ApplyClipboardFrom(clipboardCircuito);
        selectedCuadro?.NotifyCircuitosActivosChanged();
        return $"Circuito {destino} actualizado con la copia (mismo N°, sin fila nueva)";
    }

    public (CircuitoSCRow Clone, int Numero, string StatusMessage)? Duplicate(
        CircuitoScRowVm? selectedCircuito,
        CuadroCargaSCRow? cuadro)
    {
        if (selectedCircuito == null || cuadro == null) return null;
        int numero = NextCircuitoNumero(cuadro);
        var clone = selectedCircuito.Source.Clone(numero);
        var status = $"Circuito {numero} duplicado · cambia REC/ubicación y sigue con Ctrl+D";
        return (clone, numero, status);
    }

    public static int NextCircuitoNumero(CuadroCargaSCRow cuadro) =>
        cuadro.Circuitos.Count == 0 ? 1 : cuadro.Circuitos.Max(c => c.Numero) + 1;

    public static void ApplyCanalizacionTemplate(CircuitoSCRow from, CircuitoSCRow to)
    {
        to.Tipo = from.Tipo;
        to.Sistema = from.Sistema;
        to.FaseRst = from.FaseRst;
        to.NumConductores = from.NumConductores;
        to.MermaPct = from.MermaPct;
        to.Ubicacion = from.Ubicacion;
        to.TipoRecorrido = from.TipoRecorrido;
        to.TipoTuberia = from.TipoTuberia;
        to.SeccionTuberia = from.SeccionTuberia;
        to.TipoBandeja = from.TipoBandeja;
        to.AnchoBandeja = from.AnchoBandeja;
        to.ConductorDistribucion = from.ConductorDistribucion;
        to.SeccionDistribucion = from.SeccionDistribucion;
        to.ConductorDerivacion = from.ConductorDerivacion;
        to.SeccionDerivacion = from.SeccionDerivacion;
        to.MontajesAlumbrado = from.MontajesAlumbrado;
        to.Cajas58 = from.Cajas58;
        to.Cajas100x100 = from.Cajas100x100;
        to.Cajas100x65 = from.Cajas100x65;
        to.SensoresMovimiento = from.SensoresMovimiento;
        to.Senaleticas = from.Senaleticas;
        to.CintaLedMts = from.CintaLedMts;
        if (from.PisosTramos != null && from.PisosTramos.Count > 0)
        {
            to.PisosTramos = from.PisosTramos.Select(t => new CircuitoPisoTramo
            {
                ZonaId = t.ZonaId,
                ZonaNombre = t.ZonaNombre,
                MetrosBandeja = 0,
                MetrosTuberia = 0
            }).ToList();
        }
    }
}
