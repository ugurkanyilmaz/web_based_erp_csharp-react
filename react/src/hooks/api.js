import axios from 'axios';

// In Vite use import.meta.env.VITE_API_URL for runtime env vars.
// You can create a `.env` file at the project root with e.g.:
// VITE_API_URL=http://localhost:5019
const base = import.meta.env.VITE_API_URL || 'http://localhost:5019';

const api = axios.create({
  baseURL: base,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Attach token from localStorage to each request
api.interceptors.request.use((cfg) => {
  const token = localStorage.getItem('token');
  if (token) cfg.headers.Authorization = `Bearer ${token}`;
  return cfg;
});

export default api;
