import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import type { Producto } from '@/models/producto.model';
import { productosService } from '@/services';
import { ImagenProducto, elegirYSubirImagen } from '@/components/productos';
import { Aviso, Boton, Cargando, Chip, Dato, Datos, DialogoTexto, Fila, Pantalla, Tarjeta, Texto, avisar, confirmar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useDatos } from '@/hooks/use-datos';
import { Formato, colorEstadoAprobacion } from '@/lib/formato';
import { Permisos } from '@/lib/permisos';
import { useSesion } from '@/lib/sesion';

/** Ficha de producto con las acciones de la tabla de Productos.razor y de Aprobaciones.razor. */
export default function DetalleProducto() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { puede, usuarioId } = useSesion();
  const { datos: p, error, cargando, recargar, setDatos } = useDatos(() => productosService.obtener(id), [id]);
  const [ocupado, setOcupado] = useState<string | null>(null);
  const [rechazando, setRechazando] = useState(false);

  const accion = async (nombre: string, fn: () => Promise<Producto | null>) => {
    setOcupado(nombre);
    try {
      const r = await fn();
      if (r) setDatos(r);
    } catch (e) {
      avisar('No se pudo completar', mensajeDe(e));
    } finally {
      setOcupado(null);
    }
  };

  const puedeResolver = p?.estado === 'Pendiente' && puede(Permisos.Productos.Aprobar) && p.gerenteAprobadorId === usuarioId;

  return (
    <Pantalla alRecargar={recargar} recargando={cargando && !!p}>
      <Stack.Screen options={{ title: p?.referencia ?? 'Producto' }} />
      {error ? <Aviso tipo="error" texto={error} /> : null}
      {!p && cargando ? <Cargando /> : null}
      {p ? (
        <View style={{ gap: Spacing.three }}>
          <Fila gap={Spacing.three} style={{ alignItems: 'flex-start' }}>
            <ImagenProducto ruta={p.imagenRuta} tamano={112} />
            <View style={{ flex: 1, gap: 6 }}>
              <Texto tipo="titulo">{p.referencia}</Texto>
              <Texto>{p.descripcion}</Texto>
              <Fila style={{ flexWrap: 'wrap' }} gap={6}>
                <Chip texto={p.estado} color={colorEstadoAprobacion(p.estado)} />
                <Chip texto={p.activo ? 'Activo' : 'Inactivo'} color={p.activo ? 'success' : 'default'} />
              </Fila>
            </View>
          </Fila>

          {p.estado === 'Rechazado' && p.motivoRechazo ? <Aviso tipo="error" texto={`Rechazado: ${p.motivoRechazo}`} /> : null}
          {p.estado === 'Pendiente' ? (
            <Aviso tipo="alerta" texto={`Pendiente de aprobación${p.gerenteAprobador ? ` por ${p.gerenteAprobador}` : ''}. Hasta entonces no se puede recibir, vender ni traspasar.`} />
          ) : null}

          <Tarjeta>
            <Datos>
              <Dato etiqueta="Unidad" valor={`${p.unidad}${p.permiteFracciones ? '' : ' · solo enteros'}`} />
              <Dato etiqueta="Existencia total" valor={`${Formato.cantidad(p.existencia)} ${p.unidad}`} />
              <Dato etiqueta="Dio de alta" valor={p.creadoPor} detalle={Formato.fechaHora(p.creadoEn)} />
              {p.gerenteAprobador ? <Dato etiqueta="Gerente aprobador" valor={p.gerenteAprobador} /> : null}
            </Datos>
          </Tarjeta>

          {puedeResolver ? (
            <Fila style={{ flexWrap: 'wrap' }}>
              <Boton texto="Aprobar" icono="check" cargando={ocupado === 'aprobar'} alPresionar={() => accion('aprobar', () => productosService.aprobar(p.id))} />
              <Boton texto="Rechazar" variante="peligro" alPresionar={() => setRechazando(true)} />
            </Fila>
          ) : null}

          <Fila style={{ flexWrap: 'wrap' }}>
            {puede(Permisos.Productos.Editar) ? (
              <>
                <Boton texto="Editar" icono="edit" variante="contorno" alPresionar={() => router.push(`/productos/editar?id=${p.id}`)} />
                <Boton
                  texto={p.activo ? 'Desactivar' : 'Activar'}
                  icono={p.activo ? 'block' : 'check-circle'}
                  variante={p.activo ? 'peligro' : 'contorno'}
                  cargando={ocupado === 'estado'}
                  alPresionar={async () => {
                    if (p.activo && !(await confirmar('Desactivar producto', `${p.referencia} ya no aparecerá para capturar documentos.`, 'Desactivar'))) return;
                    await accion('estado', () => productosService.cambiarEstado(p.id, !p.activo));
                  }}
                />
              </>
            ) : null}
            {puede(Permisos.Productos.Crear) ? (
              <Boton
                texto={p.imagenRuta ? 'Cambiar imagen' : 'Agregar imagen'}
                icono="image"
                variante="contorno"
                cargando={ocupado === 'imagen'}
                alPresionar={() => accion('imagen', () => elegirYSubirImagen(p.id))}
              />
            ) : null}
            {puede(Permisos.Almacenes.VerExistencias) && p.estado === 'Aprobado' ? (
              <Boton texto="Ver existencias" icono="warehouse" variante="contorno" alPresionar={() => router.push(`/existencias?busqueda=${encodeURIComponent(p.referencia)}`)} />
            ) : null}
          </Fila>
        </View>
      ) : null}

      <DialogoTexto
        visible={rechazando}
        titulo="Rechazar producto"
        etiqueta="Motivo del rechazo"
        textoConfirmar="Rechazar"
        peligro
        alCerrar={() => setRechazando(false)}
        alConfirmar={async (motivo) => {
          setDatos(await productosService.rechazar(id, motivo));
        }}
      />
    </Pantalla>
  );
}
