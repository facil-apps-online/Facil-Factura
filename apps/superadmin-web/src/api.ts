import axios from 'axios';

// La URL base del API. Asumiremos 7196 (HTTPS) o 5262 (HTTP)
// Vamos a usar la típica para .NET local, la dejaremos parametrizable
export const api = axios.create({
  baseURL: 'http://localhost:5103/api/superadmin',
  headers: {
    'Content-Type': 'application/json'
  }
});
