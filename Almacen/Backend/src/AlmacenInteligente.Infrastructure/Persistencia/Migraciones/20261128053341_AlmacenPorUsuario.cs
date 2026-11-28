using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmacenInteligente.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AlmacenPorUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "almacen_id",
                table: "usuarios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "almacen_id",
                table: "iot_consultas_stock",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "usuario_id",
                table: "iot_consultas_stock",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_almacen_id",
                table: "usuarios",
                column: "almacen_id");

            migrationBuilder.CreateIndex(
                name: "ix_iot_consultas_stock_almacen_id",
                table: "iot_consultas_stock",
                column: "almacen_id");

            migrationBuilder.AddForeignKey(
                name: "fk_usuarios_almacenes_almacen_id",
                table: "usuarios",
                column: "almacen_id",
                principalTable: "almacenes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Para que nadie quede sin poder trabajar: los usuarios que ya existían (salvo el superadmin) quedan en el
            // almacén activo más antiguo. El superadmin los puede reasignar en Usuarios.
            migrationBuilder.Sql("""
                UPDATE usuarios u SET almacen_id = (SELECT a.id FROM almacenes a WHERE a.activo ORDER BY a.creado_fecha, a.nombre LIMIT 1)
                WHERE u.almacen_id IS NULL
                  AND NOT EXISTS (SELECT 1 FROM usuarios_roles ur JOIN roles r ON r.id = ur.role_id
                                  WHERE ur.user_id = u.id AND r.name = 'Superadmin');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_usuarios_almacenes_almacen_id",
                table: "usuarios");

            migrationBuilder.DropIndex(
                name: "ix_usuarios_almacen_id",
                table: "usuarios");

            migrationBuilder.DropIndex(
                name: "ix_iot_consultas_stock_almacen_id",
                table: "iot_consultas_stock");

            migrationBuilder.DropColumn(
                name: "almacen_id",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "almacen_id",
                table: "iot_consultas_stock");

            migrationBuilder.DropColumn(
                name: "usuario_id",
                table: "iot_consultas_stock");
        }
    }
}
