/** Cuánto hay de un producto en un almacén (al capturar documentos). Contrato de la API. */
export interface ExistenciaOpcionDto {
  almacenId: string;
  almacen: string;
  cantidad: number;
  costoPromedio: number;
}
