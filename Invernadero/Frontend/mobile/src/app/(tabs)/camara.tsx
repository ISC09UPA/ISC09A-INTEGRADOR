import { colores } from '@/theme/tokens';
import { CameraView, type CameraType, useCameraPermissions } from 'expo-camera';
import { useIsFocused } from 'expo-router';
import { useRef, useState } from 'react';
import {
  ActivityIndicator,
  Image,
  Linking,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';

export default function Camara() {
  const [permission, requestPermission] = useCameraPermissions();
  const [facing, setFacing] = useState<CameraType>('back');
  const [fotoUri, setFotoUri] = useState<string | null>(null);
  const [camaraLista, setCamaraLista] = useState(false);
  const [capturando, setCapturando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const cameraRef = useRef<CameraView>(null);
  const estaEnfocada = useIsFocused();

  const tomarFoto = async () => {
    if (!cameraRef.current || capturando) return;

    setCapturando(true);
    setError(null);
    try {
      const foto = await cameraRef.current.takePictureAsync({ quality: 0.85 });
      setFotoUri(foto.uri);
    } catch (e) {
      console.error('No se pudo tomar la foto.', e);
      setError('No se pudo tomar la foto. Comprueba que la cámara esté disponible e inténtalo de nuevo.');
    } finally {
      setCapturando(false);
    }
  };

  const solicitarPermiso = async () => {
    try {
      await requestPermission();
    } catch (e) {
      console.error('No se pudo solicitar el permiso de cámara.', e);
      setError('No se pudo solicitar el permiso. Revisa los ajustes de la aplicación e inténtalo de nuevo.');
    }
  };

  const cambiarCamara = () => {
    setCamaraLista(false);
    setFacing((actual) => (actual === 'back' ? 'front' : 'back'));
  };

  const abrirAjustes = async () => {
    try {
      await Linking.openSettings();
    } catch (e) {
      console.error('No se pudieron abrir los ajustes de la aplicación.', e);
      setError('No se pudieron abrir los ajustes. Habilita el permiso de cámara desde la configuración del dispositivo.');
    }
  };

  if (!estaEnfocada) {
    return <View style={styles.pantalla} />;
  }

  if (!permission) {
    return (
      <View style={[styles.pantalla, styles.centrado]}>
        <ActivityIndicator color={colores.primary} />
        <Text style={styles.textoAyuda}>Comprobando permiso de cámara…</Text>
      </View>
    );
  }

  if (!permission.granted) {
    const puedeSolicitar = permission.canAskAgain;

    return (
      <View style={[styles.pantalla, styles.centrado, styles.contenidoPermiso]}>
        <MaterialIcons name="no-photography" size={52} color={colores.onSurfaceVariant} />
        <Text style={styles.titulo}>Permiso de cámara</Text>
        <Text style={styles.textoAyuda}>
          {puedeSolicitar
            ? 'Permite el acceso a la cámara para tomar una foto de tu planta.'
            : Platform.OS === 'web'
              ? 'El acceso está bloqueado. Habilita la cámara desde los permisos del sitio en tu navegador y vuelve a cargar la página.'
              : 'El acceso está bloqueado. Habilita el permiso de cámara en los ajustes de la aplicación.'}
        </Text>
        {error ? (
          <Text accessibilityRole="alert" style={styles.textoErrorPermiso}>
            {error}
          </Text>
        ) : null}
        {puedeSolicitar ? (
          <Pressable
            accessibilityRole="button"
            onPress={() => void solicitarPermiso()}
            style={({ pressed }) => [styles.botonPrincipal, pressed && styles.botonPresionado]}
          >
            <Text style={styles.textoBotonPrincipal}>Permitir cámara</Text>
          </Pressable>
        ) : Platform.OS !== 'web' ? (
          <Pressable
            accessibilityRole="button"
            onPress={() => void abrirAjustes()}
            style={({ pressed }) => [styles.botonPrincipal, pressed && styles.botonPresionado]}
          >
            <Text style={styles.textoBotonPrincipal}>Abrir ajustes</Text>
          </Pressable>
        ) : null}
      </View>
    );
  }

  return (
    <View style={styles.pantalla}>
      <View style={styles.encabezado}>
        <Text style={styles.titulo}>{fotoUri ? 'Vista previa' : 'Reconoce tu planta'}</Text>
        <Text style={styles.textoAyuda}>
          {fotoUri ? 'La foto se queda en este dispositivo.' : 'Centra la planta y toma una foto.'}
        </Text>
      </View>

      <View style={styles.marcoCamara}>
        {fotoUri ? (
          <Image source={{ uri: fotoUri }} style={styles.vistaCamara} resizeMode="cover" />
        ) : (
          <CameraView
            ref={cameraRef}
            style={styles.vistaCamara}
            facing={facing}
            mode="picture"
            onCameraReady={() => setCamaraLista(true)}
            onMountError={(event) => {
              console.error('No se pudo iniciar la cámara.', event.message);
              setError('No se pudo iniciar la cámara. Cierra otras aplicaciones que la estén usando e inténtalo de nuevo.');
            }}
          />
        )}
        {error ? (
          <View accessibilityRole="alert" style={styles.avisoError}>
            <Text style={styles.textoError}>{error}</Text>
          </View>
        ) : null}
      </View>

      <View style={styles.controles}>
        {fotoUri ? (
          <Pressable
            accessibilityRole="button"
            onPress={() => {
              setError(null);
              setFotoUri(null);
              setCamaraLista(false);
            }}
            style={({ pressed }) => [styles.botonSecundario, pressed && styles.botonPresionado]}
          >
            <MaterialIcons name="replay" size={20} color={colores.primary} />
            <Text style={styles.textoBotonSecundario}>Tomar otra</Text>
          </Pressable>
        ) : (
          <>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Cambiar cámara"
              onPress={cambiarCamara}
              style={({ pressed }) => [styles.botonIcono, pressed && styles.botonPresionado]}
            >
              <MaterialIcons name="flip-camera-android" size={24} color={colores.onSurface} />
            </Pressable>
            <Pressable
              accessibilityRole="button"
              onPress={() => void tomarFoto()}
              disabled={!camaraLista || capturando}
              style={({ pressed }) => [
                styles.botonPrincipal,
                (!camaraLista || capturando) && styles.botonDeshabilitado,
                pressed && styles.botonPresionado,
              ]}
            >
              {capturando ? (
                <ActivityIndicator color={colores.onPrimary} />
              ) : (
                <>
                  <MaterialIcons name="camera-alt" size={20} color={colores.onPrimary} />
                  <Text style={styles.textoBotonPrincipal}>Tomar foto</Text>
                </>
              )}
            </Pressable>
            <View style={styles.espaciadorControl} />
          </>
        )}
      </View>
      <Text style={styles.nota}>El reconocimiento de plantas todavía no está conectado.</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  pantalla: { flex: 1, backgroundColor: colores.surface, padding: 16 },
  centrado: { alignItems: 'center', justifyContent: 'center' },
  contenidoPermiso: { gap: 16, paddingHorizontal: 28 },
  encabezado: { gap: 4, paddingBottom: 12 },
  titulo: { fontSize: 20, fontWeight: '600', color: colores.onSurface },
  textoAyuda: { fontSize: 14, lineHeight: 20, color: colores.onSurfaceVariant, textAlign: 'center' },
  textoErrorPermiso: { color: colores.onErrorContainer, fontSize: 13, textAlign: 'center' },
  marcoCamara: {
    flex: 1,
    minHeight: 240,
    overflow: 'hidden',
    borderRadius: 20,
    backgroundColor: colores.surfaceContainerHighest,
  },
  vistaCamara: { width: '100%', height: '100%' },
  avisoError: {
    position: 'absolute',
    left: 12,
    right: 12,
    bottom: 12,
    borderRadius: 10,
    backgroundColor: colores.errorContainer,
    padding: 12,
  },
  textoError: { color: colores.onErrorContainer, fontSize: 13 },
  controles: {
    minHeight: 76,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 16,
    paddingVertical: 12,
  },
  botonPrincipal: {
    minHeight: 48,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    borderRadius: 999,
    backgroundColor: colores.primary,
    paddingHorizontal: 24,
  },
  textoBotonPrincipal: { color: colores.onPrimary, fontSize: 15, fontWeight: '600' },
  botonSecundario: {
    minHeight: 48,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    borderRadius: 999,
    backgroundColor: colores.primaryContainer,
    paddingHorizontal: 24,
  },
  textoBotonSecundario: { color: colores.onPrimaryContainer, fontSize: 15, fontWeight: '600' },
  botonIcono: {
    width: 48,
    height: 48,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 24,
    backgroundColor: colores.surfaceContainer,
  },
  botonPresionado: { opacity: 0.75 },
  botonDeshabilitado: { opacity: 0.55 },
  espaciadorControl: { width: 48, height: 48 },
  nota: { paddingBottom: 4, color: colores.onSurfaceVariant, fontSize: 12, textAlign: 'center' },
});
