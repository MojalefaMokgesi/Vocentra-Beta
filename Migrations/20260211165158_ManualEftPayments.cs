using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vocentra.Migrations
{
    /// <inheritdoc />
    public partial class ManualEftPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProviderPaymentId",
                table: "PaymentTransactions",
                newName: "UserPaymentReference");

            migrationBuilder.AddColumn<string>(
                name: "ProofFilePath",
                table: "PaymentTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProofSubmittedAtUtc",
                table: "PaymentTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "PaymentTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "PaymentTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "PaymentTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserBankName",
                table: "PaymentTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentStatus",
                table: "Jobs",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentReference",
                table: "Jobs",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentProvider",
                table: "Jobs",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProofFilePath",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ProofSubmittedAtUtc",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "UserBankName",
                table: "PaymentTransactions");

            migrationBuilder.RenameColumn(
                name: "UserPaymentReference",
                table: "PaymentTransactions",
                newName: "ProviderPaymentId");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentStatus",
                table: "Jobs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentReference",
                table: "Jobs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentProvider",
                table: "Jobs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
