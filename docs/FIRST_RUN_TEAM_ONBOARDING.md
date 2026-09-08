# Primer arranque de equipo (3–8 usuarios; 1 editor por .qce)

## Objetivo

Dejar el equipo operativo en menos de 20 minutos con cuentas separadas, roles claros y flujo repetible.

## Paso 1 - Arranque inicial (admin)

1. Abrir Qubi Cost Electrical por primera vez.
2. Crear usuario administrador inicial.
3. Configurar Preferencias globales (membrete, carpeta de proyectos, banco de precios).

## Paso 2 - Alta de cuentas del equipo

Desde `Preferencias > Usuarios`:

1. Crear usuarios del equipo (nombre, correo, PIN, rol).
2. Validar que cada cuenta quede activa.
3. Enviar PIN inicial por canal interno seguro.

## Paso 3 - Roles recomendados

- **Admin:** 1 persona (líder técnico/comercial).
- **Estimador:** 2–6 personas.
- **Visor:** perfiles de revisión/gerencia.

## Paso 4 - Verificación operativa rápida

1. Login con un estimador.
2. Crear proyecto de prueba, editar y guardar.
3. Exportar un PDF de prueba.
4. Confirmar que un Visor no pueda editar ni exportar.
- Abrir el mismo .qce desde otra estación: segundo usuario en solo lectura mientras el primero edita.

## Paso 5 - Checklist de cierre

- [ ] Cuentas creadas y roles revisados.
- [ ] PIN inicial cambiado por cada usuario.
- [ ] Ruta de proyectos definida (local/UNC).
- [ ] Banco de precios accesible.
- [ ] Exportación validada.

## Sesión guiada 20 minutos (Admin + Estimador + Visor)

Objetivo: dejar el equipo con roles claros sobre el mismo .qce en carpeta SMB/UNC: 1 editor + N consultores (solo lectura). No es co-edición en tiempo real.

### Minuto a minuto

**00:00–02:00 · Admin inicia**

- [ ] Login en **Iniciar sesión** con cuenta Administrador.
- [ ] Abrir **Más** → **Preferencias** → sección **Usuarios**.
- [ ] Ver bloque **USUARIOS Y PERMISOS**.

**02:00–06:00 · Admin crea cuentas**

- [ ] Crear un usuario **Estimador** con nombre, correo, PIN y rol.
- [ ] Crear un usuario **Visor** con nombre, correo, PIN y rol.
- [ ] Ver ambos usuarios en la grilla `Users`.

**06:00–08:00 · Explicar roles**

- [ ] Aclarar regla: **Administrador** crea cuentas y define preferencias globales.
- [ ] Aclarar regla: **Estimador** puede editar presupuesto y exportar.
- [ ] Aclarar regla: **Visor** solo consulta. No edita ni exporta.

**08:00–12:00 · Estimador trabaja**

1. [ ] **Cerrar sesión**.
2. [ ] Login como **Estimador**.
3. [ ] **Nuevo proyecto** con nombre de prueba.
4. [ ] **Proyecto** → completar **CLIENTE / MANDANTE** y **UBICACIÓN DE LA OBRA**.
5. [ ] **Itemizado** → crear 1 o 2 partidas.
6. [ ] **Cubicación** → agregar un recurso o escribir una fórmula simple.
7. [ ] **Guardar**.

**12:00–16:00 · Visor valida solo lectura**

1. [ ] **Cerrar sesión**.
2. [ ] Login como **Visor**.
3. [ ] Abrir el mismo `.qce`.
4. [ ] Intentar editar Itemizado o Cubicación: edición bloqueada.
5. [ ] Intentar exportar desde **Cierre** → **Oferta y exportación**: exportación bloqueada.

**16:00–18:00 · Admin recupera control**

1. [ ] **Cerrar sesión**.
2. [ ] Login Admin.
3. [ ] Volver a **Preferencias** → **Usuarios**.
4. [ ] Mostrar que puede cambiar rol, resetear PIN o desbloquear cuenta.

**18:00–20:00 · Cierre**

- [ ] Confirmar que los tres entienden dónde entrar: **Resumen**, **Proyecto**, **Itemizado**, **Cubicación**, **Cierre**.
- [ ] Confirmar que el archivo de trabajo es un `.qce`.
- [ ] Confirmar canal interno para entregar PIN inicial y política de cambio.

### Criterio de aprobación

- [ ] Admin creó Estimador y Visor sin ayuda externa.
- [ ] Estimador editó y guardó un proyecto real.
- [ ] Visor abrió el mismo proyecto y quedó en solo lectura.
- [ ] El equipo identifica `Preferencias → Usuarios` como punto de administración.

## Nota comercial

Para ventas a contratistas pequeños/medianos, este flujo debe presentarse como “Onboarding de equipo en 20 minutos”.
