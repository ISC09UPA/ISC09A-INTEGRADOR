/**
 * Tipos y constantes del flujo de voz (Push to Talk).
 *
 * Estados del flujo:
 *  - idle:       esperando una grabación.
 *  - recording:  grabando audio (el usuario mantiene presionado el botón).
 *  - review:     el usuario revisa la grabación (confirmar / eliminar).
 *  - sent:       audio confirmado y "enviado" (SIMULADO por ahora).
 *  - processing: esperando la respuesta del asistente (SIMULADO por ahora).
 */
export type EstadoVoz = 'idle' | 'recording' | 'review' | 'sent' | 'processing';

export interface Grabacion {
  /** URI local temporal del archivo de audio (puede ser null si el recorder no lo entregó). */
  uri: string | null;
  duracionMs: number;
  /** Transcripción final conservada junto al audio. */
  transcripcion: string;
}

/** Grabaciones más cortas que esto se consideran toques accidentales y se descartan. */
export const DURACION_MINIMA_MS = 600;

/** Tiempo que se muestra "Audio enviado correctamente" antes de pasar a "Procesando". */
export const TIEMPO_ENVIADO_MS = 1800;
