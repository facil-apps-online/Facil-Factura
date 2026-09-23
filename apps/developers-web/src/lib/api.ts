import axios from 'axios';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5106/api',
});

// x-developer-id sigue viajando (el backend lo sigue leyendo así — ver
// DeveloperController.GetCurrentDeveloperId), pero ya no basta por sí solo: el backend ahora
// exige que coincida con la claim DeveloperId del JWT (Authorization: Bearer) — sin el JWT, el
// header se rechaza con 401 aunque tenga el GUID correcto.
api.interceptors.request.use((config) => {
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
    if (error.response?.status === 401) {
      localStorage.removeItem('fel_developer_auth');
      localStorage.removeItem('fel_developer_id');
      if (!window.location.pathname.includes('/login')) {
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);
