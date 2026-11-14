import type { ExistenciaDto } from '@/dtos/existencia.dto';
import type { PuntoDto } from '@/dtos/punto.dto';

/** Valor del inventario y productos bajo mínimo. Contrato de la API. */
export interface ReporteInventarioDto {
  valorTotal: number;
  valorPorAlmacen: PuntoDto[];
  bajoMinimo: ExistenciaDto[];
}
