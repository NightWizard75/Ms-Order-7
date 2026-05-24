using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false, comment: "Идентификатор заказа (связь без навигационного свойства)"),
                    EventType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "Полное имя типа события (для десериализации)"),
                    Payload = table.Column<string>(type: "jsonb", nullable: false, comment: "JSON-сериализованное тело события (PostgreSQL jsonb)"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP", comment: "Время создания сообщения (фиксация на уровне БД)"),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Время успешной публикации. NULL = ещё не опубликовано")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                },
                comment: "Transactional Outbox: События, ожидающие публикации в шину сообщений");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_published_at_created_at",
                schema: "public",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "public");
        }
    }
}
