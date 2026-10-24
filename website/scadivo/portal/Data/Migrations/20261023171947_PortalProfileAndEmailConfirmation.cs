using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadivo.Portal.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortalProfileAndEmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityArea",
                table: "AspNetUsers",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActivityAreaOther",
                table: "AspNetUsers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "AspNetUsers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "AspNetUsers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MarketingEmailOptIn",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MarketingEmailOptInAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrganizationOrSchool",
                table: "AspNetUsers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProfileType",
                table: "AspNetUsers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityArea",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ActivityAreaOther",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MarketingEmailOptIn",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MarketingEmailOptInAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OrganizationOrSchool",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfileType",
                table: "AspNetUsers");
        }
    }
}
