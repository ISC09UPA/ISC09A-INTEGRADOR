import type { NaturalezaFinanciera, TipoDocumento } from '@/types/enums';

/** Ingreso por venta o egreso por recepción (y sus reversos). Contrato de la API. */
export interface MovimientoFinancieroDto {
  id: string;
  fecha: string;
  naturaleza: NaturalezaFinanciera;
  documentoTipo: TipoDocumento;
  documentoId: string;
  folio: number;
  tercero: string;
  subtotal: number;
  iva: number;
  total: number;
  esReverso: boolean;
}
