import type { ReactElement, ReactNode } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { Aviso, Texto, Vacio } from '@/components/ui';
import { MaxContentWidth, Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';

/** Pantalla de listado: encabezado con filtros, filas, "jalar para recargar" y "cargar más" al final. */
export function ListaPantalla<T>({
  encabezado,
  elementos,
  total,
  cargando,
  error,
  alRecargar,
  alLlegarAlFinal,
  fila,
  clave,
  vacio = 'No hay registros que coincidan.',
  pie,
}: {
  encabezado?: ReactElement;
  elementos: T[];
  total?: number;
  cargando: boolean;
  error?: string | null;
  alRecargar: () => void;
  alLlegarAlFinal?: () => void;
  fila: (item: T) => ReactElement;
  clave: (item: T, i: number) => string;
  vacio?: string;
  pie?: ReactNode;
}) {
  const c = useColores();
  return (
    <SafeAreaView edges={['bottom', 'left', 'right']} style={{ flex: 1, backgroundColor: c.background }}>
      <FlatList
        data={elementos}
        keyExtractor={clave}
        renderItem={({ item }) => fila(item)}
        contentContainerStyle={{ padding: Spacing.three, paddingBottom: Spacing.six, gap: Spacing.two, width: '100%', maxWidth: MaxContentWidth, alignSelf: 'center' }}
        keyboardShouldPersistTaps="handled"
        refreshControl={<RefreshControl refreshing={cargando && elementos.length > 0} onRefresh={alRecargar} />}
        onEndReached={alLlegarAlFinal}
        onEndReachedThreshold={0.4}
        ListHeaderComponent={
          <View style={{ gap: Spacing.two, marginBottom: Spacing.two }}>
            {encabezado}
            {error ? <Aviso tipo="error" texto={error} /> : null}
            {total !== undefined && !error ? <Texto tipo="pequeno">{total === 1 ? '1 registro' : `${total} registros`}</Texto> : null}
          </View>
        }
        ListEmptyComponent={cargando ? <ActivityIndicator color={c.primary} style={{ padding: Spacing.four }} /> : error ? undefined : <Vacio texto={vacio} />}
        ListFooterComponent={
          <View>
            {cargando && elementos.length > 0 ? <ActivityIndicator color={c.primary} style={{ padding: Spacing.three }} /> : null}
            {pie}
          </View>
        }
      />
    </SafeAreaView>
  );
}
