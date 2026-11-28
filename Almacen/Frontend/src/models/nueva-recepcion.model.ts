import type { LineaNuevaRecepcion } from '@/models/linea-nueva-recepcion.model';

/** Lo que se envía en POST /api/recepciones. */
export interface NuevaRecepcion {
  proveedorId: string;
  almacenId: string;
  fecha: string;
  lineas: LineaNuevaRecepcion[];
}
