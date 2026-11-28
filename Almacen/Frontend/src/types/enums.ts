// Enums de la API. Viajan como texto (JsonStringEnumConverter), así que se usan igual en DTOs y models.

export type EstadoDocumento = 'Borrador' | 'Confirmado' | 'Cancelado';
export type EstadoAprobacion = 'Pendiente' | 'Aprobado' | 'Rechazado';
export type NaturalezaFinanciera = 'Ingreso' | 'Egreso';
export type TipoDocumento = 'Recepcion' | 'Venta' | 'Traspaso';
export type TipoMovimientoInventario =
  | 'Inicial'
  | 'Recepcion'
  | 'Venta'
  | 'TraspasoSalida'
  | 'TraspasoEntrada'
  | 'Ajuste'
  | 'CancelacionRecepcion'
  | 'CancelacionVenta';
export type TipoReporte = 'General' | 'Ventas' | 'Compras' | 'Inventario' | 'Financiero' | 'Margen';
/** Semáforo de stock: Rojo sin existencia, Amarillo en o bajo el mínimo, Verde arriba del mínimo. */
export type NivelStock = 'Verde' | 'Amarillo' | 'Rojo';
