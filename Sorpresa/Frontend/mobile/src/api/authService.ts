import client from './client';
import type { AuthResponse, Usuario } from '@/types';

export const login = (email: string, password: string) =>
  client.post<AuthResponse>('/api/auth/login', { email, password }).then((r) => r.data);

export const registro = (nombre: string, email: string, password: string) =>
  client.post<AuthResponse>('/api/auth/registro', { nombre, email, password }).then((r) => r.data);

export const me = () => client.get<Usuario>('/api/auth/me').then((r) => r.data);
