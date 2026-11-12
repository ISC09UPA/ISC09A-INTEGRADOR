/**
 * ──────────────────────────────────────────────────────────────────────────
 *  PUNTO DE INTEGRACIÓN: ENVÍO DEL AUDIO + TRANSCRIPCIÓN AL BACKEND
 * ──────────────────────────────────────────────────────────────────────────
 *  Este frontend NO envía nada todavía. `envioSimulado` solo espera un
 *  instante y resuelve, para poder representar los estados "enviado" y
 *  "procesando". No existe ninguna petición de red real.
 *
 *  Para integrar el backend, implementa `ServicioEnvio.enviar` (por ejemplo,
 *  un POST multipart con el archivo `uri` y la `transcripcion`) y cambia la
 *  exportación `servicioEnvio` al final de este archivo.
 */

export interface GrabacionParaEnviar {
  /** URI local del archivo de audio (m4a/AAC según el preset del recorder). */
  uri: string | null;
  duracionMs: number;
  transcripcion: string;
}

export interface ServicioEnvio {
  /** true mientras no exista integración real con el backend. */
  readonly esSimulado: boolean;
  /** Debe rechazar la promesa si el envío falla. */
  enviar: (grabacion: GrabacionParaEnviar) => Promise<void>;
}

export const envioSimulado: ServicioEnvio = {
  esSimulado: true,
  enviar: () => new Promise<void>((resolver) => setTimeout(resolver, 700)),
};

/** Servicio activo. Sustituir por la implementación real cuando exista. */
export const servicioEnvio: ServicioEnvio = envioSimulado;
