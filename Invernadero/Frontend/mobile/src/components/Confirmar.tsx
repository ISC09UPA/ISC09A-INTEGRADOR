import { Modal, Pressable, Text, View } from 'react-native';

interface Props {
  visible: boolean;
  titulo: string;
  descripcion: string;
  textoConfirmar: string;
  textoCancelar?: string;
  peligroso?: boolean;
  onCancelar: () => void;
  onConfirmar: () => void;
}

export function Confirmar({ visible, titulo, descripcion, textoConfirmar, textoCancelar = 'Cancelar', peligroso = false, onCancelar, onConfirmar }: Props) {
  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onCancelar}>
      <View className="flex-1 items-center justify-center bg-black/50 px-gutter-mobile">
        <View className="w-full rounded-2xl bg-surface-container-lowest p-space-md" style={{ maxWidth: 340 }}>
          <Text className="text-title-md text-on-surface" style={{ fontWeight: '600' }}>{titulo}</Text>
          <Text className="mt-1.5 text-body-md text-on-surface-variant">{descripcion}</Text>
          <View className="mt-space-md flex-row gap-2">
            <Pressable
              onPress={onCancelar}
              accessibilityRole="button"
              className="min-h-[48px] flex-1 items-center justify-center rounded-xl bg-surface-container py-3"
            >
              <Text className="text-label-lg text-on-surface" style={{ fontWeight: '600' }}>{textoCancelar}</Text>
            </Pressable>
            <Pressable
              onPress={onConfirmar}
              accessibilityRole="button"
              className={`min-h-[48px] flex-1 items-center justify-center rounded-xl py-3 ${peligroso ? 'bg-error' : 'bg-primary'}`}
            >
              <Text className="text-label-lg text-on-primary" style={{ fontWeight: '600' }}>{textoConfirmar}</Text>
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}
