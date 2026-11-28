import { Platform } from 'react-native';
import { create } from 'axios';
import * as SecureStore from 'expo-secure-store';

const CLAVE_TOKEN = 'token';
let tokenEnMemoria: string | null = null;

const urlBase = process.env.EXPO_PUBLIC_API_URL ?? 'http://10.0.2.2:5106';
export const API_URL = Platform.OS === 'web'
  ? urlBase.replace('10.0.2.2', 'localhost').replace('192.168.56.1', 'localhost')
  : urlBase;

export const guardarToken = async (token: string) => {
  if (Platform.OS === 'web') {
    tokenEnMemoria = token;
    return;
  }
  await SecureStore.setItemAsync(CLAVE_TOKEN, token);
};

export const leerToken = async (): Promise<string | null> => {
  if (Platform.OS === 'web') return tokenEnMemoria;
  return SecureStore.getItemAsync(CLAVE_TOKEN);
};

export const borrarToken = async () => {
  if (Platform.OS === 'web') {
    tokenEnMemoria = null;
    return;
  }
  await SecureStore.deleteItemAsync(CLAVE_TOKEN);
};

let alExpirar: (() => void) | null = null;
export const setAlExpirarSesion = (fn: () => void) => {
  alExpirar = fn;
};

const client = create({ baseURL: API_URL, timeout: 10000 });

client.interceptors.request.use(async (config) => {
  const token = await leerToken().catch(() => null);
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
