import type { Existencia } from '@/models/existencia.model';
import type { Punto } from '@/models/punto.model';

/** Valor del inventario y productos bajo mínimo. */
export interface ReporteInventario {
  valorTotal: number;
  valorPorAlmacen: Punto[];
  bajoMinimo: Existencia[];
}
