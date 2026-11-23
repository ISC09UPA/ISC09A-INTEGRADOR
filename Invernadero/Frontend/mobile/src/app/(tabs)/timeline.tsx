import { Confirmar } from '@/components/Confirmar';
import { TimelineItem } from '@/components/TimelineItem';
import { Card } from '@/components/ui/Card';
import { Chip } from '@/components/ui/Chip';
import { ErrorConReintento, Vacio } from '@/components/ui/Estados';
import { Select, type Opcion } from '@/components/ui/Select';
import { useTema } from '@/hooks/useTema';
import { MaterialIcons } from '@expo/vector-icons';
import { useInfiniteQuery, useQueryClient } from '@tanstack/react-query';
import * as FileSystem from 'expo-file-system';
import * as Sharing from 'expo-sharing';
import { useState } from 'react';
import { Alert, FlatList, Pressable, ScrollView, Text, View } from 'react-native';
import { timelineService } from '../../../services';
import { backendApiEnabled } from '../../../services/backendApi';
import { hapticoToque } from '../../../utils/haptico';

type FiltroTipo = 'todo' | 'riego' | 'alerta' | 'clima' | 'ia';
type FiltroPlanta = string; // 'todas' | id
type FiltroRango = 'hoy' | 'semana' | 'mes';

const OPCIONES_TIPO: Opcion<FiltroTipo>[] = [
  { valor: 'todo', etiqueta: 'Todo' },
  { valor: 'riego', etiqueta: '💧 Riegos' },
  { valor: 'alerta', etiqueta: '⚠️ Alertas' },
  { valor: 'clima', etiqueta: '🌡️ Clima' },
  { valor: 'ia', etiqueta: '🤖 Azure IA' },
];

const OPCIONES_RANGO: Opcion<FiltroRango>[] = [
  { valor: 'hoy', etiqueta: '📅 Hoy' },
  { valor: 'semana', etiqueta: 'Últimos 7 días' },
  { valor: 'mes', etiqueta: 'Este mes' },
];

const OPCIONES_PLANTA: Opcion<FiltroPlanta>[] = [
  { valor: 'todas', etiqueta: '🌱 Todas las plantas' },
  { valor: 'albahaca-1', etiqueta: '🌿 Albahaca #1' },
  { valor: 'tomate-1', etiqueta: '🍅 Tomate Cherry' },
  { valor: 'menta-1', etiqueta: '🍃 Menta Piperita' },
];

const DIAS_RANGO: Record<FiltroRango, number> = { hoy: 1, semana: 7, mes: 30 };
const MS_DIA = 86_400_000;

