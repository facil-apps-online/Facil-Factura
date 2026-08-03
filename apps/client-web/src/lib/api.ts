import axios from 'axios';

export const api = axios.create({
  baseURL: 'http://localhost:5000/api', // Ajustar al puerto de Services (5236 o el que sea)
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
