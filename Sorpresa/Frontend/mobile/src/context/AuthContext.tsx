import {
  createContext, useCallback, useContext, useEffect, useState, type ReactNode,
} from 'react';
import * as authApi from '@/api/authService';
import { borrarToken, guardarToken, leerToken, setAlExpirarSesion } from '@/api/client';
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
    await borrarToken().catch(() => undefined);
    setUsuario(null);
  }, []);

  // Al abrir la app: si hay token guardado, recupera el usuario
  useEffect(() => {
    setAlExpirarSesion(cerrarSesion);
    (async () => {
      try {
        const token = await leerToken();
        if (token) setUsuario(await authApi.me());
      } catch {
        // sin sesión válida: se queda en login
      } finally {
        setCargando(false);
      }
    })();
  }, [cerrarSesion]);

  const guardar = async ({ token, usuario }: AuthResponse) => {
    if (!token || !usuario) throw new Error('La respuesta del servidor no es válida');
    await guardarToken(token);
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
