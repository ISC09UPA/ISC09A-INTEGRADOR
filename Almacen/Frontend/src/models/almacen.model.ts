/** Almacén con su valor de inventario. */
export interface Almacen {
  id: string;
  nombre: string;
  ubicacion: string;
  activo: boolean;
  productos: number;
  valorInventario: number;
}
