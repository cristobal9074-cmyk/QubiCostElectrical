namespace QubiCostElectrical;

public sealed class CircuitoSCRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Numero { get; set; } = 1;
    // "Tipo" clasifica el USO del circuito, abreviado para el pill de la grilla: A=Alumbrado,
    // E=Enchufe, CL=Clima, C=Computación, F=Fuerza. Debe calzar con RefreshCatalogCollections
    // (CubicacionScViewModel) y los DataTrigger de SistemaChipBorder/Text (CubicacionScView.xaml).
    public string Tipo { get; set; } = "A";
    public string Estado { get; set; } = "Activo";
    public string Descripcion { get; set; } = "";
    // Ubicación libre: piso individual (ej: "1") o rango (ej: "-1 al 12"). Permite
    // filtrar circuitos por piso en la vista de cubicación SC.
    public string Ubicacion { get; set; } = "";

    public decimal DistanciaTablero { get; set; }
    // LEGACY: antes un circuito tenía un único largo, canalizado por bandeja O tubería según
    // TipoRecorrido. Se conserva solo para migrar proyectos viejos — ProjectSnapshotIntegrity
    // siembra MetrosBandeja/MetrosTuberia desde este valor la primera vez. El código nuevo usa
    // los dos largos independientes de abajo; el ViewModel mantiene LargoCircuito = suma de ambos.
    public decimal LargoCircuito { get; set; }
    // Largos independientes por tramo: un circuito puede correr parte por bandeja y parte por
    // tubería/PVC, cada uno con su propio metraje editable (columnas L. Band / L. PVC) y su
    // propio total de conductor. Ya no se excluyen mutuamente por TipoRecorrido.
    // Cuando el circuito se cubica por piso (PisosTramos no vacío), estos dos campos se ignoran y
    // el metraje efectivo es la suma de los tramos (MetrosBandejaEfectivo/MetrosTuberiaEfectivo).
    public decimal MetrosBandeja { get; set; }
    public decimal MetrosTuberia { get; set; }
    public decimal MetrosTuberia2 { get; set; }

    // Desglose por piso: cuando un circuito abarca varios pisos (ej. "-1 al 10"), cada piso lleva
    // su propio metraje de bandeja y tubería. Vacío = circuito de un solo largo (usa Metros*).
    // Se persiste dentro del JSON del cuadro (retrocompatible, sin migración SQL).
    public List<CircuitoPisoTramo> PisosTramos { get; set; } = new();
    public bool TienePisos => PisosTramos.Count > 0;
    public decimal MetrosBandejaEfectivo => TienePisos ? PisosTramos.Sum(t => t.MetrosBandeja) : MetrosBandeja;
    public decimal MetrosTuberiaEfectivo => TienePisos ? PisosTramos.Sum(t => t.MetrosTuberia) : MetrosTuberia;
    // Recorrido: por defecto se canaliza en tubería; si es por bandeja, TipoTuberia/
    // SeccionTuberia no aplican y se usan TipoBandeja/AnchoBandeja en su lugar.
    public string TipoRecorrido { get; set; } = "Tubería";
    public string TipoTuberia { get; set; } = "PVCLH";
    public string SeccionTuberia { get; set; } = "20mm";
    public string TipoTuberia2 { get; set; } = "PVCLH";
    public string SeccionTuberia2 { get; set; } = "20mm";
    public string TipoBandeja { get; set; } = "";
    public decimal AnchoBandeja { get; set; }
    public string ConductorDerivacion { get; set; } = "H07V-U";
    public string SeccionDerivacion { get; set; } = "4mm2";
    public string ConductorDistribucion { get; set; } = "H07V-U";
    public string SeccionDistribucion { get; set; } = "1.5mm2";
    // Valor por defecto de respaldo (monofásico = 3). Lo normal es que el ViewModel lo sincronice
    // con la alim/fase desde el catálogo (×3/×4/×5) apenas se carga/crea el circuito.
    public decimal NumConductores { get; set; } = 3m;
    // "Sistema" es la FASE de alimentación (Monofásico/Trifásico): ver
    // CubicacionScConfig.Sistemas y CubicacionSCEngine.ApplySistema, que ya usa este campo
    // para derivar NumConductores/MermaPct automáticamente al elegir la fase.
    public string Sistema { get; set; } = "M";
    public string FaseRst { get; set; } = "";
    public decimal PotenciaW { get; set; } = 0m;
    public decimal MermaPct { get; set; } = 0m;
    public List<ArtefactoSCRow> Artefactos { get; set; } = new();
    public decimal SensoresMovimiento { get; set; }
    public decimal Senaleticas { get; set; }
    public decimal CintaLedMts { get; set; }
    public decimal MontajesAlumbrado { get; set; }
    public decimal Cajas58 { get; set; }
    public decimal Cajas100x100 { get; set; }
    // 1 automático en tableros generales (una caja por circuito). En departamentos,
    // AddCircuito lo deja en 0 para que la cantidad A01 se edite en la grilla.
    public decimal Cajas100x65 { get; set; } = 1;
    public bool EsPorBandeja => string.Equals(TipoRecorrido, "Bandeja", StringComparison.OrdinalIgnoreCase);
    public bool EsActivo => string.Equals(Estado, "Activo", StringComparison.OrdinalIgnoreCase);
    public bool EsReservaEquipada => Estado.StartsWith("Reserva", StringComparison.OrdinalIgnoreCase);
    public decimal FactorMerma => 1m + (MermaPct / 100m);
    // Cada total sale de SU propio tramo × N° de conductores (que depende de la alim/fase) × merma,
    // redondeado a entero (sin decimales .0): Total M PVC = L. PVC × conductores; Total M Band =
    // L. Band × conductores. Como un circuito se canaliza por tubería O por bandeja (TipoRecorrido),
    // solo uno de los dos largos es distinto de cero, así que solo aporta el conductor de ese tramo.
    public decimal MetrosAltura => EsActivo ? Artefactos.Sum(a => a.Cantidad * a.AlturaBajada) : 0m;
    public decimal LargoTuberia => EsActivo ? (MetrosTuberiaEfectivo + MetrosAltura) : 0m;
    public decimal LargoTuberia2 => EsActivo ? MetrosTuberia2 : 0m;
    public decimal LargoTuberiaTotal => LargoTuberia + LargoTuberia2;
    public decimal LargoDerivacion => EsActivo ? Math.Round(LargoTuberiaTotal * NumConductores * FactorMerma, 0, MidpointRounding.AwayFromZero) : 0m;
    public decimal LargoDistribucion => EsActivo ? Math.Round(LargoBandeja * NumConductores * FactorMerma, 0, MidpointRounding.AwayFromZero) : 0m;
    public decimal LargoBandeja => EsActivo ? MetrosBandejaEfectivo : 0m;
    // Largo total del circuito = suma de ambos tramos (para reportes que necesitan el metraje).
    public decimal LargoTotal => LargoBandeja + LargoTuberia + LargoTuberia2;
    public int TotalCajas => (int)Math.Round(Cajas58 + Cajas100x100 + Cajas100x65);
    public bool TieneElementosConPunto => Artefactos.Any(a =>
        a.Cantidad > 0m && !string.IsNullOrWhiteSpace(a.Nombre));
    public decimal TotalElementosConPunto => TieneElementosConPunto
        ? Artefactos.Where(a => a.Cantidad > 0m && !string.IsNullOrWhiteSpace(a.Nombre)).Sum(a => a.Cantidad)
        : 0m;
    public decimal TotalCajasEfectivas => TieneElementosConPunto ? TotalElementosConPunto : TotalCajas;
    // Cada salida mediante terminal flexible desde el tramo de bandeja requiere una caja
    // metálica propia. La cantidad sigue la misma cantidad efectiva que alimenta el flexible:
    // una por elemento/salida cuando existe, y al menos una para un circuito legacy.
    public int CajasSalidaBandeja
    {
        get
        {
            if (!EsActivo || LargoBandeja <= 0m || LargoTuberia <= 0m)
                return 0;

            int salidas = (int)Math.Round(TotalCajasEfectivas, MidpointRounding.AwayFromZero);
            return Math.Max(1, salidas);
        }
    }

    /// <summary>
    /// Copia profunda del circuito (artefactos + tramos por piso) con Id nuevo y número indicado.
    /// Usado por Ctrl+C/V/D en Cubic.Sc.
    /// </summary>
    public CircuitoSCRow Clone(int nuevoNumero) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Numero = nuevoNumero,
        Tipo = Tipo,
        Estado = Estado,
        Descripcion = Descripcion,
        Ubicacion = Ubicacion,
        DistanciaTablero = DistanciaTablero,
        LargoCircuito = LargoCircuito,
        MetrosBandeja = MetrosBandeja,
        MetrosTuberia = MetrosTuberia,
        MetrosTuberia2 = MetrosTuberia2,
        PisosTramos = PisosTramos.Select(t => new CircuitoPisoTramo
        {
            ZonaId = t.ZonaId,
            ZonaNombre = t.ZonaNombre,
            MetrosBandeja = t.MetrosBandeja,
            MetrosTuberia = t.MetrosTuberia,
        }).ToList(),
        TipoRecorrido = TipoRecorrido,
        TipoTuberia = TipoTuberia,
        SeccionTuberia = SeccionTuberia,
        TipoTuberia2 = TipoTuberia2,
        SeccionTuberia2 = SeccionTuberia2,
        TipoBandeja = TipoBandeja,
        AnchoBandeja = AnchoBandeja,
        ConductorDerivacion = ConductorDerivacion,
        SeccionDerivacion = SeccionDerivacion,
        ConductorDistribucion = ConductorDistribucion,
        SeccionDistribucion = SeccionDistribucion,
        NumConductores = NumConductores,
        Sistema = Sistema,
        FaseRst = FaseRst,
        PotenciaW = PotenciaW,
        MermaPct = MermaPct,
        Artefactos = Artefactos.Select(a => new ArtefactoSCRow
        {
            Nombre = a.Nombre,
            Categoria = a.Categoria,
            Cantidad = a.Cantidad,
            ZonaId = a.ZonaId,
            AlturaBajada = a.AlturaBajada,
        }).ToList(),
        SensoresMovimiento = SensoresMovimiento,
        Senaleticas = Senaleticas,
        CintaLedMts = CintaLedMts,
        MontajesAlumbrado = MontajesAlumbrado,
        Cajas58 = Cajas58,
        Cajas100x100 = Cajas100x100,
        Cajas100x65 = Cajas100x65,
    };
}

