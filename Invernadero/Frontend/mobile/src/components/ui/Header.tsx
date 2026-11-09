import { MaterialIcons } from '@expo/vector-icons';
import { Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { colores } from '@/theme/tokens';
import { StatusPill } from './StatusPill';

interface Props {
  tituloPantalla: string;
  online: boolean;
}

export function Header({ tituloPantalla, online }: Props) {
  const insets = useSafeAreaInsets();

  return (
    <View
      className="flex-row items-center justify-between px-gutter-mobile"
      style={{ paddingTop: insets.top, height: 64 + insets.top }}
    >
      <View className="flex-row items-center gap-space-xs">
        <View className="h-10 w-10 items-center justify-center rounded-xl bg-primary-container">
          <MaterialIcons name="eco" size={22} color={colores.primary} />
        </View>
        <View className="flex flex-col">
          <Text className="text-title-sm text-on-surface" style={{ fontWeight: '600' }}>
            RiegoSmart IA
          </Text>
          <Text className="text-label-sm text-on-surface-variant" style={{ fontWeight: '500' }}>
            {tituloPantalla}
          </Text>
        </View>
      </View>
      <View className="flex-row items-center gap-space-sm">
        <StatusPill online={online} />
      </View>
    </View>
  );
}