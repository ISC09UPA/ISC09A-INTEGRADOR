import type { Pagina } from '@/models/pagina.model';
import type { NivelStock } from '@/types/enums';

export interface SemaforoAlmacen {
  almacenId: string;
  almacen: string;
  cantidad: number;
  minimo: number;
  nivel: NivelStock;
}

/** Producto con existencia y mínimo sumados en todos los almacenes. */
export interface SemaforoProducto {
  productoId: string;
  referencia: string;
  descripcion: string;
  unidad: string;
  familiaNfc?: string;
  existencia: number;
  minimo: number;
  nivel: NivelStock;
  almacenes: SemaforoAlmacen[];
}

export interface SemaforoStock {
  productos: Pagina<SemaforoProducto>;
  verdes: number;
  amarillos: number;
  rojos: number;
}
