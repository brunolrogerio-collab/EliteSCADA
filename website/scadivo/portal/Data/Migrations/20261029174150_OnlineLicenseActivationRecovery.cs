using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadivo.Portal.Data.Migrations
{
    /// <inheritdoc />
    public partial class OnlineLicenseActivationRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ExpectedCredentialGeneration",
                table: "ProductLicensePreparations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedLeaseEnvelope",
                table: "ProductLicensePreparations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Reactivation",
                table: "ProductLicensePreparations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryCredentialHash",
                table: "ProductLicensePreparations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CredentialGeneration",
                table: "ProductLicenseActivations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_ProductLicenseActivations_LicenseId_MachineFingerprint_Stat~",
                table: "ProductLicenseActivations",
                columns: new[] { "LicenseId", "MachineFingerprint", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductLicenseActivations_LicenseId_MachineFingerprint_Stat~",
                table: "ProductLicenseActivations");

            migrationBuilder.DropColumn(
                name: "ExpectedCredentialGeneration",
                table: "ProductLicensePreparations");

            migrationBuilder.DropColumn(
                name: "ConfirmedLeaseEnvelope",
                table: "ProductLicensePreparations");

            migrationBuilder.DropColumn(
                name: "Reactivation",
                table: "ProductLicensePreparations");

            migrationBuilder.DropColumn(
                name: "RecoveryCredentialHash",
                table: "ProductLicensePreparations");

            migrationBuilder.DropColumn(
                name: "CredentialGeneration",
                table: "ProductLicenseActivations");
        }
    }
}
