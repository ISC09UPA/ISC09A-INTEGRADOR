
import { NativeTabs } from 'expo-router/unstable-native-tabs';
import { useColorScheme } from 'nativewind';

import { colores } from '@/theme/tokens';

export default function AppTabs() {
  const { colorScheme } = useColorScheme();

  const tema =
    colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <NativeTabs
      backgroundColor={tema.surface}
      indicatorColor={tema.primaryContainer}
      labelStyle={{
        selected: {
          color: tema.primary,
        },
      }}
    >
      <NativeTabs.Trigger name="index">
        <NativeTabs.Trigger.Label>
          Inicio
        </NativeTabs.Trigger.Label>

        <NativeTabs.Trigger.Icon
          src={require('@/assets/images/tabIcons/home.png')}
          renderingMode="template"
        />
      </NativeTabs.Trigger>

      <NativeTabs.Trigger name="explore">
        <NativeTabs.Trigger.Label>
          Explorar
        </NativeTabs.Trigger.Label>

        <NativeTabs.Trigger.Icon
          src={require('@/assets/images/tabIcons/explore.png')}
          renderingMode="template"
        />
      </NativeTabs.Trigger>
    </NativeTabs>
  );
}
