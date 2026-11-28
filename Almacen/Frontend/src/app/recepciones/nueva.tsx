import { router } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState, Pressable, View } from 'react-native';

import type { Escaneo } from '@/models/escaneo.model';
import type { Opcion } from '@/models/opcion.model';
import { almacenesService, iotService, recepcionesService } from '@/services';
import { BotonAgregarLinea, CampoPrecio, SelectorIva, TarjetaLinea, errorCantidad, nuevaClave, type LineaBase } from '@/components/captura';
import { CampoFecha } from '@/components/fechas';
import { SelectorTercero } from '@/components/selectores';
import { Aviso, Boton, Campo, Chip, Encabezado, Fila, Hoja, Interruptor, Pantalla, Selector, Tarjeta, Texto, Totales, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { useMiAlmacen } from '@/hooks/use-mi-almacen';
import { useDatos } from '@/hooks/use-datos';
import { Fechas, Formato, importeLinea, leerNumero, redondear } from '@/lib/formato';

interface Linea extends LineaBase {
  costo: string;
  tasa: number;
}

const lineaVacia = (): Linea => ({ clave: nuevaClave(), producto: null, cantidad: '', costo: '', tasa: 0.16 });
const importe = (l: Linea) => importeLinea(leerNumero(l.cantidad), leerNumero(l.costo), l.tasa);

/** Tamaños de paquete más comunes; si no es ninguno se escribe el número. */
const TAMANOS_PAQUETE = [10, 20, 25, 50, 100];
const INTERVALO_LECTOR_MS = 1000;
const INTERVALO_LATIDO_MS = 15000;
const DURACION_ALERTA_MS = 4000;
let siguienteAlerta = 1;

/**
 * Captura de recepción (Recepciones/NuevaRecepcion.razor). Con la recepción inteligente, cada paquete que pasa por el
 * lector NFC (su código proveedor) pregunta de cuántas piezas es y se suma a la línea de su producto.
 */
export default function NuevaRecepcion() {
  const c = useColores();
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

  // ---- Recepción inteligente
  const [modo, setModo] = useState(false);
  const [cambiandoModo, setCambiandoModo] = useState(false);
  const [avisoModo, setAvisoModo] = useState<string | null>(null);
  const [alertas, setAlertas] = useState<{ id: number; texto: string }[]>([]);
  const [cola, setCola] = useState<Escaneo[]>([]);
  const [tamano, setTamano] = useState<number | null>(null);
  const [otro, setOtro] = useState('');
  const [paquetes, setPaquetes] = useState(0);
  const modoRef = useRef(false);
  const paquete = cola[0];
  const tamanoElegido = leerNumero(otro) > 0 ? Math.floor(leerNumero(otro)) : tamano;

  const alerta = useCallback((texto: string) => {
    const id = siguienteAlerta++;
    setAlertas((a) => [{ id, texto }, ...a].slice(0, 5));
    setTimeout(() => setAlertas((a) => a.filter((x) => x.id !== id)), DURACION_ALERTA_MS);
  }, []);

  const desactivarModo = useCallback(async () => {
    if (!modoRef.current) return;
    modoRef.current = false;
    setModo(false);
    try {
      await iotService.liberarModo();
    } catch {
      // El candado vence solo al dejar de recibir latidos.
    }
  }, []);

  useEffect(() => () => void desactivarModo(), [desactivarModo]);
  useEffect(() => {
    const sub = AppState.addEventListener('change', (estado) => {
      if (estado === 'background') void desactivarModo();
    });
    return () => sub.remove();
  }, [desactivarModo]);

  // Cada segundo recoge los paquetes nuevos; cada 15 s renueva el candado (también con el modal abierto).
  useEffect(() => {
    if (!modo) return;
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
        if (!escaneos.length || !modoRef.current) return;
        for (const e of escaneos) if (e.rechazo || !e.producto) alerta(e.rechazo ?? `No se reconoció el paquete ${e.uid}.`);
        const buenos = escaneos.filter((e) => !e.rechazo && e.producto);
        if (buenos.length) setCola((anterior) => [...anterior, ...buenos]);
      } catch {
        // Falla de red pasajera: se reintenta en el siguiente segundo.
      } finally {
        ocupado = false;
      }
    }, INTERVALO_LECTOR_MS);
    return () => clearInterval(reloj);
  }, [modo, alerta]);

  const cambiarModo = async (activar: boolean) => {
    setAvisoModo(null);
    if (!activar) return desactivarModo();
    if (!almacenId) return setAvisoModo('Elige el almacén de entrada antes de activar la recepción inteligente.');
    setCambiandoModo(true);
    try {
      await iotService.tomarModoRecepcion(almacenId);
      modoRef.current = true;
      setModo(true);
    } catch (e) {
      setAvisoModo(mensajeDe(e));
    } finally {
      setCambiandoModo(false);
    }
  };

  const cerrarPaquete = () => {
    setCola((c) => c.slice(1));
    setTamano(null);
    setOtro('');
  };

  const confirmarPaquete = () => {
    const producto = paquete?.producto;
    if (!producto || !tamanoElegido) return;
    setLineas((ls) => {
      const existente = ls.find((l) => l.producto?.id === producto.id) ?? ls.find((l) => !l.producto);
      const suma = (l: Linea) => String((Number.isNaN(leerNumero(l.cantidad)) ? 0 : leerNumero(l.cantidad)) + tamanoElegido);
      return existente
        ? ls.map((l) => (l.clave === existente.clave ? { ...l, producto, cantidad: suma(l) } : l))
        : [...ls, { ...lineaVacia(), producto, cantidad: String(tamanoElegido) }];
    });
    setPaquetes((n) => n + 1);
    cerrarPaquete();
  };

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
      await desactivarModo();
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
            deshabilitado={!!mio || modo}
          />
          {almacenes.error ? <Aviso tipo="error" texto={almacenes.error} /> : null}
          <CampoFecha etiqueta="Fecha" valor={fecha} alCambiar={setFecha} />
        </Tarjeta>

        <Tarjeta style={{ gap: Spacing.two }}>
          <Interruptor etiqueta="Recepción inteligente (lector NFC)" valor={modo} alCambiar={(v) => !cambiandoModo && cambiarModo(v)} />
          {modo ? (
            <Fila style={{ flexWrap: 'wrap' }}>
              <Chip texto={`Escuchando el lector NFC · ${paquetes} paquetes`} color="success" />
            </Fila>
          ) : (
            <Texto tipo="pequeno">Pasa la etiqueta de cada paquete por el lector (su código proveedor) y di de cuántas piezas es.</Texto>
          )}
          {avisoModo ? <Aviso tipo="alerta" texto={avisoModo} /> : null}
          {alertas.map((a) => (
            <Aviso key={a.id} tipo="alerta" texto={a.texto} />
          ))}
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

      <Hoja visible={!!paquete} titulo="¿De cuántas piezas es el paquete?" alCerrar={cerrarPaquete}>
        {paquete?.producto ? (
          <View style={{ gap: Spacing.three }}>
            <Texto tipo="fuerte">
              {paquete.producto.referencia} <Texto tipo="secundario">{paquete.producto.descripcion}</Texto>
            </Texto>
            <Fila style={{ flexWrap: 'wrap' }} gap={8}>
              {TAMANOS_PAQUETE.map((t) => (
                <Pressable
                  key={t}
                  accessibilityRole="button"
                  accessibilityState={{ selected: tamano === t && !otro }}
                  onPress={() => {
                    setTamano(t);
                    setOtro('');
                  }}
                  style={{ borderWidth: 1, borderColor: tamano === t && !otro ? c.primary : c.line, backgroundColor: tamano === t && !otro ? c.primary + '1A' : c.surface, borderRadius: 999, paddingHorizontal: 16, paddingVertical: 8 }}>
                  <Texto tipo={tamano === t && !otro ? 'fuerte' : 'cuerpo'}>{t}</Texto>
                </Pressable>
              ))}
            </Fila>
            <Campo etiqueta="Otro tamaño" value={otro} onChangeText={setOtro} keyboardType="number-pad" ayuda="Si el paquete no es de ninguno de los de arriba" />
            {cola.length > 1 ? <Texto tipo="pequeno">Faltan {cola.length - 1} paquetes más por confirmar.</Texto> : null}
            <Fila style={{ justifyContent: 'flex-end' }}>
              <Boton texto="Descartar" variante="texto" alPresionar={cerrarPaquete} />
              <Boton texto="Agregar" icono="add" alPresionar={confirmarPaquete} deshabilitado={!tamanoElegido} />
            </Fila>
          </View>
        ) : null}
      </Hoja>
    </Pantalla>
  );
}
