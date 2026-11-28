import type { ReactNode } from 'react';
import { View } from 'react-native';

import type { ProductoOpcion } from '@/models/producto-opcion.model';
import { SelectorProducto } from '@/components/selectores';
import { Boton, BotonIcono, Campo, Fila, Selector, Tarjeta, Texto } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { Formato, TasasIva, leerNumero } from '@/lib/formato';

/** Línea editable de un documento: producto, cantidad y lo que cada documento agregue (precio, IVA, almacén). */
export interface LineaBase {
  clave: number;
  producto: ProductoOpcion | null;
  cantidad: string;
}

let siguiente = 1;
export const nuevaClave = () => siguiente++;

export function TarjetaLinea({
  numero,
  producto,
  alElegirProducto,
  cantidad,
  alCambiarCantidad,
  errorCantidad,
  puedeQuitar,
  alQuitar,
  children,
  debajoProducto,
  importe,
}: {
  numero: number;
  producto: ProductoOpcion | null;
  alElegirProducto: (p: ProductoOpcion) => void;
  cantidad: string;
  alCambiarCantidad: (t: string) => void;
  errorCantidad?: string | null;
  puedeQuitar: boolean;
  alQuitar: () => void;
  children?: ReactNode;
  debajoProducto?: ReactNode;
  importe?: string;
}) {
  const c = useColores();
  return (
    <Tarjeta style={{ gap: Spacing.two }}>
      <Fila style={{ justifyContent: 'space-between' }}>
        <Texto tipo="etiqueta">Línea {numero}</Texto>
        {puedeQuitar ? <BotonIcono icono="delete" etiqueta="Quitar línea" alPresionar={alQuitar} color={c.error} /> : null}
      </Fila>
      <SelectorProducto valor={producto} alCambiar={alElegirProducto} />
      {debajoProducto}
      <Fila style={{ alignItems: 'flex-start', flexWrap: 'wrap' }}>
        <Campo
          etiqueta={producto ? `Cantidad (${producto.unidad})` : 'Cantidad'}
          value={cantidad}
          onChangeText={alCambiarCantidad}
          keyboardType={producto?.permiteFracciones === false ? 'number-pad' : 'decimal-pad'}
          error={errorCantidad}
          style={{ flex: 1, minWidth: 110 }}
        />
        {children}
      </Fila>
      {importe ? (
        <Fila style={{ justifyContent: 'flex-end' }}>
          <Texto tipo="secundario">Subtotal </Texto>
          <Texto tipo="fuerte">{importe}</Texto>
        </Fila>
      ) : null}
    </Tarjeta>
  );
}

export function CampoPrecio({ etiqueta, valor, alCambiar }: { etiqueta: string; valor: string; alCambiar: (t: string) => void }) {
  const n = leerNumero(valor);
  return (
    <Campo
      etiqueta={etiqueta}
      value={valor}
      onChangeText={alCambiar}
      keyboardType="decimal-pad"
      placeholder="$0.00"
      error={valor && (Number.isNaN(n) || n < 0) ? 'Inválido' : null}
      style={{ flex: 1, minWidth: 110 }}
    />
  );
}

export function SelectorIva({ valor, alCambiar }: { valor: number; alCambiar: (v: number) => void }) {
  return (
    <View style={{ minWidth: 120 }}>
      <Selector etiqueta="IVA" valor={valor} opciones={TasasIva.map((t) => ({ valor: t.valor, texto: t.texto }))} alCambiar={alCambiar} />
    </View>
  );
}

export function BotonAgregarLinea({ alPresionar }: { alPresionar: () => void }) {
  return <Boton texto="Agregar producto" icono="add" variante="texto" alPresionar={alPresionar} style={{ alignSelf: 'flex-start' }} />;
}

/** Cantidad positiva y, si la unidad no admite fracciones, entera. */
export function errorCantidad(texto: string, producto: ProductoOpcion | null, disponible?: number): string | null {
  if (!texto) return null;
  const n = leerNumero(texto);
  if (Number.isNaN(n) || n <= 0) return 'Mayor que cero';
  if (producto && !producto.permiteFracciones && !Number.isInteger(n)) return `${producto.unidad} no admite fracciones`;
  if (disponible !== undefined && n > disponible) return `Hay ${Formato.cantidad(disponible)}`;
  return null;
}
