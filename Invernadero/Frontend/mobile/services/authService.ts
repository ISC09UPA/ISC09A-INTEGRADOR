
import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

import type { Usuario } from '../types';
import { espera, leer } from './bd';

// Servicio de autenticación mock. La UI solo habla con estas funciones.

const CLAVE_SESION = 'riegosmart.sesion';

export interface SesionGuardada {
  usuarioId: string;
  creadoEn: string;
}

/**
 * Guarda la sesión según la plataforma:
 * - Android/iOS: SecureStore.
 * - Web: AsyncStorage.
 */
async function guardarSesion(valor: string): Promise<void> {
  if (Platform.OS === 'web') {
    await AsyncStorage.setItem(CLAVE_SESION, valor);
    return;
  }

  await SecureStore.setItemAsync(CLAVE_SESION, valor);
}

/** Recupera la sesión guardada. */
async function obtenerSesion(): Promise<string | null> {
  if (Platform.OS === 'web') {
    return AsyncStorage.getItem(CLAVE_SESION);
  }

  return SecureStore.getItemAsync(CLAVE_SESION);
}

/** Elimina la sesión guardada. */
async function eliminarSesion(): Promise<void> {
  if (Platform.OS === 'web') {
    await AsyncStorage.removeItem(CLAVE_SESION);
    return;
  }

  await SecureStore.deleteItemAsync(CLAVE_SESION);
}

/** Usuarios demo: admin@demo.com y operador@demo.com (contraseña: demo123). */
function credencialPara(
  email: string,
  contrasena: string,
  usuarios: Usuario[],
): Usuario | null {
  const limpio = email.trim().toLowerCase();

  if (contrasena !== 'demo123') return null;

  return usuarios.find((u) => u.email.toLowerCase() === limpio) ?? null;
}

export async function iniciarSesion(
  email: string,
  contrasena: string,
): Promise<Usuario> {
  await espera(500);

  const bd = await leer();
  const usuario = credencialPara(email, contrasena, bd.usuarios);

  if (!usuario) {
    if (contrasena !== 'demo123') {
      throw new Error('Contraseña incorrecta. Los usuarios demo usan "demo123".');
    }

    throw new Error('No encontramos una cuenta con ese correo.');
  }

  const sesion: SesionGuardada = {
    usuarioId: usuario.id,
    creadoEn: new Date().toISOString(),
  };

  await guardarSesion(JSON.stringify(sesion));

  return usuario;
}

export async function usuarioActual(): Promise<Usuario | null> {
  try {
    const bruto = await obtenerSesion();

    if (!bruto) return null;

    const sesion = JSON.parse(bruto) as SesionGuardada;
    const bd = await leer();

    return bd.usuarios.find((u) => u.id === sesion.usuarioId) ?? null;
  } catch {
    return null;
  }
}

export async function cerrarSesion(): Promise<void> {
  await eliminarSesion();
}