export default function Timeline() {
  const tema = useTema();
  const qc = useQueryClient();

  const [filtroTipo, setFiltroTipo] = useState<FiltroTipo>('todo');
  const [filtroPlanta, setFiltroPlanta] = useState<FiltroPlanta>('todas');
  const [filtroRango, setFiltroRango] = useState<FiltroRango>('semana');
  const [confirmarLimpieza, setConfirmarLimpieza] = useState(false);
  // Referencia de "ahora" para el rango y los KPIs. Se inicializa de forma perezosa
  // (sin parpadeo) y se actualiza al refrescar.
  const [ahoraMs, setAhoraMs] = useState(() => Date.now());
  const hoyStr = new Date(ahoraMs).toDateString();

  const eventos = useInfiniteQuery({
    queryKey: ['eventos', filtroTipo, filtroPlanta],
    queryFn: ({ pageParam }) =>
      timelineService.listarEventos({ tipo: filtroTipo, plantaId: filtroPlanta, pagina: pageParam }),
    initialPageParam: 0,
    getNextPageParam: (ultima) => ultima.siguientePagina ?? undefined,
  });

  const todas = eventos.data?.pages.flatMap((p) => p.eventos) ?? [];
  const total = eventos.data?.pages[0]?.totalFiltrado ?? 0;

  // El rango se aplica en la pantalla, sobre los eventos ya cargados.
  const desdeMs = ahoraMs - DIAS_RANGO[filtroRango] * MS_DIA;
  const visibles = todas.filter((e) => Date.parse(e.fecha) >= desdeMs);

  // La línea conectora solo abarca desde la primera hasta la última alerta visible.
  const primeraAlerta = visibles.findIndex((e) => e.tipo === 'alerta');
  const ultimaAlerta = visibles.reduce(
    (ultima, evento, indice) => (evento.tipo === 'alerta' ? indice : ultima),
    -1,
  );

  // KPIs del día (sobre los eventos cargados).
  const eventosHoy = todas.filter((e) => new Date(e.fecha).toDateString() === hoyStr);
  const riegosHoy = eventosHoy.filter((e) => e.tipo === 'riego');
  const mlHoy = riegosHoy.reduce((a, e) => a + (e.volumenMl ?? 0), 0);
  const alertasHoy = eventosHoy.filter((e) => e.tipo === 'alerta').length;

  function restablecerFiltros() {
    setFiltroTipo('todo');
    setFiltroPlanta('todas');
    setFiltroRango('semana');
    void eventos.refetch();
  }

  function refrescar() {
    setAhoraMs(Date.now());
    void eventos.refetch();
  }

  async function compartirCsv() {
    try {
      const csv = await timelineService.exportarCsv();
      const archivo = new FileSystem.File(FileSystem.Paths.cache, 'riegosmart-timeline.csv');
      archivo.create({ overwrite: true });
      await archivo.write(csv);
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(archivo.uri, { mimeType: 'text/csv', dialogTitle: 'Exportar historial' });
      }
    } catch (error) {
      Alert.alert(
        'No se pudo exportar el historial',
        error instanceof Error ? error.message : 'Ocurrió un error inesperado.',
      );
    }
  }

  if (eventos.isError) {
    return (
      <View className="flex-1 bg-surface">
        <View className="p-4">
          <ErrorConReintento mensaje="No se pudieron cargar los eventos." intentarDeNuevo={() => void eventos.refetch()} />
        </View>
      </View>
    );
  }

  return (
    <View className="relative flex-1 bg-surface">
      <View className="relative flex-1">
        <FlatList
          data={visibles}
          keyExtractor={(e) => e.id}
          onRefresh={refrescar}
          refreshing={eventos.isRefetching}
          onEndReached={() => {
            if (eventos.hasNextPage && !eventos.isFetchingNextPage) void eventos.fetchNextPage();
          }}
          onEndReachedThreshold={0.4}
          ListHeaderComponent={
            <View className="px-4 pb-1 pt-2">
              {/* Título y botón de compartir */}
              <View className="flex-row items-center gap-3 pt-2">
                <View className="min-w-0 flex-1">
                  <Text className="text-headline-lg-mobile text-on-surface" style={{ fontWeight: '700' }}>
                    Timeline
                  </Text>
                  <Text className="text-body-sm text-on-surface-variant">
                    Línea temporal de riegos, eventos y alertas del invernadero
                  </Text>
                </View>
                <Pressable
                  onPress={() => {
                    hapticoToque();
                    void compartirCsv();
                  }}
                  className="h-10 w-10 shrink-0 items-center justify-center rounded-full bg-surface-container-high active:scale-95"
                  style={{ marginRight: 4 }}
                  accessibilityRole="button"
                  accessibilityLabel="Exportar historial a CSV"
                >
                  <MaterialIcons name="share" size={20} color={tema.onSurface} />
                </Pressable>
              </View>

              {/* KPIs del día */}
              <Card className="mt-3 flex-row items-center justify-between">
                <View className="flex-1 items-center">
                  <MaterialIcons name="water-drop" size={16} color={tema.onSecondaryContainer} />
                  <Text className="text-label-sm font-semibold uppercase text-secondary" style={{ fontSize: 10 }}>
                    Riegos hoy
                  </Text>
                  <Text className="mt-0.5 text-title-md text-on-surface">
                    {riegosHoy.length} <Text className="text-label-md font-normal text-on-surface-variant">({mlHoy} mL)</Text>
                  </Text>
                </View>
                <View className="h-8 w-px bg-surface-container-highest" />
                <View className="flex-1 items-center">
                  <MaterialIcons name="notifications-active" size={16} color={tema.tertiary} />
                  <Text className="text-label-sm font-semibold uppercase text-tertiary" style={{ fontSize: 10 }}>
                    Alertas
                  </Text>
                  <View className="mt-0.5 flex-row items-center gap-1">
                    <Text className="text-title-md text-on-surface">{alertasHoy}</Text>
                    {alertasHoy > 0 && <View className="h-2 w-2 rounded-full bg-error" />}
                  </View>
                </View>
                <View className="h-8 w-px bg-surface-container-highest" />
                <View className="flex-1 items-center">
                  <MaterialIcons name="verified" size={16} color={tema.primary} />
                  <Text className="text-label-sm font-semibold uppercase text-primary" style={{ fontSize: 10 }}>
                    Eficiencia
                  </Text>
                  <Text className="mt-0.5 text-title-md text-primary">96%</Text>
                </View>
              </Card>

              {/* Filtros */}
              <View className="mt-3 flex-row gap-2">
                <Select valor={filtroPlanta} opciones={OPCIONES_PLANTA} onChange={setFiltroPlanta} />
                <Select valor={filtroRango} opciones={OPCIONES_RANGO} onChange={setFiltroRango} />
              </View>

              {/* Chips por categoría */}
              <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 8 }} className="mt-2 py-1">
                {OPCIONES_TIPO.map((o) => (
                  <Chip
                    key={o.valor}
                    texto={o.etiqueta}
                    activo={filtroTipo === o.valor}
                    conteo={o.valor === 'todo' && filtroPlanta === 'todas' && filtroRango === 'semana' ? total : undefined}
                    onPress={() => {
                      hapticoToque();
                      setFiltroTipo(o.valor);
                    }}
                  />
                ))}
              </ScrollView>
            </View>
          }
          ListEmptyComponent={
            eventos.isLoading ? (
              <View className="gap-2 p-4">
                <View className="h-24 rounded-xl bg-surface-container-high" />
                <View className="h-24 rounded-xl bg-surface-container-high" />
                <View className="h-24 rounded-xl bg-surface-container-high" />
              </View>
            ) : (
              <View className="p-4">
                <Vacio
                  titulo="No hay eventos que coincidan"
                  descripcion="Prueba cambiando de categoría o selecciona otra planta."
                  accionTexto="Restablecer filtros"
                  accionOn={restablecerFiltros}
                />
              </View>
            )
          }
          ListFooterComponent={
            eventos.isFetchingNextPage ? (
              <Text className="py-4 text-center text-label-md text-on-surface-variant">Cargando más…</Text>
            ) : (
              <View />
            )
          }
          renderItem={({ item, index }) => (
            <TimelineItem
              evento={item}
              mostrarConector={primeraAlerta !== -1 && index >= primeraAlerta && index < ultimaAlerta}
            />
          )}
          ItemSeparatorComponent={() => <View style={{ height: 12 }} />}
          contentContainerStyle={{ paddingLeft: 16, paddingRight: 16, paddingBottom: 16 }}
        />
      </View>

      {/* Pie: acciones secundarias */}
      <View className="flex-row items-center justify-end border-t border-surface-container-high bg-surface px-4 py-2">
        {!backendApiEnabled && (
          <Pressable
            onPress={() => {
              hapticoToque();
              setConfirmarLimpieza(true);
            }}
            className="min-h-[44px] flex-row items-center gap-1 rounded-lg px-2 py-1.5"
            accessibilityRole="button"
          >
            <MaterialIcons name="delete-sweep" size={16} color={tema.onSurfaceVariant} />
            <Text className="text-label-md text-on-surface-variant">Limpiar historial</Text>
          </Pressable>
        )}
        <Pressable
          onPress={() => {
            hapticoToque();
            void compartirCsv();
          }}
          className="min-h-[44px] flex-row items-center gap-1 rounded-lg px-2 py-1.5"
          accessibilityRole="button"
        >
          <MaterialIcons name="file-download" size={16} color={tema.primary} />
          <Text className="text-label-md text-primary" style={{ fontWeight: '500' }}>Descargar CSV</Text>
        </Pressable>
      </View>

      {!backendApiEnabled && (
        <Confirmar
          visible={confirmarLimpieza}
          titulo="Limpiar historial"
          descripcion="Se eliminarán todos los eventos del timeline de esta demo. La acción no se puede deshacer."
          textoConfirmar="Limpiar"
          peligroso
          onCancelar={() => setConfirmarLimpieza(false)}
          onConfirmar={() => {
            setConfirmarLimpieza(false);
            void timelineService.limpiarHistorial().then(() => {
              qc.invalidateQueries({ queryKey: ['eventos'] });
            });
          }}
        />
      )}
    </View>
  );
}
