# Política de PIN y seguridad (equipo local)

## Alcance

Aplica a autenticación local por PIN en instalaciones de equipo (3–8 estimadores), sin IdP corporativo.

## Reglas vigentes

- Longitud: `4–6` dígitos.
- Solo dígitos (`0-9`).
- Bloqueadas combinaciones débiles:
  - secuencias ascendentes (`1234`, `2345`, ...),
  - secuencias descendentes (`4321`, `9876`, ...),
  - repetición total (`1111`, `0000`, ...).
- Límite de intentos fallidos y bloqueo temporal: gestionado por `SqliteAuthStore`/`AuthService`.
- Reset de PIN: solo por administrador desde Preferencias.

## Recomendación operativa

- Rotación sugerida cada 90 días para equipos con alta rotación.
- Evitar compartir cuentas; una cuenta por persona.
- Auditoría interna: registrar cambios de usuarios/roles/PIN en módulo de administración.

## Futuro (opcional, roadmap)

- Expiración obligatoria configurable por empresa.
- Desbloqueo administrativo explícito con trazabilidad persistente.
- Política avanzada configurable (longitud mínima, historial de PIN, caducidad).
