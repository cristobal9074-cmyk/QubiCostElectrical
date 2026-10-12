using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Catálogo de plantillas de obras eléctricas típicas chilenas preconfiguradas
/// bajo la normativa de la Superintendencia de Electricidad y Combustibles (SEC / Pliegos RIC N° 01 al 19).
/// </summary>
public static class ProjectTemplateCatalog
{
    private static readonly Lazy<IReadOnlyList<ProjectTemplate>> TemplatesLazy = new(BuildCatalog);

    public static IReadOnlyList<ProjectTemplate> All => TemplatesLazy.Value;

    /// <summary>
    /// Plantilla en blanco: sin partidas. Punto de partida de un proyecto nuevo, para que el
    /// itemizado nazca vacío y sea el archivo —no la app— el que guarde su contenido.
    /// </summary>
    public static ProjectTemplate Blank => All.First(t => t.EsEnBlanco);

    private static IReadOnlyList<ProjectTemplate> BuildCatalog()
    {
        return new List<ProjectTemplate>
        {
            BuildBlankTemplate(),
            BuildResidentialBuildingTemplate(),
            BuildIndustrialWarehouseTemplate(),
            BuildSolarPvPlantTemplate(),
            BuildCommercialRetailTemplate()
        };
    }

    private static ProjectTemplate BuildBlankTemplate()
    {
        return new ProjectTemplate
        {
            Id = "blank",
            Nombre = "Presupuesto en Blanco",
            Subtitulo = "Sin partidas preconfiguradas",
            Descripcion = "Inicia un proyecto limpio desde cero para importar una tabla o crear partidas manualmente.",
            NormaSec = "General",
            Icono = "📄",
            EsEnBlanco = true,
            Filas = Array.Empty<ProjectTemplateItemRow>()
        };
    }

    private static ProjectTemplate BuildResidentialBuildingTemplate()
    {
        return new ProjectTemplate
        {
            Id = "residential_building",
            Nombre = "Edificio Residencial en Altura",
            Subtitulo = "Vivienda colectiva y servicios comunes",
            Descripcion = "Estructura completa para edificios habitacionales: empalmes, tableros generales, shaft eléctrico vertical, departamentos tipo, servicios comunes y certificación SEC TE1.",
            NormaSec = "RIC N° 01, 02, 03, 08, 10",
            Icono = "🏢",
            EsEnBlanco = false,
            Filas = new List<ProjectTemplateItemRow>
            {
                // Capítulo 1
                new("1", "OBRAS PRELIMINARES Y FAENAS", esTitulo: true),
                new("1.1", "Empalme provisorio y tablero de faena certificado", false, Unidades.GL, 1m),
                new("1.2", "Trazados, replanteos y canalizaciones primarias", false, Unidades.GL, 1m),
                new("1.3", "Protecciones provisorias y faenas de montaje", false, Unidades.GL, 1m),

                // Capítulo 2
                new("2", "ALIMENTADORES Y TABLEROS GENERALES", esTitulo: true),
                new("2.1", "Tablero General de Servicios Comunes (TGSC) autosoportado", false, Unidades.UN, 1m),
                new("2.2", "Tablero de Emergencia y Transferencia Automática (TTA)", false, Unidades.UN, 1m),
                new("2.3", "Banco de medidores concentrados y nichos normativos (RIC N° 01)", false, Unidades.GL, 1m),
                new("2.4", "Alimentador principal libre de halógeno H07Z1-K / EVA", false, Unidades.ML, 150m),
                new("2.5", "Escalerillas portaconductores en shaft vertical con sello cortafuego", false, Unidades.ML, 45m),

                // Capítulo 3
                new("3", "DEPARTAMENTOS TIPO", esTitulo: true),
                new("3.1", "Tablero de Distribución de Alumbrado y Enchufes (TDA)", false, Unidades.UN, 40m),
                new("3.2", "Circuitos de alumbrado embutido LED con interruptores 9/12 y 9/15", false, Unidades.UN, 120m),
                new("3.3", "Circuitos de enchufes generales 10A/16A con protección diferencial", false, Unidades.UN, 240m),
                new("3.4", "Circuitos especiales de fuerza cocina y logia 16A (RIC N° 10)", false, Unidades.UN, 80m),
                new("3.5", "Alimentación termoeléctrico y aire acondicionado", false, Unidades.UN, 40m),

                // Capítulo 4
                new("4", "SERVICIOS COMUNES Y EQUIPOS", esTitulo: true),
                new("4.1", "Iluminación de pasillos, cajas de escala y accesos con sensores", false, Unidades.UN, 60m),
                new("4.2", "Alumbrado de emergencia autónomo con autonomía 90 min (RIC N° 08)", false, Unidades.UN, 30m),
                new("4.3", "Fuerza y control para sala de bombas de agua potable", false, Unidades.GL, 1m),
                new("4.4", "Fuerza sistema de presurización de cajas de escala", false, Unidades.GL, 1m),
                new("4.5", "Alimentación eléctrica portones vehiculares y control de acceso", false, Unidades.GL, 1m),

                // Capítulo 5
                new("5", "PUESTA A TIERRA Y CORRIENTES DÉBILES", esTitulo: true),
                new("5.1", "Malla de puesta a tierra de protección y servicio (RIC N° 06)", false, Unidades.GL, 1m),
                new("5.2", "Citofonía digital intercomunicada y control de accesos", false, Unidades.GL, 1m),
                new("5.3", "Canalizaciones para corrientes débiles y fibra óptica (Ley de Ductos)", false, Unidades.GL, 1m),
                new("5.4", "Sistema de Circuito Cerrado de Televisión (CCTV)", false, Unidades.GL, 1m),

                // Capítulo 6
                new("6", "ENSAYOS Y CERTIFICACIÓN SEC", esTitulo: true),
                new("6.1", "Pruebas de aislación, continuidad y medición de resistividad", false, Unidades.GL, 1m),
                new("6.2", "Planos As-Built y carpeta técnica de obra", false, Unidades.GL, 1m),
                new("6.3", "Declaración y tramitación TE1 ante SEC", false, Unidades.GL, 1m)
            }
        };
    }

