using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvernaderoInteligente.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LecturasSensor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SensorId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Valor = table.Column<double>(type: "float", nullable: false),
                    Unidad = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    InvernaderoId = table.Column<int>(type: "int", nullable: true),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LecturasSensor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Riegos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvernaderoId = table.Column<int>(type: "int", nullable: true),
                    Zona = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FechaHoraInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DuracionSegundos = table.Column<int>(type: "int", nullable: false),
                    VolumenMl = table.Column<double>(type: "float", nullable: true),
                    Origen = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    HumedadDetectada = table.Column<double>(type: "float", nullable: true),
                    Umbral = table.Column<double>(type: "float", nullable: true),
                    LecturaSensorId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Riegos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LecturasSensor_FechaHora",
                table: "LecturasSensor",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_LecturasSensor_SensorId_Tipo_FechaHora",
                table: "LecturasSensor",
                columns: new[] { "SensorId", "Tipo", "FechaHora" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Riegos_FechaHoraInicio",
                table: "Riegos",
                column: "FechaHoraInicio");

            migrationBuilder.CreateIndex(
                name: "IX_Riegos_InvernaderoId_Zona",
                table: "Riegos",
                columns: new[] { "InvernaderoId", "Zona" });

            migrationBuilder.CreateIndex(
                name: "IX_Riegos_InvernaderoId_Zona_FechaHoraInicio",
                table: "Riegos",
                columns: new[] { "InvernaderoId", "Zona", "FechaHoraInicio" },
                unique: true,
                filter: "[InvernaderoId] IS NOT NULL AND [Zona] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LecturasSensor");

            migrationBuilder.DropTable(
                name: "Riegos");
        }
    }
}
