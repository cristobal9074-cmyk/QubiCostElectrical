# Earned Value Formulas — Fase 7C-C

| Indicador | Fórmula |
|-----------|---------|
| PV | Valor planificado acumulado a la fecha de estado |
| EV | BAC_actividad × % completado (o método físico/0-100/50-50) |
| AC | Costo real informado acumulado |
| BAC | Presupuesto al término (proyecto o suma ponderada) |
| SV | EV − PV |
| CV | EV − AC |
| SPI | EV / PV (null si PV = 0) |
| CPI | EV / AC (null si AC = 0) |
| EAC (CPI) | BAC / CPI |
| EAC (AC+rem) | AC + (BAC − EV) |
| EAC (CPI×SPI) | AC + (BAC − EV) / (CPI × SPI) |
| ETC | EAC − AC |
| VAC | BAC − EAC |
| TCPI | (BAC − EV) / (BAC − AC) (null si denom = 0) |
| % Plan | PV / BAC × 100 |
| % Ganado | EV / BAC × 100 |
| % Actual | AC / BAC × 100 |

Redondeo: dinero 2 decimales; índices 4 decimales; MidpointRounding.ToEven.
