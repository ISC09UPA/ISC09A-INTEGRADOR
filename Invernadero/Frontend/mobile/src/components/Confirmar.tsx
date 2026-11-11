
import { Modal, Pressable, Text, View } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

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

export function Confirmar({
  visible,
  titulo,
  descripcion,
  textoConfirmar,
  textoCancelar = 'Cancelar',
  peligroso = false,
  onCancelar,
  onConfirmar,
}: Props) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={onCancelar}
    >
      <View className="flex-1 items-center justify-center bg-black/50 px-6">
        <View
          className="w-full rounded-2xl p-4"
          style={{
            maxWidth: 340,
            backgroundColor: tema.surfaceContainerLowest,
          }}
        >
          <Text style={{ fontSize: 18, fontWeight: '600', color: tema.onSurface }}>
            {titulo}
          </Text>

          <Text style={{ marginTop: 6, fontSize: 14, color: tema.onSurfaceVariant }}>
            {descripcion}
          </Text>

          <View className="mt-4 flex-row gap-2">
            <Pressable
              onPress={onCancelar}
              accessibilityRole="button"
              className="min-h-12 flex-1 items-center justify-center rounded-xl py-3"
              style={{ backgroundColor: tema.surfaceContainer }}
            >
              <Text style={{ fontSize: 14, fontWeight: '600', color: tema.onSurface }}>
                {textoCancelar}
              </Text>
            </Pressable>

            <Pressable
              onPress={onConfirmar}
              accessibilityRole="button"
              className="min-h-12 flex-1 items-center justify-center rounded-xl py-3"
              style={{ backgroundColor: peligroso ? tema.error : tema.primary }}
            >
              <Text style={{ fontSize: 14, fontWeight: '600', color: peligroso ? tema.onError : tema.onPrimary }}>
                {textoConfirmar}
              </Text>
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}
