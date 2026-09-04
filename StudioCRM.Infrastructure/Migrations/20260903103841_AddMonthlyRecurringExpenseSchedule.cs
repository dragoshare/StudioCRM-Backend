using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyRecurringExpenseSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecurrenceDayOfMonth",
                table: "CompanyExpenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecurrenceEndDate",
                table: "CompanyExpenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceInstanceNumber",
                table: "CompanyExpenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceIntervalMonths",
                table: "CompanyExpenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecurrenceStartDate",
                table: "CompanyExpenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyExpenses_RecurringGroupId",
                table: "CompanyExpenses",
                column: "RecurringGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyExpenses_RecurringGroupId_IssueDate",
                table: "CompanyExpenses",
                columns: new[] { "RecurringGroupId", "IssueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyExpenses_RecurringGroupId",
                table: "CompanyExpenses");

            migrationBuilder.DropIndex(
                name: "IX_CompanyExpenses_RecurringGroupId_IssueDate",
                table: "CompanyExpenses");

            migrationBuilder.DropColumn(
                name: "RecurrenceDayOfMonth",
                table: "CompanyExpenses");

            migrationBuilder.DropColumn(
                name: "RecurrenceEndDate",
                table: "CompanyExpenses");

            migrationBuilder.DropColumn(
                name: "RecurrenceInstanceNumber",
                table: "CompanyExpenses");

            migrationBuilder.DropColumn(
                name: "RecurrenceIntervalMonths",
                table: "CompanyExpenses");

            migrationBuilder.DropColumn(
                name: "RecurrenceStartDate",
                table: "CompanyExpenses");
        }
    }
}
