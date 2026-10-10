using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Security
{
    /// <inheritdoc />
    public partial class AddUserInvitationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                schema: "security",
                table: "UserInvitations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "security",
                table: "UserInvitations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                schema: "security",
                table: "UserInvitations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "security",
                table: "UserInvitations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "security",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "security",
                table: "UserInvitations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "security",
                table: "UserInvitations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "security",
                table: "UserInvitations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "security",
                table: "UserInvitations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "security",
                table: "AspNetUsers");
        }
    }
}
