using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace University.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LicznikiIndeksow",
                columns: table => new
                {
                    Prefix = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    AktualnaWartosc = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicznikiIndeksow", x => x.Prefix);
                });

            migrationBuilder.CreateTable(
                name: "Profesorowie",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Imie = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Nazwisko = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IndeksUczelniany = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TytulNaukowy = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Ulica = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Miasto = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    KodPocztowy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OstatniaModyfikacja = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profesorowie", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Wydzialy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nazwa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wydzialy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gabinety",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumerPokoju = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Budynek = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProfesorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gabinety", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gabinety_Profesorowie_ProfesorId",
                        column: x => x.ProfesorId,
                        principalTable: "Profesorowie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Studenci",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Imie = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Nazwisko = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IndeksUczelniany = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RokStudiow = table.Column<int>(type: "int", nullable: false),
                    Ulica = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Miasto = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    KodPocztowy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OstatniaModyfikacja = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TypStudenta = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TematPracyDyplomowej = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PromotorId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Studenci", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Studenci_Profesorowie_PromotorId",
                        column: x => x.PromotorId,
                        principalTable: "Profesorowie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Kursy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nazwa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    KodKursu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PunktyECTS = table.Column<int>(type: "int", nullable: false),
                    WydzialId = table.Column<int>(type: "int", nullable: false),
                    ProwadzacyId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kursy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kursy_Profesorowie_ProwadzacyId",
                        column: x => x.ProwadzacyId,
                        principalTable: "Profesorowie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Kursy_Wydzialy_WydzialId",
                        column: x => x.WydzialId,
                        principalTable: "Wydzialy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WymaganiaWstepneKursow",
                columns: table => new
                {
                    KursId = table.Column<int>(type: "int", nullable: false),
                    WymaganieWstepneId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WymaganiaWstepneKursow", x => new { x.KursId, x.WymaganieWstepneId });
                    table.ForeignKey(
                        name: "FK_WymaganiaWstepneKursow_Kursy_KursId",
                        column: x => x.KursId,
                        principalTable: "Kursy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WymaganiaWstepneKursow_Kursy_WymaganieWstepneId",
                        column: x => x.WymaganieWstepneId,
                        principalTable: "Kursy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Zapisy",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    KursId = table.Column<int>(type: "int", nullable: false),
                    Ocena = table.Column<double>(type: "float", nullable: true),
                    Semestr = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zapisy", x => new { x.StudentId, x.KursId });
                    table.ForeignKey(
                        name: "FK_Zapisy_Kursy_KursId",
                        column: x => x.KursId,
                        principalTable: "Kursy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Zapisy_Studenci_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Studenci",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "LicznikiIndeksow",
                columns: new[] { "Prefix", "AktualnaWartosc" },
                values: new object[,]
                {
                    { "P", 100 },
                    { "S", 1000 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gabinety_ProfesorId",
                table: "Gabinety",
                column: "ProfesorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kursy_ProwadzacyId",
                table: "Kursy",
                column: "ProwadzacyId");

            migrationBuilder.CreateIndex(
                name: "IX_Kursy_WydzialId",
                table: "Kursy",
                column: "WydzialId");

            migrationBuilder.CreateIndex(
                name: "IX_Profesorowie_IndeksUczelniany",
                table: "Profesorowie",
                column: "IndeksUczelniany",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Studenci_IndeksUczelniany",
                table: "Studenci",
                column: "IndeksUczelniany",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Studenci_PromotorId",
                table: "Studenci",
                column: "PromotorId");

            migrationBuilder.CreateIndex(
                name: "IX_WymaganiaWstepneKursow_WymaganieWstepneId",
                table: "WymaganiaWstepneKursow",
                column: "WymaganieWstepneId");

            migrationBuilder.CreateIndex(
                name: "IX_Zapisy_KursId",
                table: "Zapisy",
                column: "KursId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Gabinety");

            migrationBuilder.DropTable(
                name: "LicznikiIndeksow");

            migrationBuilder.DropTable(
                name: "WymaganiaWstepneKursow");

            migrationBuilder.DropTable(
                name: "Zapisy");

            migrationBuilder.DropTable(
                name: "Kursy");

            migrationBuilder.DropTable(
                name: "Studenci");

            migrationBuilder.DropTable(
                name: "Wydzialy");

            migrationBuilder.DropTable(
                name: "Profesorowie");
        }
    }
}
