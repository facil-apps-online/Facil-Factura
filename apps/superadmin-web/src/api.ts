import axios from 'axios';

// La URL base del API. Asumiremos 7196 (HTTPS) o 5262 (HTTP)
// Vamos a usar la típica para .NET local, la dejaremos parametrizable
export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5103/api/superadmin',
  headers: {
    'Content-Type': 'application/json'
  }
});

// El backend ahora exige el JWT (antes se guardaba en localStorage tras el login pero nunca se
// enviaba — el API nunca lo validaba, así que no se notaba). Sin este interceptor, cada llamada
// llegaría sin el header Authorization y el backend respondería 401.
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('fel_superadmin_auth');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Si el token expiró o es inválido, el backend responde 401 — se limpia la sesión guardada y se
// manda de vuelta al login en vez de dejar la pantalla en un estado inconsistente.
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('fel_superadmin_auth');
      if (!window.location.pathname.includes('/login')) {
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);
