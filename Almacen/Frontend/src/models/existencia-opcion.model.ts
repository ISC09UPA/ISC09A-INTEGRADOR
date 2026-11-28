/** Cuánto hay de un producto en un almacén (al capturar documentos). */
export interface ExistenciaOpcion {
  almacenId: string;
  almacen: string;
  cantidad: number;
  costoPromedio: number;
}
