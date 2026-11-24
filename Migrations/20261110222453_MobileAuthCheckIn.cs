using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace uGYM.API.Migrations
{
    /// <inheritdoc />
    public partial class MobileAuthCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DiaLocal",
                table: "CheckIns",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OperadorId",
                table: "CheckIns",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NomeNormalizado = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SenhaHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_AlunoId_DataHora",
                table: "CheckIns",
                columns: new[] { "AlunoId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_AlunoId_DiaLocal",
                table: "CheckIns",
                columns: new[] { "AlunoId", "DiaLocal" },
                unique: true,
                filter: "[DiaLocal] IS NOT NULL AND [Permitido] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NomeNormalizado",
                table: "Usuarios",
                column: "NomeNormalizado",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rollback bloqueado para preservar usuários e histórico. Restaure um backup revisado se necessário.");
        }
    }
}
