import type { PaginaDto } from '@/dtos/pagina.dto';
import type { NivelStock } from '@/types/enums';

/** Existencia de un producto en un almacén con su nivel. Contrato de la API. */
export interface SemaforoAlmacenDto {
  almacenId: string;
  almacen: string;
  cantidad: number;
  minimo: number;
  nivel: NivelStock;
}

/** Producto con existencia y mínimo sumados en todos los almacenes. Contrato de la API. */
export interface SemaforoProductoDto {
  productoId: string;
  referencia: string;
  descripcion: string;
  unidad: string;
  familiaNfc?: string | null;
  existencia: number;
  minimo: number;
  nivel: NivelStock;
  almacenes: SemaforoAlmacenDto[];
}

/** Respuesta de GET /api/stock/semaforo. Contrato de la API. */
export interface SemaforoStockDto {
  productos: PaginaDto<SemaforoProductoDto>;
  verdes: number;
  amarillos: number;
  rojos: number;
}
