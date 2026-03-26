using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace testapi1.Migrations
{
    /// <inheritdoc />
    public partial class PostgresAuthoritativeRuntimePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoreChunks_DocId",
                table: "LoreChunks");

            migrationBuilder.DropIndex(
                name: "IX_DialogueTemplates_NpcId",
                table: "DialogueTemplates");

            migrationBuilder.AddColumn<int>(
                name: "PlayerId",
                table: "ProgressionSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NpcCode",
                table: "Npcs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocKey",
                table: "LoreDocs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "LoreDocs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "LoreDocs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ChunkKey",
                table: "LoreChunks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChunkOrder",
                table: "LoreChunks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "LoreChunks",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "LoreChunks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "ActionCatalog",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressionEventType",
                table: "ActionCatalog",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgressionStateAllowedActions",
                columns: table => new
                {
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressionStateAllowedActions", x => new { x.State, x.ActionId });
                    table.ForeignKey(
                        name: "FK_ProgressionStateAllowedActions_ActionCatalog_ActionId",
                        column: x => x.ActionId,
                        principalTable: "ActionCatalog",
                        principalColumn: "ActionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "Players" ("PlayerId", "DisplayName", "CreatedAt")
                SELECT 1, 'Seed Player', NOW()
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "Players"
                    WHERE "PlayerId" = 1
                );
                """);

            migrationBuilder.Sql("""
                UPDATE "ProgressionSessions"
                SET "PlayerId" = 1
                WHERE "PlayerId" IS NULL OR "PlayerId" = 0;
                """);

            migrationBuilder.Sql("""
                UPDATE "Npcs"
                SET "NpcCode" = CONCAT('npc_', "NpcId")
                WHERE "NpcCode" IS NULL OR BTRIM("NpcCode") = '';
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "NpcId", "NpcCode",
                           ROW_NUMBER() OVER (PARTITION BY "NpcCode" ORDER BY "NpcId") AS rn
                    FROM "Npcs"
                )
                UPDATE "Npcs" n
                SET "NpcCode" = CONCAT(n."NpcCode", '_', n."NpcId")
                FROM ranked r
                WHERE n."NpcId" = r."NpcId" AND r.rn > 1;
                """);

            migrationBuilder.Sql("""
                UPDATE "LoreDocs"
                SET "DocKey" = CONCAT('doc_', "DocId")
                WHERE "DocKey" IS NULL OR BTRIM("DocKey") = '';
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "DocId", "DocKey",
                           ROW_NUMBER() OVER (PARTITION BY "DocKey" ORDER BY "DocId") AS rn
                    FROM "LoreDocs"
                )
                UPDATE "LoreDocs" d
                SET "DocKey" = CONCAT(d."DocKey", '_', d."DocId")
                FROM ranked r
                WHERE d."DocId" = r."DocId" AND r.rn > 1;
                """);

            migrationBuilder.Sql("""
                UPDATE "LoreChunks"
                SET "ChunkKey" = CONCAT('doc_', "DocId", '_chunk_', "ChunkId")
                WHERE "ChunkKey" IS NULL OR BTRIM("ChunkKey") = '';
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "ChunkId", "ChunkKey",
                           ROW_NUMBER() OVER (PARTITION BY "ChunkKey" ORDER BY "ChunkId") AS rn
                    FROM "LoreChunks"
                )
                UPDATE "LoreChunks" c
                SET "ChunkKey" = CONCAT(c."ChunkKey", '_', c."ChunkId")
                FROM ranked r
                WHERE c."ChunkId" = r."ChunkId" AND r.rn > 1;
                """);

            migrationBuilder.Sql("""
                WITH ordered AS (
                    SELECT "ChunkId",
                           ROW_NUMBER() OVER (PARTITION BY "DocId" ORDER BY "ChunkId") AS rn
                    FROM "LoreChunks"
                )
                UPDATE "LoreChunks" c
                SET "ChunkOrder" = o.rn
                FROM ordered o
                WHERE c."ChunkId" = o."ChunkId"
                  AND (c."ChunkOrder" IS NULL OR c."ChunkOrder" = 0);
                """);

            migrationBuilder.Sql("""
                UPDATE "LoreChunks"
                SET "UpdatedAt" = NOW()
                WHERE "UpdatedAt" IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE "ActionCatalog"
                SET "Code" = CONCAT('ACTION_', "ActionId")
                WHERE "Code" IS NULL OR BTRIM("Code") = '';
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "ActionId", "Code",
                           ROW_NUMBER() OVER (PARTITION BY "Code" ORDER BY "ActionId") AS rn
                    FROM "ActionCatalog"
                )
                UPDATE "ActionCatalog" a
                SET "Code" = CONCAT(a."Code", '_', a."ActionId")
                FROM ranked r
                WHERE a."ActionId" = r."ActionId" AND r.rn > 1;
                """);

            migrationBuilder.Sql("""
                UPDATE "ActionCatalog"
                SET "ProgressionEventType" = CASE
                    WHEN COALESCE("Code", '') = 'ASK_OPEN_QUESTION' OR COALESCE("IntentTag", '') = 'ASK_OPEN_QUESTION' THEN 'AskOpenQuestion'
                    WHEN COALESCE("Code", '') = 'ASK_TIMELINE' OR COALESCE("IntentTag", '') = 'ASK_TIMELINE' THEN 'AskTimeline'
                    WHEN COALESCE("Code", '') = 'EMPATHY' OR COALESCE("IntentTag", '') = 'EMPATHY' THEN 'Empathy'
                    WHEN COALESCE("Code", '') = 'PRESENT_EVIDENCE' OR COALESCE("IntentTag", '') = 'PRESENT_EVIDENCE' THEN 'PresentEvidence'
                    WHEN COALESCE("Code", '') = 'CONTRADICTION' OR COALESCE("IntentTag", '') = 'CONTRADICTION' THEN 'Contradiction'
                    WHEN COALESCE("Code", '') = 'SILENCE' OR COALESCE("IntentTag", '') = 'SILENCE' THEN 'Silence'
                    WHEN COALESCE("Code", '') = 'INTIMIDATE' OR COALESCE("IntentTag", '') = 'INTIMIDATE' THEN 'Intimidate'
                    WHEN COALESCE("Code", '') = 'CLOSE_INTERROGATION' OR COALESCE("IntentTag", '') = 'CLOSE_INTERROGATION' THEN 'CloseInterrogation'
                    ELSE 'Unknown'
                END
                WHERE "ProgressionEventType" IS NULL OR BTRIM("ProgressionEventType") = '';
                """);

            migrationBuilder.Sql("""
                UPDATE "DialogueTemplates"
                SET "ToneTag" = 'neutral'
                WHERE "ToneTag" IS NULL OR BTRIM("ToneTag") = '';
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "TemplateId",
                           ROW_NUMBER() OVER (PARTITION BY "NpcId", "ActionId", "ToneTag" ORDER BY "TemplateId") AS rn
                    FROM "DialogueTemplates"
                )
                UPDATE "DialogueTemplates" d
                SET "ToneTag" = CONCAT(d."ToneTag", '_', d."TemplateId")
                FROM ranked r
                WHERE d."TemplateId" = r."TemplateId" AND r.rn > 1;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "PlayerId",
                table: "ProgressionSessions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NpcCode",
                table: "Npcs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DocKey",
                table: "LoreDocs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChunkKey",
                table: "LoreChunks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ChunkOrder",
                table: "LoreChunks",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "LoreChunks",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProgressionEventType",
                table: "ActionCatalog",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressionSessions_PlayerId_UpdatedAtUtc",
                table: "ProgressionSessions",
                columns: new[] { "PlayerId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_NpcCode",
                table: "Npcs",
                column: "NpcCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoreDocs_DocKey",
                table: "LoreDocs",
                column: "DocKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoreDocs_IsActive",
                table: "LoreDocs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_LoreChunks_ChunkKey",
                table: "LoreChunks",
                column: "ChunkKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoreChunks_DocId_ChunkOrder",
                table: "LoreChunks",
                columns: new[] { "DocId", "ChunkOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LoreChunks_IsActive",
                table: "LoreChunks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueTemplates_NpcId_ActionId_ToneTag",
                table: "DialogueTemplates",
                columns: new[] { "NpcId", "ActionId", "ToneTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActionCatalog_Code",
                table: "ActionCatalog",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressionStateAllowedActions_ActionId",
                table: "ProgressionStateAllowedActions",
                column: "ActionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressionSessions_Players_PlayerId",
                table: "ProgressionSessions",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgressionSessions_Players_PlayerId",
                table: "ProgressionSessions");

            migrationBuilder.DropTable(
                name: "ProgressionStateAllowedActions");

            migrationBuilder.DropIndex(
                name: "IX_ProgressionSessions_PlayerId_UpdatedAtUtc",
                table: "ProgressionSessions");

            migrationBuilder.DropIndex(
                name: "IX_Npcs_NpcCode",
                table: "Npcs");

            migrationBuilder.DropIndex(
                name: "IX_LoreDocs_DocKey",
                table: "LoreDocs");

            migrationBuilder.DropIndex(
                name: "IX_LoreDocs_IsActive",
                table: "LoreDocs");

            migrationBuilder.DropIndex(
                name: "IX_LoreChunks_ChunkKey",
                table: "LoreChunks");

            migrationBuilder.DropIndex(
                name: "IX_LoreChunks_DocId_ChunkOrder",
                table: "LoreChunks");

            migrationBuilder.DropIndex(
                name: "IX_LoreChunks_IsActive",
                table: "LoreChunks");

            migrationBuilder.DropIndex(
                name: "IX_DialogueTemplates_NpcId_ActionId_ToneTag",
                table: "DialogueTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ActionCatalog_Code",
                table: "ActionCatalog");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "ProgressionSessions");

            migrationBuilder.DropColumn(
                name: "NpcCode",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "DocKey",
                table: "LoreDocs");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "LoreDocs");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "LoreDocs");

            migrationBuilder.DropColumn(
                name: "ChunkKey",
                table: "LoreChunks");

            migrationBuilder.DropColumn(
                name: "ChunkOrder",
                table: "LoreChunks");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "LoreChunks");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "LoreChunks");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "ActionCatalog");

            migrationBuilder.DropColumn(
                name: "ProgressionEventType",
                table: "ActionCatalog");

            migrationBuilder.CreateIndex(
                name: "IX_LoreChunks_DocId",
                table: "LoreChunks",
                column: "DocId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueTemplates_NpcId",
                table: "DialogueTemplates",
                column: "NpcId");
        }
    }
}
