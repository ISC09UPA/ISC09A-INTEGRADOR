import { router } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import type { Producto } from '@/models/producto.model';
import { productosService } from '@/services';
import type { EstadoAprobacion } from '@/types/enums';
import { ListaPantalla } from '@/components/lista';
import { ImagenProducto } from '@/components/productos';
import { Boton, Busqueda, Chip, Fila, Interruptor, Selector, Tarjeta, Texto } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { usePaginado } from '@/hooks/use-datos';
import { Formato, colorEstadoAprobacion } from '@/lib/formato';
import { Permisos } from '@/lib/permisos';
import { useSesion } from '@/lib/sesion';

/** Catálogo de productos (Productos.razor). */
export default function Productos() {
  const { puede } = useSesion();
  const [busqueda, setBusqueda] = useState('');
  const [estado, setEstado] = useState<EstadoAprobacion | ''>('');
  const [inactivos, setInactivos] = useState(false);
  const lista = usePaginado<Producto>(
    (pagina) => productosService.listar({ busqueda, estado: estado || undefined, inactivos, pagina }),
    [busqueda, estado, inactivos],
  );

  return (
    <ListaPantalla
      {...lista}
      alRecargar={lista.recargar}
      alLlegarAlFinal={lista.cargarMas}
      clave={(p) => p.id}
      vacio="No hay productos que coincidan."
      encabezado={
        <View style={{ gap: Spacing.two }}>
          <Texto tipo="secundario">Catálogo de productos. Solo los aprobados se pueden recibir, vender o traspasar.</Texto>
          <Fila style={{ flexWrap: 'wrap' }}>
            {puede(Permisos.Productos.Crear) ? <Boton texto="Nuevo producto" icono="add" alPresionar={() => router.push('/productos/editar')} /> : null}
            {puede(Permisos.Productos.Aprobar) ? <Boton texto="Aprobaciones" icono="fact-check" variante="contorno" alPresionar={() => router.push('/aprobaciones')} /> : null}
          </Fila>
          <Busqueda valor={busqueda} alCambiar={setBusqueda} placeholder="Referencia o descripción" />
          <Selector<EstadoAprobacion | ''>
            etiqueta="Aprobación"
            valor={estado}
            alCambiar={setEstado}
            opciones={[
              { valor: '', texto: 'Todas' },
              { valor: 'Aprobado', texto: 'Aprobados' },
              { valor: 'Pendiente', texto: 'Pendientes' },
              { valor: 'Rechazado', texto: 'Rechazados' },
            ]}
          />
          <Interruptor etiqueta="Mostrar inactivos" valor={inactivos} alCambiar={setInactivos} />
        </View>
      }
      fila={(p) => (
        <Tarjeta alPresionar={() => router.push(`/productos/${p.id}`)}>
          <Fila gap={Spacing.three} style={{ alignItems: 'flex-start' }}>
            <ImagenProducto ruta={p.imagenRuta} tamano={56} />
            <View style={{ flex: 1, gap: 4 }}>
              <Texto tipo="fuerte">{p.referencia}</Texto>
              <Texto numberOfLines={2}>{p.descripcion}</Texto>
              <Fila style={{ flexWrap: 'wrap' }} gap={6}>
                <Chip texto={p.estado} color={colorEstadoAprobacion(p.estado)} />
                {!p.activo ? <Chip texto="Inactivo" /> : null}
                <Texto tipo="pequeno">
                  {Formato.cantidad(p.existencia)} {p.unidad}
                </Texto>
              </Fila>
            </View>
          </Fila>
        </Tarjeta>
      )}
    />
  );
}
