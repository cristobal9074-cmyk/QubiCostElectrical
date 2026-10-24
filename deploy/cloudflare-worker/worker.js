/**
 * Cloudflare Worker para Qubi Cost Electrical:
 * 1. Landing Page Corporativa de Descarga (/).
 * 2. Panel Web de Administrador con Login y Contraseña Maestra (/admin).
 * 3. API de Verificación de Licencia en tiempo real (/api/verify-license).
 * 4. Endpoint de Descarga y Auto-Update seguro desde GitHub privado.
 *
 * Seguridad: la clave maestra SOLO vive en la variable de entorno ADMIN_SECRET_KEY (nunca en
 * el código), viaja en el header X-Admin-Key (nunca en la query string), las contraseñas de
 * clientes se guardan SOLO como digest SHA-256 "correo:contraseña" y los intentos de acceso
 * tienen límite por IP.
 */

// Digest SHA-256 en hex minúsculas. Misma fórmula que calcula la app en C# (LicenseCredentialDigest).
async function sha256Hex(text) {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text));
  return Array.from(new Uint8Array(digest)).map(b => b.toString(16).padStart(2, "0")).join("");
}

function timingSafeEqualHex(a, b) {
  if (typeof a !== "string" || typeof b !== "string" || a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diff === 0;
}

// Límite de intentos por IP (ventana deslizante en memoria de la instancia).
function allowAttempt(key, limit, windowMs) {
  const now = Date.now();
  const store = (globalThis.__qubiRateLimit = globalThis.__qubiRateLimit || new Map());
  const hits = (store.get(key) || []).filter(t => now - t < windowMs);
  if (hits.length >= limit) {
    store.set(key, hits);
    return false;
  }
  hits.push(now);
  store.set(key, hits);
  if (store.size > 5000) store.clear();
  return true;
}

function clientIp(request) {
  return request.headers.get("CF-Connecting-IP") || "unknown";
}

function getLandingHtml(version) {
  return [
    '<!DOCTYPE html>',
    '<html lang="es">',
    '<head>',
    '  <meta charset="UTF-8">',
    '  <meta name="viewport" content="width=device-width, initial-scale=1.0">',
    '  <title>Descargar Qubi Cost Electrical</title>',
    '  <style>',
    '    :root {',
    '      --bg: #090D16;',
    '      --card-bg: rgba(15, 23, 42, 0.75);',
    '      --border: rgba(255, 255, 255, 0.1);',
    '      --accent: #0284C7;',
    '      --accent-hover: #0369A1;',
    '      --accent-glow: rgba(2, 132, 199, 0.35);',
    '      --text: #F8FAFC;',
    '      --text-muted: #94A3B8;',
    '    }',
    '    * { box-sizing: border-box; margin: 0; padding: 0; }',
    '    body {',
    '      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;',
    '      background-color: var(--bg);',
    '      color: var(--text);',
    '      min-height: 100vh;',
    '      display: flex;',
    '      flex-direction: column;',
    '      align-items: center;',
    '      justify-content: center;',
    '      padding: 24px;',
    '      background-image:',
    '        radial-gradient(circle at 50% 20%, rgba(2, 132, 199, 0.18) 0%, transparent 60%),',
    '        radial-gradient(circle at 80% 80%, rgba(14, 165, 233, 0.08) 0%, transparent 50%);',
    '    }',
    '    .container {',
    '      max-width: 680px;',
    '      width: 100%;',
    '      background: var(--card-bg);',
    '      border: 1px solid var(--border);',
    '      backdrop-filter: blur(20px);',
    '      border-radius: 24px;',
    '      padding: 48px 40px;',
    '      text-align: center;',
    '      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.6);',
    '    }',
    '    .icon-box {',
    '      width: 80px;',
    '      height: 80px;',
    '      margin: 0 auto 20px;',
    '      border-radius: 20px;',
    '      background: linear-gradient(135deg, #0284C7, #0F172A);',
    '      border: 1px solid rgba(2, 132, 199, 0.5);',
    '      display: flex;',
    '      align-items: center;',
    '      justify-content: center;',
    '      box-shadow: 0 10px 25px var(--accent-glow);',
    '    }',
    '    .badge {',
    '      display: inline-block;',
    '      padding: 6px 14px;',
    '      background: rgba(2, 132, 199, 0.15);',
    '      border: 1px solid rgba(2, 132, 199, 0.35);',
    '      color: #38BDF8;',
    '      border-radius: 999px;',
    '      font-size: 13px;',
    '      font-weight: 600;',
    '      margin-bottom: 16px;',
    '    }',
    '    h1 {',
    '      font-size: 32px;',
    '      font-weight: 800;',
    '      letter-spacing: -0.5px;',
    '      margin-bottom: 12px;',
    '      background: linear-gradient(to right, #FFFFFF, #94A3B8);',
    '      -webkit-background-clip: text;',
    '      -webkit-text-fill-color: transparent;',
    '    }',
    '    p.lead {',
    '      color: var(--text-muted);',
    '      font-size: 16px;',
    '      line-height: 1.6;',
    '      margin-bottom: 36px;',
    '    }',
    '    .btn-download {',
    '      display: inline-flex;',
    '      align-items: center;',
    '      justify-content: center;',
    '      gap: 12px;',
    '      width: 100%;',
    '      max-width: 420px;',
    '      padding: 16px 28px;',
    '      background: linear-gradient(135deg, #0284C7, #0369A1);',
    '      color: #FFFFFF;',
    '      text-decoration: none;',
    '      font-size: 17px;',
    '      font-weight: 700;',
    '      border-radius: 14px;',
    '      box-shadow: 0 10px 25px var(--accent-glow);',
    '      transition: all 0.2s ease;',
    '    }',
    '    .btn-download:hover {',
    '      background: linear-gradient(135deg, #0369A1, #075985);',
    '      transform: translateY(-2px);',
    '    }',
    '    .specs {',
    '      display: flex;',
    '      justify-content: center;',
    '      gap: 24px;',
    '      margin-top: 18px;',
    '      font-size: 13px;',
    '      color: var(--text-muted);',
    '    }',
    '    .features {',
    '      margin-top: 40px;',
    '      padding-top: 32px;',
    '      border-top: 1px solid var(--border);',
    '      display: grid;',
    '      grid-template-columns: repeat(3, 1fr);',
    '      gap: 16px;',
    '      text-align: left;',
    '    }',
    '    .feat {',
    '      background: rgba(255, 255, 255, 0.02);',
    '      border: 1px solid rgba(255, 255, 255, 0.05);',
    '      padding: 16px;',
    '      border-radius: 12px;',
    '    }',
    '    .feat-title {',
    '      font-size: 14px;',
    '      font-weight: 700;',
    '      color: #E2E8F0;',
    '      margin-bottom: 4px;',
    '    }',
    '    .feat-desc {',
    '      font-size: 12px;',
    '      color: var(--text-muted);',
    '      line-height: 1.4;',
    '    }',
    '    footer {',
    '      margin-top: 32px;',
    '      font-size: 13px;',
    '      color: #64748B;',
    '    }',
    '  </style>',
    '</head>',
    '<body>',
    '  <div class="container">',
    '    <div class="icon-box">',
    '      <svg width="42" height="42" viewBox="0 0 24 24" fill="none" stroke="#FFFFFF" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">',
    '        <polygon points="12 2 2 7 12 12 22 7 12 2"></polygon>',
    '        <polyline points="2 17 12 22 22 17"></polyline>',
    '        <polyline points="2 12 12 17 22 12"></polyline>',
    '      </svg>',
    '    </div>',
    '    <div class="badge">VERSION OFICIAL ' + version + '</div>',
    '    <h1>Qubi Cost Electrical</h1>',
    '    <p class="lead">Software profesional para cubicacion y presupuestos de instalaciones electricas en baja y media tension.</p>',
    '    <a href="/QubiCostElectrical-win-Setup.exe" class="btn-download">',
    '      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">',
    '        <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>',
    '        <polyline points="7 10 12 15 17 10"></polyline>',
    '        <line x1="12" y1="15" x2="12" y2="3"></line>',
    '      </svg>',
    '      Descargar Instalador (Windows 64-bit)',
    '    </a>',
    '    <div class="specs">',
    '      <span>Windows 10 / 11 (x64)</span>',
    '      <span>Instalacion en 3 seg</span>',
    '      <span>~100 MB</span>',
    '    </div>',
    '    <div class="features">',
    '      <div class="feat">',
    '        <div class="feat-title">Cero Permisos</div>',
    '        <div class="feat-desc">No requiere permisos de Administrador para instalar.</div>',
    '      </div>',
    '      <div class="feat">',
    '        <div class="feat-title">Auto-Actualizaciones</div>',
    '        <div class="feat-desc">Parches y mejoras continuas en segundo plano.</div>',
    '      </div>',
    '      <div class="feat">',
    '        <div class="feat-title">Proyectos .QCE</div>',
    '        <div class="feat-desc">Base de datos SQLite local rapida y segura.</div>',
    '      </div>',
    '    </div>',
    '  </div>',
    '  <footer>',
    '    &copy; 2026 Qubi Cost Electrical. Todos los derechos reservados.',
    '  </footer>',
    '</body>',
    '</html>'
  ].join('\n');
}

function getAdminHtml() {
  return [
    '<!DOCTYPE html>',
    '<html lang="es">',
    '<head>',
    '  <meta charset="UTF-8">',
    '  <meta name="viewport" content="width=device-width, initial-scale=1.0">',
    '  <title>Qubi Cost — Panel de Suscripciones y Clientes</title>',
    '  <style>',
    '    :root {',
    '      --bg: #090D16;',
    '      --card: #0F172A;',
    '      --border: rgba(255, 255, 255, 0.08);',
    '      --primary: #0284C7;',
    '      --primary-hover: #0369A1;',
    '      --danger: #EF4444;',
    '      --danger-hover: #DC2626;',
    '      --success: #10B981;',
    '      --text: #F8FAFC;',
    '      --muted: #94A3B8;',
    '    }',
    '    * { box-sizing: border-box; margin: 0; padding: 0; }',
    '    body {',
    '      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;',
    '      background: var(--bg);',
    '      color: var(--text);',
    '      padding: 32px 20px;',
    '      min-height: 100vh;',
    '    }',
    '    .login-box {',
    '      max-width: 420px;',
    '      margin: 80px auto;',
    '      background: var(--card);',
    '      border: 1px solid var(--border);',
    '      border-radius: 20px;',
    '      padding: 36px 30px;',
    '      text-align: center;',
    '      box-shadow: 0 20px 40px rgba(0, 0, 0, 0.6);',
    '    }',
    '    .top-bar {',
    '      max-width: 1000px;',
    '      margin: 0 auto 28px;',
    '      display: flex;',
    '      justify-content: space-between;',
    '      align-items: center;',
    '    }',
    '    .logo-area { display: flex; align-items: center; gap: 12px; }',
    '    .logo-badge {',
    '      width: 40px; height: 40px; background: #0284C7; border-radius: 10px;',
    '      display: flex; align-items: center; justify-content: center;',
    '    }',
    '    .title { font-size: 20px; font-weight: 800; }',
    '    .subtitle { font-size: 12px; color: #38BDF8; letter-spacing: 1.5px; }',
    '    .badge-admin {',
    '      background: rgba(2, 132, 199, 0.15); border: 1px solid rgba(2, 132, 199, 0.35);',
    '      color: #38BDF8; padding: 6px 14px; border-radius: 999px; font-size: 12px; font-weight: 700;',
    '    }',
    '    .container {',
    '      max-width: 1000px;',
    '      margin: 0 auto;',
    '      background: var(--card);',
    '      border: 1px solid var(--border);',
    '      border-radius: 20px;',
    '      padding: 30px;',
    '      box-shadow: 0 20px 40px rgba(0, 0, 0, 0.5);',
    '    }',
    '    .add-form {',
    '      background: #1E293B;',
    '      border: 1px solid var(--border);',
    '      border-radius: 14px;',
    '      padding: 20px;',
    '      margin-bottom: 28px;',
    '      display: grid;',
    '      grid-template-columns: 2fr 2fr 1.5fr 1fr auto;',
    '      gap: 12px;',
    '      align-items: end;',
    '    }',
    '    .field-label { font-size: 11.5px; font-weight: 600; color: var(--muted); margin-bottom: 6px; display: block; text-align: left; }',
    '    .input {',
    '      width: 100%; height: 38px; background: #0F172A; border: 1px solid #334155;',
    '      border-radius: 8px; padding: 0 12px; color: #FFF; font-size: 13px; outline: none;',
    '    }',
    '    .input:focus { border-color: var(--primary); }',
    '    .btn {',
    '      height: 38px; padding: 0 18px; border-radius: 8px; border: none;',
    '      font-weight: 700; font-size: 13px; cursor: pointer; transition: all 0.15s ease;',
    '      display: inline-flex; align-items: center; justify-content: center; gap: 6px;',
    '    }',
    '    .btn-primary { background: var(--primary); color: #FFF; }',
    '    .btn-primary:hover { background: var(--primary-hover); }',
    '    .btn-danger { background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3); color: #F87171; }',
    '    .btn-danger:hover { background: var(--danger); color: #FFF; }',
    '    .btn-success { background: rgba(16, 185, 129, 0.15); border: 1px solid rgba(16, 185, 129, 0.3); color: #34D399; }',
    '    .btn-success:hover { background: var(--success); color: #FFF; }',
    '    table { width: 100%; border-collapse: collapse; margin-top: 10px; }',
    '    th {',
    '      text-align: left; padding: 12px 14px; font-size: 11.5px; font-weight: 700;',
    '      color: var(--muted); text-transform: uppercase; letter-spacing: 0.5px;',
    '      border-bottom: 1px solid var(--border);',
    '    }',
    '    td {',
    '      padding: 16px 14px; font-size: 13px; border-bottom: 1px solid rgba(255, 255, 255, 0.04);',
    '    }',
    '    tr:hover td { background: rgba(255, 255, 255, 0.02); }',
    '    .status-badge {',
    '      display: inline-flex; align-items: center; gap: 6px;',
    '      padding: 4px 10px; border-radius: 999px; font-size: 11.5px; font-weight: 700;',
    '    }',
    '    .status-active { background: rgba(16, 185, 129, 0.15); color: #34D399; }',
    '    .status-suspended { background: rgba(239, 68, 68, 0.15); color: #F87171; }',
    '    .empty-state { text-align: center; padding: 40px 0; color: var(--muted); font-size: 14px; }',
    '  </style>',
    '</head>',
    '<body>',
    '  <!-- Pantalla de Login de Administrador -->',
    '  <div id="authScreen" class="login-box">',
    '    <div style="width: 48px; height: 48px; background: #0284C7; border-radius: 12px; margin: 0 auto 16px; display: flex; align-items: center; justify-content: center;">',
    '      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#FFF" stroke-width="2.5"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect><path d="M7 11V7a5 5 0 0 1 10 0v4"></path></svg>',
    '    </div>',
    '    <h2 style="font-size: 20px; font-weight: 800; margin-bottom: 6px;">Acceso Maestro</h2>',
    '    <p style="font-size: 12.5px; color: var(--muted); margin-bottom: 24px;">Ingresa tu clave maestra de administrador para gestionar las licencias.</p>',
    '    <form onsubmit="verifyAdminPass(event)">',
    '      <input id="adminPassInput" type="password" class="input" placeholder="Contraseña Maestra" required style="margin-bottom: 16px; text-align: center;">',
    '      <button type="submit" class="btn btn-primary" style="width: 100%;">Ingresar al Panel</button>',
    '    </form>',
    '    <div id="authError" style="display: none; color: #F87171; font-size: 12px; margin-top: 14px; font-weight: 600;">Contraseña maestra incorrecta.</div>',
    '  </div>',
    '',
    '  <!-- Panel Principal (Oculto hasta que ingresa la clave) -->',
    '  <div id="mainDashboard" style="display: none;">',
    '    <div class="top-bar">',
    '      <div class="logo-area">',
    '        <div class="logo-badge">',
    '          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#FFF" stroke-width="2.5"><polygon points="12 2 2 7 12 12 22 7 12 2"></polygon><polyline points="2 17 12 22 22 17"></polyline><polyline points="2 12 12 17 22 12"></polyline></svg>',
    '        </div>',
    '        <div>',
    '          <div class="title">QUBI COST ELECTRICAL</div>',
    '          <div class="subtitle">CONTROL MAESTRO DE SUSCRIPCIONES</div>',
    '        </div>',
    '      </div>',
    '      <div style="display: flex; gap: 10px; align-items: center;">',
    '        <div class="badge-admin">ADMINISTRADOR SEGURO</div>',
    '        <button class="btn btn-danger" onclick="logout()" style="height: 30px; font-size: 11px; padding: 0 12px;">Cerrar Sesión</button>',
    '      </div>',
    '    </div>',
    '    <div class="container">',
    '      <form id="addForm" class="add-form" onsubmit="addUser(event)">',
    '        <div>',
    '          <label class="field-label">Nombre del Cliente / Tester</label>',
    '          <input id="userName" class="input" placeholder="Ej: Juan Pérez" required>',
    '        </div>',
    '        <div>',
    '          <label class="field-label">Correo electrónico</label>',
    '          <input id="userEmail" type="email" class="input" placeholder="cliente@empresa.cl" required>',
    '        </div>',
    '        <div>',
    '          <label class="field-label">Contraseña</label>',
    '          <input id="userPass" class="input" placeholder="Clave123!" required>',
    '        </div>',
    '        <div>',
    '          <label class="field-label">Plan / Días</label>',
    '          <select id="userPlan" class="input">',
    '            <option value="15">Prueba 15 días</option>',
    '            <option value="30">Mensual (30 días)</option>',
    '            <option value="365">Anual (1 año)</option>',
    '            <option value="9999">Vitalicio / Sin límite</option>',
    '          </select>',
    '        </div>',
    '        <button type="submit" class="btn btn-primary">+ Crear Acceso</button>',
    '      </form>',
    '      <table>',
    '        <thead>',
    '          <tr>',
    '            <th>Cliente</th>',
    '            <th>Correo</th>',
    '            <th>Credencial</th>',
    '            <th>Vencimiento</th>',
    '            <th>Estado</th>',
    '            <th style="text-align: right;">Control</th>',
    '          </tr>',
    '        </thead>',
    '        <tbody id="usersTable">',
    '        </tbody>',
    '      </table>',
    '      <div id="emptyState" class="empty-state" style="display: none;">No hay clientes registrados aún. Usa el formulario de arriba para añadir el primero.</div>',
    '    </div>',
    '  </div>',
    '',
    '  <script>',
    '    let currentKey = sessionStorage.getItem("qubi_admin_key") || "";',
    '    if (currentKey) { checkSession(); }',
    '    async function verifyAdminPass(e) {',
    '      e.preventDefault();',
    '      const pass = document.getElementById("adminPassInput").value;',
    '      const res = await fetch("/api/admin/verify-pass", {',
    '        method: "POST", headers: { "Content-Type": "application/json" },',
    '        body: JSON.stringify({ pass })',
    '      });',
    '      if (res.ok) {',
    '        currentKey = pass;',
    '        sessionStorage.setItem("qubi_admin_key", pass);',
    '        showDashboard();',
    '      } else {',
    '        document.getElementById("authError").style.display = "block";',
    '      }',
    '    }',
    '    async function checkSession() {',
    '      const res = await fetch("/api/admin/users", { headers: { "X-Admin-Key": currentKey } });',
    '      if (res.ok) { showDashboard(); } else { sessionStorage.removeItem("qubi_admin_key"); }',
    '    }',
    '    function showDashboard() {',
    '      document.getElementById("authScreen").style.display = "none";',
    '      document.getElementById("mainDashboard").style.display = "block";',
    '      loadUsers();',
    '    }',
    '    function logout() {',
    '      sessionStorage.removeItem("qubi_admin_key");',
    '      location.reload();',
    '    }',
    '    async function loadUsers() {',
    '      const res = await fetch("/api/admin/users", { headers: { "X-Admin-Key": currentKey } });',
    '      if (res.ok) { const users = await res.json(); renderUsers(users); }',
    '    }',
    '    function renderUsers(users) {',
    '      const tbody = document.getElementById("usersTable");',
    '      const empty = document.getElementById("emptyState");',
    '      tbody.innerHTML = "";',
    '      if (!users || users.length === 0) { empty.style.display = "block"; return; }',
    '      empty.style.display = "none";',
    '      users.forEach(u => {',
    '        const tr = document.createElement("tr");',
    '        const isActive = u.status === "active";',
    '        tr.innerHTML = `',
    '          <td><strong>${u.name}</strong></td>',
    '          <td>${u.email}</td>',
    '          <td><code>${u.password_hash ? "Cifrada (SHA-256)" : "—"}</code></td>',
    '          <td>${u.expires_at || "Sin vencimiento"}</td>',
    '          <td><span class="status-badge ${isActive ? "status-active" : "status-suspended"}">${isActive ? "● ACTIVO" : "■ CANCELADO"}</span></td>',
    '          <td style="text-align: right;">',
    '            <button class="btn ${isActive ? "btn-danger" : "btn-success"}" onclick="toggleStatus(\'${u.email}\', \'${isActive ? "suspended" : "active"}\')">',
    '              ${isActive ? "Cancelar Suscripción" : "Reactivar"}',
    '            </button>',
    '          </td>',
    '        `;',
    '        tbody.appendChild(tr);',
    '      });',
    '    }',
    '    async function addUser(e) {',
    '      e.preventDefault();',
    '      const name = document.getElementById("userName").value;',
    '      const email = document.getElementById("userEmail").value;',
    '      const password = document.getElementById("userPass").value;',
    '      const days = parseInt(document.getElementById("userPlan").value);',
    '      const res = await fetch("/api/admin/users", {',
    '        method: "POST", headers: { "Content-Type": "application/json", "X-Admin-Key": currentKey },',
    '        body: JSON.stringify({ name, email, password, days })',
    '      });',
    '      if (res.ok) { document.getElementById("addForm").reset(); loadUsers(); }',
    '      else { alert("Error guardando cliente"); }',
    '    }',
    '    async function toggleStatus(email, newStatus) {',
    '      const res = await fetch("/api/admin/users/status", {',
    '        method: "POST", headers: { "Content-Type": "application/json", "X-Admin-Key": currentKey },',
    '        body: JSON.stringify({ email, status: newStatus })',
    '      });',
    '      if (res.ok) { loadUsers(); }',
    '    }',
    '  </script>',
    '</body>',
    '</html>'
  ].join('\n');
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const path = url.pathname.replace(/^\/+/, "");

    const repo = env.GITHUB_REPO;
    const token = env.GITHUB_TOKEN;
    const adminPass = env.ADMIN_SECRET_KEY || "";

    // Sin clave maestra configurada no existe panel de administración.
    const adminReady = adminPass.length > 0;
    const adminKey = request.headers.get("X-Admin-Key") || "";
    const adminAuthorized = adminReady && adminKey.length > 0 && timingSafeEqualHex(adminKey, adminPass);
    const adminUnavailable = () => new Response(
      "Admin key no configurada en el Worker (variable ADMIN_SECRET_KEY).",
      { status: 500 });

    // --- 1. Panel de Administración (/admin) protegido con Contraseña Maestra ---
    if (path === "admin" || path === "admin/") {
      return new Response(getAdminHtml(), {
        status: 200,
        headers: { "Content-Type": "text/html; charset=utf-8" }
      });
    }

    // Endpoint para validar la contraseña del admin
    if (path === "api/admin/verify-pass" && request.method === "POST") {
      if (!adminReady) return adminUnavailable();
      if (!allowAttempt("admin-login:" + clientIp(request), 5, 15 * 60 * 1000)) {
        return new Response("Demasiados intentos. Intenta más tarde.", { status: 429 });
      }
      const body = await request.json();
      if (typeof body.pass === "string" && timingSafeEqualHex(body.pass, adminPass)) {
        return new Response(JSON.stringify({ success: true }), { status: 200, headers: { "Content-Type": "application/json" } });
      }
      return new Response("Invalid password", { status: 401 });
    }

    // --- 2. API de Administración protegida (Listar, Crear, Suspender usuarios) ---
    if (path === "api/admin/users") {
      if (!adminReady) return adminUnavailable();
      if (!adminAuthorized) {
        return new Response("Unauthorized", { status: 401 });
      }

      if (request.method === "GET") {
        let users = [];
        if (env.SUBSCRIBERS_KV) {
          const raw = await env.SUBSCRIBERS_KV.get("users_list");
          if (raw) users = JSON.parse(raw);
        }
        return new Response(JSON.stringify(users), {
          status: 200,
          headers: { "Content-Type": "application/json" }
        });
      }

      if (request.method === "POST") {
        const body = await request.json();
        let users = [];
        if (env.SUBSCRIBERS_KV) {
          const raw = await env.SUBSCRIBERS_KV.get("users_list");
          if (raw) users = JSON.parse(raw);
        }

        const expDate = new Date();
        expDate.setDate(expDate.getDate() + (body.days || 30));
        const formattedDate = expDate.toISOString().split("T")[0];

        const normalizedEmail = (body.email || "").trim().toLowerCase();
        const existingIdx = users.findIndex(u => u.email.toLowerCase() === normalizedEmail);
        const newUser = {
          name: body.name,
          email: normalizedEmail,
          // Nunca se guarda la contraseña: solo el digest "correo:contraseña".
          password_hash: await sha256Hex(normalizedEmail + ":" + (body.password || "")),
          status: "active",
          created_at: new Date().toISOString().split("T")[0],
          expires_at: body.days >= 9000 ? "Vitalicio" : formattedDate
        };

        if (existingIdx >= 0) {
          delete users[existingIdx].password;
          users[existingIdx] = { ...users[existingIdx], ...newUser };
        } else {
          users.push(newUser);
        }

        if (env.SUBSCRIBERS_KV) {
          await env.SUBSCRIBERS_KV.put("users_list", JSON.stringify(users));
        }

        return new Response(JSON.stringify({ success: true }), { status: 200 });
      }
    }

    if (path === "api/admin/users/status" && request.method === "POST") {
      if (!adminReady) return adminUnavailable();
      if (!adminAuthorized) return new Response("Unauthorized", { status: 401 });

      const body = await request.json();
      let users = [];
      if (env.SUBSCRIBERS_KV) {
        const raw = await env.SUBSCRIBERS_KV.get("users_list");
        if (raw) users = JSON.parse(raw);
      }

      const user = users.find(u => u.email.toLowerCase() === body.email.toLowerCase());
      if (user) {
        user.status = body.status;
        if (env.SUBSCRIBERS_KV) {
          await env.SUBSCRIBERS_KV.put("users_list", JSON.stringify(users));
        }
      }
      return new Response(JSON.stringify({ success: true }), { status: 200 });
    }

    // --- 3. API Verificación de Licencia (llamada desde la App en C#) ---
    if (path === "api/verify-license" && request.method === "POST") {
      if (!allowAttempt("license:" + clientIp(request), 10, 10 * 60 * 1000)) {
        return new Response(JSON.stringify({
          valid: false,
          reason: "rate_limited",
          message: "Demasiados intentos de acceso. Espera unos minutos e inténtalo de nuevo."
        }), { status: 429, headers: { "Content-Type": "application/json" } });
      }

      const body = await request.json();
      const email = (body.email || "").trim().toLowerCase();
      const passwordHash = typeof body.passwordHash === "string" ? body.passwordHash.trim().toLowerCase() : "";

      let users = [];
      if (env.SUBSCRIBERS_KV) {
        const raw = await env.SUBSCRIBERS_KV.get("users_list");
        if (raw) users = JSON.parse(raw);
      }

      // Mensaje idéntico existente/inexistente: no se revela qué correos están registrados.
      const invalidCredentials = () => new Response(JSON.stringify({
        valid: false,
        reason: "invalid_credentials",
        message: "Correo o contraseña incorrectos."
      }), { status: 200, headers: { "Content-Type": "application/json" } });

      const user = users.find(u => u.email.toLowerCase() === email);
      if (!user || passwordHash.length !== 64) return invalidCredentials();

      let match = false;
      if (typeof user.password_hash === "string") {
        match = timingSafeEqualHex(user.password_hash, passwordHash);
      } else if (typeof user.password === "string") {
        // Registro antiguo guardado en claro: se deriva el digest para comparar y se migra.
        const legacyHash = await sha256Hex(email + ":" + user.password);
        match = timingSafeEqualHex(legacyHash, passwordHash);
        if (match && env.SUBSCRIBERS_KV) {
          user.password_hash = legacyHash;
          delete user.password;
          await env.SUBSCRIBERS_KV.put("users_list", JSON.stringify(users));
        }
      }

      if (!match) return invalidCredentials();

      if (user.status !== "active") {
        return new Response(JSON.stringify({
          valid: false,
          reason: "suspended",
          message: "Tu suscripción o periodo de prueba ha sido cancelado. Contacta a Qubi Cost Electrical."
        }), { status: 200, headers: { "Content-Type": "application/json" } });
      }

      if (user.expires_at && user.expires_at !== "Vitalicio") {
        const today = new Date().toISOString().split("T")[0];
        if (today > user.expires_at) {
          return new Response(JSON.stringify({
            valid: false,
            reason: "expired",
            message: "Tu suscripción ha vencido el " + user.expires_at + ". Por favor renueva tu plan."
          }), { status: 200, headers: { "Content-Type": "application/json" } });
        }
      }

      return new Response(JSON.stringify({
        valid: true,
        displayName: user.name,
        expiresAt: user.expires_at
      }), { status: 200, headers: { "Content-Type": "application/json" } });
    }

    // --- 4. Landing Page Corporativa de Descarga (/) ---
    if (!path || path === "" || path === "index.html") {
      return new Response(getLandingHtml("v1.1.2"), {
        status: 200,
        headers: { "Content-Type": "text/html; charset=utf-8" }
      });
    }

    // --- 5. Descarga de Instalador y deltas desde GitHub Privado ---
    const releasesApiUrl = "https://api.github.com/repos/" + repo + "/releases/latest";
    const releaseRes = await fetch(releasesApiUrl, {
      headers: {
        "Authorization": "token " + token,
        "User-Agent": "QubiCost-Worker",
        "Accept": "application/vnd.github+json"
      }
    });

    if (!releaseRes.ok) {
      const errText = await releaseRes.text();
      return new Response("Error GitHub: " + errText, { status: releaseRes.status });
    }

    const releaseData = await releaseRes.json();
    const fileName = path.split("/").pop();

    const asset = releaseData.assets.find(a => a.name.toLowerCase() === fileName.toLowerCase());
    if (!asset) {
      return new Response("Asset no encontrado: " + fileName, { status: 404 });
    }

    const downloadRes = await fetch(asset.url, {
      headers: {
        "Authorization": "token " + token,
        "User-Agent": "QubiCost-Worker",
        "Accept": "application/octet-stream"
      },
      redirect: "follow"
    });

    return new Response(downloadRes.body, {
      status: downloadRes.status,
      headers: {
        "Content-Type": "application/octet-stream",
        "Content-Disposition": 'attachment; filename="' + asset.name + '"',
        "Content-Length": downloadRes.headers.get("Content-Length") || "",
        "Access-Control-Allow-Origin": "*"
      }
    });
  }
};
