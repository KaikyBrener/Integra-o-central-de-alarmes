using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Centrals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    IP = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    Porta = table.Column<int>(type: "INTEGER", nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Senha = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    BuildingId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fabricante = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Modelo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Firmware = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Latencia = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Centrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Centrals_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Histories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Data = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CentralId = table.Column<int>(type: "INTEGER", nullable: false),
                    PGM = table.Column<int>(type: "INTEGER", nullable: false),
                    Comando = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Sucesso = table.Column<bool>(type: "INTEGER", nullable: false),
                    Resultado = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Histories_Centrals_CentralId",
                        column: x => x.CentralId,
                        principalTable: "Centrals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Centrals_BuildingId",
                table: "Centrals",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Histories_CentralId",
                table: "Histories",
                column: "CentralId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Histories");

            migrationBuilder.DropTable(
                name: "Centrals");

            migrationBuilder.DropTable(
                name: "Buildings");
        }
    }
}
