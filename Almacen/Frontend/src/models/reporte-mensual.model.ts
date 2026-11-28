import type { FilaMes } from '@/models/fila-mes.model';
import type { ValoresAnio } from '@/models/valores-anio.model';

/** Reporte mensual Debe / Haber de varios años. */
export interface ReporteMensual {
  anios: number[];
  meses: FilaMes[];
  totales: ValoresAnio[];
  saldoActual: number;
}
