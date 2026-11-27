import type { ProductoOpcionDto } from '@/dtos/producto-opcion.dto';

/** Quién tiene el lector de ventas (GET/POST /api/iot/venta…). Contrato de la API. */
export interface SesionVentaDto {
  sesionId: string;
  usuarioId: string;
  usuario: string;
  almacenId: string;
  almacen: string;
  inicio: string;
  ultimoLatido: string;
  esMia: boolean;
}

/** Producto que pasó por el lector (POST /api/iot/venta/escaneos). Contrato de la API. */
export interface EscaneoDto {
  id: string;
  fecha: string;
  producto: ProductoOpcionDto;
}
