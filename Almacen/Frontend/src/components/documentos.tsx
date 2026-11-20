import { router, Stack } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import type { DocumentoResumen } from '@/models/documento-resumen.model';
import { archivosService, recepcionesService, traspasosService, ventasService } from '@/services';
import type { DocumentosService } from '@/services/documentos.service';
import type { EstadoDocumento, TipoDocumento } from '@/types/enums';
import { RangoFechas, type Rango } from '@/components/fechas';
import { ListaPantalla } from '@/components/lista';
import {
  Aviso,
  Boton,
  Busqueda,
  Cargando,
  Chip,
  Dato,
  Datos,
  DialogoTexto,
  Fila,
  Pantalla,
  Selector,
  Separador,
  Tarjeta,
  Texto,
  Totales,
  avisar,
  mensajeDe,
} from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useDatos, usePaginado } from '@/hooks/use-datos';
import { Formato, colorEstadoDocumento } from '@/lib/formato';
import { useSesion } from '@/lib/sesion';
import { mostrarArchivo } from '@/lib/visor';

export interface ConfigDocumento {
  nombre: string; // "Venta"
  plural: string; // "Ventas"
  prefijo: string; // "VTA"
  tipo: TipoDocumento;
  servicio: DocumentosService;
  rutaApp: string; // "/ventas"
  permisoCrear: string;
  permisoCancelar?: string;
  textoNuevo: string;
  rutaNuevo: string;
  etiquetaTercero: string;
  etiquetaAlmacen?: string;
  etiquetaPrecio?: string;
  almacenPorLinea?: boolean;
  mostrarImportes?: boolean;
  descripcion: string;
}

/** Lista de recepciones, ventas o traspasos con búsqueda, periodo y estado (ListaDocumentos.razor). */
export function ListaDocumentos({ config }: { config: ConfigDocumento }) {
  const { puede } = useSesion();
  const [busqueda, setBusqueda] = useState('');
  const [rango, setRango] = useState<Rango | null>(null);
  const [estado, setEstado] = useState<EstadoDocumento | ''>('');
  const [filtros, setFiltros] = useState(false);
  const mostrarImportes = config.mostrarImportes ?? true;

  const lista = usePaginado<DocumentoResumen>(
    (pagina) =>
      config.servicio.listar({ busqueda, estado: estado || undefined, desde: rango?.desde, hasta: rango?.hasta, pagina }),
    [busqueda, rango, estado],
  );

  return (
    <ListaPantalla
      {...lista}
      alRecargar={lista.recargar}
      alLlegarAlFinal={lista.cargarMas}
      clave={(d) => d.id}
      vacio="No hay documentos que coincidan."
      encabezado={
        <View style={{ gap: Spacing.two }}>
          <Texto tipo="secundario">{config.descripcion}</Texto>
          <Fila style={{ flexWrap: 'wrap' }}>
            {puede(config.permisoCrear) ? <Boton texto={config.textoNuevo} icono="add" alPresionar={() => router.push(config.rutaNuevo as never)} /> : null}
            <Boton texto={filtros ? 'Ocultar filtros' : 'Filtros'} icono="filter-list" variante="contorno" alPresionar={() => setFiltros(!filtros)} />
          </Fila>
          <Busqueda valor={busqueda} alCambiar={setBusqueda} placeholder={`Folio o ${config.etiquetaTercero.toLowerCase()}`} />
          {filtros ? (
            <Tarjeta style={{ gap: Spacing.two }}>
              <RangoFechas valor={rango} alCambiar={setRango} opcional />
              <Selector<EstadoDocumento | ''>
                etiqueta="Estado"
                valor={estado}
                alCambiar={setEstado}
                opciones={[
                  { valor: '', texto: 'Todos' },
                  { valor: 'Confirmado', texto: 'Confirmados' },
                  { valor: 'Cancelado', texto: 'Cancelados' },
                ]}
              />
            </Tarjeta>
          ) : null}
        </View>
      }
      fila={(d) => (
        <Tarjeta alPresionar={() => router.push(`${config.rutaApp}/${d.id}` as never)} style={{ gap: 6 }}>
          <Fila style={{ justifyContent: 'space-between' }}>
            <Texto tipo="fuerte">{Formato.folio(config.prefijo, d.folio)}</Texto>
            <Chip texto={d.estado} color={colorEstadoDocumento(d.estado)} />
          </Fila>
          <Texto numberOfLines={1}>{d.tercero}</Texto>
          <Fila style={{ justifyContent: 'space-between' }}>
            <Texto tipo="pequeno">
              {Formato.fecha(d.fecha)} · {d.almacen} · {d.lineas} {d.lineas === 1 ? 'línea' : 'líneas'}
            </Texto>
            {mostrarImportes ? <Texto tipo="fuerte">{Formato.dinero(d.total)}</Texto> : null}
          </Fila>
        </Tarjeta>
      )}
    />
  );
}

