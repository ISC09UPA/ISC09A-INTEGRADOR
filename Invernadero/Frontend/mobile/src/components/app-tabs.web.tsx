
import React, { forwardRef } from 'react';
import {
  Tabs,
  TabList,
  TabTrigger,
  TabSlot,
  type TabTriggerSlotProps,
  type TabListProps,
} from 'expo-router/ui';

import {
  Pressable,
  Text,
  View,
  StyleSheet,
} from 'react-native';

import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

// Botón de cada pestaña
type TabButtonProps = Omit<TabTriggerSlotProps, 'ref'>;

export const TabButton = forwardRef<
  React.ElementRef<typeof Pressable>,
  TabButtonProps
>(({ children, isFocused, ...props }, ref) => {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <Pressable
      ref={ref}
      {...props}
      style={({ pressed }) => [
        styles.tabButton,
        {
          backgroundColor: isFocused
            ? tema.primaryContainer
            : 'transparent',
          opacity: pressed ? 0.7 : 1,
        },
      ]}
    >
      <Text
        style={{
          fontSize: 14,
          fontWeight: '600',
          color: isFocused
            ? tema.onPrimaryContainer
            : tema.onSurfaceVariant,
        }}
      >
        {children}
      </Text>
    </Pressable>
  );
});

TabButton.displayName = 'TabButton';

// Contenedor de las pestañas
export function CustomTabList(props: TabListProps) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <View
      {...props}
      style={[
        styles.tabListContainer,
        props.style,
      ]}
    >
      <View
        style={[
          styles.innerContainer,
          { backgroundColor: tema.surfaceContainer },
        ]}
      >
        {props.children}
      </View>
    </View>
  );
}

// Navegación
export default function AppTabs() {
  return (
    <Tabs>
      <TabSlot style={{ height: '100%' }} />

      <TabList asChild>
        <CustomTabList>
          <TabTrigger name="home" href="/" asChild>
            <TabButton>Inicio</TabButton>
          </TabTrigger>

          <TabTrigger name="explore" href="/explore" asChild>
            <TabButton>Explorar</TabButton>
          </TabTrigger>
        </CustomTabList>
      </TabList>
    </Tabs>
  );
}

// Estilos
const styles = StyleSheet.create({
  tabListContainer: {
    position: 'absolute',
    width: '100%',
    padding: 12,
    justifyContent: 'center',
    alignItems: 'center',
    flexDirection: 'row',
  },
  innerContainer: {
    paddingVertical: 8,
    paddingHorizontal: 12,
    borderRadius: 20,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-around',
    flexGrow: 1,
    gap: 8,
    maxWidth: 500,
  },
  tabButton: {
    paddingVertical: 8,
    paddingHorizontal: 12,
    borderRadius: 12,
  },
});
