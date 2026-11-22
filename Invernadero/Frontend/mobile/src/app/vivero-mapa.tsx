
import NurseryMap from '@/components/mapa/NurseryMap';
import { MaterialIcons } from '@expo/vector-icons';
import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { FlatList, Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { viverosService } from '../../services';
import { ErrorConReintento } from '../components/ui/Estados';
import { useTema } from '@/hooks/useTema';

/** Pantalla vivero-mapa: componente mapa aislado + lista de viveros cercanos. */
export default function ViveroMapa() {
  const tema = useTema();
  const insets = useSafeAreaInsets();

  const datos = useQuery({
    queryKey: ['viveros'],
    queryFn: viverosService.listarViverosCercanos,
  });

  // Error al consultar los viveros
  if (datos.isError) {
    return (
      <View
        className="flex-1 items-center justify-center bg-surface px-6"
        style={{ paddingTop: insets.top }}
      >
        <ErrorConReintento
          mensaje="No se pudieron cargar los viveros."
          intentarDeNuevo={() => void datos.refetch()}
        />
      </View>
    );
  }

  // Estado de carga
  if (datos.isLoading || !datos.data) {
    return (
      <View
        className="flex-1 items-center justify-center bg-surface"
        style={{ paddingTop: insets.top }}
      >
        <Text className="text-body-md text-on-surface-variant">
          Buscando viveros cercanos…
        </Text>
      </View>
    );
  }

  return (
    <View
      className="flex-1 bg-surface"
      style={{ paddingTop: insets.top }}
    >
      {/* Encabezado */}
      <View className="h-16 flex-row items-center gap-2 px-gutter-mobile pt-2">
        <Pressable
          onPress={() => router.back()}
          className="h-10 w-10 items-center justify-center rounded-full bg-surface-container-low"
          accessibilityRole="button"
          accessibilityLabel="Cerrar"
        >
          <MaterialIcons
            name="close"
            size={20}
            color={tema.onSurface}
          />
        </Pressable>

        <View className="flex-col">
          <Text
            className="text-title-md text-on-surface"
            style={{ fontWeight: '600' }}
          >
            Viveros cercanos
          </Text>

          <Text className="text-body-sm text-on-surface-variant">
            Donde conseguir tus plantas e insumos
          </Text>
        </View>
      </View>

      {/* Componente de mapa aislado */}
      <View
        className="mx-gutter-mobile overflow-hidden rounded-2xl"
        style={{ height: 260 }}
      >
        <NurseryMap
          usuario={datos.data.usuario}
          viveros={datos.data.viveros}
        />
      </View>

      {/* Lista de viveros */}
      <FlatList
        className="flex-1 px-gutter-mobile pt-3"
        contentContainerStyle={{
          paddingBottom: 24,
          gap: 8,
        }}
        data={datos.data.viveros}
        keyExtractor={(v) => v.id}
        renderItem={({ item }) => (
          <View className="rounded-xl bg-surface-container-lowest p-3 shadow-sm">
            <View className="flex-row items-center justify-between">
              <Text
                className="text-title-sm text-on-surface"
                style={{ fontWeight: '600' }}
              >
                {item.nombre}
              </Text>

              <Text className="text-label-md font-semibold text-primary">
                {item.distanciaKm} km
              </Text>
            </View>

            <Text className="mt-0.5 text-body-sm text-on-surface-variant">
              {item.especialidades.join(' · ')}
            </Text>

            <View className="mt-1 flex-row items-center justify-between">
              <Text className="text-label-sm text-on-surface-variant">
                {item.abierto
                  ? `Abierto · ${item.horario}`
                  : `Cerrado · abre ${item.horario}`}
              </Text>

              <Text className="text-label-md text-on-surface">
                ≈ ${item.precioMedio} / planta
              </Text>
            </View>
          </View>
        )}
      />
    </View>
  );
}
