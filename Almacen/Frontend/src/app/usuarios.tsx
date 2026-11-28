import { useState } from 'react';
import { View } from 'react-native';

import type { Usuario } from '@/models/usuario.model';
import { usuariosService } from '@/services';
import { Aviso, Boton, Busqueda, Campo, Cargando, Chip, Encabezado, Fila, Hoja, Interruptor, Pantalla, Selector, Tarjeta, Texto, Vacio, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useDatos } from '@/hooks/use-datos';
import { Roles, nombreRol } from '@/lib/permisos';

const OPCIONES_ROL = Roles.map((r) => ({ valor: r as string, texto: nombreRol(r) }));

/** Cuentas y roles (Usuarios.razor). Solo el superadmin. */
export default function Usuarios() {
  const [rol, setRol] = useState('');
  const [busqueda, setBusqueda] = useState('');
  const [editando, setEditando] = useState<Usuario | 'nuevo' | null>(null);
  const [contrasenaDe, setContrasenaDe] = useState<Usuario | null>(null);
  const { datos, error, cargando, recargar } = useDatos(() => usuariosService.listar(rol || undefined), [rol]);
  const texto = busqueda.trim().toLowerCase();
  const visibles = (datos ?? []).filter((u) => !texto || u.nombre.toLowerCase().includes(texto) || u.correo.toLowerCase().includes(texto));

  return (
    <Pantalla alRecargar={recargar} recargando={cargando && !!datos}>
      <Encabezado
        titulo="Usuarios"
        descripcion="Cuentas del sistema y su rol. El rol define qué puede ver y hacer cada quien, en la web y en la app."
        acciones={<Boton texto="Nuevo usuario" icono="person-add" alPresionar={() => setEditando('nuevo')} />}
      />
      <View style={{ gap: Spacing.two, marginBottom: Spacing.three }}>
        <Busqueda valor={busqueda} alCambiar={setBusqueda} placeholder="Nombre o correo" />
        <Selector<string> etiqueta="Rol" valor={rol} opciones={[{ valor: '', texto: 'Todos' }, ...OPCIONES_ROL]} alCambiar={setRol} />
      </View>
      {error ? <Aviso tipo="error" texto={error} /> : null}
      {!datos && cargando ? <Cargando /> : null}
      {datos && visibles.length === 0 ? <Vacio texto="No hay usuarios que coincidan." /> : null}
      <View style={{ gap: Spacing.two }}>
        {visibles.map((u) => (
          <Tarjeta key={u.id} style={{ gap: 4 }}>
            <Fila style={{ justifyContent: 'space-between' }}>
              <Texto tipo="fuerte">{u.nombre}</Texto>
              <Chip texto={u.activo ? 'Activo' : 'Inactivo'} color={u.activo ? 'success' : 'default'} />
            </Fila>
            <Texto tipo="secundario">{u.correo}</Texto>
            <Texto tipo="pequeno">{nombreRol(u.rol)}</Texto>
            <Fila style={{ justifyContent: 'flex-end', flexWrap: 'wrap' }}>
              <Boton texto="Editar" icono="edit" variante="texto" alPresionar={() => setEditando(u)} />
              <Boton texto="Contraseña" icono="lock-reset" variante="texto" alPresionar={() => setContrasenaDe(u)} />
            </Fila>
          </Tarjeta>
        ))}
      </View>
      <DialogoUsuario actual={editando} alCerrar={() => setEditando(null)} alGuardar={recargar} />
      <DialogoContrasena usuario={contrasenaDe} alCerrar={() => setContrasenaDe(null)} />
    </Pantalla>
  );
}

function DialogoUsuario({ actual, alCerrar, alGuardar }: { actual: Usuario | 'nuevo' | null; alCerrar: () => void; alGuardar: () => void }) {
  const [nombre, setNombre] = useState('');
  const [correo, setCorreo] = useState('');
  const [rol, setRol] = useState<string | undefined>();
  const [activo, setActivo] = useState(true);
  const [contrasena, setContrasena] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const [para, setPara] = useState<typeof actual>(null);
  const edicion = actual && actual !== 'nuevo' ? actual : null;
  if (actual !== para) {
    setPara(actual);
    setNombre(edicion?.nombre ?? '');
    setCorreo(edicion?.correo ?? '');
    setRol(edicion?.rol);
    setActivo(edicion?.activo ?? true);
    setContrasena('');
    setError(null);
  }

  return (
    <Hoja visible={!!actual} titulo={edicion ? 'Editar usuario' : 'Nuevo usuario'} alCerrar={alCerrar}>
      <View style={{ gap: Spacing.three }}>
        <Campo etiqueta="Nombre" value={nombre} onChangeText={setNombre} />
        <Campo etiqueta="Correo" value={correo} onChangeText={setCorreo} editable={!edicion} keyboardType="email-address" autoCapitalize="none" ayuda={edicion ? 'No se puede cambiar' : undefined} />
        <Selector<string> etiqueta="Rol" valor={rol} opciones={OPCIONES_ROL} alCambiar={setRol} />
        {edicion ? (
          <Interruptor etiqueta="Activo" valor={activo} alCambiar={setActivo} />
        ) : (
          <Campo etiqueta="Contraseña inicial" value={contrasena} onChangeText={setContrasena} secureTextEntry ayuda="Mínimo 8 caracteres, con mayúscula, minúscula y número." />
        )}
        {error ? <Aviso tipo="error" texto={error} /> : null}
        <Fila style={{ justifyContent: 'flex-end' }}>
          <Boton texto="Cancelar" variante="texto" alPresionar={alCerrar} />
          <Boton
            texto="Guardar"
            cargando={guardando}
            alPresionar={async () => {
              if (!nombre.trim() || !rol || (!edicion && (!correo.trim() || !contrasena))) return setError('Completa todos los campos.');
              setGuardando(true);
              setError(null);
              try {
                if (edicion) await usuariosService.actualizar(edicion.id, { nombre, rol, activo });
                else await usuariosService.crear({ nombre, correo, rol, contrasena });
                alCerrar();
                alGuardar();
              } catch (e) {
                setError(mensajeDe(e));
              } finally {
                setGuardando(false);
              }
            }}
          />
        </Fila>
      </View>
    </Hoja>
  );
}

function DialogoContrasena({ usuario, alCerrar }: { usuario: Usuario | null; alCerrar: () => void }) {
  const [contrasena, setContrasena] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const cerrar = () => {
    setContrasena('');
    setError(null);
    alCerrar();
  };
  return (
    <Hoja visible={!!usuario} titulo={`Restablecer contraseña${usuario ? ` de ${usuario.nombre}` : ''}`} alCerrar={cerrar}>
      <View style={{ gap: Spacing.three }}>
        <Campo etiqueta="Nueva contraseña" value={contrasena} onChangeText={setContrasena} secureTextEntry ayuda="Mínimo 8 caracteres, con mayúscula, minúscula y número." />
        {error ? <Aviso tipo="error" texto={error} /> : null}
        <Fila style={{ justifyContent: 'flex-end' }}>
          <Boton texto="Cancelar" variante="texto" alPresionar={cerrar} />
          <Boton
            texto="Restablecer"
            cargando={guardando}
            alPresionar={async () => {
              if (!contrasena) return setError('Escribe la nueva contraseña.');
              setGuardando(true);
              try {
                await usuariosService.restablecerContrasena(usuario!.id, contrasena);
                avisar('Contraseña restablecida.');
                cerrar();
              } catch (e) {
                setError(mensajeDe(e));
              } finally {
                setGuardando(false);
              }
            }}
          />
        </Fila>
      </View>
    </Hoja>
  );
}
