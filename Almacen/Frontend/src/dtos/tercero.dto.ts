import type { DireccionDto } from '@/dtos/direccion.dto';

/** Cliente o proveedor. Contrato de la API. */
export interface TerceroDto {
  id: string;
  codigo: string;
  nombre: string;
  correo: string;
  rfc: string;
  direccion: DireccionDto;
  activo: boolean;
}
