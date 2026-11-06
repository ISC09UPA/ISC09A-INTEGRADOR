import {
  createContext, useCallback, useContext, useEffect, useState, type ReactNode,
} from 'react';
import * as SecureStore from 'expo-secure-store';
import * as authApi from '@/api/authService';
import { setAlExpirarSesion } from '@/api/client';
import type { AuthResponse, Usuario } from '@/types';

interface AuthCtx {
  usuario: Usuario | null;
  cargando: boolean;
  iniciarSesion: (email: string, password: string) => Promise<void>;
  registrarse: (nombre: string, email: string, password: string) => Promise<void>;
  cerrarSesion: () => Promise<void>;
}

const AuthContext = createContext<AuthCtx | null>(null);

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider');
  return ctx;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Usuario | null>(null);
  const [cargando, setCargando] = useState(true);

  const cerrarSesion = useCallback(async () => {
    await SecureStore.deleteItemAsync('token');
    setUsuario(null);
  }, []);

  // Al abrir la app: si hay token guardado, recupera el usuario
  useEffect(() => {
    setAlExpirarSesion(cerrarSesion);
    (async () => {
      try {
        const token = await SecureStore.getItemAsync('token');
        if (token) setUsuario(await authApi.me());
      } catch {
        // sin sesión válida: se queda en login
      } finally {
        setCargando(false);
      }
    })();
  }, [cerrarSesion]);

  const guardar = async ({ token, usuario }: AuthResponse) => {
    await SecureStore.setItemAsync('token', token);
    setUsuario(usuario);
  };

  const iniciarSesion = async (email: string, password: string) =>
    guardar(await authApi.login(email, password));

  const registrarse = async (nombre: string, email: string, password: string) =>
    guardar(await authApi.registro(nombre, email, password));

  return (
    <AuthContext.Provider value={{ usuario, cargando, iniciarSesion, registrarse, cerrarSesion }}>
      {children}
    </AuthContext.Provider>
  );
}