// Tramo de un circuito en una zona/piso específico del proyecto, con su metraje propio de
// bandeja y tubería. ZonaId referencia una ZonaProyecto (las mismas zonas de Cubicación);
// ZonaNombre queda cacheado para mostrar/agrupar aunque la zona se borre o renombre después.
public sealed class CircuitoPisoTramo
{
    public string ZonaId { get; set; } = "";
    public string ZonaNombre { get; set; } = "";
    public decimal MetrosBandeja { get; set; }
    public decimal MetrosTuberia { get; set; }
    public decimal MetrosTuberia2 { get; set; }
}

public sealed class ArtefactoSCRow
{
    public string Nombre { get; set; } = "";
    // Categoría solo para agrupar la vista (Luminaria, Enchufe, Interruptor, Caja,
    // Señalética, Sensor, Cinta LED, Montaje, Otro) — no lleva precio: C.SC cuantifica,
    // el costeo se hace aparte (APU) como hasta ahora.
    public string Categoria { get; set; } = "Otro";
    public decimal Cantidad { get; set; } = 1;
    // Zona del proyecto (ZonaProyecto.Id) a la que pertenece este artefacto, para llevar el
    // control por piso. Vacío = sin zona asignada (artefactos de proyectos anteriores a esta
    // función, que se pueden reasignar). Se persiste dentro del JSON de CuadroCargaSCRow, así
    // que agregar este campo es retrocompatible sin migración de esquema SQLite.
    public string ZonaId { get; set; } = "";
    // Altura de bajada vertical a este artefacto (m), ej: 2.20 m para enchufes, 1.30 m para interruptores.
    public decimal AlturaBajada { get; set; } = 0m;
}

