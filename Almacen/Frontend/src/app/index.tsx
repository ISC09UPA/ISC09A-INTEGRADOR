import MaterialIcons from '@expo/vector-icons/MaterialIcons';
import { router, Stack, type Href } from 'expo-router';
import { Pressable, StyleSheet, View } from 'react-native';

import { BotonIcono, Pantalla, Texto, confirmar, type NombreIcono } from '@/components/ui';
import { Radio, Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { Permisos, nombreRol } from '@/lib/permisos';
import { useSesion } from '@/lib/sesion';

interface Acceso {
  titulo: string;
  descripcion?: string;
  ruta: Href;
  icono: NombreIcono;
  permiso: string | string[];
}

// Accesos rápidos de la página de inicio de la web (Home.razor).
const RAPIDOS: Acceso[] = [
  { titulo: 'Nueva venta', descripcion: 'Captura una venta y descuenta existencias', ruta: '/ventas/nueva', icono: 'point-of-sale', permiso: Permisos.Ventas.Crear },
  { titulo: 'Nueva recepción', descripcion: 'Registra mercancía que llegó de un proveedor', ruta: '/recepciones/nueva', icono: 'move-to-inbox', permiso: Permisos.Recepciones.Crear },
  { titulo: 'Nuevo traspaso', descripcion: 'Mueve mercancía entre almacenes', ruta: '/traspasos/nuevo', icono: 'sync-alt', permiso: Permisos.Traspasos.Crear },
  { titulo: 'Aprobaciones', descripcion: 'Productos que esperan tu aprobación', ruta: '/aprobaciones', icono: 'fact-check', permiso: Permisos.Productos.Aprobar },
];

// El menú lateral de la web (NavMenu.razor), por secciones.
const SECCIONES: { titulo: string; accesos: Acceso[] }[] = [
  {
    titulo: 'Operación',
    accesos: [
      { titulo: 'Ventas', ruta: '/ventas', icono: 'point-of-sale', permiso: Permisos.Ventas.Ver },
      { titulo: 'Recepciones', ruta: '/recepciones', icono: 'move-to-inbox', permiso: Permisos.Recepciones.Ver },
      { titulo: 'Traspasos', ruta: '/traspasos', icono: 'sync-alt', permiso: Permisos.Traspasos.Ver },
    ],
  },
  {
    titulo: 'Inventario',
    accesos: [
      { titulo: 'Existencias', descripcion: 'Qué hay en cada almacén y qué está bajo mínimo', ruta: '/existencias', icono: 'inventory', permiso: Permisos.Almacenes.VerExistencias },
      { titulo: 'Movimientos', ruta: '/movimientos', icono: 'swap-vert', permiso: Permisos.Almacenes.VerExistencias },
      { titulo: 'Kardex', ruta: '/kardex', icono: 'receipt-long', permiso: Permisos.Almacenes.VerExistencias },
    ],
  },
  {
    titulo: 'Catálogos',
    accesos: [
      { titulo: 'Productos', ruta: '/productos', icono: 'category', permiso: Permisos.Productos.Ver },
      { titulo: 'Almacenes', ruta: '/almacenes', icono: 'warehouse', permiso: Permisos.Almacenes.Ver },
      { titulo: 'Clientes', ruta: '/clientes', icono: 'groups', permiso: Permisos.Clientes.Ver },
      { titulo: 'Proveedores', ruta: '/proveedores', icono: 'local-shipping', permiso: Permisos.Proveedores.Ver },
    ],
  },
  {
    titulo: 'Análisis',
    accesos: [
      { titulo: 'Reportes', descripcion: 'Ventas, compras, inventario, flujo y margen', ruta: '/reportes', icono: 'insights', permiso: Permisos.Reportes.Ver },
      { titulo: 'Movimientos financieros', ruta: '/finanzas', icono: 'account-balance', permiso: Permisos.Finanzas.Ver },
    ],
  },
  {
    titulo: 'Administración',
    accesos: [{ titulo: 'Usuarios', descripcion: 'Cuentas y roles del sistema', ruta: '/usuarios', icono: 'manage-accounts', permiso: Permisos.Usuarios.Gestionar }],
  },
];

export default function Inicio() {
  const c = useColores();
  const { sesion, rol, puede, salir } = useSesion();
  const permitido = (a: Acceso) => (Array.isArray(a.permiso) ? a.permiso.some(puede) : puede(a.permiso));
  const rapidos = RAPIDOS.filter(permitido);

  return (
    <Pantalla>
      <Stack.Screen
        options={{
          headerRight: () => (
            <BotonIcono
              icono="logout"
              etiqueta="Cerrar sesión"
              alPresionar={async () => {
                if (await confirmar('Cerrar sesión', '¿Quieres salir de tu cuenta?', 'Salir')) salir();
              }}
            />
          ),
        }}
      />
      <Texto tipo="titulo">Hola, {sesion?.nombre}</Texto>
      <Texto tipo="secundario" style={{ marginBottom: Spacing.three }}>
        {nombreRol(rol)}
      </Texto>

      {rapidos.length > 0 ? (
        <View style={estilos.rejilla}>
          {rapidos.map((a) => (
            <Pressable
              key={a.titulo}
              onPress={() => router.push(a.ruta)}
              style={({ pressed }) => [estilos.rapido, { backgroundColor: c.surface, borderColor: c.line, opacity: pressed ? 0.75 : 1 }]}>
              <View style={[estilos.avatar, { borderColor: c.primary }]}>
                <MaterialIcons name={a.icono} size={22} color={c.primary} />
              </View>
              <Texto tipo="fuerte">{a.titulo}</Texto>
              <Texto tipo="pequeno">{a.descripcion}</Texto>
            </Pressable>
          ))}
        </View>
      ) : null}

      {SECCIONES.map((s) => {
        const visibles = s.accesos.filter(permitido);
        if (!visibles.length) return null;
        return (
          <View key={s.titulo} style={{ marginTop: Spacing.three }}>
            <Texto tipo="etiqueta" style={{ marginBottom: Spacing.two }}>
              {s.titulo}
            </Texto>
            <View style={[estilos.menu, { backgroundColor: c.surface, borderColor: c.line }]}>
              {visibles.map((a, i) => (
                <Pressable
                  key={a.titulo}
                  onPress={() => router.push(a.ruta)}
                  style={({ pressed }) => [
                    estilos.opcion,
                    { borderTopColor: c.line, borderTopWidth: i ? StyleSheet.hairlineWidth : 0, backgroundColor: pressed ? c.background : 'transparent' },
                  ]}>
                  <MaterialIcons name={a.icono} size={22} color={c.primary} />
                  <View style={{ flex: 1 }}>
                    <Texto>{a.titulo}</Texto>
                    {a.descripcion ? <Texto tipo="pequeno">{a.descripcion}</Texto> : null}
                  </View>
                  <MaterialIcons name="chevron-right" size={22} color={c.textSecondary} />
                </Pressable>
              ))}
            </View>
          </View>
        );
      })}
    </Pantalla>
  );
}

const estilos = StyleSheet.create({
  rejilla: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: Spacing.two,
  },
  rapido: {
    flexGrow: 1,
    flexBasis: '45%',
    minWidth: 150,
    borderWidth: 1,
    borderRadius: Radio,
    padding: Spacing.three,
    gap: 6,
  },
  avatar: {
    width: 40,
    height: 40,
    borderRadius: 20,
    borderWidth: 1.5,
    alignItems: 'center',
    justifyContent: 'center',
  },
  menu: {
    borderWidth: 1,
    borderRadius: Radio,
    overflow: 'hidden',
  },
  opcion: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.three,
    paddingVertical: 14,
    paddingHorizontal: Spacing.three,
  },
});
