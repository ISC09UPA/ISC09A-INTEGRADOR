using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmacenInteligente.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class InicioSesionIot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "iot_solicitudes_inicio_sesion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    creada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    resuelta_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iot_solicitudes_inicio_sesion", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_iot_solicitudes_inicio_sesion_creada",
                table: "iot_solicitudes_inicio_sesion",
                column: "creada",
                filter: "estado = 'Pendiente'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "iot_solicitudes_inicio_sesion");
        }
    }
}
