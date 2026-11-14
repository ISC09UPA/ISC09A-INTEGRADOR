import type { TipoMovimientoInventario } from '@/types/enums';
import type { DocumentoRef } from '@/models/documento-ref.model';

/** Movimiento de inventario del reporte de movimientos. */
export interface MovimientoInventario {
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
  documento?: DocumentoRef;
  usuario: string;
  valor: number;
}
