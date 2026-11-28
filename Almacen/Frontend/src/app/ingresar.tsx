import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native';

import { API_URL } from '@/api/http-client';
import { Aviso, Boton, Campo, Texto, mensajeDe } from '@/components/ui';
import { Radio, Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { useSesion } from '@/lib/sesion';

/** Inicio de sesión: los mismos usuarios, roles y permisos que la web (Cuenta/Ingresar.razor). */
export default function Ingresar() {
  const c = useColores();
  const { ingresar } = useSesion();
  const [correo, setCorreo] = useState('');
  const [contrasena, setContrasena] = useState('');
  const [verContrasena, setVerContrasena] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [entrando, setEntrando] = useState(false);

  const entrar = async () => {
    setError(null);
    if (!correo.trim() || !/^\S+@\S+\.\S+$/.test(correo.trim())) {
      setError('Escribe un correo válido.');
      return;
    }
    if (!contrasena) {
      setError('Escribe tu contraseña.');
      return;
    }
    setEntrando(true);
    try {
      await ingresar(correo, contrasena);
    } catch (e) {
      setError(mensajeDe(e));
      setEntrando(false);
    }
  };

  return (
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1, backgroundColor: c.drawer }}>
      <ScrollView contentContainerStyle={estilos.fondo} keyboardShouldPersistTaps="handled">
        <View style={[estilos.tarjeta, { backgroundColor: c.surface }]}>
          <View style={estilos.marca}>
            <View style={[estilos.icono, { backgroundColor: c.primary }]}>
              <Text style={{ color: c.onPrimary, fontWeight: '700' }}>AI</Text>
            </View>
            <Texto tipo="fuerte">Almacén inteligente</Texto>
          </View>
          <Texto tipo="titulo">Iniciar sesión</Texto>
          <Texto tipo="secundario">Entra con tu correo y contraseña.</Texto>

          {error ? <Aviso tipo="error" texto={error} /> : null}

          <Campo
            etiqueta="Correo"
            value={correo}
            onChangeText={setCorreo}
            autoCapitalize="none"
            autoComplete="email"
            keyboardType="email-address"
            textContentType="username"
            autoFocus
          />
          <View style={{ gap: 4 }}>
            <Campo
              etiqueta="Contraseña"
              value={contrasena}
              onChangeText={setContrasena}
              secureTextEntry={!verContrasena}
              autoComplete="current-password"
              textContentType="password"
              onSubmitEditing={entrar}
              returnKeyType="go"
            />
            <Boton
              texto={verContrasena ? 'Ocultar contraseña' : 'Mostrar contraseña'}
              icono={verContrasena ? 'visibility-off' : 'visibility'}
              variante="texto"
              alPresionar={() => setVerContrasena(!verContrasena)}
              style={{ alignSelf: 'flex-start' }}
            />
          </View>
          <Boton texto={entrando ? 'Entrando…' : 'Entrar'} cargando={entrando} alPresionar={entrar} />
          <Texto tipo="pequeno" style={{ textAlign: 'center' }}>
            API: {API_URL}
          </Texto>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const estilos = StyleSheet.create({
  fondo: {
    flexGrow: 1,
    justifyContent: 'center',
    padding: Spacing.three,
  },
  tarjeta: {
    width: '100%',
    maxWidth: 420,
    alignSelf: 'center',
    borderRadius: 12,
    padding: Spacing.four,
    gap: Spacing.three,
  },
  marca: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
  },
  icono: {
    width: 36,
    height: 36,
    borderRadius: Radio,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