    private static ProjectTemplate BuildIndustrialWarehouseTemplate()
    {
        return new ProjectTemplate
        {
            Id = "industrial_warehouse",
            Nombre = "Galpón Industrial / Fabril",
            Subtitulo = "Fuerza motriz e infraestructura industrial",
            Descripcion = "Especializado para plantas productivas, centros logísticos y maestranzas: subestación, distribución pesada, iluminación High-Bay, circuitos trifásicos y mallas profundas.",
            NormaSec = "RIC N° 04, 06, 10, 11",
            Icono = "🏭",
            EsEnBlanco = false,
            Filas = new List<ProjectTemplateItemRow>
            {
                // Capítulo 1
                new("1", "EMPALME Y SUBESTACIÓN DEDICADA", esTitulo: true),
                new("1.1", "Acometida de media tensión y celdas de seccionamiento", false, Unidades.GL, 1m),
                new("1.2", "Transformador trifásico en aceite / seco con cuba de contención", false, Unidades.UN, 1m),
                new("1.3", "Tablero General de Fuerza y Alumbrado (TGFA) IP55", false, Unidades.UN, 1m),
                new("1.4", "Banco automático de condensadores para corrección de factor de potencia", false, Unidades.UN, 1m),

                // Capítulo 2
                new("2", "CANALIZACIONES INDUSTRIALES", esTitulo: true),
                new("2.1", "Bandejas portacables tipo escalerilla galvanizada en caliente", false, Unidades.ML, 220m),
                new("2.2", "Tuberías Conduit de acero galvanizado (CAG) a la vista", false, Unidades.ML, 350m),
                new("2.3", "Cajas de paso y derivación estancas IP66", false, Unidades.UN, 45m),

                // Capítulo 3
                new("3", "FUERZA MOTRIZ Y MAQUINARIA", esTitulo: true),
                new("3.1", "Tableros de Distribución de Fuerza Seccionales (TDF)", false, Unidades.UN, 4m),
                new("3.2", "Alimentadores trifásicos para maquinaria y compresores", false, Unidades.ML, 180m),
                new("3.3", "Tomas de corriente industriales CEE trifásicas 3P+N+T 32A/63A", false, Unidades.UN, 18m),
                new("3.4", "Circuitos de parada de emergencia y enclavamientos de seguridad", false, Unidades.GL, 1m),

                // Capítulo 4
                new("4", "ILUMINACIÓN INDUSTRIAL HIGH-BAY", esTitulo: true),
                new("4.1", "Luminarias industriales tipo campana LED UFO High-Bay 150W/200W IP65", false, Unidades.UN, 36m),
                new("4.2", "Proyectores LED exteriores perimetrales sobre poste", false, Unidades.UN, 12m),
                new("4.3", "Tablero de control y encendido por contactores / fotoceldas", false, Unidades.UN, 1m),

                // Capítulo 5
                new("5", "PUESTA A TIERRA EQUIPOTENCIAL Y PARARRAYOS", esTitulo: true),
                new("5.1", "Malla de tierra profunda con soldadura exotérmica (RIC N° 06)", false, Unidades.GL, 1m),
                new("5.2", "Anillo equipotencial perimetral a estructura metálica", false, Unidades.ML, 120m),
                new("5.3", "Sistema de captación de descargas atmosféricas (pararrayos)", false, Unidades.UN, 1m),

                // Capítulo 6
                new("6", "PRUEBAS, PROTOCOLOS Y TRAMITACIÓN TE1", esTitulo: true),
                new("6.1", "Inspección termográfica de tableros y conexiones bajo carga", false, Unidades.GL, 1m),
                new("6.2", "Protocolo de medición de resistencia de puesta a tierra", false, Unidades.GL, 1m),
                new("6.3", "Tramitación y certificación TE1 Industrial ante SEC", false, Unidades.GL, 1m)
            }
        };
    }

