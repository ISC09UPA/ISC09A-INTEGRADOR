
import { Platform, Text, View } from 'react-native';
import { type ComponentType, type PropsWithChildren } from 'react';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';
import type { Vivero } from '../../../types';
import type { PuntoMapa } from '../../../services/viverosService';

// El mapa vive aislado para poder cambiar de proveedor
// (Google Maps, Mapbox, Apple Maps…) editando solo este archivo.

interface Props {
  usuario: PuntoMapa;
  viveros: Vivero[];
}

type MapViewProps = PropsWithChildren<{
  style: { flex: number };
  initialRegion: {
    latitude: number;
    longitude: number;
    latitudeDelta: number;
    longitudeDelta: number;
  };
  showsUserLocation: boolean;
}>;

/** Vista demo para web (Expo Go web no soporta react-native-maps). */
function MapaDemo({ viveros }: Props) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <View
      className="flex-1 items-center justify-center gap-1 rounded-2xl p-4"
      style={{ backgroundColor: tema.primaryContainer + '99' }}
    >
      <Text
        style={{ fontSize: 16, fontWeight: '600', color: tema.onPrimaryContainer }}
      >
        Mapa (vista demo web)
      </Text>

      <Text style={{ textAlign: 'center', fontSize: 14, color: tema.onPrimaryContainer }}>
        {viveros.length} viveros en un radio de{' '}
        {Math.ceil(Math.max(1, ...viveros.map((v) => v.distanciaKm)))} km
      </Text>

      <Text style={{ fontSize: 12, color: tema.onPrimaryContainer }}>
        Usa la lista de abajo para los detalles
      </Text>
    </View>
  );
}

/** Mapa nativo con react-native-maps (Google en Android, Apple en iOS). */
function MapaNativo({ usuario, viveros }: Props) {
  // Import diferido para evitar romper el bundle web.
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const mod = require('react-native-maps') as {
    default: ComponentType<MapViewProps>;
    Marker: ComponentType<{
      coordinate: { latitude: number; longitude: number };
      title: string;
      description: string;
    }>;
  };

  const MapView = mod.default;
  const Marker = mod.Marker;

  return (
    <MapView
      style={{ flex: 1 }}
      initialRegion={{
        latitude: usuario.latitud,
        longitude: usuario.longitud,
        latitudeDelta: 0.06,
        longitudeDelta: 0.06,
      }}
      showsUserLocation
    >
      {viveros.map((v) => (
        <Marker
          key={v.id}
          coordinate={{ latitude: v.latitud, longitude: v.longitud }}
          title={v.nombre}
          description={`${v.distanciaKm} km · ${v.abierto ? 'Abierto' : 'Cerrado'} · ${v.horario}`}
        />
      ))}
    </MapView>
  );
}

const Imprimible: ComponentType<Props> =
  Platform.OS === 'web' ? MapaDemo : MapaNativo;

export default function NurseryMap(props: Props) {
  return <Imprimible {...props} />;
}
