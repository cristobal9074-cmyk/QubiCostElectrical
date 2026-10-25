# Manual de Usuario Oficial — Qubi Cost Electrical

Este directorio contiene la documentación oficial ilustrada y el manual en formato PDF para el uso de la aplicación **Qubi Cost Electrical** (v1.1.2).

## Archivo PDF Generado

- 📄 **[Manual_de_Usuario_QubiCost_Electrical.pdf](Manual_de_Usuario_QubiCost_Electrical.pdf)** (1.7 MB, 11 páginas a todo color con capturas reales de la aplicación).

## Contenido del Manual

1. **Portada Oficial y Filosofía de Trabajo**:
   - Por qué se utiliza un único archivo `.qce` (SQLite) eliminando las planillas Excel descoordinadas.
   - Seguridad y colaboración en red (bloqueo automático `.qce.lock`).
2. **Paso 01: Inicio y Creación del Proyecto**:
   - Apertura de archivos, proyectos recientes y nuevo presupuesto (`Ctrl + N`).
3. **Paso 02: Datos de Obra y Parámetros Comerciales**:
   - Mandante, ubicación, m², indicadores económicos (UF/CLP), % GG, utilidad y política de IVA.
4. **Paso 03: Estructuración del Itemizado**:
   - Capítulos, partidas, códigos WBS, unidades y cantidades contractuales.
5. **Paso 04: Cubicación Técnica y Fórmulas en Casillas**:
   - Asignación de materiales, desglose de tramos, casillas con operaciones aritméticas (`120+40`, `32*2`), mermas y redondeos comerciales.
6. **Paso 05: Cubicación de Servicios Comunes (C.SC)**:
   - Modelamiento de tableros (TDA, TDF, TDA-SC), circuitos de distribución (alumbrado, enchufes, fuerza), tramos por piso y comando directo **"Tomar de SC" (`Alt + L`)**.
7. **Paso 06: Análisis de Precios Unitarios (APU)**:
   - Costo directo de materiales, cuadrillas y rendimientos en Horas-Hombre (HH), leyes sociales, herramientas menores y factores de altura/entorno.
8. **Paso 07: Banco de Precios, Cotizaciones y Lista de Compra**:
   - Catálogo maestro de insumos, actualización de precios y consolidación de materiales para compras y cotizaciones.
9. **Paso 08: Revisión Final y Control de Calidad**:
   - Auditoría automática previa al cierre, alertas bloqueantes (rojas), alertas de advertencia (amarillas) y botón **"Ir a corregir"**.
10. **Paso 09: Oferta Comercial y Exportación Oficial**:
    - Asistente de cierre de 4 pasos (Validar → Configurar → Vista previa → Exportar), generación de cartas de oferta en PDF y libros Excel auditables.
11. **Dashboard y Atajos de Teclado**:
    - Seguimiento de indicadores ($/m², avance), regla de oro del estimador y tabla de atajos clave (`Ctrl+N`, `Ctrl+O`, `Ctrl+S`, `Ctrl+P`, `Alt+L`, `F5`).

## Generación y Actualización

Para volver a generar el manual o actualizar las capturas de pantalla:
```powershell
# 1. Capturar pantallas reales de la UI de WPF:
dotnet run --project tools/VisualCapture/VisualCapture.csproj

# 2. Generar el documento PDF con QuestPDF:
dotnet run --project tools/ManualPdfGenerator/ManualPdfGenerator.csproj
```
