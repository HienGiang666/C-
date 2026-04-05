using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TourApp.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLocationAndTourPOI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TourPOIs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TourId = table.Column<int>(type: "int", nullable: false),
                    POIId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TourPOIs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TourPOIs_POIs_POIId",
                        column: x => x.POIId,
                        principalTable: "POIs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TourPOIs_Tours_TourId",
                        column: x => x.TourId,
                        principalTable: "Tours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLocationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLocationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Audios_POIId",
                table: "Audios",
                column: "POIId");

            migrationBuilder.CreateIndex(
                name: "IX_TourPOIs_POIId",
                table: "TourPOIs",
                column: "POIId");

            migrationBuilder.CreateIndex(
                name: "IX_TourPOIs_TourId",
                table: "TourPOIs",
                column: "TourId");

            migrationBuilder.AddForeignKey(
                name: "FK_Audios_POIs_POIId",
                table: "Audios",
                column: "POIId",
                principalTable: "POIs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Audios_POIs_POIId",
                table: "Audios");

            migrationBuilder.DropTable(
                name: "TourPOIs");

            migrationBuilder.DropTable(
                name: "UserLocationLogs");

            migrationBuilder.DropIndex(
                name: "IX_Audios_POIId",
                table: "Audios");
        }
    }
}
