/** Por producto y almacén: inicial, entradas, salidas y final del periodo. */
export interface ResumenProducto {
  almacen: string;
  referencia: string;
  descripcion: string;
  unidad: string;
  inicial: number;
  entradas: number;
  salidas: number;
  final: number;
  valorEntradas: number;
  valorSalidas: number;
}
