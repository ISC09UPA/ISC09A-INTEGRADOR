import { create } from 'zustand';
import * as authService from '../services/authService';
import type { Usuario } from '../types';

// Store de sesión (Zustand). La persistencia real vive en SecureStore a
// través de authService, así que este store es un reflejo en memoria.

interface EstadoSesion {
  usuario: Usuario | null;
  /** true mientras se resuelve si hay sesión guardada (splash). */
  inicializando: boolean;
  /** Modo del dispositivo simulado: alterna desde Perfil. */
  inicializar: () => Promise<void>;
  iniciarSesion: (email: string, contrasena: string) => Promise<Usuario>;
  cerrarSesion: () => Promise<void>;
  fijarUsuario: (usuario: Usuario) => void;
}

export const useSesion = create<EstadoSesion>((set) => ({
  usuario: null,
  inicializando: true,

  inicializar: async () => {
    const usuario = await authService.usuarioActual();
    set({ usuario, inicializando: false });
  },

  iniciarSesion: async (email, contrasena) => {
    const usuario = await authService.iniciarSesion(email, contrasena);
    set({ usuario });
    return usuario;
  },

  cerrarSesion: async () => {
    await authService.cerrarSesion();
    set({ usuario: null });
  },

  fijarUsuario: (usuario) => set({ usuario }),
}));