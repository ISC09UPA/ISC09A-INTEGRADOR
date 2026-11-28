import type { ProductoOpcionDto } from '@/dtos/producto-opcion.dto';

/** Una pieza que pasó por el lector NFC. Si trae "rechazo" no se agrega a la venta. Contrato de la API. */
export interface EscaneoDto {
  id: string;
  fecha: string;
  /** UID de fábrica de la etiqueta: identifica la pieza física. */
  uid: string;
  /** Familia NFC del producto que mandó el lector. */
  familia: string;
  producto?: ProductoOpcionDto | null;
  rechazo?: string | null;
  /** El rechazo es por duplicado: alerta amarilla, no error. */
  duplicado?: boolean;
}
