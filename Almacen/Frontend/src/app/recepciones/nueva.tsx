import { router } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import type { Opcion } from '@/models/opcion.model';
import { almacenesService, recepcionesService } from '@/services';
import { BotonAgregarLinea, CampoPrecio, SelectorIva, TarjetaLinea, errorCantidad, nuevaClave, type LineaBase } from '@/components/captura';
import { CampoFecha } from '@/components/fechas';
import { SelectorTercero } from '@/components/selectores';
import { Aviso, Boton, Encabezado, Pantalla, Selector, Tarjeta, Totales, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useMiAlmacen } from '@/hooks/use-mi-almacen';
import { useDatos } from '@/hooks/use-datos';
import { Fechas, Formato, importeLinea, leerNumero, redondear } from '@/lib/formato';

interface Linea extends LineaBase {
  costo: string;
  tasa: number;
}

const lineaVacia = (): Linea => ({ clave: nuevaClave(), producto: null, cantidad: '', costo: '', tasa: 0.16 });
const importe = (l: Linea) => importeLinea(leerNumero(l.cantidad), leerNumero(l.costo), l.tasa);

/** Captura de recepción (Recepciones/NuevaRecepcion.razor). */
export default function NuevaRecepcion() {
  const [proveedor, setProveedor] = useState<Opcion | null>(null);
  const [almacenElegido, setAlmacenId] = useState<string | undefined>();
  const mio = useMiAlmacen();
  const almacenId = mio ?? almacenElegido;
  const [fecha, setFecha] = useState(Fechas.hoy());
  const [lineas, setLineas] = useState<Linea[]>([lineaVacia()]);
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const almacenes = useDatos(async () => {
    const r = await almacenesService.listar();
    if (r.length === 1) setAlmacenId((a) => a ?? r[0].id);
    return r;
  });

  const cambiar = (clave: number, cambios: Partial<Linea>) => setLineas((ls) => ls.map((l) => (l.clave === clave ? { ...l, ...cambios } : l)));
  const total = lineas.map(importe).reduce((t, i) => ({ subtotal: t.subtotal + i.subtotal, iva: t.iva + i.iva }), { subtotal: 0, iva: 0 });

  const registrar = async () => {
    setError(null);
    if (!proveedor || !almacenId || !Fechas.valida(fecha)) return setError('Elige proveedor, almacén y fecha.');
    if (lineas.some((l) => !l.producto || !(leerNumero(l.cantidad) > 0))) return setError('Cada línea necesita producto y cantidad mayor que cero.');
    if (lineas.some((l) => Number.isNaN(leerNumero(l.costo)) || leerNumero(l.costo) < 0)) return setError('Captura el costo unitario de cada línea.');

    setGuardando(true);
    try {
      const r = await recepcionesService.registrar({
        proveedorId: proveedor.id,
        almacenId,
        fecha,
        lineas: lineas.map((l) => ({ productoId: l.producto!.id, cantidad: leerNumero(l.cantidad), costoUnitario: leerNumero(l.costo), tasaIva: l.tasa })),
      });
      avisar(`Recepción ${Formato.folio('REC', r.folio)} registrada.`);
      router.replace(`/recepciones/${r.id}`);
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setGuardando(false);
    }
  };

  return (
    <Pantalla>
      <Encabezado titulo="Nueva recepción" descripcion="Al registrarla se suman las existencias, se recalcula el costo promedio y se registra el egreso." />
      <View style={{ gap: Spacing.three }}>
        <Tarjeta style={{ gap: Spacing.three }}>
          <SelectorTercero clientes={false} valor={proveedor} alCambiar={setProveedor} />
          <Selector<string>
            etiqueta="Almacén de entrada"
            valor={almacenId}
            opciones={(almacenes.datos ?? []).map((a) => ({ valor: a.id, texto: a.nombre, detalle: a.ubicacion }))}
            alCambiar={setAlmacenId}
            deshabilitado={!!mio}
          />
          {almacenes.error ? <Aviso tipo="error" texto={almacenes.error} /> : null}
          <CampoFecha etiqueta="Fecha" valor={fecha} alCambiar={setFecha} />
        </Tarjeta>

        {lineas.map((l, i) => (
          <TarjetaLinea
            key={l.clave}
            numero={i + 1}
            producto={l.producto}
            alElegirProducto={(p) => cambiar(l.clave, { producto: p })}
            cantidad={l.cantidad}
            alCambiarCantidad={(t) => cambiar(l.clave, { cantidad: t })}
            errorCantidad={errorCantidad(l.cantidad, l.producto)}
            puedeQuitar={lineas.length > 1}
            alQuitar={() => setLineas((ls) => ls.filter((x) => x.clave !== l.clave))}
            importe={Formato.dinero(importe(l).subtotal)}>
            <CampoPrecio etiqueta="Costo unitario" valor={l.costo} alCambiar={(t) => cambiar(l.clave, { costo: t })} />
            <SelectorIva valor={l.tasa} alCambiar={(t) => cambiar(l.clave, { tasa: t })} />
          </TarjetaLinea>
        ))}

        <BotonAgregarLinea alPresionar={() => setLineas((ls) => [...ls, lineaVacia()])} />

        <Tarjeta style={{ gap: Spacing.three }}>
          {error ? <Aviso tipo="error" texto={error} /> : null}
          <Totales subtotal={Formato.dinero(redondear(total.subtotal))} iva={Formato.dinero(redondear(total.iva))} total={Formato.dinero(redondear(total.subtotal + total.iva))} />
          <Boton texto={guardando ? 'Registrando…' : 'Registrar recepción'} cargando={guardando} alPresionar={registrar} />
        </Tarjeta>
      </View>
    </Pantalla>
  );
}
