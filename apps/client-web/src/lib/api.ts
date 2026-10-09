import axios from 'axios';

// Sucursal elegida en el portal: el id de una sucursal o 'all' (solo consultas, para quien puede ver todas).
export const BRANCH_STORAGE_KEY = 'fel_client_branch';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// x-client-id sigue viajando (el backend lo sigue leyendo así en cada controlador), pero ya no
// basta por sí solo: el backend ahora exige que coincida con la claim ClientId del JWT
// (Authorization: Bearer) — sin el JWT, el header se rechaza con 401 aunque tenga el GUID correcto.
// Cabeceras de sesión comunes a axios y a las llamadas con fetch (respuestas por partes).
function sessionHeaders(): Record<string, string> {
  const headers: Record<string, string> = {};
  const token = localStorage.getItem('fel_client_auth');
  const clientId = localStorage.getItem('fel_client_id');
  if (token) headers.Authorization = `Bearer ${token}`;
  if (clientId) headers['x-client-id'] = clientId;
  const branchId = localStorage.getItem(BRANCH_STORAGE_KEY);
  if (branchId) headers['x-branch-id'] = branchId;
  return headers;
}

// Sesión vencida o rechazada: se limpia y se vuelve al login.
function endSession() {
  const tenantSlug = localStorage.getItem('fel_client_tenant');
  localStorage.removeItem('fel_client_auth');
  localStorage.removeItem('fel_client_id');
  localStorage.removeItem(BRANCH_STORAGE_KEY);
  if (!window.location.pathname.startsWith('/login')) {
    window.location.href = tenantSlug ? `/login?tenant=${tenantSlug}` : '/login';
  }
}

api.interceptors.request.use(config => {
  Object.entries(sessionHeaders()).forEach(([name, value]) => config.headers.set(name, value));
  return config;
});

api.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) endSession();
    return Promise.reject(error);
  }
);

// POST que responde una línea JSON por evento (lotes): axios no entrega el cuerpo por partes en el navegador, por eso fetch.
// Devuelve cuando el servidor termina de responder; los 401 cierran la sesión igual que en el resto del portal.
export async function postNdjson<T>(path: string, body: unknown, onEvent: (event: T) => void): Promise<void> {
  const response = await fetch(`${api.defaults.baseURL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...sessionHeaders() },
    body: JSON.stringify(body),
  });
  if (response.status === 401) {
    endSession();
    throw new Error('Tu sesión expiró. Inicia sesión de nuevo.');
  }
  if (!response.ok || !response.body) {
    const text = await response.text().catch(() => '');
    throw new Error(text || `El servidor respondió ${response.status}.`);
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let pending = '';
  for (;;) {
    const { done, value } = await reader.read();
    pending += decoder.decode(value, { stream: !done });
    const lines = pending.split('\n');
    pending = lines.pop() ?? '';
    for (const line of lines) if (line.trim()) onEvent(JSON.parse(line) as T);
    if (done) break;
  }
  if (pending.trim()) onEvent(JSON.parse(pending) as T);
}

// El backend a veces responde con un string plano (BadRequest("mensaje")) y a veces con un
// ProblemDetails/ValidationProblemDetails de ASP.NET Core ({ title, detail, errors: {...} }).
// Esta función siempre devuelve un string seguro de renderizar (nunca el objeto crudo), evitando
// que un toast o un <p>{...}</p> reciba un objeto y rompa React (error #31).
export function getErrorMessage(err: any, fallback: string): string {
  const data = err?.response?.data;
  if (!data) return fallback;
  if (typeof data === 'string') return data;
  if (typeof data.detail === 'string') return data.detail;
  if (data.errors && typeof data.errors === 'object') {
    const firstError = Object.values(data.errors).flat()[0];
    if (typeof firstError === 'string') return firstError;
  }
  if (typeof data.title === 'string') return data.title;
  return fallback;
}
