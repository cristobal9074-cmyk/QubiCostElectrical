# Banco de precios en carpeta compartida (equipo)

## Objetivo

Una sola base `banco_precios.db` en red para que todos los estimadores compartan catálogo y precios.

## Configuración

1. Cree una carpeta UNC accesible por el equipo, por ejemplo: `\\servidor\Presupuestos\BancoPrecios\`
2. En **Preferencias → Banco de precios corporativo**, indique la ruta completa del archivo, por ejemplo: `\\servidor\Presupuestos\BancoPrecios\banco_precios.db`
3. Reinicie la aplicación en cada estación (el banco se abre al iniciar).

## Permisos

- **Un editor a la vez** en escritura: SQLite no soporta escritura concurrente multiusuario. Operación recomendada: un administrador actualiza precios; el resto consulta o edita en horarios alternados.
- La carpeta debe permitir **lectura** a todos y **escritura** al rol que mantiene el banco.

## Modelo de colaboración: qué es esto y qué NO es

Este mecanismo es un **archivo compartido con un editor a la vez** (patrón similar a un `.docx` bloqueado por Word en una carpeta de red), no una base de datos multiusuario con sincronización real ni resolución de conflictos. Es la opción honesta y funcional para equipos chicos con un share de red de verdad (SMB/UNC clásico).

**No use una carpeta sincronizada por un cliente de nube (OneDrive, Dropbox, Google Drive, iCloud) para `banco_precios.db` ni para archivos `.qce` compartidos.** SQLite no tolera que el archivo se sincronice a medio escribir — el resultado más probable es corrupción silenciosa del banco de precios. La app detecta esta situación en **Preferencias** y muestra una advertencia cuando la ruta configurada cae dentro de una de esas carpetas, pero no lo bloquea (puede haber falsos positivos con nombres de carpeta personalizados).

Si su equipo no tiene un share SMB/UNC real disponible, la alternativa honesta hoy es **mono-usuario por estación** (`%LOCALAPPDATA%\QubiCostElectrical\banco_precios.db`, valor por defecto) con export/import manual del catálogo entre estimadores, no una carpeta de nube haciendo de base de datos compartida.

## Ruta UNC en Preferencias

En **Preferencias → Archivo del banco de precios** indique la ruta completa, por ejemplo:

`\\servidor\Presupuestos\BancoPrecios\banco_precios.db`

La aplicación valida que la ruta sea absoluta (local o UNC). En rutas locales comprueba que exista la carpeta padre; en UNC confía en que el equipo tenga acceso al compartir al reiniciar.

## Verificación automatizada

```powershell
dotnet test QubiCostElectrical.Wpf.sln -c Release --filter "FullyQualifiedName~CorporatePriceBankTests|ProductPathsTests"
```

## Respaldo

Copie periódicamente `banco_precios.db` fuera de la carpeta compartida. El archivo vive fuera de cada `.qce`.

## Valor por defecto

Si la ruta está vacía, se usa `%LOCALAPPDATA%\QubiCostElectrical\banco_precios.db` (por máquina).
