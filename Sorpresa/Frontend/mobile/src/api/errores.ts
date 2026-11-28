import { isAxiosError } from 'axios';

export function mensajeError(e: unknown): string {
  if (isAxiosError(e)) {
    if (typeof e.response?.data?.error === 'string') return e.response.data.error;
    if (e.response?.status === 401) return 'Correo o contraseña incorrectos';
    if (!e.response) return 'No se pudo conectar con el servidor';
    if (e.response.status >= 500) return 'El servidor no pudo procesar la solicitud';
  }
  return e instanceof Error && e.message ? e.message : 'Ocurrió un error inesperado';
}
