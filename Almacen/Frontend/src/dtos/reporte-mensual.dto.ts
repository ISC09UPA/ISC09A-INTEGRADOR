import type { FilaMesDto } from '@/dtos/fila-mes.dto';
import type { ValoresAnioDto } from '@/dtos/valores-anio.dto';

/** Reporte mensual Debe / Haber de varios años. Contrato de la API. */
export interface ReporteMensualDto {
  anios: number[];
  meses: FilaMesDto[];
  totales: ValoresAnioDto[];
  saldoActual: number;
}
