import { Redirect, Stack } from 'expo-router';
import { useAuth } from '@/context/AuthContext';

export default function AuthLayout() {
  const { usuario, cargando } = useAuth();
  if (cargando) return null;
  if (usuario) return <Redirect href="/" />;
  return <Stack screenOptions={{ headerShown: false }} />;
}
