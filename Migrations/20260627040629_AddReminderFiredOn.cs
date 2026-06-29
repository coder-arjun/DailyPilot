using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyPilot.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderFiredOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ReminderFiredOn",
                table: "Tasks",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderFiredOn",
                table: "Tasks");
        }
    }
}
