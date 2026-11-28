import type { Sesion } from '@/models/sesion.model';

/** Solicitud de inicio de sesión con el lector (tarjeta NFC + rostro) que la app sondea. */
export interface SolicitudInicioSesionIot {
  id: string;
  expira: Date;
  vigenciaSegundos: number;
}

/** Cómo va la solicitud. Con "completada" ya trae la sesión lista para usarse. */
export type EstadoInicioSesionIot =
  | { estado: 'pendiente' }
  | { estado: 'completada'; sesion: Sesion }
  | { estado: 'negada' | 'expirada' | 'consumida' | 'cancelada'; motivo: string };
