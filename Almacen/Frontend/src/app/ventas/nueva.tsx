import { router } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState, Pressable, ScrollView, View } from 'react-native';

import type { Escaneo } from '@/models/escaneo.model';
import type { Opcion } from '@/models/opcion.model';
import type { ProductoOpcion } from '@/models/producto-opcion.model';
import type { Tercero } from '@/models/tercero.model';
import { almacenesService, iotService, ventasService } from '@/services';
import { BotonAgregarLinea, CampoPrecio, SelectorIva, TarjetaLinea, errorCantidad, nuevaClave, type LineaBase } from '@/components/captura';
import { CampoFecha } from '@/components/fechas';
import { SelectorTercero } from '@/components/selectores';
import { FormularioTercero, Terceros } from '@/components/terceros';
import { Aviso, Boton, Campo, Chip, Encabezado, Fila, Hoja, Icono, Interruptor, Pantalla, Selector, Tarjeta, Texto, Totales, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { useDatos } from '@/hooks/use-datos';
import { Fechas, Formato, importeLinea, leerNumero, redondear } from '@/lib/formato';
import { Permisos } from '@/lib/permisos';
import { useSesion } from '@/lib/sesion';

interface Linea extends LineaBase {
  almacenId: string | null;
  precio: string;
  tasa: number;
  /** UID de la etiqueta NFC de cada pieza que llegó del lector; la cantidad nunca es menor. */
  piezas: string[];
  /** UID que se está capturando a mano. */
  nuevoUid: string;
}

const lineaVacia = (): Linea => ({ clave: nuevaClave(), producto: null, almacenId: null, cantidad: '', precio: '', tasa: 0.16, piezas: [], nuevoUid: '' });

/** Productos por pieza llevan un UID por unidad; los de peso o volumen no. */
const llevaUid = (l: Linea) => l.producto?.permiteFracciones === false;
const faltanUid = (l: Linea) => {
  const n = leerNumero(l.cantidad);
  return llevaUid(l) && !Number.isNaN(n) ? Math.max(Math.ceil(n) - l.piezas.length, 0) : 0;
};
/** UID en hexadecimal, mayúsculas y sin separadores, como lo guarda la API; null si no es válido. */
const normalizarUid = (texto: string) => {
  const limpio = texto.replace(/[:\-\s]/g, '').toUpperCase();
  return /^[0-9A-F]{1,32}$/.test(limpio) ? limpio : null;
};

/** Alertas del lector: las amarillas (duplicados) se quitan solas; las rojas se quedan hasta cerrarlas. */
interface AlertaLector {
  id: number;
  texto: string;
  tipo: 'alerta' | 'error';
}
let siguienteAlerta = 1;
const DURACION_ALERTA_MS = 4000;
const sumar = (cantidad: string, delta: number) => {
  const n = leerNumero(cantidad);
  return String(Math.max((Number.isNaN(n) ? 0 : n) + delta, 0));
};

/** El lector NFC se consulta cada segundo; el candado se renueva cada 15 s. */
const INTERVALO_LECTOR_MS = 1000;
const INTERVALO_LATIDO_MS = 15000;

/**
 * Cada escaneo es una pieza física (UID). Los rechazados solo se avisan. Si ya hay línea del producto en el almacén
 * por defecto suma la pieza; si no, usa una línea vacía o agrega una. Igual que Ventas/NuevaVenta.razor.
 */
function aplicarEscaneos(lineas: Linea[], escaneos: Escaneo[], almacenId: string) {
  let resultado = lineas;
  const avisos: AlertaLector[] = [];
  let agregados = 0;
  for (const e of escaneos) {
    const producto = e.producto;
    if (e.rechazo || !producto) {
      avisos.push({ id: siguienteAlerta++, tipo: 'error', texto: e.rechazo ?? `No se reconoció la pieza ${e.uid}.` });
      continue;
    }
    if (resultado.some((l) => l.piezas.includes(e.uid))) {
      avisos.push({ id: siguienteAlerta++, tipo: 'alerta', texto: `La pieza ${e.uid} ya está en esta venta.` });
      continue;
    }
    agregados++;
    const existente = resultado.find((l) => l.producto?.id === producto.id && l.almacenId === almacenId);
    if (existente) {
      resultado = resultado.map((l) => (l === existente ? { ...l, cantidad: sumar(l.cantidad, 1), piezas: [...l.piezas, e.uid] } : l));
      continue;
    }
    const nueva: Linea = { ...(resultado.find((l) => !l.producto) ?? lineaVacia()), producto, almacenId, cantidad: '1', piezas: [e.uid] };
    resultado = resultado.some((l) => l.clave === nueva.clave) ? resultado.map((l) => (l.clave === nueva.clave ? nueva : l)) : [...resultado, nueva];
  }
  return { lineas: resultado, avisos, agregados };
}

const disponible = (l: Linea) => l.producto?.existencias.find((e) => e.almacenId === l.almacenId)?.cantidad ?? 0;
const importe = (l: Linea) => importeLinea(leerNumero(l.cantidad), leerNumero(l.precio), l.tasa);

/**
 * Captura de venta (Ventas/NuevaVenta.razor): cliente, fecha y por línea el almacén de salida y el precio.
 * Con el modo inteligente, cada pieza que pasa por el lector NFC se agrega sola y su UID se registra con la venta.
 */
export default function NuevaVenta() {
  const { puede } = useSesion();
  const [cliente, setCliente] = useState<Opcion | null>(null);
  const [fecha, setFecha] = useState(Fechas.hoy());
  const [lineas, setLineas] = useState<Linea[]>([lineaVacia()]);
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const [altaCliente, setAltaCliente] = useState(false);
  const c = useColores();

  // ---- Modo inteligente: el lector NFC del embebido agrega piezas mientras este usuario tiene el candado.
  const almacenes = useDatos(() => almacenesService.listar());
  const [almacenElegido, setAlmacenModo] = useState<string | undefined>();
  const [modo, setModo] = useState(false);
  const [cambiandoModo, setCambiandoModo] = useState(false);
  const [avisoModo, setAvisoModo] = useState<string | null>(null);
  const [rechazos, setRechazos] = useState<AlertaLector[]>([]);
  const mostrarAlertas = useCallback((nuevas: AlertaLector[]) => {
    if (!nuevas.length) return;
    setRechazos((ant) => [...[...nuevas].reverse(), ...ant].slice(0, 6));
    for (const a of nuevas)
      if (a.tipo === 'alerta') setTimeout(() => setRechazos((ant) => ant.filter((x) => x.id !== a.id)), DURACION_ALERTA_MS);
  }, []);
  const [escaneados, setEscaneados] = useState(0);
  const lineasRef = useRef(lineas);
  useEffect(() => {
    lineasRef.current = lineas;
  }, [lineas]);
  const modoRef = useRef(false);

  const opcionesAlmacen = (almacenes.datos ?? []).filter((a) => a.activo).map((a) => ({ valor: a.id, texto: a.nombre }));
  // Con un solo almacén se propone ese.
  const almacenModo = almacenElegido ?? (opcionesAlmacen.length === 1 ? opcionesAlmacen[0].valor : undefined);

  useEffect(() => {
    iotService
      .sesionVenta()
      .then((s) => {
        if (s && !s.esMia) setAvisoModo(`El modo inteligente lo está usando ${s.usuario}.`);
      })
      .catch(() => {});
  }, []);

  const desactivarModo = useCallback(async () => {
    if (!modoRef.current) return;
    modoRef.current = false;
    setModo(false);
    try {
      await iotService.liberarModo();
    } catch {
      // Si no se pudo avisar, el candado vence solo al dejar de recibir latidos.
    }
  }, []);

  // Al salir de la pantalla o pasar la app a segundo plano se libera el lector.
  useEffect(() => () => void desactivarModo(), [desactivarModo]);
  useEffect(() => {
    const sub = AppState.addEventListener('change', (estado) => {
      if (estado === 'background') void desactivarModo();
    });
    return () => sub.remove();
  }, [desactivarModo]);

  useEffect(() => {
    if (!modo || !almacenModo) return;
    let ocupado = false;
    let ultimoLatido = Date.now();
    const reloj = setInterval(async () => {
      if (ocupado || !modoRef.current) return;
      ocupado = true;
      try {
        if (Date.now() - ultimoLatido >= INTERVALO_LATIDO_MS) {
          ultimoLatido = Date.now();
          try {
            await iotService.latido();
          } catch (e) {
            modoRef.current = false;
            setModo(false);
            setAvisoModo(mensajeDe(e));
            return;
          }
        }
        const escaneos = await iotService.escaneos();
        if (escaneos.length && modoRef.current) {
          const r = aplicarEscaneos(lineasRef.current, escaneos, almacenModo);
          setLineas(r.lineas);
          setEscaneados((n) => n + r.agregados);
          mostrarAlertas(r.avisos);
        }
      } catch {
        // Falla de red pasajera: se reintenta en el siguiente segundo.
      } finally {
        ocupado = false;
      }
    }, INTERVALO_LECTOR_MS);
    return () => clearInterval(reloj);
  }, [modo, almacenModo, mostrarAlertas]);

  const cambiarModo = async (activar: boolean) => {
    setAvisoModo(null);
    if (!activar) return desactivarModo();
    if (!almacenModo) return setAvisoModo('Elige el almacén por defecto antes de activar el modo inteligente.');
    setCambiandoModo(true);
    try {
      await iotService.tomarModo(almacenModo);
      modoRef.current = true;
      setModo(true);
      setRechazos([]);
    } catch (e) {
      setAvisoModo(mensajeDe(e));
    } finally {
      setCambiandoModo(false);
    }
  };

  const agregarUid = (l: Linea) => {
    const uid = normalizarUid(l.nuevoUid);
    if (!uid) return mostrarAlertas([{ id: siguienteAlerta++, tipo: 'alerta', texto: 'El UID debe ser hexadecimal, por ejemplo 53:2B:84:E5.' }]);
    if (lineas.some((x) => x.piezas.includes(uid)))
      return mostrarAlertas([{ id: siguienteAlerta++, tipo: 'alerta', texto: `La pieza ${uid} ya está en esta venta.` }]);
    cambiar(l.clave, { piezas: [...l.piezas, uid], nuevoUid: '' });
  };

  const quitarPieza = (clave: number, uid: string) =>
    setLineas((ls) => ls.map((l) => (l.clave === clave && l.piezas.includes(uid) ? { ...l, piezas: l.piezas.filter((p) => p !== uid), cantidad: sumar(l.cantidad, -1) } : l)));

  const cambiar = (clave: number, cambios: Partial<Linea>) => setLineas((ls) => ls.map((l) => (l.clave === clave ? { ...l, ...cambios } : l)));
  const total = lineas.map(importe).reduce((t, i) => ({ subtotal: t.subtotal + i.subtotal, iva: t.iva + i.iva }), { subtotal: 0, iva: 0 });

  const registrar = async () => {
    setError(null);
    if (!cliente || !Fechas.valida(fecha)) return setError('Elige el cliente y la fecha.');
    if (lineas.some((l) => !l.producto || !l.almacenId || !(leerNumero(l.cantidad) > 0))) return setError('Cada línea necesita producto, almacén y cantidad mayor que cero.');
    if (lineas.some((l) => leerNumero(l.cantidad) < l.piezas.length))
      return setError('La cantidad de una línea no puede ser menor que sus piezas escaneadas. Quita las piezas que no salen.');
    const incompleta = lineas.find((l) => faltanUid(l) > 0);
    if (incompleta) return setError(`A ${incompleta.producto!.referencia} le faltan ${faltanUid(incompleta)} UID: escanea las piezas o captura su UID.`);
    if (lineas.some((l) => leerNumero(l.cantidad) > disponible(l))) return setError('Hay líneas con más cantidad de la que hay en el almacén.');
    if (lineas.some((l) => Number.isNaN(leerNumero(l.precio)) || leerNumero(l.precio) < 0)) return setError('Captura el precio unitario de cada línea.');

    setGuardando(true);
    try {
      const r = await ventasService.registrar({
        clienteId: cliente.id,
        fecha,
        lineas: lineas.map((l) => ({
          productoId: l.producto!.id,
          almacenId: l.almacenId!,
          cantidad: leerNumero(l.cantidad),
          precioUnitario: leerNumero(l.precio),
          tasaIva: l.tasa,
          piezas: l.piezas.length ? l.piezas : undefined,
        })),
      });
      await desactivarModo();
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

        <Tarjeta style={{ gap: Spacing.two }}>
          <Interruptor etiqueta="Modo inteligente (lector NFC)" valor={modo} alCambiar={(v) => !cambiandoModo && cambiarModo(v)} />
          <Selector<string>
            etiqueta="Almacén por defecto"
            valor={almacenModo}
            opciones={opcionesAlmacen}
            alCambiar={setAlmacenModo}
            deshabilitado={modo || cambiandoModo}
          />
          {modo ? (
            <Fila style={{ flexWrap: 'wrap' }}>
              <Chip texto={`Escuchando el lector NFC · ${escaneados} escaneadas`} color="success" />
            </Fila>
          ) : (
            <Texto tipo="pequeno">
              Pasa la etiqueta NFC de cada pieza por el lector y se agrega sola (una pieza del almacén por defecto). Una pieza ya vendida se rechaza.
            </Texto>
          )}
          {avisoModo ? <Aviso tipo="alerta" texto={avisoModo} /> : null}
          {rechazos.map((a) => (
            <View key={a.id} style={{ flexDirection: 'row', alignItems: 'center', gap: 4 }}>
              <View style={{ flex: 1 }}>
                <Aviso tipo={a.tipo} texto={a.texto} />
              </View>
              {a.tipo === 'error' ? (
                <Pressable accessibilityRole="button" accessibilityLabel="Cerrar alerta" onPress={() => setRechazos((ant) => ant.filter((x) => x.id !== a.id))} style={{ padding: 6 }}>
                  <Icono nombre="close" tamano={18} />
                </Pressable>
              ) : null}
            </View>
          ))}
        </Tarjeta>

        {lineas.map((l, i) => (
          <TarjetaLinea
            key={l.clave}
            numero={i + 1}
            producto={l.producto}
            alElegirProducto={(p: ProductoOpcion) =>
              // Propone el almacén con más existencia, como la web.
              // Las piezas escaneadas eran de otro producto.
              cambiar(l.clave, { producto: p, piezas: [], almacenId: [...p.existencias].sort((a, b) => b.cantidad - a.cantidad)[0]?.almacenId ?? null })
            }
            debajoProducto={
              l.producto ? (
                <View style={{ gap: Spacing.two }}>
                <Selector<string>
                  etiqueta="Sale de"
                  valor={l.almacenId ?? undefined}
                  opciones={l.producto.existencias.map((e) => ({ valor: e.almacenId, texto: `${e.almacen} (${Formato.cantidad(e.cantidad)})` }))}
                  alCambiar={(a) => cambiar(l.clave, { almacenId: a })}
                  placeholder="Sin existencias en ningún almacén"
                  deshabilitado={!l.producto.existencias.length}
                />
                {llevaUid(l) ? (
                  <View style={{ gap: 4 }}>
                    <Texto tipo="pequeno" color={faltanUid(l) > 0 ? c.warning : undefined}>
                      UID de las piezas ({l.piezas.length} de {Formato.cantidad(leerNumero(l.cantidad) || 0)}){l.piezas.length ? ', toca una para quitarla' : ''}:
                    </Texto>
                    <Fila style={{ flexWrap: 'wrap' }} gap={6}>
                      {l.piezas.map((uid) => (
                        <Pressable
                          key={uid}
                          accessibilityRole="button"
                          accessibilityLabel={`Quitar pieza ${uid}`}
                          onPress={() => quitarPieza(l.clave, uid)}
                          style={{ flexDirection: 'row', alignItems: 'center', gap: 4, borderWidth: 1, borderColor: c.line, borderRadius: 12, paddingHorizontal: 8, paddingVertical: 2 }}>
                          <Texto tipo="pequeno">{uid}</Texto>
                          <Icono nombre="close" tamano={14} />
                        </Pressable>
                      ))}
                    </Fila>
                    {faltanUid(l) > 0 ? (
                      <Fila style={{ alignItems: 'flex-end' }}>
                        <Campo
                          etiqueta={`Faltan ${faltanUid(l)} UID`}
                          value={l.nuevoUid}
                          onChangeText={(t) => cambiar(l.clave, { nuevoUid: t })}
                          onSubmitEditing={() => agregarUid(l)}
                          autoCapitalize="characters"
                          autoCorrect={false}
                          placeholder="UID de la etiqueta"
                          style={{ flex: 1 }}
                        />
                        <Boton texto="Agregar" icono="add" variante="contorno" alPresionar={() => agregarUid(l)} />
                      </Fila>
                    ) : null}
                  </View>
                ) : null}
                </View>
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

