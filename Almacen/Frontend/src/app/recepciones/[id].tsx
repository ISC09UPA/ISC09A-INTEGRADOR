import { useLocalSearchParams } from 'expo-router';

import { DetalleDocumento, Documentos } from '@/components/documentos';

export default function Detalle() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <DetalleDocumento config={Documentos.recepciones} id={id} />;
}
