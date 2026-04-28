using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TourApp.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdAndNameToUserLocationLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "UserLocationLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "UserLocationLogs",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "UserLocationLogs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "UserLocationLogs");
        }
    }
}