public sealed class CuadroCargaSCRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nombre { get; set; } = "TDA SC";
    public string Descripcion { get; set; } = "";
    public int CircuitosProyectados { get; set; } = 40;
    public List<CircuitoSCRow> Circuitos { get; set; } = new();
    public string Categoria { get; set; } = "Generales";

    public bool EsDepto =>
        string.Equals(Categoria, "Deptos", StringComparison.OrdinalIgnoreCase) ||
        (Nombre?.Contains("DEPTO", StringComparison.OrdinalIgnoreCase) ?? false) ||
        (Nombre?.Contains("DEPARTAMENTO", StringComparison.OrdinalIgnoreCase) ?? false) ||
        (Id?.Contains("DEPTO", StringComparison.OrdinalIgnoreCase) ?? false);
}

public sealed class SistemaSC
{
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal NumConductores { get; set; } = 3m;
    public decimal MermaPct { get; set; } = 0m;
}

public sealed class FlexibleMapSC
{
    public string Diametro { get; set; } = "";
    public string TipoFlexible { get; set; } = "";
    public decimal MetrosPorCaja { get; set; } = 0.5m;
}

// Catálogo interno de artefactos: el usuario elige de esta lista al agregar un artefacto a
// un circuito, y puede crear uno nuevo al vuelo si no está (mismo patrón que el catálogo de
// materiales en Cubicación). Solo nombre + categoría, sin precio: C.SC cuantifica, no costea.
[System.Text.Json.Serialization.JsonConverter(typeof(ArtefactoCatalogoItemJsonConverter))]
public sealed class ArtefactoCatalogoItem
{
    public string Tag { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Categoria { get; set; } = "Otro";
    public decimal AlturaBajada { get; set; } = 0m;
}

/// <summary>
/// Antes de introducir ArtefactoCatalogoItem, este catálogo era una lista plana de strings
/// (igual que Tipos/TiposCaja/etc.). Proyectos guardados con esa versión anterior siguen
/// teniendo "Artefactos" como array de strings en su JSON — este converter acepta ambos
/// formatos para no romper la apertura de proyectos guardados antes del cambio de modelo.
/// </summary>
public sealed class ArtefactoCatalogoItemJsonConverter : System.Text.Json.Serialization.JsonConverter<ArtefactoCatalogoItem>
{
    public override ArtefactoCatalogoItem Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.String)
            return new ArtefactoCatalogoItem { Nombre = reader.GetString() ?? "" };

