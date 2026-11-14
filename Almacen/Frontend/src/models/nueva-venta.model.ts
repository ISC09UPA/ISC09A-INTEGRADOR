import type { LineaNuevaVenta } from '@/models/linea-nueva-venta.model';

/** Lo que se envía en POST /api/ventas. */
export interface NuevaVenta {
  clienteId: string;
  fecha: string;
  lineas: LineaNuevaVenta[];
}
