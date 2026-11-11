
import { Pressable, ScrollView, Text } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';
import { FILTROS, type Categoria } from './datos';

interface Props {
  seleccionado: Categoria | 'todas';
  onSelect: (id: Categoria | 'todas') => void;
}

/** Chips horizontales para filtrar reglas por categoría. */
export function FiltrosReglas({ seleccionado, onSelect }: Props) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={{ flexDirection: 'row', gap: 6, paddingVertical: 4 }}
    >
      {FILTROS.map((f) => {
        const sel = f.id === seleccionado;

        return (
          <Pressable
            key={f.id}
            onPress={() => onSelect(f.id)}
            className="rounded-full px-3 py-1.5"
            style={{
              backgroundColor: sel ? tema.primary : tema.surfaceContainerLowest,
            }}
          >
            <Text
              style={{
                fontSize: 13,
                fontWeight: '600',
                color: sel ? tema.onPrimary : tema.onSurface,
              }}
            >
              {f.etiqueta}
            </Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}
