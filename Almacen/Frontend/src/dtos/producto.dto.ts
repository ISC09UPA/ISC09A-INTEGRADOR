import type { EstadoAprobacion } from '@/types/enums';

/** Producto del catálogo. Contrato de la API. */
export interface ProductoDto {
  id: string;
  referencia: string;
  descripcion: string;
  unidadId: string;
  unidad: string;
  permiteFracciones: boolean;
  imagenRuta?: string | null;
  activo: boolean;
  estado: EstadoAprobacion;
  gerenteAprobadorId?: string | null;
  gerenteAprobador?: string | null;
  creadoPor: string;
  creadoEn: string;
  motivoRechazo?: string | null;
  existencia: number;
}
