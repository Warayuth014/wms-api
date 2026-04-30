using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCheckInSlotTrackingId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrackingId",
                schema: "packing",
                table: "CheckInSlots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrackingId",
                schema: "packing",
                table: "CheckInSlots",
                type: "nvarchar(50)",
                nullable: true);
        }
    }
}
