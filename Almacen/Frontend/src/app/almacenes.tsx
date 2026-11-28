import { router } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import type { Almacen } from '@/models/almacen.model';
import { almacenesService } from '@/services';
import { Aviso, Boton, Campo, Cargando, Chip, Encabezado, Fila, Hoja, Interruptor, Pantalla, Tarjeta, Texto, Vacio, avisar, confirmar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useDatos } from '@/hooks/use-datos';
import { Formato } from '@/lib/formato';
import { Permisos } from '@/lib/permisos';
import { useSesion } from '@/lib/sesion';

/** Almacenes con su valor de inventario (Almacenes.razor). */
export default function Almacenes() {
  const { puede } = useSesion();
  const [inactivos, setInactivos] = useState(false);
  const [editando, setEditando] = useState<Almacen | 'nuevo' | null>(null);
  const { datos, error, cargando, recargar } = useDatos(() => almacenesService.listar(inactivos), [inactivos]);
  const gestionar = puede(Permisos.Almacenes.Gestionar);

  const cambiarEstado = async (a: Almacen) => {
    if (a.activo && !(await confirmar('Desactivar almacén', `${a.nombre} ya no aparecerá para capturar documentos.`, 'Desactivar'))) return;
    try {
      await almacenesService.cambiarEstado(a.id, !a.activo);
      await recargar();
    } catch (e) {
      avisar('No se pudo cambiar el estado', mensajeDe(e));
    }
  };

  return (
    <Pantalla alRecargar={recargar} recargando={cargando && !!datos}>
      <Encabezado
        titulo="Almacenes"
        descripcion="Lugares donde se guarda la mercancía y su valor de inventario."
        acciones={gestionar ? <Boton texto="Nuevo almacén" icono="add" alPresionar={() => setEditando('nuevo')} /> : undefined}
      />
      <Interruptor etiqueta="Mostrar inactivos" valor={inactivos} alCambiar={setInactivos} />
      {error ? <Aviso tipo="error" texto={error} /> : null}
      {!datos && cargando ? <Cargando /> : null}
      {datos && datos.length === 0 ? <Vacio texto="No hay almacenes." /> : null}
      <View style={{ gap: Spacing.two, marginTop: Spacing.two }}>
        {(datos ?? []).map((a) => (
          <Tarjeta key={a.id} style={{ gap: 6 }}>
            <Fila style={{ justifyContent: 'space-between' }}>
              <Texto tipo="subtitulo">{a.nombre}</Texto>
              <Chip texto={a.activo ? 'Activo' : 'Inactivo'} color={a.activo ? 'success' : 'default'} />
            </Fila>
            <Texto tipo="secundario">{a.ubicacion}</Texto>
            <Fila style={{ justifyContent: 'space-between' }}>
              <Texto tipo="pequeno">{a.productos} productos</Texto>
              <Texto tipo="fuerte">{Formato.dinero(a.valorInventario)}</Texto>
            </Fila>
            <Fila style={{ flexWrap: 'wrap', justifyContent: 'flex-end' }}>
              {puede(Permisos.Almacenes.VerExistencias) ? <Boton texto="Existencias" icono="inventory" variante="texto" alPresionar={() => router.push('/existencias')} /> : null}
              {gestionar ? (
                <>
                  <Boton texto="Editar" icono="edit" variante="texto" alPresionar={() => setEditando(a)} />
                  <Boton texto={a.activo ? 'Desactivar' : 'Activar'} variante="texto" alPresionar={() => cambiarEstado(a)} />
                </>
              ) : null}
            </Fila>
          </Tarjeta>
        ))}
      </View>
      <DialogoAlmacen actual={editando} alCerrar={() => setEditando(null)} alGuardar={recargar} />
    </Pantalla>
  );
}

function DialogoAlmacen({ actual, alCerrar, alGuardar }: { actual: Almacen | 'nuevo' | null; alCerrar: () => void; alGuardar: () => void }) {
  const [nombre, setNombre] = useState('');
  const [ubicacion, setUbicacion] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const [para, setPara] = useState<typeof actual>(null);
  if (actual !== para) {
    setPara(actual);
    setNombre(actual && actual !== 'nuevo' ? actual.nombre : '');
    setUbicacion(actual && actual !== 'nuevo' ? actual.ubicacion : '');
    setError(null);
  }
  const edicion = actual && actual !== 'nuevo' ? actual : null;

  return (
    <Hoja visible={!!actual} titulo={edicion ? 'Editar almacén' : 'Nuevo almacén'} alCerrar={alCerrar}>
      <View style={{ gap: Spacing.three }}>
        <Campo etiqueta="Nombre" value={nombre} onChangeText={setNombre} maxLength={120} />
        <Campo etiqueta="Ubicación" value={ubicacion} onChangeText={setUbicacion} maxLength={300} multiline ayuda="Dirección o referencia del lugar" />
        {error ? <Aviso tipo="error" texto={error} /> : null}
        <Fila style={{ justifyContent: 'flex-end' }}>
          <Boton texto="Cancelar" variante="texto" alPresionar={alCerrar} />
          <Boton
            texto="Guardar"
            cargando={guardando}
            alPresionar={async () => {
              if (!nombre.trim() || !ubicacion.trim()) return setError('Escribe nombre y ubicación.');
              setGuardando(true);
              try {
                const datos = { nombre, ubicacion };
                if (edicion) await almacenesService.editar(edicion.id, datos);
                else await almacenesService.crear(datos);
                alCerrar();
                alGuardar();
              } catch (e) {
                setError(mensajeDe(e));
              } finally {
                setGuardando(false);
              }
            }}
          />
        </Fila>
      </View>
    </Hoja>
  );
}
