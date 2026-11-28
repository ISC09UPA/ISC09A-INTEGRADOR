import { ListaTerceros, Terceros } from '@/components/terceros';

export default function Lista() {
  return <ListaTerceros config={Terceros.clientes} />;
}
