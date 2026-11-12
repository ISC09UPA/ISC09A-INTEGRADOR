import { colores } from '@/theme/tokens';
import { useColorScheme } from 'nativewind';
import { Pressable, Text, View } from 'react-native';
import { FILTROS, type Categoria } from './datos';

interface Props {
  seleccionado: Categoria | 'todas';
  onSelect: (id: Categoria | 'todas') => void;
}

/** Chips fijos para filtrar reglas por categoría. */
export function FiltrosReglas({ seleccionado, onSelect }: Props) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <View
      style={{
        width: '100%',
        gap: 8,
        paddingVertical: 4,
      }}
    >
      <View style={{ flexDirection: 'row', gap: 8 }}>
        {FILTROS.slice(0, 3).map((f) => {
          const sel = f.id === seleccionado;

          return (
            <Pressable
              key={f.id}
              onPress={() => onSelect(f.id)}
              style={{
                flex: 1,
                alignItems: 'center',
                justifyContent: 'center',
                borderRadius: 999,
                paddingHorizontal: 8,
                paddingVertical: 9,
                backgroundColor: sel
                  ? tema.primary
                  : tema.surfaceContainerLowest,
              }}
            >
              <Text
                style={{
                  fontSize: 13,
                  fontWeight: '600',
                  color: sel ? tema.onPrimary : tema.onSurface,
                }}
                numberOfLines={1}
              >
                {f.etiqueta}
              </Text>
            </Pressable>
          );
        })}
      </View>

      <View style={{ flexDirection: 'row', gap: 8 }}>
        {FILTROS.slice(3).map((f) => {
          const sel = f.id === seleccionado;

          return (
            <Pressable
              key={f.id}
              onPress={() => onSelect(f.id)}
              style={{
                flex: 1,
                alignItems: 'center',
                justifyContent: 'center',
                borderRadius: 999,
                paddingHorizontal: 8,
                paddingVertical: 9,
                backgroundColor: sel
                  ? tema.primary
                  : tema.surfaceContainerLowest,
              }}
            >
              <Text
                style={{
                  fontSize: 13,
                  fontWeight: '600',
                  color: sel ? tema.onPrimary : tema.onSurface,
                }}
                numberOfLines={1}
              >
                {f.etiqueta}
              </Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}