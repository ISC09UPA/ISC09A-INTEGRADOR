import type { Punto } from '@/models/punto.model';

/** Reporte de compras (recepciones) del periodo. */
export interface ReporteCompras {
  numeroRecepciones: number;
  subtotal: number;
  iva: number;
  total: number;
  porPeriodo: Punto[];
  porProveedor: Punto[];
}
