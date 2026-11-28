import type { ValoresAnio } from '@/models/valores-anio.model';

/** Un mes del reporte mensual, con los valores de cada año. */
export interface FilaMes {
  mes: number;
  nombre: string;
  porAnio: ValoresAnio[];
}
