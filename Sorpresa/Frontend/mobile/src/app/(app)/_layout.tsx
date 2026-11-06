import { Redirect, Tabs } from 'expo-router';
import { Feather } from '@expo/vector-icons';
import { useAuth } from '@/context/AuthContext';
import { colors, font } from '@/constants/tokens';

type Icono = keyof typeof Feather.glyphMap;

// El color del icono depende de si la pestaña está activa (sin usar el color que pasa el navegador)
const tab = (title: string, icono: Icono) => ({
  title,
  tabBarIcon: ({ focused, size }: { focused: boolean; size: number }) => (
    <Feather name={icono} size={size} color={focused ? colors.verdeOscuro : colors.textoSuave} />
  ),
});

export default function AppLayout() {
  const { usuario, cargando } = useAuth();
  if (cargando) return null;
  if (!usuario) return <Redirect href="/login" />;

  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: colors.verdeOscuro,
        tabBarInactiveTintColor: colors.textoSuave,
        tabBarStyle: { backgroundColor: colors.fondo, borderTopColor: colors.borde },
        tabBarLabelStyle: { fontFamily: font.medium, fontSize: 11 },
      }}
    >
      <Tabs.Screen name="index" options={tab('Inicio', 'home')} />
      <Tabs.Screen name="lockers" options={tab('Lockers', 'archive')} />
      <Tabs.Screen name="actividad" options={tab('Actividad', 'bell')} />
      <Tabs.Screen name="perfil" options={tab('Perfil', 'user')} />
    </Tabs>
  );
}
