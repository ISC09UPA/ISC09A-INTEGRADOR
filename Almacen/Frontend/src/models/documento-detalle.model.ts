import type { EstadoDocumento } from '@/types/enums';
import type { Cancelacion } from '@/models/cancelacion.model';
import type { LineaDocumento } from '@/models/linea-documento.model';

/** Venta, recepción o traspaso con sus líneas. */
export interface DocumentoDetalle {
  id: string;
  folio: number;
  fecha: string;
  estado: EstadoDocumento;
  tercero?: string;
  terceroRfc?: string;
  terceroCorreo?: string;
  almacen: string;
  almacenDestino?: string;
  lineas: LineaDocumento[];
  creadoPor: string;
  creadoEn: string;
  /** Solo si está cancelado. */
  cancelacion?: Cancelacion;
  subtotal: number;
  iva: number;
  total: number;
}
