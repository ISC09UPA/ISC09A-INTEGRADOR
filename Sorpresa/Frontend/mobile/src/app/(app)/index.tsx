import Pantalla from '@/components/ui/Pantalla';
import Encabezado from '@/components/Encabezado';
import RoleToggle from '@/components/RoleToggle';
import UserHome from '@/components/home/UserHome';
import AdminHome from '@/components/home/AdminHome';
import { useModo } from '@/context/ModoContext';
import { useLockers } from '@/hooks/useLockers';

export default function Inicio() {
  const { modo, esAdmin } = useModo();
  const datos = useLockers();

  return (
    <Pantalla onRefrescar={datos.recargar}>
      <Encabezado etiqueta={modo === 'admin' ? 'PANEL DE CONTROL' : 'MI ESPACIO SEGURO'} />
      {esAdmin && <RoleToggle />}
      {modo === 'admin' ? <AdminHome {...datos} /> : <UserHome {...datos} />}
    </Pantalla>
  );
}
