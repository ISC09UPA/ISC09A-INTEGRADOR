import { MaterialIcons } from '@expo/vector-icons';
import { Modal, Pressable, Text, View } from 'react-native';
import { useState } from 'react';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

export interface Opcion<T extends string> {
  valor: T;
  etiqueta: string;
}

interface Props<T extends string> {
  valor: T;
  opciones: Opcion<T>[];
  onChange: (v: T) => void;
  prefijo?: string;
}

export function Select<T extends string>({
  valor,
  opciones,
  onChange,
  prefijo,
}: Props<T>) {
  const [abierto, setAbierto] = useState(false);
  const { colorScheme } = useColorScheme();

  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;
  const activa = opciones.find((o) => o.valor === valor) ?? opciones[0];

  return (
    <View className="flex-1">
      <Pressable
        onPress={() => setAbierto(true)}
        accessibilityRole="button"
        accessibilityLabel={`Filtro: ${activa.etiqueta}`}
        className="flex-row items-center justify-between rounded-xl px-3 py-2 shadow-sm min-h-[44px]"
        style={{ backgroundColor: tema.surfaceContainerLow }}
      >
        <Text
          className="text-label-md"
          style={{ color: tema.onSurface }}
          numberOfLines={1}
        >
          {prefijo ? `${prefijo} ` : ''}
          {activa.etiqueta}
        </Text>

        <MaterialIcons
          name="expand-more"
          size={18}
          color={tema.onSurfaceVariant}
        />
      </Pressable>

      <Modal
        visible={abierto}
        transparent
        animationType="fade"
        onRequestClose={() => setAbierto(false)}
      >
        <Pressable
          className="flex-1 items-center justify-center bg-black/50 px-6"
          onPress={() => setAbierto(false)}
        >
          <Pressable
            className="w-full max-w-[300px] overflow-hidden rounded-2xl p-1"
            style={{ backgroundColor: tema.surfaceContainerLowest }}
            onPress={(event) => event.stopPropagation()}
          >
            {opciones.map((o) => {
              const seleccionado = o.valor === valor;

              return (
                <Pressable
                  key={o.valor}
                  onPress={() => {
                    onChange(o.valor);
                    setAbierto(false);
                  }}
                  className="min-h-[44px] flex-row items-center justify-between rounded-xl px-3 py-2.5"
                  style={{
                    backgroundColor: seleccionado
                      ? tema.primaryContainer
                      : 'transparent',
                  }}
                  accessibilityRole="button"
                >
                  <Text
                    className="text-body-md"
                    style={{
                      color: seleccionado
                        ? tema.onPrimaryContainer
                        : tema.onSurface,
                      fontWeight: seleccionado ? '600' : '400',
                    }}
                  >
                    {o.etiqueta}
                  </Text>

                  {seleccionado && (
                    <MaterialIcons
                      name="check"
                      size={18}
                      color={tema.onPrimaryContainer}
                    />
                  )}
                </Pressable>
              );
            })}
          </Pressable>
        </Pressable>
      </Modal>
    </View>
  );
}