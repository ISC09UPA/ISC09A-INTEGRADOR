import '@/global.css';

import { Tabs } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { StyleSheet } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { useFonts, Inter_400Regular, Inter_500Medium, Inter_600SemiBold } from '@expo-google-fonts/inter';
import { TabBar } from '@/components/tab-bar';

export default function RootLayout() {
  const [fontsLoaded] = useFonts({ Inter_400Regular, Inter_500Medium, Inter_600SemiBold });

  if (!fontsLoaded) return null;

  return (
    <SafeAreaProvider>
      <StatusBar style="dark" />
      {/* El menú inferior es propio (TabBar) y navega entre las rutas de src/app. */}
      <Tabs tabBar={() => <TabBar />} screenOptions={{ headerShown: false, sceneStyle: styles.scene }} />
    </SafeAreaProvider>
  );
}

const styles = StyleSheet.create({
  scene: { backgroundColor: '#ECEEF7' },
});
