import api from './api';

export async function login(username, password) {
  const res = await api.post('/api/auth/login', { userName: username, password });
  return res.data;
}

export default { login };
