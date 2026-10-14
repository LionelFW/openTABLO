using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenTablo.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStopAreaZdc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ZdcId",
                table: "StopAreas",
                type: "text",
                nullable: true);

            // Forces the next startup to re-import stops, which fills ZdcId.
            migrationBuilder.Sql("""DELETE FROM "ReferentialImports" WHERE "Name" = 'idfm-transit'""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ZdcId",
                table: "StopAreas");
        }
    }
}
