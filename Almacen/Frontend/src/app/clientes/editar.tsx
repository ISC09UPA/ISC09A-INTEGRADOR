import { useLocalSearchParams } from 'expo-router';

import { EditarTercero, Terceros } from '@/components/terceros';

export default function Editar() {
  const { id } = useLocalSearchParams<{ id?: string }>();
  return <EditarTercero config={Terceros.clientes} id={id} />;
}
