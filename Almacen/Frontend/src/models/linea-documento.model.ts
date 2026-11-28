/** Línea de un documento registrado. */
export interface LineaDocumento {
  productoId: string;
  referencia: string;
  descripcion: string;
  unidad: string;
  almacen?: string;
  cantidad: number;
  precioUnitario: number;
  tasaIva: number;
  subtotal: number;
  iva: number;
  costoUnitario: number;
  total: number;
}
