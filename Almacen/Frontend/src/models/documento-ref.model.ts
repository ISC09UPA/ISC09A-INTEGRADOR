import type { TipoDocumento } from '@/types/enums';

/** Referencia a una venta, recepción o traspaso. */
export interface DocumentoRef {
  tipo: TipoDocumento;
  id: string;
  folio: number;
}
