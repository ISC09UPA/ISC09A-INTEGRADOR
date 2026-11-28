import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { View } from 'react-native';

import type { Unidad } from '@/models/unidad.model';
import type { Usuario } from '@/models/usuario.model';
import { productosService } from '@/services';
import { Aviso, Boton, Campo, Cargando, Fila, Pantalla, Selector, Tarjeta, Texto, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { Permisos } from '@/lib/permisos';
import { useSesion } from '@/lib/sesion';

/** Alta (sin id) o edición (?id=) de producto (ProductoDialog.razor). */
export default function EditarProducto() {
  const { id } = useLocalSearchParams<{ id?: string }>();
  const { puede } = useSesion();
  const esEdicion = !!id;
  const requiereAprobacion = !puede(Permisos.Productos.Aprobar);
  const [unidades, setUnidades] = useState<Unidad[]>([]);
  const [gerentes, setGerentes] = useState<Usuario[]>([]);
  const [referencia, setReferencia] = useState('');
  const [descripcion, setDescripcion] = useState('');
  const [unidadId, setUnidadId] = useState<string | undefined>();
  const [gerenteId, setGerenteId] = useState<string | undefined>();
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        setUnidades(await productosService.unidades());
        if (!esEdicion && requiereAprobacion) {
          const g = await productosService.gerentes();
          setGerentes(g);
          if (g.length === 1) setGerenteId(g[0].id);
        }
        if (id) {
          const p = await productosService.obtener(id);
          setReferencia(p.referencia);
          setDescripcion(p.descripcion);
          setUnidadId(p.unidadId);
        }
      } catch (e) {
        setError(mensajeDe(e));
      } finally {
        setCargando(false);
      }
    })();
  }, [id, esEdicion, requiereAprobacion]);

  const guardar = async () => {
    setError(null);
    if (!referencia.trim() || !descripcion.trim() || !unidadId) return setError('Completa referencia, descripción y unidad.');
    if (!esEdicion && requiereAprobacion && !gerenteId) return setError('Elige al gerente que aprueba.');
    setGuardando(true);
    try {
      const p = esEdicion
        ? await productosService.editar(id, { descripcion, unidadId })
        : await productosService.crear({ referencia, descripcion, unidadId, gerenteAprobadorId: gerenteId });
      avisar(esEdicion ? 'Producto actualizado.' : p.estado === 'Pendiente' ? 'Producto enviado a aprobación.' : 'Producto dado de alta.');
      router.replace(`/productos/${p.id}`);
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setGuardando(false);
    }
  };

  return (
    <Pantalla>
      <Stack.Screen options={{ title: esEdicion ? 'Editar producto' : 'Nuevo producto' }} />
      {cargando ? <Cargando /> : null}
      {!cargando ? (
        <Tarjeta style={{ gap: Spacing.three }}>
          {error ? <Aviso tipo="error" texto={error} /> : null}
          <Campo
            etiqueta="Referencia interna"
            value={referencia}
            onChangeText={setReferencia}
            editable={!esEdicion}
            autoCapitalize="characters"
            maxLength={50}
            ayuda={esEdicion ? 'No se puede cambiar' : 'Código único del producto, por ejemplo TOR-001'}
          />
          <Campo etiqueta="Descripción" value={descripcion} onChangeText={setDescripcion} maxLength={500} multiline />
          <Selector<string>
            etiqueta="Unidad"
            valor={unidadId}
            opciones={unidades.map((u) => ({ valor: u.id, texto: `${u.nombre} (${u.clave})`, detalle: u.permiteFracciones ? undefined : 'Solo enteros' }))}
            alCambiar={setUnidadId}
          />
          {!esEdicion && requiereAprobacion ? (
            <View style={{ gap: Spacing.two }}>
              <Aviso texto="El producto quedará pendiente hasta que lo apruebe el gerente que elijas." />
              <Selector<string> etiqueta="Gerente que aprueba" valor={gerenteId} opciones={gerentes.map((g) => ({ valor: g.id, texto: g.nombre }))} alCambiar={setGerenteId} />
              {gerentes.length === 0 ? <Aviso tipo="error" texto="No hay gerentes activos. Pide al superadmin que dé de alta uno." /> : null}
            </View>
          ) : null}
          {!esEdicion ? <Texto tipo="pequeno">La imagen se agrega desde la ficha del producto, después de darlo de alta.</Texto> : null}
          <Fila style={{ justifyContent: 'flex-end' }}>
            <Boton texto="Cancelar" variante="texto" alPresionar={() => router.back()} />
            <Boton texto={esEdicion ? 'Guardar cambios' : 'Dar de alta'} cargando={guardando} alPresionar={guardar} />
          </Fila>
        </Tarjeta>
      ) : null}
    </Pantalla>
  );
}
