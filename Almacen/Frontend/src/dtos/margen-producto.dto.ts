/** Margen de un producto vendido en el periodo. Contrato de la API. */
export interface MargenProductoDto {
  referencia: string;
  descripcion: string;
  cantidad: number;
  ventas: number;
  costo: number;
  margen: number;
  margenPorcentaje: number;
}
