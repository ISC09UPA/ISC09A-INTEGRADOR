import type { ExistenciaOpcion } from '@/models/existencia-opcion.model';

/** Producto aprobado y activo para elegir en un documento. */
export interface ProductoOpcion {
  id: string;
  referencia: string;
  descripcion: string;
  unidad: string;
  permiteFracciones: boolean;
  existencias: ExistenciaOpcion[];
  /** "REF · descripción". */
  texto: string;
}