/** Detalle de un documento con PDF y cancelación (DetalleDocumento.razor). */
export function DetalleDocumento({ config, id }: { config: ConfigDocumento; id: string }) {
  const { puede } = useSesion();
  const { datos: d, error, cargando, recargar } = useDatos(() => config.servicio.obtener(id), [id]);
  const [cancelando, setCancelando] = useState(false);
  const [abriendo, setAbriendo] = useState(false);
  const mostrarImportes = config.mostrarImportes ?? true;
  const titulo = d ? `${config.nombre} ${Formato.folio(config.prefijo, d.folio)}` : config.nombre;

  const verPdf = async () => {
    if (!d) return;
    setAbriendo(true);
    try {
      await mostrarArchivo(await archivosService.comprobante(config.tipo, d.id, `${Formato.folio(config.prefijo, d.folio)}.pdf`));
    } catch (e) {
      avisar('No se pudo abrir el PDF', mensajeDe(e));
    } finally {
      setAbriendo(false);
    }
  };

  return (
    <Pantalla alRecargar={recargar} recargando={cargando && !!d}>
      <Stack.Screen options={{ title: titulo }} />
      {error ? <Aviso tipo="error" texto={error} /> : null}
      {!d && cargando ? <Cargando /> : null}
      {d ? (
        <View style={{ gap: Spacing.three }}>
          <Fila style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
            <Texto tipo="titulo">{Formato.folio(config.prefijo, d.folio)}</Texto>
            <Chip texto={d.estado} color={colorEstadoDocumento(d.estado)} />
          </Fila>
          <Fila style={{ flexWrap: 'wrap' }}>
            <Boton texto="Ver PDF" icono="picture-as-pdf" variante="contorno" cargando={abriendo} alPresionar={verPdf} />
            {config.permisoCancelar && puede(config.permisoCancelar) && d.estado === 'Confirmado' ? (
              <Boton texto={`Cancelar ${config.nombre.toLowerCase()}`} icono="cancel" variante="peligro" alPresionar={() => setCancelando(true)} />
            ) : null}
          </Fila>

          {d.cancelacion ? (
            <Aviso tipo="error" texto={`Cancelado por ${d.cancelacion.por} el ${Formato.fechaHora(d.cancelacion.en)}. Motivo: ${d.cancelacion.motivo}`} />
          ) : null}

          {d.tercero ? (
            <Tarjeta style={{ gap: 2 }}>
              <Texto tipo="etiqueta">{config.etiquetaTercero}</Texto>
              <Texto tipo="subtitulo">{d.tercero}</Texto>
              {d.terceroRfc ? <Texto>RFC {d.terceroRfc}</Texto> : null}
              {d.terceroCorreo ? <Texto tipo="secundario">{d.terceroCorreo}</Texto> : null}
            </Tarjeta>
          ) : null}

          <Tarjeta>
            <Datos>
              <Dato etiqueta="Fecha" valor={Formato.fecha(d.fecha)} />
              <Dato etiqueta={d.almacenDestino ? 'Origen' : 'Almacén'} valor={d.almacen} />
              {d.almacenDestino ? <Dato etiqueta="Destino" valor={d.almacenDestino} /> : null}
              <Dato etiqueta="Registró" valor={d.creadoPor} detalle={Formato.fechaHora(d.creadoEn)} />
            </Datos>
          </Tarjeta>

          <Tarjeta style={{ gap: Spacing.two }}>
            <Texto tipo="subtitulo">Productos</Texto>
            {d.lineas.map((l, i) => (
              <View key={`${l.productoId}-${i}`} style={{ gap: 2 }}>
                {i > 0 ? <Separador /> : null}
                <Texto tipo="fuerte">
                  {l.referencia} <Texto tipo="secundario">{l.descripcion}</Texto>
                </Texto>
                {config.almacenPorLinea && l.almacen ? <Texto tipo="pequeno">Sale de {l.almacen}</Texto> : null}
                <Fila style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
                  <Texto tipo="secundario">
                    {Formato.cantidad(l.cantidad)} {l.unidad}
                    {mostrarImportes ? ` × ${Formato.costo(l.precioUnitario)} · IVA ${Formato.porcentaje(l.tasaIva)}` : ''}
                  </Texto>
                  {mostrarImportes ? <Texto tipo="fuerte">{Formato.dinero(l.subtotal)}</Texto> : null}
                </Fila>
              </View>
            ))}
            {mostrarImportes ? (
              <>
                <Separador />
                <Totales subtotal={Formato.dinero(d.subtotal)} iva={Formato.dinero(d.iva)} total={Formato.dinero(d.total)} />
              </>
            ) : null}
          </Tarjeta>
        </View>
      ) : null}

      <DialogoTexto
        visible={cancelando}
        titulo={`Cancelar ${config.nombre.toLowerCase()}`}
        etiqueta="Motivo de la cancelación"
        textoConfirmar="Cancelar documento"
        peligro
        alCerrar={() => setCancelando(false)}
        alConfirmar={async (motivo) => {
          await config.servicio.cancelar(id, motivo);
          await recargar();
        }}
      />
    </Pantalla>
  );
}

