import axios from 'axios';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Interceptor de ejemplo para el JWT del Cliente
api.interceptors.request.use(config => {
  const token = localStorage.getItem('fel_client_auth');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
