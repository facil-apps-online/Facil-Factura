import axios from 'axios';

export const api = axios.create({ 
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5103/api'
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('fel_tenant_auth');
  if (token) {
    config.headers['x-tenant-id'] = token;
  }
  return config;
});
