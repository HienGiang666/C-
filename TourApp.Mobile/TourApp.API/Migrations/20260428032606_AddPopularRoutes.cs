using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TourApp.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPopularRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PopularRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    OriginLat = table.Column<double>(type: "float", nullable: false),
                    OriginLng = table.Column<double>(type: "float", nullable: false),
                    DestLat = table.Column<double>(type: "float", nullable: false),
                    DestLng = table.Column<double>(type: "float", nullable: false),
                    Polyline = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UseCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PopularRoutes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PopularRoutes");
        }
    }
}
