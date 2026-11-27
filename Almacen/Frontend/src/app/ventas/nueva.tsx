import { router } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ScrollView, View } from 'react-native';

import type { Almacen } from '@/models/almacen.model';
import type { Opcion } from '@/models/opcion.model';
import type { ProductoOpcion } from '@/models/producto-opcion.model';
import type { Tercero } from '@/models/tercero.model';
import type { Escaneo, SesionVenta } from '@/models/venta-inteligente.model';
import { almacenesService, ventasService } from '@/services';
import { BotonAgregarLinea, CampoPrecio, SelectorIva, TarjetaLinea, errorCantidad, nuevaClave, type LineaBase } from '@/components/captura';
import { CampoFecha } from '@/components/fechas';
import { SelectorTercero } from '@/components/selectores';
import { FormularioTercero, Terceros } from '@/components/terceros';
import { Aviso, Boton, Encabezado, Hoja, Interruptor, Pantalla, Selector, Tarjeta, Texto, Totales, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useVentaInteligente } from '@/hooks/use-venta-inteligente';
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

/**
 * Agrega los productos escaneados como en la web: suma 1 si ya hay línea del producto en el almacén por defecto;
 * si no, usa la primera línea vacía o agrega una. El precio lo sigue capturando el usuario.
 */
function agregarEscaneos(lineas: Linea[], escaneos: Escaneo[], almacenId: string): Linea[] {
  let resultado = lineas;
  for (const { producto } of escaneos) {
    const existente = resultado.find((l) => l.producto?.id === producto.id && l.almacenId === almacenId);
    if (existente) {
      const cantidad = String(redondear(leerNumero(existente.cantidad || '0') + 1));
      resultado = resultado.map((l) => (l === existente ? { ...l, cantidad } : l));
      continue;
    }
    const vacia = resultado.find((l) => !l.producto);
    const nueva: Linea = { ...(vacia ?? lineaVacia()), producto, almacenId, cantidad: '1' };
    resultado = vacia ? resultado.map((l) => (l === vacia ? nueva : l)) : [...resultado, nueva];
  }
  return resultado;
}

/** Captura de venta (Ventas/NuevaVenta.razor): cliente, fecha y por línea el almacén de salida y el precio. */
export default function NuevaVenta() {
  const { puede } = useSesion();
  const [cliente, setCliente] = useState<Opcion | null>(null);
  const [fecha, setFecha] = useState(Fechas.hoy());
  const [lineas, setLineas] = useState<Linea[]>([lineaVacia()]);
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const [altaCliente, setAltaCliente] = useState(false);
  const [almacenes, setAlmacenes] = useState<Almacen[]>([]);
  const [almacenModo, setAlmacenModo] = useState<string | null>(null);
  const [escaneados, setEscaneados] = useState(0);
  const puedeModo = puede(Permisos.Ventas.Crear);

  const alEscanear = useCallback((escaneos: Escaneo[], s: SesionVenta) => {
    setLineas((ls) => agregarEscaneos(ls, escaneos, s.almacenId));
    setEscaneados((n) => n + escaneos.length);
  }, []);
  const modo = useVentaInteligente(alEscanear);

  useEffect(() => {
    if (!puedeModo) return;
    almacenesService
      .listar()
      .then((as) => setAlmacenes(as.filter((a) => a.activo)))
      .catch(() => setAlmacenes([]));
  }, [puedeModo]);

  const cambiarModo = (encender: boolean) => {
    if (!encender) return void modo.desactivar();
    if (!almacenModo) return setError('Elige el almacén por defecto para el modo inteligente.');
    setError(null);
    setEscaneados(0);
    void modo.activar(almacenModo);
  };

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
      // El lector queda libre en cuanto se registra; los escaneos que no alcanzaron a llegar se descartan.
      await modo.desactivar();
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

        {puedeModo ? (
          <Tarjeta style={{ gap: Spacing.three }}>
            <Interruptor etiqueta="Modo inteligente" valor={modo.activo || modo.tomando} alCambiar={cambiarModo} />
            <Texto tipo="secundario">
              Pasa los productos por el lector NFC del almacén y se agregan solos con cantidad 1. El precio lo capturas tú.
            </Texto>
            <Selector<string>
              etiqueta="Almacén por defecto"
              valor={modo.sesion?.almacenId ?? almacenModo ?? undefined}
              opciones={almacenes.map((a) => ({ valor: a.id, texto: a.nombre, detalle: a.ubicacion }))}
              alCambiar={setAlmacenModo}
              placeholder="Elige el almacén de donde sale lo escaneado"
              deshabilitado={modo.activo || modo.tomando}
            />
            {modo.error ? <Aviso tipo="error" texto={modo.error} /> : null}
            {modo.activo && modo.sesion ? (
              <Aviso tipo="exito" texto={`Escuchando el lector NFC · ${modo.sesion.almacen} · ${escaneados} escaneados`} />
            ) : null}
          </Tarjeta>
        ) : null}

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

