import type { MovimientoDto } from '@/dtos/movimiento.dto';
import type { PaginaDto } from '@/dtos/pagina.dto';
import type { ResumenProductoDto } from '@/dtos/resumen-producto.dto';

/** Reporte de movimientos de inventario. Contrato de la API. */
export interface ReporteMovimientosDto {
  movimientos: PaginaDto<MovimientoDto>;
  numeroMovimientos: number;
  entradas: number;
  salidas: number;
  valorEntradas: number;
  valorSalidas: number;
  porProducto: ResumenProductoDto[];
}