export const Documentos = {
  ventas: {
    nombre: 'Venta',
    plural: 'Ventas',
    prefijo: 'VTA',
    tipo: 'Venta',
    servicio: ventasService,
    rutaApp: '/ventas',
    permisoCrear: 'ventas.crear',
    permisoCancelar: 'ventas.cancelar',
    textoNuevo: 'Nueva venta',
    rutaNuevo: '/ventas/nueva',
    etiquetaTercero: 'Cliente',
    etiquetaAlmacen: 'Almacén',
    almacenPorLinea: true,
    descripcion: 'Salidas de mercancía a clientes. Descuentan existencias del almacén elegido en cada línea y registran el ingreso.',
  },
  recepciones: {
    nombre: 'Recepción',
    plural: 'Recepciones',
    prefijo: 'REC',
    tipo: 'Recepcion',
    servicio: recepcionesService,
    rutaApp: '/recepciones',
    permisoCrear: 'recepciones.crear',
    permisoCancelar: 'recepciones.cancelar',
    textoNuevo: 'Nueva recepción',
    rutaNuevo: '/recepciones/nueva',
    etiquetaTercero: 'Proveedor',
    etiquetaPrecio: 'Costo unitario',
    descripcion: 'Entradas de mercancía de proveedores. Suman existencias y registran la compra.',
  },
  traspasos: {
    nombre: 'Traspaso',
    plural: 'Traspasos',
    prefijo: 'TRA',
    tipo: 'Traspaso',
    servicio: traspasosService,
    rutaApp: '/traspasos',
    permisoCrear: 'traspasos.crear',
    textoNuevo: 'Nuevo traspaso',
    rutaNuevo: '/traspasos/nuevo',
    etiquetaTercero: 'Origen → destino',
    mostrarImportes: false,
    descripcion: 'Movimientos de mercancía entre almacenes al costo promedio de origen. No generan movimiento financiero.',
  },
} satisfies Record<string, ConfigDocumento>;
