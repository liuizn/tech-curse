using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechCurse.Migrations
{
    /// <inheritdoc />
    public partial class InclusaoCamposEntidadePagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptUrl",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptUrl",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "Payments");
        }
    }
}
