import type { LineaVentaDto } from '@/dtos/linea-venta.dto';

/** Cuerpo de POST /api/ventas. Contrato de la API. */
export interface RegistrarVentaDto {
  clienteId: string;
  fecha: string;
  lineas: LineaVentaDto[];
}
