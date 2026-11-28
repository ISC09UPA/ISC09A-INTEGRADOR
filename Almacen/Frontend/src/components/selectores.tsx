import MaterialIcons from '@expo/vector-icons/MaterialIcons';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

import type { Opcion } from '@/models/opcion.model';
import type { ProductoOpcion } from '@/models/producto-opcion.model';
import { clientesService, productosService, proveedoresService } from '@/services';
import { Aviso, Busqueda, Hoja, Texto, mensajeDe } from '@/components/ui';
import { Radio, Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { Formato } from '@/lib/formato';

/** Campo que abre una hoja con búsqueda en la API (como SelectorTercero / SelectorProducto de Blazor). */
function SelectorBuscable<T>({
  etiqueta,
  texto,
  buscar,
  textoDe,
  detalleDe,
  clave,
  alElegir,
  placeholder,
}: {
  etiqueta: string;
  texto: string | null;
  buscar: (t: string) => Promise<T[]>;
  textoDe: (x: T) => string;
  detalleDe?: (x: T) => string | undefined;
  clave: (x: T) => string;
  alElegir: (x: T) => void;
  placeholder: string;
}) {
  const c = useColores();
  const [abierto, setAbierto] = useState(false);
  const [filtro, setFiltro] = useState('');
  const [resultados, setResultados] = useState<T[]>([]);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!abierto) return;
    let vigente = true;
    const t = setTimeout(async () => {
      setCargando(true);
      try {
        const r = await buscar(filtro);
        if (vigente) {
          setResultados(r);
          setError(null);
        }
      } catch (e) {
        if (vigente) setError(mensajeDe(e));
      } finally {
        if (vigente) setCargando(false);
      }
    }, 250);
    return () => {
      vigente = false;
      clearTimeout(t);
    };
    // buscar cambia en cada render; basta con el texto y la apertura.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtro, abierto]);

  return (
    <View style={{ gap: 4, flexGrow: 1 }}>
      <Texto tipo="secundario">{etiqueta}</Texto>
      <Pressable onPress={() => setAbierto(true)} style={[estilos.campo, { borderColor: c.line, backgroundColor: c.surface }]}>
        <Text style={{ color: texto ? c.text : c.textSecondary, flex: 1, fontSize: 15 }} numberOfLines={1}>
          {texto ?? placeholder}
        </Text>
        <MaterialIcons name="search" size={20} color={c.textSecondary} />
      </Pressable>
      <Hoja visible={abierto} titulo={etiqueta} alCerrar={() => setAbierto(false)}>
        <View style={{ gap: Spacing.two }}>
          <Busqueda valor={filtro} alCambiar={setFiltro} placeholder="Escribe para buscar" />
          {error ? <Aviso tipo="error" texto={error} /> : null}
          {cargando ? <ActivityIndicator color={c.primary} /> : null}
          <ScrollView style={{ maxHeight: 380 }} keyboardShouldPersistTaps="handled">
            {resultados.map((r) => (
              <Pressable
                key={clave(r)}
                onPress={() => {
                  alElegir(r);
                  setAbierto(false);
                }}
                style={({ pressed }) => [estilos.opcion, { borderColor: c.line, backgroundColor: pressed ? c.background : 'transparent' }]}>
                <Texto>{textoDe(r)}</Texto>
                {detalleDe?.(r) ? <Texto tipo="pequeno">{detalleDe(r)}</Texto> : null}
              </Pressable>
            ))}
            {!cargando && resultados.length === 0 ? <Texto tipo="secundario" style={{ padding: Spacing.two }}>Sin resultados.</Texto> : null}
          </ScrollView>
        </View>
      </Hoja>
    </View>
  );
}

export function SelectorTercero({
  clientes,
  valor,
  alCambiar,
}: {
  clientes: boolean;
  valor: Opcion | null;
  alCambiar: (o: Opcion) => void;
}) {
  return (
    <SelectorBuscable<Opcion>
      etiqueta={clientes ? 'Cliente' : 'Proveedor'}
      placeholder={clientes ? 'Elige el cliente' : 'Elige el proveedor'}
      texto={valor?.texto ?? null}
      buscar={(texto) => (clientes ? clientesService : proveedoresService).activos(texto)}
      textoDe={(o) => o.texto}
      clave={(o) => o.id}
      alElegir={alCambiar}
    />
  );
}

export function SelectorProducto({ valor, alCambiar }: { valor: ProductoOpcion | null; alCambiar: (p: ProductoOpcion) => void }) {
  return (
    <SelectorBuscable<ProductoOpcion>
      etiqueta="Producto"
      placeholder="Busca por referencia o descripción"
      texto={valor?.texto ?? null}
      buscar={(texto) => productosService.disponibles(texto)}
      textoDe={(p) => p.texto}
      detalleDe={(p) =>
        p.existencias.length
          ? p.existencias.map((e) => `${e.almacen}: ${Formato.cantidad(e.cantidad)} ${p.unidad}`).join(' · ')
          : 'Sin existencias'
      }
      clave={(p) => p.id}
      alElegir={alCambiar}
    />
  );
}

const estilos = StyleSheet.create({
  campo: {
    flexDirection: 'row',
    alignItems: 'center',
    borderWidth: 1,
    borderRadius: Radio,
    paddingHorizontal: 12,
    paddingVertical: 10,
    minHeight: 44,
  },
  opcion: {
    paddingVertical: 10,
    paddingHorizontal: Spacing.two,
    borderBottomWidth: StyleSheet.hairlineWidth,
    gap: 2,
  },
});
