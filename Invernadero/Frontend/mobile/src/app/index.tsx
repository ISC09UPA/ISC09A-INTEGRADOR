import { Redirect } from 'expo-router';
import { useSesion } from '../../store/useSesionStore';

/** Puerta de sesión: manda a login o a las pestañas según haya sesión guardada. */
export default function Entrada() {
  const { usuario, inicializando } = useSesion();

  if (inicializando) return null;
  if (!usuario) return <Redirect href="/(auth)/login" />;
  return <Redirect href="/(tabs)/inicio" />;
}
