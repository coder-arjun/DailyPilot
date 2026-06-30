using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyPilot.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitRemindersAndSpeak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReminderSentUtc",
                table: "Habits",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderEnabled",
                table: "Habits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ReminderEnd",
                table: "Habits",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "ReminderIntervalMinutes",
                table: "Habits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ReminderStart",
                table: "Habits",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "SpeakReminders",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastReminderSentUtc",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ReminderEnabled",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ReminderEnd",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ReminderIntervalMinutes",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ReminderStart",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "SpeakReminders",
                table: "AspNetUsers");
        }
    }
}