    private static ProjectTemplate BuildSolarPvPlantTemplate()
    {
        return new ProjectTemplate
        {
            Id = "solar_pv_plant",
            Nombre = "Planta Solar Fotovoltaica PMGD / On-Grid",
            Subtitulo = "Generación renovable y medición neta",
            Descripcion = "Diseñado para instalaciones fotovoltaicas industriales, techos solares y plantas PMGD bajo el pliego técnico RIC N° 19 y ley de Net Billing / PMGD.",
            NormaSec = "RIC N° 19, RIC N° 06",
            Icono = "☀️",
            EsEnBlanco = false,
            Filas = new List<ProjectTemplateItemRow>
            {
                // Capítulo 1
                new("1", "ESTRUCTURAS Y MONTAJE MECÁNICO", esTitulo: true),
                new("1.1", "Hincado de perfiles o anclaje sobre cubierta metálica", false, Unidades.GL, 1m),
                new("1.2", "Estructura de montaje coplanar / inclinada de aluminio anodizado", false, Unidades.GL, 1m),

                // Capítulo 2
                new("2", "CAMPO FOTOVOLTAICO (LADO DC)", esTitulo: true),
                new("2.1", "Módulos fotovoltaicos monocristalinos Tier-1 (550W+)", false, Unidades.UN, 200m),
                new("2.2", "Cableado solar especializado 1x6mm² 1500V resistente a rayos UV", false, Unidades.ML, 1200m),
                new("2.3", "Conectores MC4 originales IP68 y fusibles gPV", false, Unidades.GL, 1m),
                new("2.4", "Caja combinadora de strings (String Box) con seccionador y SPD DC", false, Unidades.UN, 2m),

                // Capítulo 3
                new("3", "INVERSORES Y CONVERSIÓN", esTitulo: true),
                new("3.1", "Inversores solares trifásicos On-Grid de string con MPPT múltiple", false, Unidades.UN, 2m),
                new("3.2", "Sistema de monitoreo remoto y comunicaciones Modbus/RS485", false, Unidades.GL, 1m),
                new("3.3", "Sensores de radiación solar y temperatura de módulo", false, Unidades.UN, 2m),

                // Capítulo 4
                new("4", "CONEXIÓN AC E INTERCONEXIÓN", esTitulo: true),
                new("4.1", "Tablero de Agrupación y Evacuación AC con protecciones termomagnéticas", false, Unidades.UN, 1m),
                new("4.2", "Descargadores de sobretensión tipo SPD AC Clase I+II", false, Unidades.UN, 1m),
                new("4.3", "Interruptor motorizado con protección anti-isla (relé de red)", false, Unidades.UN, 1m),
                new("4.4", "Empalme bidireccional y equipo de medición con módem autorizado", false, Unidades.GL, 1m),

                // Capítulo 5
                new("5", "SISTEMA DE PUESTA A TIERRA Y EQUIPOTENCIALIDAD", esTitulo: true),
                new("5.1", "Malla a tierra de campo fotovoltaico e inversores", false, Unidades.GL, 1m),
                new("5.2", "Aterramiento de estructuras y marcos de módulos con grapas dentadas", false, Unidades.GL, 1m),

                // Capítulo 6
                new("6", "ENSAYOS Y CERTIFICACIÓN SEC TE4", esTitulo: true),
                new("6.1", "Pruebas de curva I-V, tensión de circuito abierto (Voc) e Isc", false, Unidades.GL, 1m),
                new("6.2", "Informe técnico de puesta en marcha y manual de operación", false, Unidades.GL, 1m),
                new("6.3", "Declaración y tramitación TE4 ante la SEC", false, Unidades.GL, 1m)
            }
        };
    }

