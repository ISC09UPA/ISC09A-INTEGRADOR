using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmacenInteligente.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Iot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_nfc",
                table: "usuarios",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "codigo_nfc",
                table: "productos",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "iot_accesos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nfc = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    permitido = table.Column<bool>(type: "boolean", nullable: false),
                    confianza = table.Column<double>(type: "double precision", nullable: false),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iot_accesos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "iot_escaneos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    procesado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iot_escaneos", x => x.id);
                    table.ForeignKey(
                        name: "fk_iot_escaneos_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "iot_fotos_rostro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruta = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    imagen_remota_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    entrenada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iot_fotos_rostro", x => x.id);
                    table.ForeignKey(
                        name: "fk_iot_fotos_rostro_asp_net_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "iot_sesion_venta",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    almacen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_latido = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iot_sesion_venta", x => x.id);
                    table.CheckConstraint("ck_iot_sesion_venta_unica", "id = 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_codigo_nfc",
                table: "usuarios",
                column: "codigo_nfc",
                unique: true,
                filter: "codigo_nfc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_productos_codigo_nfc",
                table: "productos",
                column: "codigo_nfc",
                unique: true,
                filter: "codigo_nfc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_iot_accesos_fecha",
                table: "iot_accesos",
                column: "fecha",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_iot_escaneos_producto_id",
                table: "iot_escaneos",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "ix_iot_escaneos_sesion_id",
                table: "iot_escaneos",
                column: "sesion_id",
                filter: "NOT procesado");

            migrationBuilder.CreateIndex(
                name: "ix_iot_fotos_rostro_usuario_id",
                table: "iot_fotos_rostro",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "iot_accesos");

            migrationBuilder.DropTable(
                name: "iot_escaneos");

            migrationBuilder.DropTable(
                name: "iot_fotos_rostro");

            migrationBuilder.DropTable(
                name: "iot_sesion_venta");

            migrationBuilder.DropIndex(
                name: "ix_usuarios_codigo_nfc",
                table: "usuarios");

            migrationBuilder.DropIndex(
                name: "ix_productos_codigo_nfc",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "codigo_nfc",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "codigo_nfc",
                table: "productos");
        }
    }
}
