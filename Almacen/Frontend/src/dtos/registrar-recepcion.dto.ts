import type { LineaRecepcionDto } from '@/dtos/linea-recepcion.dto';

/** Cuerpo de POST /api/recepciones. Contrato de la API. */
export interface RegistrarRecepcionDto {
  proveedorId: string;
  almacenId: string;
  fecha: string;
  lineas: LineaRecepcionDto[];
}
