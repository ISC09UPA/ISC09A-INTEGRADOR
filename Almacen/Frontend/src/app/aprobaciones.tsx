import { router } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import type { Producto } from '@/models/producto.model';
import { productosService } from '@/services';
import { ImagenProducto } from '@/components/productos';
import { Aviso, Boton, Cargando, DialogoTexto, Encabezado, Fila, Pantalla, Tarjeta, Texto, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { useDatos } from '@/hooks/use-datos';
import { Formato } from '@/lib/formato';
import { useSesion } from '@/lib/sesion';

/** Productos pendientes de aprobación (Aprobaciones.razor). El gerente ve los suyos; el superadmin, todos. */
export default function Aprobaciones() {
  const c = useColores();
  const { rol, usuarioId } = useSesion();
  const soloMias = rol === 'Gerente';
  const { datos, error, cargando, recargar } = useDatos(() =>
    productosService.listar({ estado: 'Pendiente', misAprobaciones: soloMias, tamano: 200 }),
  );
  const [ocupado, setOcupado] = useState<string | null>(null);
  const [rechazar, setRechazar] = useState<Producto | null>(null);

  const aprobar = async (p: Producto) => {
    setOcupado(p.id);
    try {
      await productosService.aprobar(p.id);
      avisar(`${p.referencia} aprobado.`);
      await recargar();
    } catch (e) {
      avisar('No se pudo aprobar', mensajeDe(e));
    } finally {
      setOcupado(null);
    }
  };

  const pendientes = datos?.elementos ?? [];

  return (
    <Pantalla alRecargar={recargar} recargando={cargando && !!datos}>
      <Encabezado
        titulo="Aprobación de productos"
        descripcion={soloMias ? 'Productos que te enviaron para aprobar.' : 'Todos los productos pendientes. Solo el gerente elegido puede resolver cada uno.'}
      />
      {error ? <Aviso tipo="error" texto={error} /> : null}
      {!datos && cargando ? <Cargando /> : null}
      {datos && pendientes.length === 0 ? (
        <Tarjeta style={{ alignItems: 'center', gap: Spacing.two, padding: Spacing.five }}>
          <Texto tipo="subtitulo" color={c.success}>
            No hay productos pendientes
          </Texto>
        </Tarjeta>
      ) : null}
      <View style={{ gap: Spacing.two }}>
        {pendientes.map((p) => (
          <Tarjeta key={p.id} style={{ gap: Spacing.two }}>
            <Fila gap={Spacing.three} style={{ alignItems: 'flex-start' }}>
              <ImagenProducto ruta={p.imagenRuta} tamano={64} />
              <View style={{ flex: 1, gap: 2 }}>
                <Texto tipo="subtitulo">{p.referencia}</Texto>
                <Texto>{p.descripcion}</Texto>
                <Texto tipo="pequeno">
                  Unidad {p.unidad} · solicitó {p.creadoPor} el {Formato.fechaHora(p.creadoEn)}
                </Texto>
                {!soloMias && p.gerenteAprobador ? <Texto tipo="pequeno">Enviado a {p.gerenteAprobador}</Texto> : null}
              </View>
            </Fila>
            <Fila style={{ flexWrap: 'wrap' }}>
              {p.gerenteAprobadorId === usuarioId ? (
                <>
                  <Boton texto="Aprobar" icono="check" cargando={ocupado === p.id} alPresionar={() => aprobar(p)} />
                  <Boton texto="Rechazar" variante="peligro" alPresionar={() => setRechazar(p)} />
                </>
              ) : null}
              <Boton texto="Ver ficha" variante="texto" alPresionar={() => router.push(`/productos/${p.id}`)} />
            </Fila>
          </Tarjeta>
        ))}
      </View>

      <DialogoTexto
        visible={!!rechazar}
        titulo={`Rechazar ${rechazar?.referencia ?? ''}`}
        etiqueta="Motivo del rechazo"
        textoConfirmar="Rechazar"
        peligro
        alCerrar={() => setRechazar(null)}
        alConfirmar={async (motivo) => {
          await productosService.rechazar(rechazar!.id, motivo);
          await recargar();
        }}
      />
    </Pantalla>
  );
}
