# Guía de Certificado Autofirmado Authenticode — Qubi Cost Electrical

Para evitar alertas de Windows Defender / SmartScreen ("Editor desconocido") y garantizar la integridad de los ejecutables `.exe` e instaladores, el proyecto cuenta con infraestructura completa de firma digital **Authenticode** con marca de tiempo criptográfica oficial de **DigiCert** (RFC3161).

---

## 1. Archivos del Certificado

Los archivos se encuentran en la carpeta `certs/` (la clave privada está en `.gitignore` para máxima seguridad):

| Archivo | Función | Destinatario |
|---|---|---|
| `certs/QubiCostElectrical-CodeSigning.pfx` | Certificado con clave privada y contraseña para firmar `.exe` | Desarrollador / CI |
| `certs/QubiCostElectrical-Public.cer` | Certificado público que valida la firma de Qubi Cost | Clientes / Equipos finales |
| `certs/Instalar-Certificado-QubiCost.bat` | Script de 1 clic para instalar el certificado de confianza en Windows | Clientes / Equipos finales |
| `certs/ci-secret-base64.txt` | Clave PFX en Base64 para GitHub Actions Secrets | Repositorio GitHub |

---

## 2. Parámetros del Certificado

* **Sujeto (Publisher):** `CN=Qubi Cost Electrical, O=Qubi Cost, C=CL`
* **Tipo:** Firma de Código (`CodeSigningCert` - OID `1.3.6.1.5.5.7.3.3`)
* **Algoritmo de Clave:** RSA 2048 bits / SHA-256
* **Validez:** 5 años (hasta Octubre de 2031)
* **Contraseña por defecto:** `QubiCost2026.CodeSign!`
* **Servidor de Marca de Tiempo (Timestamp):** `http://timestamp.digicert.com` (DigiCert Trusted CA, válido hasta 2037)

---

## 3. ¿Cómo firmar los archivos `.exe`?

El script de firma `deploy/Sign-Authenticode.ps1` detecta automáticamente el certificado local en `certs/QubiCostElectrical-CodeSigning.pfx` y soporta **doble motor**:
1. Si `signtool.exe` (Windows SDK) está instalado, lo utiliza.
2. Si `signtool.exe` **no** está instalado, utiliza el cmdlet nativo de PowerShell `Set-AuthenticodeSignature` con timestamp DigiCert.

### Firmar manualmente cualquier ejecutable:
```powershell
.\deploy\Sign-Authenticode.ps1 -Paths "publish\win-x64\QubiCostElectrical.exe"
.\deploy\Sign-Authenticode.ps1 -Paths "deploy\releases\QubiCostElectrical-win-Setup.exe"
```

### Firmar durante el proceso de build de release:
```powershell
.\deploy\build-release.ps1 -Sign
```

---

## 4. ¿Cómo instalar el certificado en la máquina de un cliente?

Para que la máquina del cliente reconozca el ejecutable sin ninguna advertencia de "Editor desconocido":

1. Entregarle los archivos `QubiCostElectrical-Public.cer` e `Instalar-Certificado-QubiCost.bat`.
2. El cliente hace clic derecho sobre **`Instalar-Certificado-QubiCost.bat`** y selecciona **"Ejecutar como administrador"** (o simplemente doble clic).
3. El instalador registra el certificado en:
   - **Editores de confianza (TrustedPublisher)**
   - **Entidades de certificación raíz de confianza (Root)**
4. ¡Listo! A partir de ese momento:
   - Las propiedades del `.exe` mostrarán la pestaña **Firmas digitales** con **Qubi Cost Electrical** como editor verificado.
   - Windows SmartScreen y el diálogo UAC mostrarán el recuadro azul de editor verificado.

---

## 5. Regenerar el certificado (si fuera necesario)

Si en el futuro deseas crear un nuevo certificado con otros datos o más años de validez:

```powershell
.\scripts\New-CodeSigningCertificate.ps1 -Subject "CN=Qubi Cost Electrical, O=Qubi Cost, C=CL" -ValidYears 10
```
