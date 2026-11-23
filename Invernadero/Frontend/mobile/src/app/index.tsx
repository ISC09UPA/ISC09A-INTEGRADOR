import { Redirect } from 'expo-router';
import { useSesion } from '../../store/useSesionStore';

/** Puerta de sesión: manda a login o a las pestañas según haya sesión guardada. */
export default function Entrada() {
  const usuario = useSesion((s) => s.usuario);
  const inicializando = useSesion((s) => s.inicializando);

  if (inicializando) return null;

  return <Redirect href={usuario ? '/(tabs)/inicio' : '/(auth)/login'} />;
}
