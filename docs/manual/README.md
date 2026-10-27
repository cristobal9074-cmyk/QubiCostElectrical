# Documentación y Manuales Oficiales — Qubi Cost Electrical

Este directorio contiene las guías maestras, manuales operativos y el dossier promocional oficial en formato PDF para la plataforma **Qubi Cost Electrical** (v1.1.2).

---

## 1. Dossier para Desarrollo de Sitio Web Promocional
📄 **[Dossier_Promocional_Web_QubiCost_Electrical.pdf](Dossier_Promocional_Web_QubiCost_Electrical.pdf)**  
*(867 KB, 7 páginas a color, con capturas reales de alta definición, wireframe y propuesta de conversión)*

### Contenido para el equipo de desarrollo web:
1. **Portada Oficial y Ficha de Producto:** Versión 1.1.2, desktop Windows 64-bit nativa, SQLite `.qce`, target de clientes.
2. **Propuesta de Valor & Copywriting:** El gran titular de la web, los 3 dolores críticos del cliente y **tabla comparativa lista para la web** (*Excel vs Qubi Cost*).
3. **Showcase de Ingeniería Eléctrica:**
   - Cubicación con fórmulas aritméticas en casillas (`120+40`, `32*2`).
   - Cubicación de Servicios Comunes (C.SC) y el atajo instantáneo `Alt + L`.
4. **Showcase de Inteligencia Financiera:**
   - La calibración de Mano de Obra y Factor FA (`0,025 × FA`).
   - Factores de Presentación (Markup por Familias: cables 1,72× vs equipos 1,43×).
   - El Tablero de 6 KPIs Financieros en tiempo real.
5. **Showcase de Confianza y Calidad:**
   - Auditoría automática previa al cierre con botón *Ir a corregir*.
   - Wizard de exportación formal de 4 pasos (PDF firmado y libros Excel con fórmulas auditables).
6. **Wireframe y Arquitectura de la Landing Page:** Estructura recomendada de 8 bloques de alta conversión.
7. **Brand Kit y Activos Digitales:** Paleta de colores oficial en HEX, tipografías recomendadas y catálogo de 12 capturas de pantalla disponibles.

---

## 2. Guía Financiera: Mano de Obra, Factor M/O y Cierre Comercial
📄 **[Guia_Financiera_ManoDeObra_y_CierreComercial.pdf](Guia_Financiera_ManoDeObra_y_CierreComercial.pdf)**  
*(212 KB, 7 páginas a color, con análisis detallado de la nómina real, fórmula 0,025 × FA y tablero de control)*

---

## 3. Manual de Usuario Integral: Flujo Paso a Paso de la App
📄 **[Manual_de_Usuario_QubiCost_Electrical.pdf](Manual_de_Usuario_QubiCost_Electrical.pdf)**  
*(1.72 MB, 11 páginas con capturas reales desde el inicio hasta la exportación final)*

---

## Generación y Compilación

Para volver a compilar los 3 documentos PDF automáticamente:
```powershell
dotnet run --project tools/ManualPdfGenerator/ManualPdfGenerator.csproj
```
