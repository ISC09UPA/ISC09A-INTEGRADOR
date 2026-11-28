import type { EstadoAprobacion } from '@/types/enums';

/** Producto del catálogo. */
export interface Producto {
  id: string;
  referencia: string;
  descripcion: string;
  unidadId: string;
  unidad: string;
  permiteFracciones: boolean;
  imagenRuta?: string;
  activo: boolean;
  estado: EstadoAprobacion;
  gerenteAprobadorId?: string;
  gerenteAprobador?: string;
  creadoPor: string;
  creadoEn: string;
  motivoRechazo?: string;
  existencia: number;
  /** "REF · descripción". */
  texto: string;
}
