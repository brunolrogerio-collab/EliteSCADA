using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadivo.Portal.Data.Migrations;

public partial class PortalOnlineLicensing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProductApplications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductApplications", x => x.Id);
                table.UniqueConstraint("AK_ProductApplications_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                table.ForeignKey("FK_ProductApplications_Organizations_OrganizationId", x => x.OrganizationId,
                    principalTable: "Organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProductLicenses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PublicCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                Tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                InteractiveSeats = table.Column<int>(type: "integer", nullable: false),
                ViewOnlySeats = table.Column<int>(type: "integer", nullable: false),
                HaRuntime = table.Column<bool>(type: "boolean", nullable: false),
                AssetProcessIntelligence = table.Column<bool>(type: "boolean", nullable: false),
                Term = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                EntitlementExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                MaxActivations = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedByUserId = table.Column<string>(type: "text", nullable: true),
                RevocationReason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductLicenses", x => x.Id);
                table.UniqueConstraint("AK_ProductLicenses_OrganizationId_ApplicationId_Id", x => new { x.OrganizationId, x.ApplicationId, x.Id });
                table.ForeignKey("FK_ProductLicenses_AspNetUsers_CreatedByUserId", x => x.CreatedByUserId,
                    principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProductLicenses_AspNetUsers_RevokedByUserId", x => x.RevokedByUserId,
                    principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProductLicenses_Organizations_OrganizationId", x => x.OrganizationId,
                    principalTable: "Organizations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProductLicenses_ProductApplications_OrganizationId_ApplicationId",
                    x => new { x.OrganizationId, x.ApplicationId }, principalTable: "ProductApplications",
                    principalColumns: new[] { "OrganizationId", "Id" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProductLicenseActivations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<string>(type: "text", nullable: false),
                MachineFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastRenewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedByUserId = table.Column<string>(type: "text", nullable: true),
                RevocationReason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                LastActivationRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                LastActivationCredentialHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ActivationLeaseEnvelope = table.Column<string>(type: "text", nullable: true),
                LastRenewalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                LastRenewalInputCredentialHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                LastRenewalOutputCredentialHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                LastLeaseEnvelope = table.Column<string>(type: "text", nullable: true),
                LastLeaseIssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductLicenseActivations", x => x.Id);
                table.ForeignKey("FK_ProductLicenseActivations_AspNetUsers_RevokedByUserId", x => x.RevokedByUserId,
                    principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProductLicenseActivations_AspNetUsers_UserId", x => x.UserId,
                    principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProductLicenseActivations_ProductLicenses_OrganizationId_ApplicationId_LicenseId",
                    x => new { x.OrganizationId, x.ApplicationId, x.LicenseId }, principalTable: "ProductLicenses",
                    principalColumns: new[] { "OrganizationId", "ApplicationId", "Id" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProductLicenseRefreshCredentials",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActivationId = table.Column<Guid>(type: "uuid", nullable: false),
                CredentialHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SupersededAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductLicenseRefreshCredentials", x => x.Id);
                table.ForeignKey("FK_ProductLicenseRefreshCredentials_ProductLicenseActivations_ActivationId",
                    x => x.ActivationId, principalTable: "ProductLicenseActivations", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_ProductApplications_OrganizationId_NormalizedName", "ProductApplications",
            new[] { "OrganizationId", "NormalizedName" }, unique: true);
        migrationBuilder.CreateIndex("IX_ProductLicenses_CreatedByUserId", "ProductLicenses", "CreatedByUserId");
        migrationBuilder.CreateIndex("IX_ProductLicenses_OrganizationId_ApplicationId_Status", "ProductLicenses",
            new[] { "OrganizationId", "ApplicationId", "Status" });
        migrationBuilder.CreateIndex("IX_ProductLicenses_PublicCode", "ProductLicenses", "PublicCode", unique: true);
        migrationBuilder.CreateIndex("IX_ProductLicenses_RevokedByUserId", "ProductLicenses", "RevokedByUserId");
        migrationBuilder.CreateIndex("IX_ProductLicenseActivations_LastActivationRequestId", "ProductLicenseActivations",
            "LastActivationRequestId", unique: true, filter: "\"LastActivationRequestId\" IS NOT NULL");
        migrationBuilder.CreateIndex("IX_ProductLicenseActivations_LastRenewalRequestId", "ProductLicenseActivations",
            "LastRenewalRequestId", unique: true, filter: "\"LastRenewalRequestId\" IS NOT NULL");
        migrationBuilder.CreateIndex("IX_ProductLicenseActivations_LicenseId_Status", "ProductLicenseActivations",
            new[] { "LicenseId", "Status" });
        migrationBuilder.CreateIndex("IX_ProductLicenseActivations_OrganizationId_ApplicationId_LicenseId", "ProductLicenseActivations",
            new[] { "OrganizationId", "ApplicationId", "LicenseId" });
        migrationBuilder.CreateIndex("IX_ProductLicenseActivations_RevokedByUserId", "ProductLicenseActivations", "RevokedByUserId");
        migrationBuilder.CreateIndex("IX_ProductLicenseActivations_UserId", "ProductLicenseActivations", "UserId");
        migrationBuilder.CreateIndex("IX_ProductLicenseRefreshCredentials_ActivationId_State", "ProductLicenseRefreshCredentials",
            new[] { "ActivationId", "State" });
        migrationBuilder.CreateIndex("IX_ProductLicenseRefreshCredentials_CredentialHash", "ProductLicenseRefreshCredentials",
            "CredentialHash", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProductLicenseRefreshCredentials");
        migrationBuilder.DropTable(name: "ProductLicenseActivations");
        migrationBuilder.DropTable(name: "ProductLicenses");
        migrationBuilder.DropTable(name: "ProductApplications");
    }
}
