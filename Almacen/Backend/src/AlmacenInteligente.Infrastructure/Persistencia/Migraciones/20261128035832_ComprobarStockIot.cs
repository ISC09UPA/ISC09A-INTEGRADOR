using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmacenInteligente.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ComprobarStockIot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "iot_consultas_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    familia = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    existencia = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    minimo = table.Column<decimal>(type: "numeric(18,3)", nullable: true),
                    nivel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iot_consultas_stock", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_iot_consultas_stock_fecha",
                table: "iot_consultas_stock",
                column: "fecha",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "iot_consultas_stock");
        }
    }
}
