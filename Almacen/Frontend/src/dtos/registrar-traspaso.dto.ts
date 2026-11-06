import type { LineaTraspasoDto } from '@/dtos/linea-traspaso.dto';

/** Cuerpo de POST /api/traspasos. Contrato de la API. */
export interface RegistrarTraspasoDto {
  almacenOrigenId: string;
  almacenDestinoId: string;
  fecha: string;
  lineas: LineaTraspasoDto[];
}
