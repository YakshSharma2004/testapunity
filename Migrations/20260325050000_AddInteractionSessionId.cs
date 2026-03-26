using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using testapi1.Infrastructure.Persistence;

#nullable disable

namespace testapi1.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260325050000_AddInteractionSessionId")]
    public partial class AddInteractionSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "Interactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_SessionId_OccurredAt",
                table: "Interactions",
                columns: new[] { "SessionId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Interactions_SessionId_OccurredAt",
                table: "Interactions");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Interactions");
        }
    }
}
