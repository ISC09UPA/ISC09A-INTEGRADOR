import { espera, escribir, leer } from './bd';
import { nuevoIdUsuario } from './ids';
import type { Rol, Usuario } from '../types';

// Gestión de usuarios (solo admin).

export async function listarUsuarios(): Promise<Usuario[]> {
  await espera(280);
  const bd = await leer();
  return [...bd.usuarios];
}

export async function crearUsuario(nombre: string, email: string, rol: Rol): Promise<Usuario> {
  await espera(400);
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) throw new Error('Correo inválido');
  const bd = await leer();
  if (bd.usuarios.some((u) => u.email.toLowerCase() === email.toLowerCase())) {
    throw new Error('Ya existe un usuario con ese correo');
  }
  const usuario: Usuario = { id: nuevoIdUsuario(), nombre, email, rol };
  await escribir((bd2) => bd2.usuarios.push(usuario));
  return usuario;
}

export async function editarRol(usuarioId: string, rol: Rol): Promise<void> {
  await espera(250);
  await escribir((bd) => {
    const u = bd.usuarios.find((x) => x.id === usuarioId);
    if (u) u.rol = rol;
  });
}

export async function eliminarUsuario(usuarioId: string): Promise<void> {
  await espera(250);
  await escribir((bd) => {
    bd.usuarios = bd.usuarios.filter((u) => u.id !== usuarioId);
  });
}
