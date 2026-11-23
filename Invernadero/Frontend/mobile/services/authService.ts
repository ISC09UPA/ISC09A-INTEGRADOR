import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';
import type { Usuario } from '../types';
import { escribir, espera, leer } from './bd';
import { nuevoIdUsuario } from './ids';

// Servicio de autenticación mock (el Backend aún no expone endpoints de usuarios).
// La UI solo habla con estas funciones: cuando exista la API real, solo se cambia este archivo.

const CLAVE_SESION = 'riegosmart.sesion';
/** Contraseña de las cuentas demo y de las creadas por un admin desde la pantalla de usuarios. */
const CLAVE_DEMO = 'demo123';
const EMAIL_REGEXP = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export interface SesionGuardada {
  usuarioId: string;
  creadoEn: string;
}

/**
 * Guarda la sesión según la plataforma:
 * - Android/iOS: SecureStore.
 * - Web: AsyncStorage.
 */
async function guardarSesion(usuario: Usuario): Promise<void> {
  const sesion: SesionGuardada = {
    usuarioId: usuario.id,
    creadoEn: new Date().toISOString(),
  };
  const valor = JSON.stringify(sesion);

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

export async function iniciarSesion(
  email: string,
  contrasena: string,
): Promise<Usuario> {
  await espera(500);

  const bd = await leer();
  const limpio = email.trim().toLowerCase();
  const usuario = bd.usuarios.find((u) => u.email.toLowerCase() === limpio);

  if (!usuario) throw new Error('No encontramos una cuenta con ese correo.');
  if (contrasena !== (bd.claves[usuario.id] ?? CLAVE_DEMO)) {
    throw new Error('Contraseña incorrecta.');
  }

  await guardarSesion(usuario);
  return usuario;
}

/** Registro público: toda cuenta nueva nace como "operador"; un admin puede ascenderla. */
export async function registrar(
  nombre: string,
  email: string,
  contrasena: string,
): Promise<Usuario> {
  await espera(500);

  const limpio = email.trim().toLowerCase();
  if (nombre.trim().length < 2) throw new Error('Escribe tu nombre.');
  if (!EMAIL_REGEXP.test(limpio)) throw new Error('Correo inválido.');
  if (contrasena.length < 6) {
    throw new Error('La contraseña debe tener al menos 6 caracteres.');
  }

  const bd = await leer();
  if (bd.usuarios.some((u) => u.email.toLowerCase() === limpio)) {
    throw new Error('Ya existe una cuenta con ese correo.');
  }

  const usuario: Usuario = {
    id: nuevoIdUsuario(),
    nombre: nombre.trim(),
    email: limpio,
    rol: 'operador',
  };

  await escribir((b) => {
    b.usuarios.push(usuario);
    b.claves[usuario.id] = contrasena;
  });

  await guardarSesion(usuario);
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