/** Almacén con su valor de inventario. Contrato de la API. */
export interface AlmacenDto {
  id: string;
  nombre: string;
  ubicacion: string;
  activo: boolean;
  productos: number;
  valorInventario: number;
}
