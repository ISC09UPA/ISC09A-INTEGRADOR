import { router } from 'expo-router';
import { useState } from 'react';
import { ScrollView, View } from 'react-native';

import type { Opcion } from '@/models/opcion.model';
import type { ProductoOpcion } from '@/models/producto-opcion.model';
import type { Tercero } from '@/models/tercero.model';
import { ventasService } from '@/services';
import { BotonAgregarLinea, CampoPrecio, SelectorIva, TarjetaLinea, errorCantidad, nuevaClave, type LineaBase } from '@/components/captura';
import { CampoFecha } from '@/components/fechas';
import { SelectorTercero } from '@/components/selectores';
import { FormularioTercero, Terceros } from '@/components/terceros';
import { Aviso, Boton, Encabezado, Hoja, Pantalla, Selector, Tarjeta, Totales, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { Fechas, Formato, importeLinea, leerNumero, redondear } from '@/lib/formato';
import { Permisos } from '@/lib/permisos';
import { useSesion } from '@/lib/sesion';

interface Linea extends LineaBase {
  almacenId: string | null;
  precio: string;
  tasa: number;
}

const lineaVacia = (): Linea => ({ clave: nuevaClave(), producto: null, almacenId: null, cantidad: '', precio: '', tasa: 0.16 });

const disponible = (l: Linea) => l.producto?.existencias.find((e) => e.almacenId === l.almacenId)?.cantidad ?? 0;
const importe = (l: Linea) => importeLinea(leerNumero(l.cantidad), leerNumero(l.precio), l.tasa);

/** Captura de venta (Ventas/NuevaVenta.razor): cliente, fecha y por línea el almacén de salida y el precio. */
export default function NuevaVenta() {
  const { puede } = useSesion();
  const [cliente, setCliente] = useState<Opcion | null>(null);
  const [fecha, setFecha] = useState(Fechas.hoy());
  const [lineas, setLineas] = useState<Linea[]>([lineaVacia()]);
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const [altaCliente, setAltaCliente] = useState(false);

  const cambiar = (clave: number, cambios: Partial<Linea>) => setLineas((ls) => ls.map((l) => (l.clave === clave ? { ...l, ...cambios } : l)));
  const total = lineas.map(importe).reduce((t, i) => ({ subtotal: t.subtotal + i.subtotal, iva: t.iva + i.iva }), { subtotal: 0, iva: 0 });

  const registrar = async () => {
    setError(null);
    if (!cliente || !Fechas.valida(fecha)) return setError('Elige el cliente y la fecha.');
    if (lineas.some((l) => !l.producto || !l.almacenId || !(leerNumero(l.cantidad) > 0))) return setError('Cada línea necesita producto, almacén y cantidad mayor que cero.');
    if (lineas.some((l) => leerNumero(l.cantidad) > disponible(l))) return setError('Hay líneas con más cantidad de la que hay en el almacén.');
    if (lineas.some((l) => Number.isNaN(leerNumero(l.precio)) || leerNumero(l.precio) < 0)) return setError('Captura el precio unitario de cada línea.');

    setGuardando(true);
    try {
      const r = await ventasService.registrar({
        clienteId: cliente.id,
        fecha,
        lineas: lineas.map((l) => ({ productoId: l.producto!.id, almacenId: l.almacenId!, cantidad: leerNumero(l.cantidad), precioUnitario: leerNumero(l.precio), tasaIva: l.tasa })),
      });
      avisar(`Venta ${Formato.folio('VTA', r.folio)} registrada.`);
      router.replace(`/ventas/${r.id}`);
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setGuardando(false);
    }
  };

  return (
    <Pantalla>
      <Encabezado
        titulo="Nueva venta"
        descripcion="Captura el precio de cada línea y el almacén de donde sale. Al registrarla se descuentan existencias y se registra el ingreso."
      />
      <View style={{ gap: Spacing.three }}>
        <Tarjeta style={{ gap: Spacing.three }}>
          <SelectorTercero clientes valor={cliente} alCambiar={setCliente} />
          {puede(Permisos.Clientes.Crear) ? (
            <Boton texto="Nuevo cliente" icono="person-add" variante="texto" alPresionar={() => setAltaCliente(true)} style={{ alignSelf: 'flex-start' }} />
          ) : null}
          <CampoFecha etiqueta="Fecha" valor={fecha} alCambiar={setFecha} />
        </Tarjeta>

        {lineas.map((l, i) => (
          <TarjetaLinea
            key={l.clave}
            numero={i + 1}
            producto={l.producto}
            alElegirProducto={(p: ProductoOpcion) =>
              // Propone el almacén con más existencia, como la web.
              cambiar(l.clave, { producto: p, almacenId: [...p.existencias].sort((a, b) => b.cantidad - a.cantidad)[0]?.almacenId ?? null })
            }
            debajoProducto={
              l.producto ? (
                <Selector<string>
                  etiqueta="Sale de"
                  valor={l.almacenId ?? undefined}
                  opciones={l.producto.existencias.map((e) => ({ valor: e.almacenId, texto: `${e.almacen} (${Formato.cantidad(e.cantidad)})` }))}
                  alCambiar={(a) => cambiar(l.clave, { almacenId: a })}
                  placeholder="Sin existencias en ningún almacén"
                  deshabilitado={!l.producto.existencias.length}
                />
              ) : null
            }
            cantidad={l.cantidad}
            alCambiarCantidad={(t) => cambiar(l.clave, { cantidad: t })}
            errorCantidad={errorCantidad(l.cantidad, l.producto, l.producto && l.almacenId ? disponible(l) : undefined)}
            puedeQuitar={lineas.length > 1}
            alQuitar={() => setLineas((ls) => ls.filter((x) => x.clave !== l.clave))}
            importe={Formato.dinero(importe(l).subtotal)}>
            <CampoPrecio etiqueta="Precio unitario" valor={l.precio} alCambiar={(t) => cambiar(l.clave, { precio: t })} />
            <SelectorIva valor={l.tasa} alCambiar={(t) => cambiar(l.clave, { tasa: t })} />
          </TarjetaLinea>
        ))}

        <BotonAgregarLinea alPresionar={() => setLineas((ls) => [...ls, lineaVacia()])} />

        <Tarjeta style={{ gap: Spacing.three }}>
          {error ? <Aviso tipo="error" texto={error} /> : null}
          <Totales subtotal={Formato.dinero(redondear(total.subtotal))} iva={Formato.dinero(redondear(total.iva))} total={Formato.dinero(redondear(total.subtotal + total.iva))} />
          <Boton texto={guardando ? 'Registrando…' : 'Registrar venta'} cargando={guardando} alPresionar={registrar} />
        </Tarjeta>
      </View>

      <Hoja visible={altaCliente} titulo="Nuevo cliente" alCerrar={() => setAltaCliente(false)}>
        <ScrollView style={{ maxHeight: 520 }} keyboardShouldPersistTaps="handled">
          <FormularioTercero
            config={Terceros.clientes}
            alCancelar={() => setAltaCliente(false)}
            alGuardar={(t: Tercero) => {
              setCliente({ id: t.id, texto: t.texto });
              setAltaCliente(false);
              avisar(`${t.nombre} se dio de alta.`);
            }}
          />
        </ScrollView>
      </Hoja>
    </Pantalla>
  );
}

