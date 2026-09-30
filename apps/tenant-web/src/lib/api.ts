import axios from 'axios';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5103/api'
});

// x-tenant-id sigue viajando (el backend lo sigue leyendo así en cada controlador), pero ya no
// basta por sí solo: el backend ahora exige que coincida con la claim TenantId del JWT
// (Authorization: Bearer) — sin el JWT, el header se rechaza con 401 aunque tenga el GUID correcto.
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('fel_tenant_auth');
  const tenantId = localStorage.getItem('fel_tenant_id');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  if (tenantId) {
    config.headers['x-tenant-id'] = tenantId;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('fel_tenant_auth');
      localStorage.removeItem('fel_tenant_id');
      localStorage.removeItem('fel_tenant_list');
      if (!window.location.pathname.includes('/login')) {
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);

// El backend ([ApiController]) responde 400 con un ValidationProblemDetails (objeto
// {type,title,status,errors,traceId}) cuando falla el model binding — pasar ese objeto directo a
// toast.error() lo intenta renderizar como children de React y revienta con el error #31.
export function getErrorMessage(err: any, fallback: string): string {
  const data = err?.response?.data;
  if (!data) return fallback;
  if (typeof data === 'string') return data;
  if (typeof data.detail === 'string') return data.detail;
  if (data.errors && typeof data.errors === 'object') {
    const firstError = Object.values(data.errors).flat()[0];
    if (typeof firstError === 'string') return firstError;
  }
  if (typeof data.message === 'string') return data.message;
  if (typeof data.title === 'string') return data.title;
  return fallback;
}
