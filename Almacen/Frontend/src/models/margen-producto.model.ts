/** Margen de un producto vendido en el periodo. */
export interface MargenProducto {
  referencia: string;
  descripcion: string;
  cantidad: number;
  ventas: number;
  costo: number;
  margen: number;
  margenPorcentaje: number;
}
