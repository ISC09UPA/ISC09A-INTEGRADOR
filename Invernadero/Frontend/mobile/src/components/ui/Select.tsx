import { MaterialIcons } from '@expo/vector-icons';
import { Modal, Pressable, Text, View } from 'react-native';
import { useState } from 'react';
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

export function Select<T extends string>({ valor, opciones, onChange, prefijo }: Props<T>) {
  const [abierto, setAbierto] = useState(false);
  const activa = opciones.find((o) => o.valor === valor) ?? opciones[0];

  return (
    <View className="flex-1">
      <Pressable
        onPress={() => setAbierto(true)}
        accessibilityRole="button"
        accessibilityLabel={`Filtro: ${activa.etiqueta}`}
        className="flex-row items-center justify-between rounded-xl bg-surface-container-low px-3 py-2 shadow-sm min-h-[44px]"
      >
        <Text className="text-label-md text-on-surface" numberOfLines={1}>
          {prefijo ? `${prefijo} ` : ''}
          {activa.etiqueta}
        </Text>
        <MaterialIcons name="expand-more" size={18} color={colores.onSurfaceVariant} />
      </Pressable>

      <Modal visible={abierto} transparent animationType="fade" onRequestClose={() => setAbierto(false)}>
        <Pressable className="flex-1 items-center justify-center bg-black/50 px-6" onPress={() => setAbierto(false)}>
          <View className="w-full max-w-[300px] overflow-hidden rounded-2xl bg-surface-container-lowest p-1">
            {opciones.map((o) => (
              <Pressable
                key={o.valor}
                onPress={() => {
                  onChange(o.valor);
                  setAbierto(false);
                }}
                className={`min-h-[44px] flex-row items-center justify-between rounded-xl px-3 py-2.5 ${o.valor === valor ? 'bg-primary-container' : ''}`}
                accessibilityRole="button"
              >
                <Text className={`text-body-md ${o.valor === valor ? 'font-semibold text-on-primary-container' : 'text-on-surface'}`}>
                  {o.etiqueta}
                </Text>
                {o.valor === valor && <MaterialIcons name="check" size={18} color={colores.onPrimaryContainer} />}
              </Pressable>
            ))}
          </View>
        </Pressable>
      </Modal>
    </View>
  );
}
