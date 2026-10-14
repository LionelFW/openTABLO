using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTablo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLineRealtime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasRealtime",
                table: "Lines",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Forces the next startup to re-import lines, which sets the flag from realtime-lines.json.
            migrationBuilder.Sql("""DELETE FROM "ReferentialImports" WHERE "Name" = 'idfm-transit'""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasRealtime",
                table: "Lines");
        }
    }
}
