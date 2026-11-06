import { useEffect } from 'react';
import { Slot } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import * as SplashScreen from 'expo-splash-screen';
import { useFonts, DMSans_400Regular, DMSans_500Medium, DMSans_700Bold } from '@expo-google-fonts/dm-sans';
import { AuthProvider } from '@/context/AuthContext';
import { ModoProvider } from '@/context/ModoContext';

SplashScreen.preventAutoHideAsync();

export default function RootLayout() {
  const [fuentesListas] = useFonts({ DMSans_400Regular, DMSans_500Medium, DMSans_700Bold });

  useEffect(() => {
    if (fuentesListas) SplashScreen.hideAsync();
  }, [fuentesListas]);

  if (!fuentesListas) return null;

  return (
    <AuthProvider>
      <ModoProvider>
        <StatusBar style="dark" />
        <Slot />
      </ModoProvider>
    </AuthProvider>
  );
}
