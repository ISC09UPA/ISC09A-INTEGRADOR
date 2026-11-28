import { router, Stack } from 'expo-router';
import { useEffect, useState } from 'react';
import { View } from 'react-native';

import type { DatosTercero } from '@/models/datos-tercero.model';
import type { Tercero } from '@/models/tercero.model';
import { clientesService, proveedoresService } from '@/services';
import type { TercerosService } from '@/services/terceros.service';
import { ListaPantalla } from '@/components/lista';
import { Aviso, Boton, Busqueda, Campo, Cargando, Chip, Fila, Interruptor, Pantalla, Selector, Tarjeta, Texto, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { usePaginado } from '@/hooks/use-datos';
import { useSesion } from '@/lib/sesion';

export const ESTADOS_MEXICO = [
  'Aguascalientes', 'Baja California', 'Baja California Sur', 'Campeche', 'Chiapas', 'Chihuahua',
  'Ciudad de México', 'Coahuila', 'Colima', 'Durango', 'Estado de México', 'Guanajuato', 'Guerrero',
  'Hidalgo', 'Jalisco', 'Michoacán', 'Morelos', 'Nayarit', 'Nuevo León', 'Oaxaca', 'Puebla',
  'Querétaro', 'Quintana Roo', 'San Luis Potosí', 'Sinaloa', 'Sonora', 'Tabasco', 'Tamaulipas',
  'Tlaxcala', 'Veracruz', 'Yucatán', 'Zacatecas',
];

export interface ConfigTercero {
  singular: string; // "Cliente"
  plural: string;
  servicio: TercerosService;
  rutaApp: string; // "/clientes"
  permisoCrear: string;
  permisoEditar: string;
  descripcion: string;
}

export const Terceros = {
  clientes: {
    singular: 'Cliente',
    plural: 'Clientes',
    servicio: clientesService,
    rutaApp: '/clientes',
    permisoCrear: 'clientes.crear',
    permisoEditar: 'clientes.editar',
    descripcion: 'Clientes a los que se vende. El código no cambia después del alta.',
  },
  proveedores: {
    singular: 'Proveedor',
    plural: 'Proveedores',
    servicio: proveedoresService,
    rutaApp: '/proveedores',
    permisoCrear: 'proveedores.crear',
    permisoEditar: 'proveedores.editar',
    descripcion: 'Proveedores de los que se recibe mercancía. El código no cambia después del alta.',
  },
} satisfies Record<string, ConfigTercero>;

/** Lista de clientes o proveedores (ListaTerceros.razor). */
export function ListaTerceros({ config }: { config: ConfigTercero }) {
  const { puede } = useSesion();
  const [busqueda, setBusqueda] = useState('');
  const [inactivos, setInactivos] = useState(false);
  const lista = usePaginado<Tercero>(
    (pagina) => config.servicio.listar({ busqueda, inactivos, pagina }),
    [busqueda, inactivos],
  );
  const puedeEditar = puede(config.permisoEditar);

  return (
    <ListaPantalla
      {...lista}
      alRecargar={lista.recargar}
      alLlegarAlFinal={lista.cargarMas}
      clave={(t) => t.id}
      encabezado={
        <View style={{ gap: Spacing.two }}>
          <Texto tipo="secundario">{config.descripcion}</Texto>
          {puede(config.permisoCrear) ? (
            <Boton texto={`Nuevo ${config.singular.toLowerCase()}`} icono="add" alPresionar={() => router.push(`${config.rutaApp}/editar` as never)} style={{ alignSelf: 'flex-start' }} />
          ) : null}
          <Busqueda valor={busqueda} alCambiar={setBusqueda} placeholder="Código, nombre, RFC o correo" />
          <Interruptor etiqueta="Incluir inactivos" valor={inactivos} alCambiar={setInactivos} />
        </View>
      }
      fila={(t) => (
        <Tarjeta alPresionar={puedeEditar ? () => router.push(`${config.rutaApp}/editar?id=${t.id}` as never) : undefined} style={{ gap: 4 }}>
          <Fila style={{ justifyContent: 'space-between' }}>
            <Texto tipo="fuerte" style={{ flex: 1 }} numberOfLines={1}>
              {t.codigo} · {t.nombre}
            </Texto>
            {!t.activo ? <Chip texto="Inactivo" /> : null}
          </Fila>
          <Texto tipo="secundario">RFC {t.rfc} · {t.correo}</Texto>
          <Texto tipo="pequeno" numberOfLines={1}>
            {t.direccion.calle} {t.direccion.numeroExterior}, {t.direccion.colonia}, {t.direccion.municipio}, {t.direccion.estado}
          </Texto>
        </Tarjeta>
      )}
    />
  );
}

const VACIO: DatosTercero = {
  codigo: '',
  nombre: '',
  correo: '',
  rfc: '',
  direccion: { calle: '', numeroExterior: '', numeroInterior: '', colonia: '', codigoPostal: '', municipio: '', estado: '', pais: 'México' },
};

/** Alta o edición de cliente/proveedor (TerceroDialog.razor). */
export function FormularioTercero({
  config,
  inicial,
  alGuardar,
  alCancelar,
}: {
  config: ConfigTercero;
  inicial?: Tercero;
  alGuardar: (t: Tercero) => void;
  alCancelar?: () => void;
}) {
  const [m, setM] = useState<DatosTercero>(inicial ? { codigo: inicial.codigo, nombre: inicial.nombre, correo: inicial.correo, rfc: inicial.rfc, direccion: { ...inicial.direccion } } : VACIO);
  const [errores, setErrores] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const [cambiandoEstado, setCambiandoEstado] = useState(false);
  const esEdicion = !!inicial;
  const mexico = (m.direccion.pais ?? 'México') === 'México';

  const campo = (k: keyof Omit<DatosTercero, 'direccion'>) => ({
    value: m[k],
    onChangeText: (v: string) => setM({ ...m, [k]: v }),
    error: errores[k],
  });
  const dir = (k: keyof DatosTercero['direccion']) => ({
    value: m.direccion[k] ?? '',
    onChangeText: (v: string) => setM({ ...m, direccion: { ...m.direccion, [k]: v } }),
    error: errores[k],
  });

  const guardar = async () => {
    const faltan: Record<string, string> = {};
    const requeridos: [string, string][] = [
      ['codigo', m.codigo],
      ['nombre', m.nombre],
      ['correo', m.correo],
      ['rfc', m.rfc],
      ['calle', m.direccion.calle],
      ['numeroExterior', m.direccion.numeroExterior],
      ['colonia', m.direccion.colonia],
      ['codigoPostal', m.direccion.codigoPostal],
      ['municipio', m.direccion.municipio],
      ['estado', m.direccion.estado],
    ];
    for (const [k, v] of requeridos) if (!v?.trim()) faltan[k] = 'Obligatorio';
    setErrores(faltan);
    if (Object.keys(faltan).length) {
      setError('Completa los campos obligatorios.');
      return;
    }
    setError(null);
    setGuardando(true);
    try {
      alGuardar(esEdicion ? await config.servicio.editar(inicial.id, m) : await config.servicio.crear(m));
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setGuardando(false);
    }
  };

  const alternarEstado = async () => {
    if (!inicial) return;
    setCambiandoEstado(true);
    try {
      alGuardar(await config.servicio.cambiarEstado(inicial.id, !inicial.activo));
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setCambiandoEstado(false);
    }
  };

  return (
    <View style={{ gap: Spacing.three }}>
      {error ? <Aviso tipo="error" texto={error} /> : null}
      <Campo etiqueta="Código" {...campo('codigo')} editable={!esEdicion} ayuda={esEdicion ? 'No se puede cambiar' : undefined} autoCapitalize="characters" maxLength={30} />
      <Campo etiqueta="Nombre o razón social" {...campo('nombre')} maxLength={200} />
      <Campo etiqueta="Correo" {...campo('correo')} keyboardType="email-address" autoCapitalize="none" />
      <Campo etiqueta="RFC" {...campo('rfc')} autoCapitalize="characters" maxLength={13} ayuda="12 caracteres persona moral, 13 persona física" />

      <Texto tipo="subtitulo">Dirección</Texto>
      <Campo etiqueta="Calle" {...dir('calle')} maxLength={150} />
      <Fila>
        <Campo etiqueta="Núm. exterior" {...dir('numeroExterior')} maxLength={20} style={{ flex: 1 }} />
        <Campo etiqueta="Núm. interior" {...dir('numeroInterior')} maxLength={20} style={{ flex: 1 }} />
      </Fila>
      <Campo etiqueta="Colonia" {...dir('colonia')} maxLength={120} />
      <Fila>
        <Campo etiqueta="Código postal" {...dir('codigoPostal')} keyboardType="number-pad" maxLength={10} style={{ flex: 1 }} />
        <Campo etiqueta="Municipio o alcaldía" {...dir('municipio')} maxLength={120} style={{ flex: 2 }} />
      </Fila>
      {mexico ? (
        <Selector
          etiqueta="Estado"
          valor={m.direccion.estado || undefined}
          opciones={ESTADOS_MEXICO.map((e) => ({ valor: e, texto: e }))}
          alCambiar={(v) => setM({ ...m, direccion: { ...m.direccion, estado: v } })}
          error={errores.estado}
        />
      ) : (
        <Campo etiqueta="Estado o provincia" {...dir('estado')} maxLength={80} />
      )}
      <Campo etiqueta="País" {...dir('pais')} maxLength={60} />

      <Fila style={{ justifyContent: 'flex-end', flexWrap: 'wrap' }}>
        {esEdicion ? (
          <Boton
            texto={inicial.activo ? 'Desactivar' : 'Activar'}
            variante={inicial.activo ? 'peligro' : 'contorno'}
            cargando={cambiandoEstado}
            alPresionar={alternarEstado}
          />
        ) : null}
        {alCancelar ? <Boton texto="Cancelar" variante="texto" alPresionar={alCancelar} /> : null}
        <Boton texto={esEdicion ? 'Guardar cambios' : `Dar de alta`} cargando={guardando} alPresionar={guardar} />
      </Fila>
    </View>
  );
}

/** Pantalla de alta (sin id) o edición (?id=) de cliente/proveedor. */
export function EditarTercero({ config, id }: { config: ConfigTercero; id?: string }) {
  const [tercero, setTercero] = useState<Tercero | undefined>();
  const [error, setError] = useState<string | null>(null);
  const [cargado, setCargado] = useState(!id);

  useEffect(() => {
    if (!id) return;
    config.servicio
      .obtener(id)
      .then(setTercero)
      .catch((e) => setError(mensajeDe(e)))
      .finally(() => setCargado(true));
  }, [id, config.servicio]);

  return (
    <Pantalla>
      <Stack.Screen options={{ title: id ? `Editar ${config.singular.toLowerCase()}` : `Nuevo ${config.singular.toLowerCase()}` }} />
      {error ? <Aviso tipo="error" texto={error} /> : null}
      {!cargado ? <Cargando /> : null}
      {cargado && (!id || tercero) ? (
        <Tarjeta>
          <FormularioTercero
            key={tercero ? `${tercero.id}-${tercero.activo}` : 'nuevo'}
            config={config}
            inicial={tercero}
            alCancelar={() => router.back()}
            alGuardar={(t) => {
              if (tercero && t.activo !== tercero.activo) {
                setTercero(t);
                return;
              }
              avisar(`${t.nombre} se ${id ? 'actualizó' : 'dio de alta'}.`);
              router.back();
            }}
          />
        </Tarjeta>
      ) : null}
    </Pantalla>
  );
}
