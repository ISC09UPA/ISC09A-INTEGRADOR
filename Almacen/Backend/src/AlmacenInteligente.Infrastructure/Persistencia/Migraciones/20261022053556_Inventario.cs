using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AlmacenInteligente.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "almacenes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ubicacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    creado_usuario_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_almacenes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "movimientos_financieros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    naturaleza = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tercero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    es_reverso = table.Column<bool>(type: "boolean", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    importe_iva = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    importe_subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    registro_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    registro_usuario_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimientos_financieros", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unidades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    permite_fracciones = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unidades", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ventas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    cancelado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelado_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmado_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    creado_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo_cancelacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ventas", x => x.id);
                    table.ForeignKey(
                        name: "fk_ventas_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recepciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    cancelado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelado_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmado_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    creado_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo_cancelacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recepciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_recepciones_almacenes_almacen_id",
                        column: x => x.almacen_id,
                        principalTable: "almacenes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recepciones_proveedores_proveedor_id",
                        column: x => x.proveedor_id,
                        principalTable: "proveedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "traspasos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    cancelado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelado_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmado_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    creado_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo_cancelacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_traspasos", x => x.id);
                    table.ForeignKey(
                        name: "fk_traspasos_almacenes_almacen_destino_id",
                        column: x => x.almacen_destino_id,
                        principalTable: "almacenes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_traspasos_almacenes_almacen_origen_id",
                        column: x => x.almacen_origen_id,
                        principalTable: "almacenes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "productos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permite_fracciones = table.Column<bool>(type: "boolean", nullable: false),
                    imagen_ruta = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gerente_aprobador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    creado_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolucion_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolucion_usuario_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_productos", x => x.id);
                    table.ForeignKey(
                        name: "fk_productos_unidades_unidad_id",
                        column: x => x.unidad_id,
                        principalTable: "unidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "existencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    stock_minimo = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    costo_promedio = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    alerta_bajo_stock_enviada = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_existencias", x => x.id);
                    table.CheckConstraint("ck_existencias_cantidad_no_negativa", "cantidad >= 0");
                    table.ForeignKey(
                        name: "fk_existencias_almacenes_almacen_id",
                        column: x => x.almacen_id,
                        principalTable: "almacenes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_existencias_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimientos_inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    saldo_cantidad = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    costo_promedio = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    documento_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    registro_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    registro_usuario_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimientos_inventario", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimientos_inventario_almacenes_almacen_id",
                        column: x => x.almacen_id,
                        principalTable: "almacenes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimientos_inventario_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recepciones_lineas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    tasa_iva = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    recepcion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    importe_iva = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    importe_subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recepciones_lineas", x => x.id);
                    table.ForeignKey(
                        name: "fk_recepciones_lineas_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recepciones_lineas_recepciones_recepcion_id",
                        column: x => x.recepcion_id,
                        principalTable: "recepciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "traspasos_lineas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    traspaso_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_traspasos_lineas", x => x.id);
                    table.ForeignKey(
                        name: "fk_traspasos_lineas_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_traspasos_lineas_traspasos_traspaso_id",
                        column: x => x.traspaso_id,
                        principalTable: "traspasos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ventas_lineas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    almacen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    tasa_iva = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    venta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    importe_iva = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    importe_subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ventas_lineas", x => x.id);
                    table.ForeignKey(
                        name: "fk_ventas_lineas_almacenes_almacen_id",
                        column: x => x.almacen_id,
                        principalTable: "almacenes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ventas_lineas_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ventas_lineas_ventas_venta_id",
                        column: x => x.venta_id,
                        principalTable: "ventas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_almacenes_nombre",
                table: "almacenes",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_existencias_almacen_id_producto_id",
                table: "existencias",
                columns: new[] { "almacen_id", "producto_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_existencias_producto_id",
                table: "existencias",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_inventario_almacen_id",
                table: "movimientos_inventario",
                column: "almacen_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_inventario_producto_id_almacen_id",
                table: "movimientos_inventario",
                columns: new[] { "producto_id", "almacen_id" });

            migrationBuilder.CreateIndex(
                name: "ix_productos_estado_gerente_aprobador_id",
                table: "productos",
                columns: new[] { "estado", "gerente_aprobador_id" });

            migrationBuilder.CreateIndex(
                name: "ix_productos_referencia",
                table: "productos",
                column: "referencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_productos_unidad_id",
                table: "productos",
                column: "unidad_id");

            migrationBuilder.CreateIndex(
                name: "ix_recepciones_almacen_id",
                table: "recepciones",
                column: "almacen_id");

            migrationBuilder.CreateIndex(
                name: "ix_recepciones_fecha",
                table: "recepciones",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "ix_recepciones_folio",
                table: "recepciones",
                column: "folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recepciones_proveedor_id",
                table: "recepciones",
                column: "proveedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_recepciones_lineas_producto_id",
                table: "recepciones_lineas",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "ix_recepciones_lineas_recepcion_id",
                table: "recepciones_lineas",
                column: "recepcion_id");

            migrationBuilder.CreateIndex(
                name: "ix_traspasos_almacen_destino_id",
                table: "traspasos",
                column: "almacen_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_traspasos_almacen_origen_id",
                table: "traspasos",
                column: "almacen_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_traspasos_fecha",
                table: "traspasos",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "ix_traspasos_folio",
                table: "traspasos",
                column: "folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_traspasos_lineas_producto_id",
                table: "traspasos_lineas",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "ix_traspasos_lineas_traspaso_id",
                table: "traspasos_lineas",
                column: "traspaso_id");

            migrationBuilder.CreateIndex(
                name: "ix_unidades_clave",
                table: "unidades",
                column: "clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ventas_cliente_id",
                table: "ventas",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_fecha",
                table: "ventas",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_folio",
                table: "ventas",
                column: "folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ventas_lineas_almacen_id",
                table: "ventas_lineas",
                column: "almacen_id");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_lineas_producto_id",
                table: "ventas_lineas",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_lineas_venta_id",
                table: "ventas_lineas",
                column: "venta_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "existencias");

            migrationBuilder.DropTable(
                name: "movimientos_financieros");

            migrationBuilder.DropTable(
                name: "movimientos_inventario");

            migrationBuilder.DropTable(
                name: "recepciones_lineas");

            migrationBuilder.DropTable(
                name: "traspasos_lineas");

            migrationBuilder.DropTable(
                name: "ventas_lineas");

            migrationBuilder.DropTable(
                name: "recepciones");

            migrationBuilder.DropTable(
                name: "traspasos");

            migrationBuilder.DropTable(
                name: "productos");

            migrationBuilder.DropTable(
                name: "ventas");

            migrationBuilder.DropTable(
                name: "almacenes");

            migrationBuilder.DropTable(
                name: "unidades");
        }
    }
}
