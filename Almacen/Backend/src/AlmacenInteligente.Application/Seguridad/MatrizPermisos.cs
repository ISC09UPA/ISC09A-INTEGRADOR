using P = AlmacenInteligente.Application.Seguridad.Permisos;

namespace AlmacenInteligente.Application.Seguridad;

/// <summary>Matriz por defecto de la sección 4 del plan. Se siembra en la base de datos y desde ahí se puede ajustar.</summary>
public static class MatrizPermisos
{
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> PorDefecto { get; } = new Dictionary<string, IReadOnlySet<string>>
    {
        // El superadmin no recibe alertas de bajo stock: esas van al encargado y al gerente.
        [Roles.Superadmin] = P.Todos.Where(p => p != P.Alertas.RecibirBajoStock).ToHashSet(),

        [Roles.AgenteVentas] = new HashSet<string>
        {
            P.Clientes.Ver, P.Clientes.Crear,
            P.Productos.Ver,
            P.Almacenes.Ver, P.Almacenes.VerExistencias,
            P.Ventas.Ver, P.Ventas.Crear, P.Ventas.Cancelar,
        },

        [Roles.EncargadoRecepciones] = new HashSet<string>
        {
            P.Proveedores.Ver, P.Proveedores.Crear, P.Proveedores.Editar,
            P.Productos.Ver, P.Productos.Crear, P.Productos.Editar,
            P.Almacenes.Ver, P.Almacenes.Gestionar, P.Almacenes.VerExistencias,
            P.Recepciones.Ver, P.Recepciones.Crear,
            P.Traspasos.Ver, P.Traspasos.Crear,
            P.Alertas.RecibirBajoStock,
        },

        // P.Productos.Crear: el plan dice que lo que crea el gerente queda aprobado directamente.
        [Roles.Gerente] = new HashSet<string>
        {
            P.Clientes.Ver,
            P.Proveedores.Ver,
            P.Productos.Ver, P.Productos.Crear, P.Productos.Aprobar,
            P.Almacenes.Ver, P.Almacenes.VerExistencias,
            P.Recepciones.Ver, P.Recepciones.Crear, P.Recepciones.Cancelar,
            P.Ventas.Ver, P.Ventas.Crear, P.Ventas.Cancelar,
            P.Traspasos.Ver, P.Traspasos.Crear,
            P.Finanzas.Ver,
            P.Reportes.Ver,
            P.Alertas.RecibirBajoStock,
            P.Api.Acceder,
        },
    };
}
