import type { ExistenciaOpcionDto } from '@/dtos/existencia-opcion.dto';

/** Producto aprobado y activo para elegir en un documento. Contrato de la API. */
export interface ProductoOpcionDto {
  id: string;
  referencia: string;
  descripcion: string;
  unidad: string;
  permiteFracciones: boolean;
  existencias: ExistenciaOpcionDto[];
}
