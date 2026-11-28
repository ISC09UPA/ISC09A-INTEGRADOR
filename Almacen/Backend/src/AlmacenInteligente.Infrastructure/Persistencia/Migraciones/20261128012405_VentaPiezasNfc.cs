using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmacenInteligente.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class VentaPiezasNfc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los escaneos pendientes del formato anterior (sin UID de pieza) ya no sirven y chocarían con el índice único.
            migrationBuilder.Sql("DELETE FROM iot_escaneos WHERE NOT procesado;");

            migrationBuilder.DropIndex(
                name: "ix_iot_escaneos_sesion_id",
                table: "iot_escaneos");

            migrationBuilder.AlterColumn<Guid>(
                name: "producto_id",
                table: "iot_escaneos",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "familia",
                table: "iot_escaneos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "rechazo",
                table: "iot_escaneos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "uid",
                table: "iot_escaneos",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ventas_piezas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    devuelta = table.Column<bool>(type: "boolean", nullable: false),
                    linea_venta_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ventas_piezas", x => x.id);
                    table.ForeignKey(
                        name: "fk_ventas_piezas_ventas_lineas_linea_venta_id",
                        column: x => x.linea_venta_id,
                        principalTable: "ventas_lineas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_iot_escaneos_sesion_uid_pendiente",
                table: "iot_escaneos",
                columns: new[] { "sesion_id", "uid" },
                unique: true,
                filter: "NOT procesado");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_piezas_linea_venta_id",
                table: "ventas_piezas",
                column: "linea_venta_id");

            migrationBuilder.CreateIndex(
                name: "ux_ventas_piezas_uid_vendida",
                table: "ventas_piezas",
                column: "uid",
                unique: true,
                filter: "NOT devuelta");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ventas_piezas");

            migrationBuilder.DropIndex(
                name: "ux_iot_escaneos_sesion_uid_pendiente",
                table: "iot_escaneos");

            migrationBuilder.DropColumn(
                name: "familia",
                table: "iot_escaneos");

            migrationBuilder.DropColumn(
                name: "rechazo",
                table: "iot_escaneos");

            migrationBuilder.DropColumn(
                name: "uid",
                table: "iot_escaneos");

            migrationBuilder.Sql("DELETE FROM iot_escaneos WHERE producto_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "producto_id",
                table: "iot_escaneos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_iot_escaneos_sesion_id",
                table: "iot_escaneos",
                column: "sesion_id",
                filter: "NOT procesado");
        }
    }
}
