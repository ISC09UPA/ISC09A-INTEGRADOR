import type { NaturalezaFinanciera } from '@/types/enums';
import type { DocumentoRef } from '@/models/documento-ref.model';

/** Ingreso por venta o egreso por recepción (y sus reversos). */
export interface MovimientoFinanciero {
  id: string;
  fecha: string;
  naturaleza: NaturalezaFinanciera;
  documento: DocumentoRef;
  tercero: string;
  subtotal: number;
  iva: number;
  total: number;
  esReverso: boolean;
}
