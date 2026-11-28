using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmacenInteligente.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class RecepcionInteligente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_proveedor",
                table: "productos",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modo",
                table: "iot_sesion_venta",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Venta");

            migrationBuilder.CreateIndex(
                name: "ix_productos_codigo_proveedor",
                table: "productos",
                column: "codigo_proveedor",
                unique: true,
                filter: "codigo_proveedor IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_productos_codigo_proveedor",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "codigo_proveedor",
                table: "productos");

            migrationBuilder.DropColumn(
                name: "modo",
                table: "iot_sesion_venta");
        }
    }
}
