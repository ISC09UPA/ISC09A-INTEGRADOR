/** Existencia de un producto en un almacén. */
export interface Existencia {
  almacenId: string;
  almacen: string;
  productoId: string;
  referencia: string;
  descripcion: string;
  unidad: string;
  permiteFracciones: boolean;
  cantidad: number;
  stockMinimo: number;
  costoPromedio: number;
  valor: number;
  bajoMinimo: boolean;
}
