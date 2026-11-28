/** Lo que se envía en POST /api/productos. */
export interface NuevoProducto {
  referencia: string;
  descripcion: string;
  unidadId: string;
  /** Solo si el usuario no puede aprobar. */
  gerenteAprobadorId?: string;
}
