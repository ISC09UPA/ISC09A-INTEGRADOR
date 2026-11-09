import { Platform, Text, View } from 'react-native';
import { type ComponentType, type PropsWithChildren } from 'react';
import type { Vivero } from '../../../types';
import type { PuntoMapa } from '../../../services/viverosService';

// El mapa vive aislado para poder cambiar de proveedor
// (Google Maps, Mapbox, Apple Maps…) editando solo este archivo.

interface Props {
  usuario: PuntoMapa;
  viveros: Vivero[];
}

/** Vista demo para web (Expo Go web no soporta react-native-maps). */
function MapaDemo({ viveros }: Props) {
  return (
    <View className="flex-1 items-center justify-center gap-1 rounded-2xl bg-primary-container/60 p-4">
      <Text className="text-title-md text-on-primary-container" style={{ fontWeight: '600' }}>
        Mapa (vista demo web)
      </Text>
      <Text className="text-center text-body-sm text-on-primary-container">
        {viveros.length} viveros en un radio de{' '}
        {Math.ceil(Math.max(1, ...viveros.map((v) => v.distanciaKm)))} km
      </Text>
      <Text className="text-label-sm text-on-primary-container">
        Usa la lista de abajo para los detalles
      </Text>
    </View>
  );
}

/** Mapa nativo con react-native-maps (Google en Android, Apple en iOS). */
type MapViewProps = PropsWithChildren<{
  style: { flex: number };
  initialRegion: { latitude: number; longitude: number; latitudeDelta: number; longitudeDelta: number };
  showsUserLocation: boolean;
}>;

function MapaNativo({ usuario, viveros }: Props) {
  // Import normal de react-native-maps (dependencia opcional nativa).
  // El require diferido evita romper el bundle web.
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
