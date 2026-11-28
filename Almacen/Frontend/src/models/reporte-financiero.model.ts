/** Flujo financiero del periodo. */
export interface ReporteFinanciero {
  periodos: string[];
  ingresos: number[];
  egresos: number[];
  totalIngresos: number;
  totalEgresos: number;
  ivaTrasladado: number;
  ivaAcreditable: number;
  saldo: number;
  /** IVA trasladado − acreditable. */
  ivaPorPagar: number;
}
