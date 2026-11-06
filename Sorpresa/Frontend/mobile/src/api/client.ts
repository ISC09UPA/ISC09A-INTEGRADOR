import { create } from 'axios';
import * as SecureStore from 'expo-secure-store';

export const API_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://10.0.2.2:5000';

let alExpirar: (() => void) | null = null;
export const setAlExpirarSesion = (fn: () => void) => {
  alExpirar = fn;
};

const client = create({ baseURL: API_URL, timeout: 10000 });

client.interceptors.request.use(async (config) => {
  const token = await SecureStore.getItemAsync('token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

client.interceptors.response.use(
  (r) => r,
  (error) => {
    const url: string = error.config?.url ?? '';
    if (error.response?.status === 401 && !url.includes('/auth/login') && alExpirar) alExpirar();
    return Promise.reject(error);
  }
);

export default client;
