import type { ProductoOpcion } from '@/models/producto-opcion.model';

/** Una pieza que pasó por el lector NFC. Si trae rechazo (ya vendida, familia desconocida…) no se agrega. */
export interface Escaneo {
  id: string;
  uid: string;
  familia: string;
  producto?: ProductoOpcion;
  rechazo?: string;
  /** La pieza o el paquete ya se había leído: alerta amarilla. */
  duplicado: boolean;
}
