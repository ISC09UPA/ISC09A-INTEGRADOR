using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LockerSmart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEventosDispositivoYRelaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventosDispositivo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LockerCodigo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecibidoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosDispositivo", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rentas_LockerId",
                table: "Rentas",
                column: "LockerId");

            migrationBuilder.CreateIndex(
                name: "IX_Rentas_UsuarioId",
                table: "Rentas",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rentas_Lockers_LockerId",
                table: "Rentas",
                column: "LockerId",
                principalTable: "Lockers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rentas_Usuarios_UsuarioId",
                table: "Rentas",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rentas_Lockers_LockerId",
                table: "Rentas");

            migrationBuilder.DropForeignKey(
                name: "FK_Rentas_Usuarios_UsuarioId",
                table: "Rentas");

            migrationBuilder.DropTable(
                name: "EventosDispositivo");

            migrationBuilder.DropIndex(
                name: "IX_Rentas_LockerId",
                table: "Rentas");

            migrationBuilder.DropIndex(
                name: "IX_Rentas_UsuarioId",
                table: "Rentas");
        }
    }
}
