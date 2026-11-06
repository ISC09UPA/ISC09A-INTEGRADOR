/** Por producto y almacén: inicial, entradas, salidas y final del periodo. Contrato de la API. */
export interface ResumenProductoDto {
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
