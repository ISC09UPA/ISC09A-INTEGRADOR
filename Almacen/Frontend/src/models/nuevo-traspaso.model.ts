import type { LineaNuevoTraspaso } from '@/models/linea-nuevo-traspaso.model';

/** Lo que se envía en POST /api/traspasos. */
export interface NuevoTraspaso {
  almacenOrigenId: string;
  almacenDestinoId: string;
  fecha: string;
  lineas: LineaNuevoTraspaso[];
}
