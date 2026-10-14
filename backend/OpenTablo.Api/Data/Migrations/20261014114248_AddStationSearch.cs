using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTablo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStationSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchName",
                table: "StopAreas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Town",
                table: "StopAreas",
                type: "text",
                nullable: true);

            // Forces a transit re-import on next start: it fills the new columns and the rail line links.
            migrationBuilder.Sql("""DELETE FROM "ReferentialImports" WHERE "Name" = 'idfm-transit'""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchName",
                table: "StopAreas");

            migrationBuilder.DropColumn(
                name: "Town",
                table: "StopAreas");
        }
    }
}
