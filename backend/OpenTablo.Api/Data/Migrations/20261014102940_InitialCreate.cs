using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace OpenTablo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "Lines",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    TextColor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrimQuota",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Used = table.Column<int>(type: "integer", nullable: false),
                    NextSlot = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrimQuota", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferentialImports",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferentialImports", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "StopAreas",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<Point>(type: "geography (point, 4326)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StopAreas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VelibStations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VelibStations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StopAreaLines",
                columns: table => new
                {
                    LinesId = table.Column<string>(type: "text", nullable: false),
                    StopAreasId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StopAreaLines", x => new { x.LinesId, x.StopAreasId });
                    table.ForeignKey(
                        name: "FK_StopAreaLines_Lines_LinesId",
                        column: x => x.LinesId,
                        principalTable: "Lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StopAreaLines_StopAreas_StopAreasId",
                        column: x => x.StopAreasId,
                        principalTable: "StopAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PrimQuota",
                columns: new[] { "Id", "Day", "NextSlot", "Used" },
                values: new object[] { 1, new DateOnly(1, 1, 1), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0 });

            migrationBuilder.CreateIndex(
                name: "IX_StopAreaLines_StopAreasId",
                table: "StopAreaLines",
                column: "StopAreasId");

            migrationBuilder.CreateIndex(
                name: "IX_StopAreas_Location",
                table: "StopAreas",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_VelibStations_Location",
                table: "VelibStations",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrimQuota");

            migrationBuilder.DropTable(
                name: "ReferentialImports");

            migrationBuilder.DropTable(
                name: "StopAreaLines");

            migrationBuilder.DropTable(
                name: "VelibStations");

            migrationBuilder.DropTable(
                name: "Lines");

            migrationBuilder.DropTable(
                name: "StopAreas");
        }
    }
}
