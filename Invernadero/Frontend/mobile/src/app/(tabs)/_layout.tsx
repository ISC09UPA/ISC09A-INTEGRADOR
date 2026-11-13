import { colores } from '@/theme/tokens';
import { MaterialIcons } from '@expo/vector-icons';
import { Tabs, router, usePathname } from 'expo-router';
import { useColorScheme } from 'nativewind';
import type { ComponentProps } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { StatusPill } from '@/components/ui/StatusPill';
import { BotonMicrofono } from '@/components/voz/BotonMicrofono';
import { useDispositivo } from '@/hooks/useTelemetria';
import { VozProvider } from '@/voz/VozProvider';

type NombreIcono = ComponentProps<typeof MaterialIcons>['name'];

function Icono({ nombre, color }: { nombre: NombreIcono; color: string }) {
  return <MaterialIcons name={nombre} size={24} color={color} />;
}

const TITULOS: Record<string, string> = {
  inicio: 'Inicio',
  timeline: 'Timeline',
  voz: 'Voz',
  camara: 'Cámara',
  reglas: 'Reglas',
};

function HeaderPersonalizado() {
  const insets = useSafeAreaInsets();
  const pathname = usePathname();
  const dispositivo = useDispositivo();
  const { colorScheme } = useColorScheme();

  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const online = dispositivo.data?.online ?? false;
  const pantalla = pathname.split('/').filter(Boolean).pop() ?? 'inicio';
  const tituloPantalla = TITULOS[pantalla] ?? 'RiegoSmart IA';

  return (
    <View
      className="flex-row items-center justify-between px-gutter-mobile"
      style={{
        paddingTop: insets.top,
        height: 64 + insets.top,
        backgroundColor: tema.surface,
      }}
    >
      <View className="flex-row items-center gap-space-xs">
        <Pressable
          onPress={() => router.push('/opciones')}
          accessibilityRole="button"
          accessibilityLabel="Abrir opciones"
          className="h-10 w-10 items-center justify-center rounded-xl"
          style={{ backgroundColor: tema.primaryContainer }}
        >
          <MaterialIcons
            name="settings"
            size={22}
            color={tema.primary}
          />
        </Pressable>

        <View className="flex flex-col">
          <Text
            className="text-title-sm"
            style={{
              fontWeight: '600',
              color: tema.onSurface,
            }}
          >
            RiegoSmart IA
          </Text>

          <Text
            className="text-label-sm"
            style={{
              fontWeight: '500',
              color: tema.onSurfaceVariant,
            }}
          >
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

export default function LayoutPestanas() {
  const { colorScheme } = useColorScheme();

  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <VozProvider>
    <Tabs
      screenOptions={{
        header: () => <HeaderPersonalizado />,
        headerShown: true,

        tabBarActiveTintColor: tema.primary,
        tabBarInactiveTintColor: tema.onSurfaceVariant,
        tabBarLabelStyle: {
          fontSize: 11,
          fontWeight: '500',
        },
        tabBarStyle: {
          height: 64,
          paddingBottom: 8,
          paddingTop: 6,
          backgroundColor: tema.surface,
        },
        sceneStyle: {
          backgroundColor: tema.surface,
        },
      }}
    >
      <Tabs.Screen
        name="inicio"
        options={{
          title: 'Inicio',
          tabBarIcon: ({ color }) => (
            <Icono nombre="home" color={String(color)} />
          ),
        }}
      />

      <Tabs.Screen
        name="timeline"
        options={{
          title: 'Timeline',
          tabBarIcon: ({ color }) => (
            <Icono nombre="timeline" color={String(color)} />
          ),
        }}
      />

      {/* Voz: el botón de micrófono global ocupa el centro de la barra. */}
      <Tabs.Screen
        name="voz"
        options={{
          title: 'Voz',
          tabBarButton: () => <BotonMicrofono />,
        }}
      />

      <Tabs.Screen
        name="camara"
        options={{
          title: 'Cámara',
          tabBarIcon: ({ color }) => (
            <Icono nombre="photo-camera" color={String(color)} />
          ),
        }}
      />

      <Tabs.Screen
        name="reglas"
        options={{
          title: 'Reglas',
          tabBarIcon: ({ color }) => (
            <Icono nombre="smart-toy" color={String(color)} />
          ),
        }}
      />
    </Tabs>
    </VozProvider>
  );
}
