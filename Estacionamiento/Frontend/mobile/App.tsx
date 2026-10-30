import React, { useState } from 'react';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { SplashScreen } from './src/features/auth/SplashScreen';
import { LoginScreen } from './src/features/auth/LoginScreen';
import { RegisterScreen } from './src/features/auth/RegisterScreen';
import { TabNavigator } from './src/navigation/TabNavigator';
import { AdminWorkspaceScreen } from './src/features/admin/AdminWorkspaceScreen';
import { useAuthStore } from './src/store/authStore';

export default function App() {
  const [isSplashDone, setIsSplashDone] = useState(false);
  const [authScreen, setAuthScreen] = useState<'login' | 'register'>('login');
  const { isAuthenticated, role } = useAuthStore();

  return (
    <SafeAreaProvider>
      <StatusBar style="dark" translucent />
      {!isSplashDone ? (
        <SplashScreen onFinish={() => setIsSplashDone(true)} />
      ) : !isAuthenticated ? (
        authScreen === 'login' ? (
          <LoginScreen onNavigateToRegister={() => setAuthScreen('register')} />
        ) : (
          <RegisterScreen onNavigateToLogin={() => setAuthScreen('login')} />
        )
      ) : role === 'ADMIN' ? (
        <AdminWorkspaceScreen />
      ) : (
        <TabNavigator />
      )}
    </SafeAreaProvider>
  );
}
