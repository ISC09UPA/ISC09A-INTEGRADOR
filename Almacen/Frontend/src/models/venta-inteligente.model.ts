import type { ProductoOpcion } from '@/models/producto-opcion.model';

/** El candado del modo inteligente de venta: hay un solo lector NFC. */
export interface SesionVenta {
  usuario: string;
  almacenId: string;
  almacen: string;
  esMia: boolean;
}

/** Un producto escaneado: se agrega a la venta con cantidad 1. */
export interface Escaneo {
  id: string;
  producto: ProductoOpcion;
}
