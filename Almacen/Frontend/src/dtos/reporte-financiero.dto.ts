/** Flujo financiero del periodo. Contrato de la API. */
export interface ReporteFinancieroDto {
  periodos: string[];
  ingresos: number[];
  egresos: number[];
  totalIngresos: number;
  totalEgresos: number;
  ivaTrasladado: number;
  ivaAcreditable: number;
  saldo: number;
}
