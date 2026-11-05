import { Image, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { colores } from '@/theme/tokens';
import type { Planta } from './datos';

interface Props {
  plantas: Planta[];
  seleccionada: string;
  onSelect: (id: string) => void;
}

/** "Mis Cultivos": chips horizontales para elegir la planta activa. */
export function CarruselCultivos({ plantas, seleccionada, onSelect }: Props) {
  return (
    <View>
      <View style={s.encabezado}>
        <Text style={s.titulo}>Mis Cultivos</Text>
        <Text style={s.zonas}>{plantas.length} Zonas Activas</Text>
      </View>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={s.carrusel}>
        {plantas.map((p) => {
          const sel = p.id === seleccionada;
          return (
            <Pressable
              key={p.id}
              onPress={() => onSelect(p.id)}
              style={[s.chip, { backgroundColor: sel ? colores.primary : colores.surfaceContainerLowest }]}
            >
              <Image source={{ uri: p.imagenUrl }} style={s.img} />
              <View>
                <Text style={[s.nombre, { color: sel ? colores.onPrimary : colores.onSurface }]}>{p.nombre}</Text>
                <Text style={[s.subtitulo, { color: sel ? colores.onPrimary : colores.onSurfaceVariant }]}>
                  {p.subtitulo}
                </Text>
              </View>
            </Pressable>
          );
        })}
      </ScrollView>
    </View>
  );
}

const s = StyleSheet.create({
  encabezado: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  titulo: { fontSize: 16, fontWeight: '600', color: colores.onSurface },
  zonas: { fontSize: 12, fontWeight: '600', color: colores.primary },
  carrusel: { gap: 8, paddingVertical: 8 },
  chip: { flexDirection: 'row', alignItems: 'center', gap: 8, borderRadius: 999, padding: 8, paddingRight: 14 },
  img: { width: 32, height: 32, borderRadius: 16 },
  nombre: { fontSize: 13, fontWeight: '600' },
  subtitulo: { fontSize: 11 },
});
