using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace testapi1.Migrations
{
    /// <inheritdoc />
    public partial class ProgressionClueTrackingV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanConfess",
                table: "ProgressionSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ClueClickHistoryJson",
                table: "ProgressionSessions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ComposureState",
                table: "ProgressionSessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Calm");

            migrationBuilder.AddColumn<string>(
                name: "DiscoveredClueIdsJson",
                table: "ProgressionSessions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "DiscussedClueIdsJson",
                table: "ProgressionSessions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ProofTier",
                table: "ProgressionSessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanConfess",
                table: "ProgressionSessions");

            migrationBuilder.DropColumn(
                name: "ClueClickHistoryJson",
                table: "ProgressionSessions");

            migrationBuilder.DropColumn(
                name: "ComposureState",
                table: "ProgressionSessions");

            migrationBuilder.DropColumn(
                name: "DiscoveredClueIdsJson",
                table: "ProgressionSessions");

            migrationBuilder.DropColumn(
                name: "DiscussedClueIdsJson",
                table: "ProgressionSessions");

            migrationBuilder.DropColumn(
                name: "ProofTier",
                table: "ProgressionSessions");
        }
    }
}
