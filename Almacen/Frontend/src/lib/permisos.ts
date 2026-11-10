// Los mismos permisos atómicos que AlmacenInteligente.Application.Seguridad.Permisos.
// El token trae los del rol del usuario; el menú y los botones se muestran según ellos,
// y la API vuelve a validarlos en cada llamada.

export const Permisos = {
  Usuarios: { Gestionar: 'usuarios.gestionar' },
  Clientes: { Ver: 'clientes.ver', Crear: 'clientes.crear', Editar: 'clientes.editar' },
  Proveedores: { Ver: 'proveedores.ver', Crear: 'proveedores.crear', Editar: 'proveedores.editar' },
  Productos: { Ver: 'productos.ver', Crear: 'productos.crear', Editar: 'productos.editar', Aprobar: 'productos.aprobar' },
  Almacenes: { Ver: 'almacenes.ver', Gestionar: 'almacenes.gestionar', VerExistencias: 'existencias.ver' },
  Recepciones: { Ver: 'recepciones.ver', Crear: 'recepciones.crear', Cancelar: 'recepciones.cancelar' },
  Ventas: { Ver: 'ventas.ver', Crear: 'ventas.crear', Cancelar: 'ventas.cancelar' },
  Traspasos: { Ver: 'traspasos.ver', Crear: 'traspasos.crear' },
  Finanzas: { Ver: 'finanzas.ver' },
  Reportes: { Ver: 'reportes.ver' },
  Api: { Acceder: 'api.acceder' },
} as const;

export const Roles = ['Superadmin', 'AgenteVentas', 'EncargadoRecepciones', 'Gerente'] as const;

export function nombreRol(rol: string) {
  switch (rol) {
    case 'AgenteVentas':
      return 'Agente de ventas';
    case 'EncargadoRecepciones':
      return 'Encargado de recepciones';
    default:
      return rol;
  }
}
