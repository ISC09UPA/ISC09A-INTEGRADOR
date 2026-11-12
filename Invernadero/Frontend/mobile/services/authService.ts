import * as SecureStore from 'expo-secure-store';
import { espera, leer } from './bd';
import type { Usuario } from '../types';

// Servicio de autenticación mock. La UI solo habla con estas funciones.

const CLAVE_SESION = 'riegosmart.sesion';

export interface SesionGuardada {
  usuarioId: string;
  creadoEn: string;
}

/** Usuarios demo: admin@demo.com y operador@demo.com (contraseña: demo123). */
function credencialPara(email: string, contrasena: string, usuarios: Usuario[]): Usuario | null {
  const limpio = email.trim().toLowerCase();
  if (contrasena !== 'demo123') return null;
  return usuarios.find((u) => u.email.toLowerCase() === limpio) ?? null;
}

export async function iniciarSesion(email: string, contrasena: string): Promise<Usuario> {
  await espera(500);
  const bd = await leer();
  const usuario = credencialPara(email, contrasena, bd.usuarios);
  if (!usuario) {
    if (contrasena !== 'demo123') {
      throw new Error('Contraseña incorrecta. Los usuarios demo usan "demo123".');
    }
    throw new Error('No encontramos una cuenta con ese correo.');
  }
  const sesion: SesionGuardada = { usuarioId: usuario.id, creadoEn: new Date().toISOString() };
  await SecureStore.setItemAsync(CLAVE_SESION, JSON.stringify(sesion));
  return usuario;
}

export async function usuarioActual(): Promise<Usuario | null> {
  try {
    const bruto = await SecureStore.getItemAsync(CLAVE_SESION);
    if (!bruto) return null;
    const sesion = JSON.parse(bruto) as SesionGuardada;
    const bd = await leer();
    return bd.usuarios.find((u) => u.id === sesion.usuarioId) ?? null;
  } catch {
    return null;
  }
}

export async function cerrarSesion(): Promise<void> {
  await SecureStore.deleteItemAsync(CLAVE_SESION);
}
