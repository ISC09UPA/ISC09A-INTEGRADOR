import { type ReactNode } from 'react';
import { ApplicationProvider } from '../core/state';
import { SafeAreaProvider } from 'react-native-safe-area-context';

export function AppProviders({ children }: { children: ReactNode }) {
  return <SafeAreaProvider><ApplicationProvider>{children}</ApplicationProvider></SafeAreaProvider>;
}
