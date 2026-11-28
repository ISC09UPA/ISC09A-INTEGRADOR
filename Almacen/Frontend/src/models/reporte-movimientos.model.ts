import type { MovimientoInventario } from '@/models/movimiento-inventario.model';
import type { Pagina } from '@/models/pagina.model';
import type { ResumenProducto } from '@/models/resumen-producto.model';

/** Reporte de movimientos de inventario. */
export interface ReporteMovimientos {
  movimientos: Pagina<MovimientoInventario>;
  numeroMovimientos: number;
  entradas: number;
  salidas: number;
  valorEntradas: number;
  valorSalidas: number;
  porProducto: ResumenProducto[];
}
