import type { TipoDocumento, TipoMovimientoInventario } from '@/types/enums';

/** Renglón del kardex de un producto en un almacén. Contrato de la API. */
export interface KardexDto {
  fecha: string;
  tipo: TipoMovimientoInventario;
  cantidad: number;
  costoUnitario: number;
  saldo: number;
  costoPromedio: number;
  documentoTipo?: TipoDocumento | null;
  documentoId?: string | null;
  folio?: number | null;
  usuario: string;
}
