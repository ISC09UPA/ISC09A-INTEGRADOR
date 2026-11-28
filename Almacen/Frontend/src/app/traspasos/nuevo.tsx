import { router } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import { almacenesService, traspasosService } from '@/services';
import { BotonAgregarLinea, TarjetaLinea, errorCantidad, nuevaClave, type LineaBase } from '@/components/captura';
import { CampoFecha } from '@/components/fechas';
import { Aviso, Boton, Encabezado, Pantalla, Selector, Tarjeta, Texto, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useMiAlmacen } from '@/hooks/use-mi-almacen';
import { useDatos } from '@/hooks/use-datos';
import { Fechas, Formato, leerNumero } from '@/lib/formato';

const lineaVacia = (): LineaBase => ({ clave: nuevaClave(), producto: null, cantidad: '' });

/** Captura de traspaso entre almacenes (Traspasos/NuevoTraspaso.razor). */
export default function NuevoTraspaso() {
  const [origenElegido, setOrigen] = useState<string | undefined>();
  // Se traspasa desde el almacén propio.
  const mio = useMiAlmacen();
  const origen = mio ?? origenElegido;
  const [destino, setDestino] = useState<string | undefined>();
  const [fecha, setFecha] = useState(Fechas.hoy());
  const [lineas, setLineas] = useState<LineaBase[]>([lineaVacia()]);
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const almacenes = useDatos(() => almacenesService.listar());
  const opciones = (almacenes.datos ?? []).map((a) => ({ valor: a.id, texto: a.nombre, detalle: a.ubicacion }));

  const disponible = (l: LineaBase) => l.producto?.existencias.find((e) => e.almacenId === origen)?.cantidad ?? 0;
  const cambiar = (clave: number, cambios: Partial<LineaBase>) => setLineas((ls) => ls.map((l) => (l.clave === clave ? { ...l, ...cambios } : l)));

  const registrar = async () => {
    setError(null);
    if (!origen || !destino || !Fechas.valida(fecha)) return setError('Elige origen, destino y fecha.');
    if (origen === destino) return setError('El origen y el destino deben ser distintos.');
    if (lineas.some((l) => !l.producto || !(leerNumero(l.cantidad) > 0))) return setError('Cada línea necesita producto y cantidad mayor que cero.');

    setGuardando(true);
    try {
      const r = await traspasosService.registrar({
        almacenOrigenId: origen,
        almacenDestinoId: destino,
        fecha,
        lineas: lineas.map((l) => ({ productoId: l.producto!.id, cantidad: leerNumero(l.cantidad) })),
      });
      avisar(`Traspaso ${Formato.folio('TRA', r.folio)} registrado.`);
      router.replace(`/traspasos/${r.id}`);
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setGuardando(false);
    }
  };

  return (
    <Pantalla>
      <Encabezado titulo="Nuevo traspaso" descripcion="La mercancía sale del almacén de origen y entra al de destino con el mismo costo." />
      <View style={{ gap: Spacing.three }}>
        <Tarjeta style={{ gap: Spacing.three }}>
          <Selector<string> etiqueta="Origen" valor={origen} opciones={opciones} alCambiar={setOrigen} deshabilitado={!!mio} />
          <Selector<string> etiqueta="Destino" valor={destino} opciones={opciones.filter((o) => o.valor !== origen)} alCambiar={setDestino} />
          {almacenes.error ? <Aviso tipo="error" texto={almacenes.error} /> : null}
          <CampoFecha etiqueta="Fecha" valor={fecha} alCambiar={setFecha} />
        </Tarjeta>

        {lineas.map((l, i) => (
          <TarjetaLinea
            key={l.clave}
            numero={i + 1}
            producto={l.producto}
            alElegirProducto={(p) => cambiar(l.clave, { producto: p })}
            debajoProducto={l.producto && origen ? <Texto tipo="pequeno">Disponible en origen: {Formato.cantidad(disponible(l))} {l.producto.unidad}</Texto> : null}
            cantidad={l.cantidad}
            alCambiarCantidad={(t) => cambiar(l.clave, { cantidad: t })}
            errorCantidad={errorCantidad(l.cantidad, l.producto, l.producto && origen ? disponible(l) : undefined)}
            puedeQuitar={lineas.length > 1}
            alQuitar={() => setLineas((ls) => ls.filter((x) => x.clave !== l.clave))}
          />
        ))}

        <BotonAgregarLinea alPresionar={() => setLineas((ls) => [...ls, lineaVacia()])} />

        {error ? <Aviso tipo="error" texto={error} /> : null}
        <Boton texto={guardando ? 'Registrando…' : 'Registrar traspaso'} cargando={guardando} alPresionar={registrar} />
      </View>
    </Pantalla>
  );
}
