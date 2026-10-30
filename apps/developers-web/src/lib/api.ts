import axios from 'axios';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5106/api',
});

// x-developer-id sigue viajando (el backend lo sigue leyendo así — ver
// DeveloperController.GetCurrentDeveloperId), pero ya no basta por sí solo: el backend ahora
// exige que coincida con la claim DeveloperId del JWT (Authorization: Bearer) — sin el JWT, el
// header se rechaza con 401 aunque tenga el GUID correcto.
// Rutas sin sesión (entrar, pedir y usar el enlace de contraseña): no llevan la sesión guardada, que puede estar vencida o ser de otra
// persona y haría que el servidor rechazara la petición con 401; y su 401 es un error del formulario, no una sesión que cerrar.
const PUBLIC_AUTH = /\/auth\/(login|forgot-password|reset-password)$/;
const isPublicAuth = (url?: string) => PUBLIC_AUTH.test((url || '').split('?')[0]);

api.interceptors.request.use((config) => {
  if (isPublicAuth(config.url)) return config;
  const token = localStorage.getItem('fel_developer_auth');
  const developerId = localStorage.getItem('fel_developer_id');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  if (developerId) {
    config.headers['x-developer-id'] = developerId;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && !isPublicAuth(error.config?.url)) {
      localStorage.removeItem('fel_developer_auth');
      localStorage.removeItem('fel_developer_id');
      if (!window.location.pathname.includes('/login')) {
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);
