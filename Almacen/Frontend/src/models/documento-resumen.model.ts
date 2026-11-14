import type { EstadoDocumento } from '@/types/enums';

/** Renglón de la lista de ventas, recepciones o traspasos. */
export interface DocumentoResumen {
  id: string;
  folio: number;
  fecha: string;
  tercero: string;
  almacen: string;
  estado: EstadoDocumento;
  lineas: number;
  subtotal: number;
  iva: number;
  total: number;
}