        var item = new ArtefactoCatalogoItem();
        if (reader.TokenType != System.Text.Json.JsonTokenType.StartObject)
            return item;

        while (reader.Read() && reader.TokenType != System.Text.Json.JsonTokenType.EndObject)
        {
            if (reader.TokenType != System.Text.Json.JsonTokenType.PropertyName) continue;
            var propertyName = reader.GetString();
            reader.Read();
            switch (propertyName)
            {
                case "Tag" or "tag": item.Tag = reader.GetString() ?? ""; break;
                case "Nombre" or "nombre": item.Nombre = reader.GetString() ?? ""; break;
                case "Categoria" or "categoria": item.Categoria = reader.GetString() ?? "Otro"; break;
                case "AlturaBajada" or "alturaBajada" or "Altura" or "altura":
                    if (reader.TokenType == System.Text.Json.JsonTokenType.Number)
                        item.AlturaBajada = reader.GetDecimal();
                    else if (decimal.TryParse(reader.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var alt))
                        item.AlturaBajada = alt;
                    break;
                default: reader.Skip(); break;
            }
        }
        return item;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, ArtefactoCatalogoItem value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (!string.IsNullOrWhiteSpace(value.Tag)) writer.WriteString("Tag", value.Tag);
        writer.WriteString("Nombre", value.Nombre);
        writer.WriteString("Categoria", value.Categoria);
        if (value.AlturaBajada > 0m) writer.WriteNumber("AlturaBajada", value.AlturaBajada);
        writer.WriteEndObject();
    }
}

public sealed class SistemaCircuitoSC
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
}

public sealed class CubicacionScConfig
{
    public List<SistemaCircuitoSC> SistemasCircuito { get; set; } = new();
    public List<string> Tipos { get; set; } = new();
    public List<string> TiposRecorrido { get; set; } = new();
    public List<string> TiposBandeja { get; set; } = new();
    public List<string> TiposTuberia { get; set; } = new();
    public List<string> DiametrosTuberia { get; set; } = new();
    public List<string> TiposConductor { get; set; } = new();
    public List<SeccionesConductorSC> SeccionesConductorDetalle { get; set; } = new();
    public List<string> SeccionesConductor { get; set; } = new();
    public List<string> TiposCaja { get; set; } = new();
    public List<SistemaSC> Sistemas { get; set; } = new();
    public List<FlexibleMapSC> Flexibles { get; set; } = new();
    public List<ArtefactoCatalogoItem> Artefactos { get; set; } = new();

