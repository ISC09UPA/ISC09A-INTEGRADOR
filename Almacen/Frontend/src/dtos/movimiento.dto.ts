import type { TipoDocumento, TipoMovimientoInventario } from '@/types/enums';

/** Movimiento de inventario del reporte de movimientos. Contrato de la API. */
export interface MovimientoDto {
  id: string;
  fecha: string;
  almacen: string;
  productoId: string;
  referencia: string;
  descripcion: string;
  unidad: string;
  tipo: TipoMovimientoInventario;
  cantidad: number;
  costoUnitario: number;
  saldo: number;
  documentoTipo?: TipoDocumento | null;
  documentoId?: string | null;
  folio?: number | null;
  usuario: string;
  valor: number;
}
