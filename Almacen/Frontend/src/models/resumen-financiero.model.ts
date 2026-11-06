import type { MovimientoFinanciero } from '@/models/movimiento-financiero.model';

/** Movimientos financieros con sus totales. */
export interface ResumenFinanciero {
  movimientos: MovimientoFinanciero[];
  total: number;
  ingresos: number;
  egresos: number;
  ivaTrasladado: number;
  ivaAcreditable: number;
  saldo: number;
  /** IVA trasladado − acreditable. */
  ivaPorPagar: number;
}
