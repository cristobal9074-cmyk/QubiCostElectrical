# Release Process — Qubi Cost Electrical

## Prerrequisitos

- .NET 10 SDK (o la versión indicada en `global.json` si existe)
- Git
- Inno Setup 6 (instalador `.exe` en CI)
- **Firma Authenticode** (obligatoria antes de GA comercial):
  - Certificado de firma de código (EV recomendado para SmartScreen)
  - Variables: `CODE_SIGNING_CERT_PFX`, `CODE_SIGNING_CERT_PASSWORD`
  - En CI: secretos `CODE_SIGNING_CERT_BASE64` y `CODE_SIGNING_CERT_PASSWORD`

## Identidad congelada

Antes de cualquier tag de release:

```powershell
.\scripts\Verify-ProductIdentity.ps1 -Strict
```

La solución canónica es `QubiCostElectrical.Wpf.sln`. No usar `PresupuestoElectrico.sln` (eliminado).

## Pasos

### 1. Repositorio limpio

```powershell
git status --porcelain
# Debe devolver vacío
```

### 2. Versión en Directory.Build.props

Fuente única de verdad:

```xml
<VersionPrefix>1.1.1</VersionPrefix>
<InformationalVersion>1.1.1</InformationalVersion>
```

Usar `Directory.Build.props` como fuente única de verdad; para este repositorio, la versión informacional vigente es `1.1.1`.

### 3. Validación pre-release

```powershell
.\scripts\Validate-Release.ps1
```

Ejecuta: identidad → restore → build → test.

### 4. Bump de versión (si aplica)

Editar `Directory.Build.props` y actualizar `CLAUDE.md` + `CHANGELOG.md`.

### 5. Commit de versionado

```powershell
git add Directory.Build.props CHANGELOG.md CLAUDE.md
git commit -m "release: bump to 1.1.1"
```

### 6. Tag semántico

```powershell
git tag v1.1.1
```

Convención: `v` + versión publicada. Evitar ejemplos `-rc.*` si no corresponden al release real del repositorio.

### 7. Push + tags

```powershell
git push origin master --tags
```

### 8. CI publica artefactos

El workflow `.github/workflows/build.yml` en push/tag `v*`:

1. Build + test (`QubiCostElectrical.Wpf.sln`)
2. Publish win-x64 self-contained
3. Firma Authenticode (si hay secretos de certificado)
4. Instalador Inno Setup (`installer/QubiCostElectrical.iss`)
5. ZIP fuente + SHA-256
6. GitHub Release

### 9. Build local wrapper

```powershell
.\deploy\build-release.ps1 -Sign
# Llama a scripts/Publish-Release.ps1 -Configuration Release -Runtime win-x64
```

### 10. Smoke test humano obligatorio

Completar **todos** los ítems de `docs/SMOKE_TEST.md` antes de marcar la release como estable.

No publicar GA comercial sin:

- [ ] Tag `v*` creado y artefactos en GitHub Releases
- [ ] Binarios firmados (SmartScreen sin “Run anyway” en máquina limpia)
- [ ] Checklist de regresión humana firmado
- [ ] QuestPDF: elegibilidad Community confirmada o licencia Professional activa

### 11. Publicar release notes

Editar notas en GitHub con cambios destacados del `CHANGELOG.md`.

## Firma Authenticode

### Local

```powershell
$env:CODE_SIGNING_CERT_PFX = "C:\certs\qubicost-codesign.pfx"
$env:CODE_SIGNING_CERT_PASSWORD = "..."
.\deploy\Sign-Authenticode.ps1 -Paths @(
    "publish\win-x64\QubiCostElectrical.exe",
    "artifacts\QubiCostElectrical-Setup-*.exe"
)
```

O integrado en el pipeline:

```powershell
.\deploy\build-release.ps1 -Sign
```

Comportamiento del script:

- `0`: firma completada correctamente.
- `0`: firma omitida si faltan variables de entorno y no se usa `-Strict` (apto para RC internas).
- `1`: faltan credenciales con `-Strict`, no se encuentra `signtool.exe`, falta un archivo requerido o falla la firma.

### CI (GitHub Actions)

Configurar secretos del repositorio:

| Secreto | Contenido |
|---------|-----------|
| `CODE_SIGNING_CERT_BASE64` | Certificado `.pfx` en base64 |
| `CODE_SIGNING_CERT_PASSWORD` | Contraseña del PFX |

Sin estos secretos, CI publica artefactos **sin firmar** (válido solo para RC internas).

### Obtener certificado

- Proveedores: DigiCert, Sectigo, SSL.com (EV Code Signing)
- SmartScreen confía más rápido con certificado EV y reputación acumulada
- Documentar fecha de primera firma para seguimiento de reputación

## Licencias de terceros

### QuestPDF

| Escenario | Acción |
|-----------|--------|
| Empresa con ingresos &lt; USD 1M/año | `LicenseType.Community` (default en `QuestPdfBootstrap`) |
| Ingresos ≥ USD 1M o requisito legal del cliente | Comprar Professional/Enterprise; `QUESTPDF_LICENSE=Professional` |
| Redistribución del binario | No eliminar avisos propietarios de QuestPDF en el ensamblado |

La licencia Community **no** exige watermark en PDFs generados; sí exige declarar el tipo de licencia en código (`QuestPDF.Settings.License`).

Verificar elegibilidad antes de cobrar por el software.

### Otros

- ClosedXML, DocumentFormat.OpenXml: revisar licencias MIT/Apache en `THIRD_PARTY_NOTICES.txt`

## Post-release

- `git status --porcelain` vacío
- Artefactos y SHA-256 en release de GitHub
- Issues de regresión abiertos si el smoke test falló algún ítem
