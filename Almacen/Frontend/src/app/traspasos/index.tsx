import { Documentos, ListaDocumentos } from '@/components/documentos';

export default function Lista() {
  return <ListaDocumentos config={Documentos.traspasos} />;
}