    public static CubicacionScConfig Default() => new()
    {
        SistemasCircuito = new()
        {
            new SistemaCircuitoSC { Codigo = "A", Nombre = "Alumbrado" },
            new SistemaCircuitoSC { Codigo = "E", Nombre = "Enchufe" },
            new SistemaCircuitoSC { Codigo = "CL", Nombre = "Clima" },
            new SistemaCircuitoSC { Codigo = "CO", Nombre = "Computación" },
            new SistemaCircuitoSC { Codigo = "C", Nombre = "Computación" },
            new SistemaCircuitoSC { Codigo = "F", Nombre = "Fuerza" },
        },
        Tipos = new() { "Alumbrado", "Enchufe", "Clima", "Computación", "Emergencia" },
        TiposRecorrido = new() { "Tubería", "Bandeja" },
        TiposBandeja = new() { "Bandeja perforada", "Bandeja tipo escalerilla", "Bandeja lisa" },
        TiposTuberia = new() { "PVC", "EMT", "CAÑERIA", "PVCLH" },
        DiametrosTuberia = new() { "16mm", "20mm", "25mm", "32mm", "40mm", "50mm", "63mm" },
        TiposConductor = new() { "NYA", "H07V-U", "SUPERFLEX", "THHN", "RVK", "XTU", "EVA", "EVANOTOX", "RZ1-K" },
        SeccionesConductor = new() { "1.5mm2", "2.5mm2", "4mm2", "6mm2", "10mm2", "16mm2", "2.08mm2", "3.31mm2", "5.26mm2", "8.37mm2", "13.3mm2", "21.2mm2" },
        TiposCaja = new() { "Caja 5/8", "Caja 100x100", "Caja 100x65" },
        // Nombre abreviado (M/T/EM) para calzar con CubicacionScViewModel.Fases (el combo real
        // que ve el usuario) y los DataTrigger de FaseChipBorder/Text en CubicacionScView.xaml —
        // antes decían "Monofásico"/"Trifásico" y CubicacionSCEngine.ApplySistema nunca
        // encontraba coincidencia al elegir desde el combo abreviado, dejando NumConductores/
        // MermaPct sin aplicar en silencio.
        Sistemas = new()
        {
            new SistemaSC { Nombre = "M", Descripcion = "MONOFASICO", NumConductores = 3m, MermaPct = 5m },
            new SistemaSC { Nombre = "T", Descripcion = "TRIFASICO", NumConductores = 5m, MermaPct = 8m },
            new SistemaSC { Nombre = "EM", Descripcion = "EMERGENCIA", NumConductores = 3m, MermaPct = 5m },
        },
        Flexibles = new()
        {
            new FlexibleMapSC { Diametro = "16mm", TipoFlexible = "TUBERIA FLEXIBLE 16mm", MetrosPorCaja = 0.5m },
            new FlexibleMapSC { Diametro = "20mm", TipoFlexible = "TUBERIA FLEXIBLE 20mm", MetrosPorCaja = 0.5m },
            new FlexibleMapSC { Diametro = "25mm", TipoFlexible = "TUBERIA FLEXIBLE 25mm", MetrosPorCaja = 0.6m },
            new FlexibleMapSC { Diametro = "32mm", TipoFlexible = "TUBERIA FLEXIBLE 32mm", MetrosPorCaja = 0.7m },
        },
        Artefactos = new()
        {
            new ArtefactoCatalogoItem { Nombre = "INT 9/12", Categoria = "Interruptor", AlturaBajada = 1.30m },
            new ArtefactoCatalogoItem { Nombre = "INT 9/15", Categoria = "Interruptor", AlturaBajada = 1.30m },
            new ArtefactoCatalogoItem { Nombre = "INT 9/24", Categoria = "Interruptor", AlturaBajada = 1.30m },
            new ArtefactoCatalogoItem { Nombre = "INT 9/32", Categoria = "Interruptor", AlturaBajada = 1.30m },
            new ArtefactoCatalogoItem { Nombre = "INT 9/12+IDRO", Categoria = "Interruptor", AlturaBajada = 1.30m },
            new ArtefactoCatalogoItem { Nombre = "INT 9/15+IDRO", Categoria = "Interruptor", AlturaBajada = 1.30m },
            new ArtefactoCatalogoItem { Nombre = "INT 9/24+IDRO", Categoria = "Interruptor", AlturaBajada = 1.30m },
            new ArtefactoCatalogoItem { Nombre = "INT 9/32+IDRO", Categoria = "Interruptor", AlturaBajada = 1.30m },
            new ArtefactoCatalogoItem { Nombre = "ENCH S10", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH D10", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH T10", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH S16", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH D16", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH T16", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH S10+IDRO", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH D10+IDRO", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH T10+IDRO", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH S16+IDRO", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH D16+IDRO", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH T16+IDRO", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH MONO", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Nombre = "ENCH TRIFA", Categoria = "Enchufe", AlturaBajada = 2.20m },
            new ArtefactoCatalogoItem { Tag = "DW12", Nombre = "Downlight LED 12W", Categoria = "Luminaria", AlturaBajada = 0m },
            new ArtefactoCatalogoItem { Nombre = "Cinta LED perimetral", Categoria = "Luminaria", AlturaBajada = 0m },
            new ArtefactoCatalogoItem { Nombre = "Señalética de salida", Categoria = "Señalética", AlturaBajada = 0m },
            new ArtefactoCatalogoItem { Nombre = "Sensor de movimiento", Categoria = "Sensor", AlturaBajada = 0m },
            new ArtefactoCatalogoItem { Tag = "MDL", Nombre = "Montaje luminaria", Categoria = "Montaje", AlturaBajada = 0m },
        },
    };
}

public sealed class SeccionesConductorSC
{
    public string Seccion { get; set; } = "";
    public decimal Mm2 { get; set; }
}
