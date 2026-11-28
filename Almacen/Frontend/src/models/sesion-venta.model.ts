/** Quién tiene el modo inteligente de venta (el lector NFC). */
export interface SesionVenta {
  usuario: string;
  almacenId: string;
  almacen: string;
  esMia: boolean;
}
