import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import Pantalla from '@/components/ui/Pantalla';
import Campo from '@/components/ui/Campo';
import Boton from '@/components/ui/Boton';
import { useAuth } from '@/context/AuthContext';
import { mensajeError } from '@/api/errores';
import { colors, font } from '@/constants/tokens';

export default function Login() {
  const { iniciarSesion } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [cargando, setCargando] = useState(false);

  const entrar = async () => {
    setError('');
    setCargando(true);
    try {
      await iniciarSesion(email, password);
    } catch (e) {
      setError(mensajeError(e));
    } finally {
      setCargando(false);
    }
  };

  return (
    <Pantalla>
      <View style={{ marginTop: 40, marginBottom: 12 }}>
        <Text style={s.etiqueta}>LOCKER INTELIGENTE</Text>
        <Text style={s.titulo}>Bienvenido de nuevo</Text>
      </View>
      <Campo label="Correo" keyboardType="email-address" value={email} onChangeText={setEmail} />
      <Campo label="Contraseña" secureTextEntry value={password} onChangeText={setPassword} />
      {!!error && <Text style={s.error}>{error}</Text>}
      <Boton titulo="Entrar" onPress={entrar} cargando={cargando} />
      <Pressable onPress={() => router.push('/registro')}>
        <Text style={s.link}>¿No tienes cuenta? Regístrate</Text>
      </Pressable>
    </Pantalla>
  );
}

const s = StyleSheet.create({
  etiqueta: { fontFamily: font.medium, fontSize: 11, letterSpacing: 1.5, color: colors.textoSuave },
  titulo: { fontFamily: font.bold, fontSize: 30, color: colors.texto },
  error: { fontFamily: font.regular, color: colors.alerta },
  link: { fontFamily: font.medium, color: colors.verdeOscuro, textAlign: 'center', marginTop: 8 },
});
