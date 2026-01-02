using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vocentra.Migrations
{
    /// <inheritdoc />
    public partial class UpdateApplicantModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AppliedAt",
                table: "Applicants",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedAt",
                table: "Applicants");
        }
    }
}
