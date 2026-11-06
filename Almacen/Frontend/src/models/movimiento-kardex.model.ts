import type { TipoMovimientoInventario } from '@/types/enums';
import type { DocumentoRef } from '@/models/documento-ref.model';

/** Renglón del kardex de un producto en un almacén. */
export interface MovimientoKardex {
  fecha: string;
  tipo: TipoMovimientoInventario;
  cantidad: number;
  costoUnitario: number;
  saldo: number;
  costoPromedio: number;
  /** Documento que originó el movimiento, si lo hay. */
  documento?: DocumentoRef;
  usuario: string;
}
