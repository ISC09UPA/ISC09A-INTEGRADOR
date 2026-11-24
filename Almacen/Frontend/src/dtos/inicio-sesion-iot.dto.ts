import type { TokenApiDto } from '@/dtos/token-api.dto';

/** Respuesta de POST /api/iot/login. Contrato de la API. */
export interface SolicitudInicioSesionIotDto {
  id: string;
  expira: string;
  vigenciaSegundos: number;
}

/** Respuesta de GET /api/iot/login/{id}. Contrato de la API. */
export interface EstadoInicioSesionIotDto {
  estado: 'pendiente' | 'completada' | 'negada' | 'expirada' | 'consumida' | 'cancelada';
  motivo: string | null;
  expira: string;
  /** Solo con estado completada, una sola vez: el mismo de POST /api/cuenta/token. */
  token: TokenApiDto | null;
}
