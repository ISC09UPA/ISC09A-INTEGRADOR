import { useEffect } from 'react';
import { BackHandler, KeyboardAvoidingView, Platform, Pressable, ScrollView, Text, View } from 'react-native';
import { Redirect, useLocalSearchParams, useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useApp } from '../../core/state';
import { Button, Copy, Note, styles, Title } from './ui';
import * as Screens from './screens';

const titles: Record<string, string> = {
  home: 'Tu hogar, en calma.',
  login: 'Bienvenido',
  'accept-invitation': 'Aceptar invitación',
  'password-recovery': 'Recuperar contraseña',
  lights: 'Luces',
  security: 'Seguridad',
  camera: 'Cámara',
  activity: 'Actividad',
  more: 'Más opciones',
  persons: 'Personas autorizadas',
  person: 'Permiso de acceso',
  enrollment: 'Registrar persona',
  access: 'Accesos y puerta',
  captures: 'Capturas',
  capture: 'Captura privada',
  location: 'Ubicación',
  preferences: 'Avisos y preferencias',
  assistant: 'Asistente',
  settings: 'Configuración',
  members: 'Miembros de la casa',
  incident: 'Detalle de actividad',
  detail: 'Detalle',
};

const tabs = [
  ['home', 'Inicio', '⌂'],
  ['lights', 'Luces', '☼'],
  ['security', 'Seguridad', '◇'],
  ['camera', 'Cámara', '▣'],
  ['activity', 'Actividad', '◷'],
];

const publicScreens = new Set(['login', 'accept-invitation', 'password-recovery']);

export function AppScreen({ screen = 'home' }: { screen?: string }) {
  const app = useApp();
  const router = useRouter();
  const params = useLocalSearchParams<{ id?: string; kind?: string }>();
  const isPublic = publicScreens.has(screen);
  const isPrimary = tabs.some(([key]) => key === screen);

  useEffect(() => {
    if (Platform.OS !== 'android' || isPublic || screen === 'home') return;
    const sub = BackHandler.addEventListener('hardwareBackPress', () => {
      if (router.canGoBack()) {
        router.back();
        return true;
      }
      if (isPrimary) {
        router.replace('/');
        return true;
      }
      if (app.profile) {
        router.replace('/');
        return true;
      }
      return false;
    });
    return () => sub.remove();
  }, [app.profile, isPrimary, isPublic, router, screen]);

  if (app.restoring) return <SafeAreaView style={styles.page}><View style={styles.content}><Copy>Restaurando sesión…</Copy></View></SafeAreaView>;
  if (!app.restoring && !app.profile && !publicScreens.has(screen)) return <Redirect href="/login" />;
  if (!app.restoring && app.profile && screen === 'login') return <Redirect href="/" />;

  let body;
  switch (screen) {
    case 'home': body = <Screens.HomeScreen />; break;
    case 'login': body = <Screens.LoginScreen />; break;
    case 'accept-invitation': body = <Screens.AcceptInvitationScreen />; break;
    case 'password-recovery': body = <Screens.PasswordRecoveryScreen />; break;
    case 'lights': body = <Screens.LightsScreen />; break;
    case 'security': body = <Screens.SecurityScreen />; break;
    case 'camera': body = <Screens.CameraScreen />; break;
    case 'activity': case 'access': case 'captures': body = <Screens.ListScreen kind={screen} />; break;
    case 'persons': body = <Screens.PersonsScreen />; break;
    case 'person': body = <Screens.PersonDetailScreen id={params.id ?? ''} />; break;
    case 'members': body = <Screens.MembersScreen />; break;
    case 'capture': case 'incident': case 'detail': body = <Screens.DetailScreen id={params.id ?? ''} kind={params.kind ?? (screen === 'capture' ? 'captures' : 'activity')} />; break;
    case 'enrollment': body = <Screens.EnrollmentScreen />; break;
    case 'location': body = <Screens.LocationScreen />; break;
    case 'preferences': body = <Screens.PreferencesScreen />; break;
    case 'assistant': body = <Screens.AssistantScreen />; break;
    case 'settings': body = <Screens.SettingsScreen />; break;
    case 'more': body = <Screens.MoreScreen />; break;
    default: body = <Note>Esta pantalla no existe.</Note>;
  }

  return (
    <SafeAreaView style={styles.page}>
      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
        <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
          <View style={[styles.row, { justifyContent: 'space-between' }]}>
            {!isPrimary && screen !== 'login'
              ? <Button secondary label="← Volver" onPress={() => router.canGoBack() ? router.back() : router.replace(app.profile ? '/' : '/login')} />
              : <Copy muted>CASA DOMÓTICA</Copy>}
            {!isPublic && <Button secondary label="Más opciones" onPress={() => router.push('/more' as never)} />}
          </View>
          <Title>{titles[screen] ?? 'Casa Domótica'}</Title>
          {!isPublic && !!app.error && <Note error>{app.error}</Note>}
          {!isPublic && app.profile && app.reconciling && <Note>Reconciliando permisos y estado REST tras cambio en tiempo real; controles bloqueados brevemente.</Note>}
          {!isPublic && app.profile && !app.reconciling && app.stale && <Note>El estado no es reciente. Los controles de luces están bloqueados hasta consultar un reporte actualizado.</Note>}
          {body}
          {!isPublic && <Button secondary disabled={app.loading || !app.house} label={app.loading ? 'Actualizando…' : 'Actualizar estado de la casa'} onPress={() => void app.reload().catch(() => {})} />}
        </ScrollView>
        {!isPublic && (
          <View accessibilityRole="tablist" style={styles.nav}>
            {tabs.map(([key, label, icon]) => (
              <Pressable
                key={key}
                accessibilityRole="tab"
                accessibilityState={{ selected: key === screen }}
                accessibilityLabel={label}
                style={[styles.navItem, key === screen && { backgroundColor: '#E2F1EB' }]}
                onPress={() => router.replace((key === 'home' ? '/' : `/${key}`) as never)}
              >
                <Text style={{ color: '#087F78', fontSize: 23 }}>{icon}</Text>
                <Text style={{ color: key === screen ? '#087F78' : '#586971', fontSize: 11, textAlign: 'center' }}>{label}</Text>
              </Pressable>
            ))}
          </View>
        )}
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
