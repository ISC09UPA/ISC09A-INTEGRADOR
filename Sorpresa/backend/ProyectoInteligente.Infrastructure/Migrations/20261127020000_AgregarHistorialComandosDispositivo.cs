using LockerSmart.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace LockerSmart.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261127020000_AgregarHistorialComandosDispositivo")]
public partial class AgregarHistorialComandosDispositivo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ComandosDispositivo",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                CommandId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LockerCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                RentaId = table.Column<int>(type: "int", nullable: true),
                UsuarioId = table.Column<int>(type: "int", nullable: false),
                Accion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RespuestaDispositivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ActualizadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ComandosDispositivo", x => x.Id));
        migrationBuilder.CreateIndex("IX_ComandosDispositivo_CommandId", "ComandosDispositivo", "CommandId", unique: true);
        migrationBuilder.CreateIndex("IX_ComandosDispositivo_RentaId", "ComandosDispositivo", "RentaId");
        migrationBuilder.CreateIndex("IX_ComandosDispositivo_UsuarioId", "ComandosDispositivo", "UsuarioId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ComandosDispositivo");
    }
}
