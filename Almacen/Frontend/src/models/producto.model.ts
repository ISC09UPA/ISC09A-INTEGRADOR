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
  /** Familia NFC: el lector la manda con el UID de cada pieza en el modo inteligente de venta. */
  codigoNfc?: string;
  /** Código NFC grabado en los paquetes del proveedor: lo lee la recepción inteligente. */
  codigoProveedor?: string;
  /** "REF · descripción". */
  texto: string;
}
