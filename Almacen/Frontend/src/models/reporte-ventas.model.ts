import type { Punto } from '@/models/punto.model';

/** Reporte de ventas del periodo. */
export interface ReporteVentas {
  numeroVentas: number;
  subtotal: number;
  iva: number;
  total: number;
  ticketPromedio: number;
  porPeriodo: Punto[];
  topClientes: Punto[];
  topProductos: Punto[];
  porAlmacen: Punto[];
}
