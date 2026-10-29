using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AlmacenInteligente.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    creado_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direccion_calle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    direccion_codigo_postal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    direccion_colonia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    direccion_estado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    direccion_municipio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    direccion_numero_exterior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    direccion_numero_interior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    direccion_pais = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    rfc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clientes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "proveedores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    creado_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direccion_calle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    direccion_codigo_postal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    direccion_colonia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    direccion_estado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    direccion_municipio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    direccion_numero_exterior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    direccion_numero_interior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    direccion_pais = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    rfc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proveedores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_roles_claims_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuarios_claims_usuarios_user_id",
                        column: x => x.user_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_usuarios_logins_usuarios_user_id",
                        column: x => x.user_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_usuarios_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_usuarios_roles_usuarios_user_id",
                        column: x => x.user_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_tokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_usuarios_tokens_usuarios_user_id",
                        column: x => x.user_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clientes_codigo",
                table: "clientes",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_proveedores_codigo",
                table: "proveedores",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_claims_role_id",
                table: "roles_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "usuarios",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "usuarios",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_claims_user_id",
                table: "usuarios_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_logins_user_id",
                table: "usuarios_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_roles_role_id",
                table: "usuarios_roles",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clientes");

            migrationBuilder.DropTable(
                name: "proveedores");

            migrationBuilder.DropTable(
                name: "roles_claims");

            migrationBuilder.DropTable(
                name: "usuarios_claims");

            migrationBuilder.DropTable(
                name: "usuarios_logins");

            migrationBuilder.DropTable(
                name: "usuarios_roles");

            migrationBuilder.DropTable(
                name: "usuarios_tokens");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
