using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace testapi1.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateAllDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionCatalog",
                columns: table => new
                {
                    ActionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    IntentTag = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionCatalog", x => x.ActionId);
                });

            migrationBuilder.CreateTable(
                name: "Npcs",
                columns: table => new
                {
                    NpcId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Archetype = table.Column<string>(type: "text", nullable: false),
                    BaseFriendliness = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    BasePatience = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    BaseCuriosity = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    BaseOpenness = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    BaseConfidence = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Npcs", x => x.NpcId);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.PlayerId);
                });

            migrationBuilder.CreateTable(
                name: "ProgressionSessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CaseId = table.Column<string>(type: "text", nullable: false),
                    NpcId = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TurnCount = table.Column<int>(type: "integer", nullable: false),
                    TrustScore = table.Column<int>(type: "integer", nullable: false),
                    ShutdownScore = table.Column<int>(type: "integer", nullable: false),
                    IsTerminal = table.Column<bool>(type: "boolean", nullable: false),
                    Ending = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PresentedEvidenceJson = table.Column<string>(type: "text", nullable: false),
                    HistoryJson = table.Column<string>(type: "text", nullable: false),
                    LastTransitionReason = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressionSessions", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "DialogueTemplates",
                columns: table => new
                {
                    TemplateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NpcId = table.Column<int>(type: "integer", nullable: false),
                    ActionId = table.Column<int>(type: "integer", nullable: false),
                    ToneTag = table.Column<string>(type: "text", nullable: false),
                    TemplateText = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueTemplates", x => x.TemplateId);
                    table.ForeignKey(
                        name: "FK_DialogueTemplates_ActionCatalog_ActionId",
                        column: x => x.ActionId,
                        principalTable: "ActionCatalog",
                        principalColumn: "ActionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DialogueTemplates_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "NpcId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoreDocs",
                columns: table => new
                {
                    DocId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NpcId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoreDocs", x => x.DocId);
                    table.ForeignKey(
                        name: "FK_LoreDocs_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "NpcId");
                });

            migrationBuilder.CreateTable(
                name: "Interactions",
                columns: table => new
                {
                    InteractionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    NpcId = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    PlayerAction = table.Column<string>(type: "text", nullable: false),
                    PlayerText = table.Column<string>(type: "text", nullable: false),
                    NluTopIntent = table.Column<string>(type: "text", nullable: false),
                    Sentiment = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    Friendliness = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    ToneTag = table.Column<string>(type: "text", nullable: false),
                    NsfwFlag = table.Column<bool>(type: "boolean", nullable: false),
                    ChosenActionId = table.Column<int>(type: "integer", nullable: true),
                    ResponseText = table.Column<string>(type: "text", nullable: false),
                    ResponseSource = table.Column<string>(type: "text", nullable: false),
                    ModelVersion = table.Column<string>(type: "text", nullable: false),
                    RewardScore = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    OutcomeFlags = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interactions", x => x.InteractionId);
                    table.ForeignKey(
                        name: "FK_Interactions_ActionCatalog_ChosenActionId",
                        column: x => x.ChosenActionId,
                        principalTable: "ActionCatalog",
                        principalColumn: "ActionId");
                    table.ForeignKey(
                        name: "FK_Interactions_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "NpcId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Interactions_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerNpcStates",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    NpcId = table.Column<int>(type: "integer", nullable: false),
                    Trust = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    Patience = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    Curiosity = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    Openness = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    Memory = table.Column<string>(type: "text", nullable: false),
                    LastInteractionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerNpcStates", x => new { x.PlayerId, x.NpcId });
                    table.ForeignKey(
                        name: "FK_PlayerNpcStates_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "NpcId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerNpcStates_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoreChunks",
                columns: table => new
                {
                    ChunkId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocId = table.Column<int>(type: "integer", nullable: false),
                    ChunkText = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoreChunks", x => x.ChunkId);
                    table.ForeignKey(
                        name: "FK_LoreChunks_LoreDocs_DocId",
                        column: x => x.DocId,
                        principalTable: "LoreDocs",
                        principalColumn: "DocId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueTemplates_ActionId",
                table: "DialogueTemplates",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueTemplates_NpcId",
                table: "DialogueTemplates",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_ChosenActionId",
                table: "Interactions",
                column: "ChosenActionId");

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_NpcId",
                table: "Interactions",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_PlayerId",
                table: "Interactions",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_LoreChunks_DocId",
                table: "LoreChunks",
                column: "DocId");

            migrationBuilder.CreateIndex(
                name: "IX_LoreDocs_NpcId",
                table: "LoreDocs",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerNpcStates_NpcId",
                table: "PlayerNpcStates",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressionSessions_ExpiresAtUtc",
                table: "ProgressionSessions",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DialogueTemplates");

            migrationBuilder.DropTable(
                name: "Interactions");

            migrationBuilder.DropTable(
                name: "LoreChunks");

            migrationBuilder.DropTable(
                name: "PlayerNpcStates");

            migrationBuilder.DropTable(
                name: "ProgressionSessions");

            migrationBuilder.DropTable(
                name: "ActionCatalog");

            migrationBuilder.DropTable(
                name: "LoreDocs");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Npcs");
        }
    }
}
