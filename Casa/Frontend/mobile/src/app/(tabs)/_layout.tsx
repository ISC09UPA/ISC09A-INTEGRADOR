import { LinearGradient } from 'expo-linear-gradient';
import { Tabs } from 'expo-router/js-tabs';
import { StyleSheet, View } from 'react-native';

import { TabBar } from '@/components';
import { Gradients } from '@/theme';

export default function TabsLayout() {
  return (
    <View style={styles.container}>
      {/* Sky gradient painted once behind every tab so the glass tab bar can blur it. */}
      <LinearGradient colors={[...Gradients.screen]} style={StyleSheet.absoluteFill} />
      <Tabs
        tabBar={(props) => <TabBar {...props} />}
        screenOptions={{
          headerShown: false,
          sceneStyle: { backgroundColor: 'transparent' },
        }}>
        <Tabs.Screen name="index" options={{ title: 'Inicio' }} />
        <Tabs.Screen name="luces" options={{ title: 'Luces' }} />
        <Tabs.Screen name="seguridad" options={{ title: 'Seguridad' }} />
        <Tabs.Screen name="camara" options={{ title: 'Cámara' }} />
        <Tabs.Screen name="actividad" options={{ title: 'Actividad' }} />
      </Tabs>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
});
