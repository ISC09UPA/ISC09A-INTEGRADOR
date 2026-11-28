/** Existencia de un producto en un almacén. Contrato de la API. */
export interface ExistenciaDto {
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
