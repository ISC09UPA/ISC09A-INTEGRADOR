import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import Pantalla from '@/components/ui/Pantalla';
import Campo from '@/components/ui/Campo';
import Boton from '@/components/ui/Boton';
import { useAuth } from '@/context/AuthContext';
import { mensajeError } from '@/api/errores';
import { colors, font } from '@/constants/tokens';

export default function Registro() {
  const { registrarse } = useAuth();
  const router = useRouter();
  const [nombre, setNombre] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [cargando, setCargando] = useState(false);

  const crear = async () => {
    setError('');
    setCargando(true);
    try {
      await registrarse(nombre, email, password);
    } catch (e) {
      setError(mensajeError(e));
    } finally {
      setCargando(false);
    }
  };

  return (
    <Pantalla>
      <View style={{ marginTop: 40, marginBottom: 12 }}>
        <Text style={s.titulo}>Crear cuenta</Text>
      </View>
      <Campo label="Nombre" autoCapitalize="words" value={nombre} onChangeText={setNombre} />
      <Campo label="Correo" keyboardType="email-address" value={email} onChangeText={setEmail} />
      <Campo label="Contraseña (mínimo 6)" secureTextEntry value={password} onChangeText={setPassword} />
      {!!error && <Text style={s.error}>{error}</Text>}
      <Boton titulo="Registrarme" onPress={crear} cargando={cargando} />
      <Pressable onPress={() => router.back()}>
        <Text style={s.link}>Ya tengo cuenta</Text>
      </Pressable>
    </Pantalla>
  );
}

const s = StyleSheet.create({
  titulo: { fontFamily: font.bold, fontSize: 30, color: colors.texto },
  error: { fontFamily: font.regular, color: colors.alerta },
  link: { fontFamily: font.medium, color: colors.verdeOscuro, textAlign: 'center', marginTop: 8 },
});
