import { isAxiosError } from 'axios';

export function mensajeError(e: unknown): string {
  if (isAxiosError(e)) {
    if (e.response?.data?.error) return e.response.data.error;
    if (e.response?.status === 401) return 'Correo o contraseña incorrectos';
    if (!e.response) return 'No se pudo conectar con el servidor';
  }
  return 'Ocurrió un error inesperado';
}
