import type { PuntoDto } from '@/dtos/punto.dto';

/** Reporte de compras (recepciones) del periodo. Contrato de la API. */
export interface ReporteComprasDto {
  numeroRecepciones: number;
  subtotal: number;
  iva: number;
  total: number;
  porPeriodo: PuntoDto[];
  porProveedor: PuntoDto[];
}
