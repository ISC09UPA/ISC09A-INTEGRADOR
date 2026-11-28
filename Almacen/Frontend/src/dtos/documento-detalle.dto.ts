import type { EstadoDocumento } from '@/types/enums';
import type { LineaDocumentoDto } from '@/dtos/linea-documento.dto';

/** Venta, recepción o traspaso con sus líneas. Contrato de la API. */
export interface DocumentoDetalleDto {
  id: string;
  folio: number;
  fecha: string;
  estado: EstadoDocumento;
  tercero?: string | null;
  terceroRfc?: string | null;
  terceroCorreo?: string | null;
  almacen: string;
  almacenDestino?: string | null;
  lineas: LineaDocumentoDto[];
  creadoPor: string;
  creadoEn: string;
  canceladoPor?: string | null;
  canceladoEn?: string | null;
  motivoCancelacion?: string | null;
  subtotal: number;
  iva: number;
  total: number;
}
