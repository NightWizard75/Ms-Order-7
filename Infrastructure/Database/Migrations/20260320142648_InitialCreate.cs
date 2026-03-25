using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PriceInKopecks = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalAmountInKopecks = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Order_PriceInKopecks_NonNegative", "\"PriceInKopecks\" >= 0");
                    table.CheckConstraint("CK_Order_Quantity_Positive", "\"Quantity\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CorrelationId",
                table: "Orders",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerEmail",
                table: "Orders",
                column: "CustomerEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
