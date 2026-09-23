import axios from 'axios';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// x-client-id sigue viajando (el backend lo sigue leyendo así en cada controlador), pero ya no
// basta por sí solo: el backend ahora exige que coincida con la claim ClientId del JWT
// (Authorization: Bearer) — sin el JWT, el header se rechaza con 401 aunque tenga el GUID correcto.
api.interceptors.request.use(config => {
  const token = localStorage.getItem('fel_client_auth');
  const clientId = localStorage.getItem('fel_client_id');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  if (clientId) {
    config.headers['x-client-id'] = clientId;
  }
  return config;
});

api.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      const tenantSlug = localStorage.getItem('fel_client_tenant');
      localStorage.removeItem('fel_client_auth');
      localStorage.removeItem('fel_client_id');
      if (!window.location.pathname.startsWith('/login')) {
        window.location.href = tenantSlug ? `/login?tenant=${tenantSlug}` : '/login';
      }
    }
    return Promise.reject(error);
  }
);

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