    private static ProjectTemplate BuildCommercialRetailTemplate()
    {
        return new ProjectTemplate
        {
            Id = "commercial_retail",
            Nombre = "Local Comercial / Retail / Oficinas",
            Subtitulo = "Comercio, climatización y datos",
            Descripcion = "Adecuado para locales en centros comerciales, strip centers y oficinas corporativas: tableros con respaldo UPS, iluminación focalizada LED, circuitos HVAC y cableado estructurado.",
            NormaSec = "RIC N° 02, 08, 10",
            Icono = "🏬",
            EsEnBlanco = false,
            Filas = new List<ProjectTemplateItemRow>
            {
                // Capítulo 1
                new("1", "EMPALME Y TABLERO GENERAL", esTitulo: true),
                new("1.1", "Tablero General de Alumbrado y Fuerza (TGAF) embutido/sobrepuesto", false, Unidades.UN, 1m),
                new("1.2", "Sistema ininterrumpido de potencia (UPS On-Line) para cajas y servidores", false, Unidades.UN, 1m),
                new("1.3", "Tablero seccional de computación y corrientes débiles", false, Unidades.UN, 1m),

                // Capítulo 2
                new("2", "ILUMINACIÓN COMERCIAL Y ARQUITECTÓNICA", esTitulo: true),
                new("2.1", "Rieles electrificados trifásicos con focos proyectores LED orientables", false, Unidades.ML, 48m),
                new("2.2", "Paneles LED 60x60 embutidos en cielo americano para oficinas", false, Unidades.UN, 24m),
                new("2.3", "Tiras LED decorativas en faldones y vitrinas comerciales", false, Unidades.ML, 35m),
                new("2.4", "Iluminación de emergencia y señalética de escape (RIC N° 08)", false, Unidades.UN, 12m),
                new("2.5", "Alimentación de letrero luminoso exterior con reloj programador", false, Unidades.GL, 1m),

                // Capítulo 3
                new("3", "CIRCUITOS DE FUERZA Y CLIMATIZACIÓN", esTitulo: true),
                new("3.1", "Circuitos de enchufes normales para servicio y vitrinas", false, Unidades.UN, 32m),
                new("3.2", "Circuitos de enchufes regulados UPS para terminales POS y cajas", false, Unidades.UN, 16m),
                new("3.3", "Alimentación eléctrica para equipos de aire acondicionado (HVAC)", false, Unidades.UN, 4m),
                new("3.4", "Fuerza para cortina de aire en acceso principal", false, Unidades.UN, 1m),

                // Capítulo 4
                new("4", "CORRIENTES DÉBILES, RED DE DATOS Y SEGURIDAD", esTitulo: true),
                new("4.1", "Puntos de red de datos Cat6 con placas dobles y certificación de enlace", false, Unidades.UN, 20m),
                new("4.2", "Gabinete mural Rack de 9U con patch panel y ordenadores", false, Unidades.UN, 1m),
                new("4.3", "Canalización y cableado para cámaras de seguridad CCTV", false, Unidades.GL, 1m),
                new("4.4", "Sistema de detección temprana de humo y alarma de incendio", false, Unidades.GL, 1m),

                // Capítulo 5
                new("5", "PUESTA A TIERRA Y CERTIFICACIÓN TE1", esTitulo: true),
                new("5.1", "Conexión a malla a tierra del centro comercial y electrodo local", false, Unidades.GL, 1m),
                new("5.2", "Ensayos de aislamiento y rotulación integral de circuitos", false, Unidades.GL, 1m),
                new("5.3", "Declaración SEC TE1 para habilitación comercial", false, Unidades.GL, 1m)
            }
        };
    }
}
