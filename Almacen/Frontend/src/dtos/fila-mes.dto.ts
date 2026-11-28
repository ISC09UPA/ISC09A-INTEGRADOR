import type { ValoresAnioDto } from '@/dtos/valores-anio.dto';

/** Un mes del reporte mensual, con los valores de cada año. Contrato de la API. */
export interface FilaMesDto {
  mes: number;
  nombre: string;
  porAnio: ValoresAnioDto[];
}
