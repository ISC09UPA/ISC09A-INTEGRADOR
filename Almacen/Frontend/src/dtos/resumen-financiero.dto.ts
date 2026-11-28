import type { MovimientoFinancieroDto } from '@/dtos/movimiento-financiero.dto';

/** Movimientos financieros con sus totales. Contrato de la API. */
export interface ResumenFinancieroDto {
  movimientos: MovimientoFinancieroDto[];
  total: number;
  ingresos: number;
  egresos: number;
  ivaTrasladado: number;
  ivaAcreditable: number;
  saldo: number;
}
