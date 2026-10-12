using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StudioCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationDataBoundaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLegalConsents_LegalEntityId",
                table: "UserLegalConsents");

            migrationBuilder.DropIndex(
                name: "IX_TrainerLocations_LocationId",
                table: "TrainerLocations");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ClientId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_PackageId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_SessionParticipants_ClientId",
                table: "SessionParticipants");

            migrationBuilder.DropIndex(
                name: "IX_SessionParticipants_PackageId",
                table: "SessionParticipants");

            migrationBuilder.DropIndex(
                name: "IX_SessionCorrections_SessionId",
                table: "SessionCorrections");

            migrationBuilder.DropIndex(
                name: "IX_Locations_LegalEntityId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_ClientId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_LocationId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_GroupEnrollmentRequests_ClientId",
                table: "GroupEnrollmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_GroupEnrollmentRequests_ClientPackageId",
                table: "GroupEnrollmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_ExternalCalendarEvents_SessionId",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_Clients_ActivePackageId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_LocationId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_LocationId",
                table: "ClientPackages");

            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_PackageId",
                table: "ClientPackages");

            migrationBuilder.DropIndex(
                name: "IX_ClientMilestones_ClientId",
                table: "ClientMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ClientMilestones_MilestoneDefinitionId",
                table: "ClientMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ClientMilestones_RewardClaimedByTrainerId",
                table: "ClientMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ClientLocationMemberships_LocationId",
                table: "ClientLocationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_CalendarSubscriptions_CalendarIntegrationId",
                table: "CalendarSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEventLinks_CalendarIntegrationId",
                table: "CalendarEventLinks");




            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "UserLegalConsents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Trainers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "TrainerRates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "TrainerMonthlySettlements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "TrainerLocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "TrainerContracts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "TrainerContractLocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "SystemSettings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "SessionParticipants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "SessionCorrections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "PaymentProviderAccounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Packages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "OutlookOAuthChallenges",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Notifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "MilestoneDefinitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Locations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "LegalEntities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Invitations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "GroupEnrollmentRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ExternalCalendarEvents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CompanyExpenses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Clients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ClientPayments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ClientPackages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ClientMilestones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ClientLocationMemberships",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ClientEmailChangeRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ClientBalanceTransactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ClientAuditEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CalendarSubscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CalendarIntegrations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CalendarEventLinks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ActivityLogEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("b5100000-0000-4000-8000-000000000002"));

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Trainers_Id_OrganizationId",
                table: "Trainers",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TrainerContracts_Id_OrganizationId",
                table: "TrainerContracts",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Sessions_Id_OrganizationId",
                table: "Sessions",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PaymentProviderAccounts_Id_OrganizationId",
                table: "PaymentProviderAccounts",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Packages_Id_OrganizationId",
                table: "Packages",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MilestoneDefinitions_Id_OrganizationId",
                table: "MilestoneDefinitions",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Locations_Id_OrganizationId",
                table: "Locations",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_LegalEntities_Id_OrganizationId",
                table: "LegalEntities",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Clients_Id_OrganizationId",
                table: "Clients",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ClientPackages_Id_OrganizationId",
                table: "ClientPackages",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CalendarIntegrations_Id_OrganizationId",
                table: "CalendarIntegrations",
                columns: new[] { "Id", "OrganizationId" });





            migrationBuilder.CreateIndex(
                name: "IX_UserLegalConsents_LegalEntityId_OrganizationId",
                table: "UserLegalConsents",
                columns: new[] { "LegalEntityId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLegalConsents_OrganizationId",
                table: "UserLegalConsents",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Trainers_OrganizationId",
                table: "Trainers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerRates_OrganizationId",
                table: "TrainerRates",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerRates_TrainerId_OrganizationId",
                table: "TrainerRates",
                columns: new[] { "TrainerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainerMonthlySettlements_OrganizationId",
                table: "TrainerMonthlySettlements",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerMonthlySettlements_TrainerId_OrganizationId",
                table: "TrainerMonthlySettlements",
                columns: new[] { "TrainerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainerLocations_LocationId_OrganizationId",
                table: "TrainerLocations",
                columns: new[] { "LocationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainerLocations_OrganizationId",
                table: "TrainerLocations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerLocations_TrainerId_OrganizationId",
                table: "TrainerLocations",
                columns: new[] { "TrainerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainerContracts_OrganizationId",
                table: "TrainerContracts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerContracts_TrainerId_OrganizationId",
                table: "TrainerContracts",
                columns: new[] { "TrainerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainerContractLocations_LocationId_OrganizationId",
                table: "TrainerContractLocations",
                columns: new[] { "LocationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainerContractLocations_OrganizationId",
                table: "TrainerContractLocations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerContractLocations_TrainerContractId_OrganizationId",
                table: "TrainerContractLocations",
                columns: new[] { "TrainerContractId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_OrganizationId",
                table: "SystemSettings",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ClientId_OrganizationId",
                table: "Sessions",
                columns: new[] { "ClientId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_LocationId_OrganizationId",
                table: "Sessions",
                columns: new[] { "LocationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_OrganizationId",
                table: "Sessions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_PackageId_OrganizationId",
                table: "Sessions",
                columns: new[] { "PackageId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TrainerId_OrganizationId",
                table: "Sessions",
                columns: new[] { "TrainerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_ClientId_OrganizationId",
                table: "SessionParticipants",
                columns: new[] { "ClientId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_ClientPackageId_OrganizationId",
                table: "SessionParticipants",
                columns: new[] { "ClientPackageId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_OrganizationId",
                table: "SessionParticipants",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_PackageId_OrganizationId",
                table: "SessionParticipants",
                columns: new[] { "PackageId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_SessionId_OrganizationId",
                table: "SessionParticipants",
                columns: new[] { "SessionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionCorrections_OrganizationId",
                table: "SessionCorrections",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCorrections_SessionId_OrganizationId",
                table: "SessionCorrections",
                columns: new[] { "SessionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviderAccounts_LegalEntityId_OrganizationId",
                table: "PaymentProviderAccounts",
                columns: new[] { "LegalEntityId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviderAccounts_LocationId_OrganizationId",
                table: "PaymentProviderAccounts",
                columns: new[] { "LocationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProviderAccounts_OrganizationId",
                table: "PaymentProviderAccounts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_LocationId_OrganizationId",
                table: "Packages",
                columns: new[] { "LocationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_OrganizationId",
                table: "Packages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OutlookOAuthChallenges_OrganizationId",
                table: "OutlookOAuthChallenges",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OrganizationId",
                table: "Notifications",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneDefinitions_OrganizationId",
                table: "MilestoneDefinitions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_LegalEntityId_OrganizationId",
                table: "Locations",
                columns: new[] { "LegalEntityId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_OrganizationId",
                table: "Locations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalEntities_OrganizationId",
                table: "LegalEntities",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_ClientId_OrganizationId",
                table: "Invitations",
                columns: new[] { "ClientId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_LocationId_OrganizationId",
                table: "Invitations",
                columns: new[] { "LocationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OrganizationId",
                table: "Invitations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TrainerId_OrganizationId",
                table: "Invitations",
                columns: new[] { "TrainerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupEnrollmentRequests_ClientId_OrganizationId",
                table: "GroupEnrollmentRequests",
                columns: new[] { "ClientId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupEnrollmentRequests_ClientPackageId_OrganizationId",
                table: "GroupEnrollmentRequests",
                columns: new[] { "ClientPackageId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupEnrollmentRequests_OrganizationId",
                table: "GroupEnrollmentRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupEnrollmentRequests_SessionId_OrganizationId",
                table: "GroupEnrollmentRequests",
                columns: new[] { "SessionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendarEvents_CalendarIntegrationId_OrganizationId",
                table: "ExternalCalendarEvents",
                columns: new[] { "CalendarIntegrationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendarEvents_OrganizationId",
                table: "ExternalCalendarEvents",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendarEvents_SessionId_OrganizationId",
                table: "ExternalCalendarEvents",
                columns: new[] { "SessionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyExpenses_LegalEntityId_OrganizationId",
                table: "CompanyExpenses",
                columns: new[] { "LegalEntityId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyExpenses_LocationId_OrganizationId",
                table: "CompanyExpenses",
                columns: new[] { "LocationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyExpenses_OrganizationId",
                table: "CompanyExpenses",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ActivePackageId_OrganizationId",
                table: "Clients",
                columns: new[] { "ActivePackageId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_LocationId_OrganizationId",
                table: "Clients",
                columns: new[] { "LocationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_OrganizationId",
                table: "Clients",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TrainerId_OrganizationId",
                table: "Clients",
                columns: new[] { "TrainerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_ClientId_OrganizationId",
                table: "ClientPayments",
                columns: new[] { "ClientId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_ClientPackageId_OrganizationId",
                table: "ClientPayments",
                columns: new[] { "ClientPackageId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_LegalEntityId_OrganizationId",
                table: "ClientPayments",
                columns: new[] { "LegalEntityId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_LocationId_OrganizationId",
                table: "ClientPayments",
                columns: new[] { "LocationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_OrganizationId",
                table: "ClientPayments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_PaymentProviderAccountId_OrganizationId",
                table: "ClientPayments",
                columns: new[] { "PaymentProviderAccountId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_ClientId_OrganizationId",
                table: "ClientPackages",
                columns: new[] { "ClientId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_LocationId_OrganizationId",
                table: "ClientPackages",
                columns: new[] { "LocationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_OrganizationId",
                table: "ClientPackages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_PackageId_OrganizationId",
                table: "ClientPackages",
                columns: new[] { "PackageId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientMilestones_ClientId_OrganizationId",
                table: "ClientMilestones",
                columns: new[] { "ClientId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientMilestones_MilestoneDefinitionId_OrganizationId",
                table: "ClientMilestones",
                columns: new[] { "MilestoneDefinitionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientMilestones_OrganizationId",
                table: "ClientMilestones",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMilestones_RewardClaimedByTrainerId_OrganizationId",
                table: "ClientMilestones",
                columns: new[] { "RewardClaimedByTrainerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientLocationMemberships_ClientId_OrganizationId",
                table: "ClientLocationMemberships",
                columns: new[] { "ClientId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientLocationMemberships_LocationId_OrganizationId",
                table: "ClientLocationMemberships",
                columns: new[] { "LocationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientLocationMemberships_OrganizationId",
                table: "ClientLocationMemberships",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientEmailChangeRequests_ClientId_OrganizationId",
                table: "ClientEmailChangeRequests",
                columns: new[] { "ClientId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientEmailChangeRequests_OrganizationId",
                table: "ClientEmailChangeRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientBalanceTransactions_ClientId_OrganizationId",
                table: "ClientBalanceTransactions",
                columns: new[] { "ClientId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientBalanceTransactions_ClientPackageId_OrganizationId",
                table: "ClientBalanceTransactions",
                columns: new[] { "ClientPackageId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientBalanceTransactions_OrganizationId",
                table: "ClientBalanceTransactions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientBalanceTransactions_SessionId_OrganizationId",
                table: "ClientBalanceTransactions",
                columns: new[] { "SessionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientAuditEntries_ClientId_OrganizationId",
                table: "ClientAuditEntries",
                columns: new[] { "ClientId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientAuditEntries_OrganizationId",
                table: "ClientAuditEntries",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptions_CalendarIntegrationId_OrganizationId",
                table: "CalendarSubscriptions",
                columns: new[] { "CalendarIntegrationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptions_OrganizationId",
                table: "CalendarSubscriptions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarIntegrations_OrganizationId",
                table: "CalendarIntegrations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventLinks_CalendarIntegrationId_OrganizationId",
                table: "CalendarEventLinks",
                columns: new[] { "CalendarIntegrationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventLinks_OrganizationId",
                table: "CalendarEventLinks",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventLinks_SessionId_OrganizationId",
                table: "CalendarEventLinks",
                columns: new[] { "SessionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogEntries_OrganizationId",
                table: "ActivityLogEntries",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogEntries_Organizations_OrganizationId",
                table: "ActivityLogEntries",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEventLinks_CalendarIntegrations_CalendarIntegratio~1",
                table: "CalendarEventLinks",
                columns: new[] { "CalendarIntegrationId", "OrganizationId" },
                principalTable: "CalendarIntegrations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEventLinks_Organizations_OrganizationId",
                table: "CalendarEventLinks",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEventLinks_Sessions_SessionId_OrganizationId",
                table: "CalendarEventLinks",
                columns: new[] { "SessionId", "OrganizationId" },
                principalTable: "Sessions",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarIntegrations_Organizations_OrganizationId",
                table: "CalendarIntegrations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarSubscriptions_CalendarIntegrations_CalendarIntegra~1",
                table: "CalendarSubscriptions",
                columns: new[] { "CalendarIntegrationId", "OrganizationId" },
                principalTable: "CalendarIntegrations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarSubscriptions_Organizations_OrganizationId",
                table: "CalendarSubscriptions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientAuditEntries_Clients_ClientId_OrganizationId",
                table: "ClientAuditEntries",
                columns: new[] { "ClientId", "OrganizationId" },
                principalTable: "Clients",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientAuditEntries_Organizations_OrganizationId",
                table: "ClientAuditEntries",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientBalanceTransactions_ClientPackages_ClientPackageId_Or~",
                table: "ClientBalanceTransactions",
                columns: new[] { "ClientPackageId", "OrganizationId" },
                principalTable: "ClientPackages",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientBalanceTransactions_Clients_ClientId_OrganizationId",
                table: "ClientBalanceTransactions",
                columns: new[] { "ClientId", "OrganizationId" },
                principalTable: "Clients",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientBalanceTransactions_Organizations_OrganizationId",
                table: "ClientBalanceTransactions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientBalanceTransactions_Sessions_SessionId_OrganizationId",
                table: "ClientBalanceTransactions",
                columns: new[] { "SessionId", "OrganizationId" },
                principalTable: "Sessions",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientEmailChangeRequests_Clients_ClientId_OrganizationId",
                table: "ClientEmailChangeRequests",
                columns: new[] { "ClientId", "OrganizationId" },
                principalTable: "Clients",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientEmailChangeRequests_Organizations_OrganizationId",
                table: "ClientEmailChangeRequests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientLocationMemberships_Clients_ClientId_OrganizationId",
                table: "ClientLocationMemberships",
                columns: new[] { "ClientId", "OrganizationId" },
                principalTable: "Clients",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientLocationMemberships_Locations_LocationId_Organization~",
                table: "ClientLocationMemberships",
                columns: new[] { "LocationId", "OrganizationId" },
                principalTable: "Locations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientLocationMemberships_Organizations_OrganizationId",
                table: "ClientLocationMemberships",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientMilestones_Clients_ClientId_OrganizationId",
                table: "ClientMilestones",
                columns: new[] { "ClientId", "OrganizationId" },
                principalTable: "Clients",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientMilestones_MilestoneDefinitions_MilestoneDefinitionId~",
                table: "ClientMilestones",
                columns: new[] { "MilestoneDefinitionId", "OrganizationId" },
                principalTable: "MilestoneDefinitions",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientMilestones_Organizations_OrganizationId",
                table: "ClientMilestones",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientMilestones_Trainers_RewardClaimedByTrainerId_Organiza~",
                table: "ClientMilestones",
                columns: new[] { "RewardClaimedByTrainerId", "OrganizationId" },
                principalTable: "Trainers",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPackages_Clients_ClientId_OrganizationId",
                table: "ClientPackages",
                columns: new[] { "ClientId", "OrganizationId" },
                principalTable: "Clients",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPackages_Locations_LocationId_OrganizationId",
                table: "ClientPackages",
                columns: new[] { "LocationId", "OrganizationId" },
                principalTable: "Locations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPackages_Organizations_OrganizationId",
                table: "ClientPackages",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPackages_Packages_PackageId_OrganizationId",
                table: "ClientPackages",
                columns: new[] { "PackageId", "OrganizationId" },
                principalTable: "Packages",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPayments_ClientPackages_ClientPackageId_OrganizationId",
                table: "ClientPayments",
                columns: new[] { "ClientPackageId", "OrganizationId" },
                principalTable: "ClientPackages",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPayments_Clients_ClientId_OrganizationId",
                table: "ClientPayments",
                columns: new[] { "ClientId", "OrganizationId" },
                principalTable: "Clients",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPayments_LegalEntities_LegalEntityId_OrganizationId",
                table: "ClientPayments",
                columns: new[] { "LegalEntityId", "OrganizationId" },
                principalTable: "LegalEntities",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPayments_Locations_LocationId_OrganizationId",
                table: "ClientPayments",
                columns: new[] { "LocationId", "OrganizationId" },
                principalTable: "Locations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPayments_Organizations_OrganizationId",
                table: "ClientPayments",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientPayments_PaymentProviderAccounts_PaymentProviderAcco~1",
                table: "ClientPayments",
                columns: new[] { "PaymentProviderAccountId", "OrganizationId" },
                principalTable: "PaymentProviderAccounts",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Locations_LocationId_OrganizationId",
                table: "Clients",
                columns: new[] { "LocationId", "OrganizationId" },
                principalTable: "Locations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Organizations_OrganizationId",
                table: "Clients",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Packages_ActivePackageId_OrganizationId",
                table: "Clients",
                columns: new[] { "ActivePackageId", "OrganizationId" },
                principalTable: "Packages",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Trainers_TrainerId_OrganizationId",
                table: "Clients",
                columns: new[] { "TrainerId", "OrganizationId" },
                principalTable: "Trainers",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyExpenses_LegalEntities_LegalEntityId_OrganizationId",
                table: "CompanyExpenses",
                columns: new[] { "LegalEntityId", "OrganizationId" },
                principalTable: "LegalEntities",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyExpenses_Locations_LocationId_OrganizationId",
                table: "CompanyExpenses",
                columns: new[] { "LocationId", "OrganizationId" },
                principalTable: "Locations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyExpenses_Organizations_OrganizationId",
                table: "CompanyExpenses",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalCalendarEvents_CalendarIntegrations_CalendarIntegr~1",
                table: "ExternalCalendarEvents",
                columns: new[] { "CalendarIntegrationId", "OrganizationId" },
                principalTable: "CalendarIntegrations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalCalendarEvents_Organizations_OrganizationId",
                table: "ExternalCalendarEvents",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalCalendarEvents_Sessions_SessionId_OrganizationId",
                table: "ExternalCalendarEvents",
                columns: new[] { "SessionId", "OrganizationId" },
                principalTable: "Sessions",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_GroupEnrollmentRequests_ClientPackages_ClientPackageId_Orga~",
                table: "GroupEnrollmentRequests",
                columns: new[] { "ClientPackageId", "OrganizationId" },
                principalTable: "ClientPackages",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_GroupEnrollmentRequests_Clients_ClientId_OrganizationId",
                table: "GroupEnrollmentRequests",
                columns: new[] { "ClientId", "OrganizationId" },
                principalTable: "Clients",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_GroupEnrollmentRequests_Organizations_OrganizationId",
                table: "GroupEnrollmentRequests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupEnrollmentRequests_Sessions_SessionId_OrganizationId",
                table: "GroupEnrollmentRequests",
                columns: new[] { "SessionId", "OrganizationId" },
                principalTable: "Sessions",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Clients_ClientId_OrganizationId",
                table: "Invitations",
                columns: new[] { "ClientId", "OrganizationId" },
                principalTable: "Clients",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Locations_LocationId_OrganizationId",
                table: "Invitations",
                columns: new[] { "LocationId", "OrganizationId" },
                principalTable: "Locations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Organizations_OrganizationId",
                table: "Invitations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Trainers_TrainerId_OrganizationId",
                table: "Invitations",
                columns: new[] { "TrainerId", "OrganizationId" },
                principalTable: "Trainers",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_LegalEntities_Organizations_OrganizationId",
                table: "LegalEntities",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_LegalEntities_LegalEntityId_OrganizationId",
                table: "Locations",
                columns: new[] { "LegalEntityId", "OrganizationId" },
                principalTable: "LegalEntities",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Organizations_OrganizationId",
                table: "Locations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MilestoneDefinitions_Organizations_OrganizationId",
                table: "MilestoneDefinitions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Organizations_OrganizationId",
                table: "Notifications",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OutlookOAuthChallenges_Organizations_OrganizationId",
                table: "OutlookOAuthChallenges",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_Locations_LocationId_OrganizationId",
                table: "Packages",
                columns: new[] { "LocationId", "OrganizationId" },
                principalTable: "Locations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_Organizations_OrganizationId",
                table: "Packages",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentProviderAccounts_LegalEntities_LegalEntityId_Organiz~",
                table: "PaymentProviderAccounts",
                columns: new[] { "LegalEntityId", "OrganizationId" },
                principalTable: "LegalEntities",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentProviderAccounts_Locations_LocationId_OrganizationId",
                table: "PaymentProviderAccounts",
                columns: new[] { "LocationId", "OrganizationId" },
                principalTable: "Locations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentProviderAccounts_Organizations_OrganizationId",
                table: "PaymentProviderAccounts",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SessionCorrections_Organizations_OrganizationId",
                table: "SessionCorrections",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SessionCorrections_Sessions_SessionId_OrganizationId",
                table: "SessionCorrections",
                columns: new[] { "SessionId", "OrganizationId" },
                principalTable: "Sessions",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SessionParticipants_ClientPackages_ClientPackageId_Organiza~",
                table: "SessionParticipants",
                columns: new[] { "ClientPackageId", "OrganizationId" },
                principalTable: "ClientPackages",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SessionParticipants_Clients_ClientId_OrganizationId",
                table: "SessionParticipants",
                columns: new[] { "ClientId", "OrganizationId" },
                principalTable: "Clients",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SessionParticipants_Organizations_OrganizationId",
                table: "SessionParticipants",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SessionParticipants_Packages_PackageId_OrganizationId",
                table: "SessionParticipants",
                columns: new[] { "PackageId", "OrganizationId" },
                principalTable: "Packages",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SessionParticipants_Sessions_SessionId_OrganizationId",
                table: "SessionParticipants",
                columns: new[] { "SessionId", "OrganizationId" },
                principalTable: "Sessions",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Clients_ClientId_OrganizationId",
                table: "Sessions",
                columns: new[] { "ClientId", "OrganizationId" },
                principalTable: "Clients",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Locations_LocationId_OrganizationId",
                table: "Sessions",
                columns: new[] { "LocationId", "OrganizationId" },
                principalTable: "Locations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Organizations_OrganizationId",
                table: "Sessions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Packages_PackageId_OrganizationId",
                table: "Sessions",
                columns: new[] { "PackageId", "OrganizationId" },
                principalTable: "Packages",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Trainers_TrainerId_OrganizationId",
                table: "Sessions",
                columns: new[] { "TrainerId", "OrganizationId" },
                principalTable: "Trainers",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SystemSettings_Organizations_OrganizationId",
                table: "SystemSettings",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerContractLocations_Locations_LocationId_OrganizationId",
                table: "TrainerContractLocations",
                columns: new[] { "LocationId", "OrganizationId" },
                principalTable: "Locations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerContractLocations_Organizations_OrganizationId",
                table: "TrainerContractLocations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerContractLocations_TrainerContracts_TrainerContractId~",
                table: "TrainerContractLocations",
                columns: new[] { "TrainerContractId", "OrganizationId" },
                principalTable: "TrainerContracts",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerContracts_Organizations_OrganizationId",
                table: "TrainerContracts",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerContracts_Trainers_TrainerId_OrganizationId",
                table: "TrainerContracts",
                columns: new[] { "TrainerId", "OrganizationId" },
                principalTable: "Trainers",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerLocations_Locations_LocationId_OrganizationId",
                table: "TrainerLocations",
                columns: new[] { "LocationId", "OrganizationId" },
                principalTable: "Locations",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerLocations_Organizations_OrganizationId",
                table: "TrainerLocations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerLocations_Trainers_TrainerId_OrganizationId",
                table: "TrainerLocations",
                columns: new[] { "TrainerId", "OrganizationId" },
                principalTable: "Trainers",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerMonthlySettlements_Organizations_OrganizationId",
                table: "TrainerMonthlySettlements",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerMonthlySettlements_Trainers_TrainerId_OrganizationId",
                table: "TrainerMonthlySettlements",
                columns: new[] { "TrainerId", "OrganizationId" },
                principalTable: "Trainers",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerRates_Organizations_OrganizationId",
                table: "TrainerRates",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerRates_Trainers_TrainerId_OrganizationId",
                table: "TrainerRates",
                columns: new[] { "TrainerId", "OrganizationId" },
                principalTable: "Trainers",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Trainers_Organizations_OrganizationId",
                table: "Trainers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserLegalConsents_LegalEntities_LegalEntityId_OrganizationId",
                table: "UserLegalConsents",
                columns: new[] { "LegalEntityId", "OrganizationId" },
                principalTable: "LegalEntities",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserLegalConsents_Organizations_OrganizationId",
                table: "UserLegalConsents",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogEntries_Organizations_OrganizationId",
                table: "ActivityLogEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEventLinks_CalendarIntegrations_CalendarIntegratio~1",
                table: "CalendarEventLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEventLinks_Organizations_OrganizationId",
                table: "CalendarEventLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEventLinks_Sessions_SessionId_OrganizationId",
                table: "CalendarEventLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarIntegrations_Organizations_OrganizationId",
                table: "CalendarIntegrations");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarSubscriptions_CalendarIntegrations_CalendarIntegra~1",
                table: "CalendarSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarSubscriptions_Organizations_OrganizationId",
                table: "CalendarSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientAuditEntries_Clients_ClientId_OrganizationId",
                table: "ClientAuditEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientAuditEntries_Organizations_OrganizationId",
                table: "ClientAuditEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientBalanceTransactions_ClientPackages_ClientPackageId_Or~",
                table: "ClientBalanceTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientBalanceTransactions_Clients_ClientId_OrganizationId",
                table: "ClientBalanceTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientBalanceTransactions_Organizations_OrganizationId",
                table: "ClientBalanceTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientBalanceTransactions_Sessions_SessionId_OrganizationId",
                table: "ClientBalanceTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientEmailChangeRequests_Clients_ClientId_OrganizationId",
                table: "ClientEmailChangeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientEmailChangeRequests_Organizations_OrganizationId",
                table: "ClientEmailChangeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientLocationMemberships_Clients_ClientId_OrganizationId",
                table: "ClientLocationMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientLocationMemberships_Locations_LocationId_Organization~",
                table: "ClientLocationMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientLocationMemberships_Organizations_OrganizationId",
                table: "ClientLocationMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientMilestones_Clients_ClientId_OrganizationId",
                table: "ClientMilestones");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientMilestones_MilestoneDefinitions_MilestoneDefinitionId~",
                table: "ClientMilestones");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientMilestones_Organizations_OrganizationId",
                table: "ClientMilestones");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientMilestones_Trainers_RewardClaimedByTrainerId_Organiza~",
                table: "ClientMilestones");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPackages_Clients_ClientId_OrganizationId",
                table: "ClientPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPackages_Locations_LocationId_OrganizationId",
                table: "ClientPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPackages_Organizations_OrganizationId",
                table: "ClientPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPackages_Packages_PackageId_OrganizationId",
                table: "ClientPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPayments_ClientPackages_ClientPackageId_OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPayments_Clients_ClientId_OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPayments_LegalEntities_LegalEntityId_OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPayments_Locations_LocationId_OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPayments_Organizations_OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientPayments_PaymentProviderAccounts_PaymentProviderAcco~1",
                table: "ClientPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Locations_LocationId_OrganizationId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Organizations_OrganizationId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Packages_ActivePackageId_OrganizationId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Trainers_TrainerId_OrganizationId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyExpenses_LegalEntities_LegalEntityId_OrganizationId",
                table: "CompanyExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyExpenses_Locations_LocationId_OrganizationId",
                table: "CompanyExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyExpenses_Organizations_OrganizationId",
                table: "CompanyExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalCalendarEvents_CalendarIntegrations_CalendarIntegr~1",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalCalendarEvents_Organizations_OrganizationId",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalCalendarEvents_Sessions_SessionId_OrganizationId",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupEnrollmentRequests_ClientPackages_ClientPackageId_Orga~",
                table: "GroupEnrollmentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupEnrollmentRequests_Clients_ClientId_OrganizationId",
                table: "GroupEnrollmentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupEnrollmentRequests_Organizations_OrganizationId",
                table: "GroupEnrollmentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupEnrollmentRequests_Sessions_SessionId_OrganizationId",
                table: "GroupEnrollmentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Clients_ClientId_OrganizationId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Locations_LocationId_OrganizationId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Organizations_OrganizationId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Trainers_TrainerId_OrganizationId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_LegalEntities_Organizations_OrganizationId",
                table: "LegalEntities");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_LegalEntities_LegalEntityId_OrganizationId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Organizations_OrganizationId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_MilestoneDefinitions_Organizations_OrganizationId",
                table: "MilestoneDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Organizations_OrganizationId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_OutlookOAuthChallenges_Organizations_OrganizationId",
                table: "OutlookOAuthChallenges");

            migrationBuilder.DropForeignKey(
                name: "FK_Packages_Locations_LocationId_OrganizationId",
                table: "Packages");

            migrationBuilder.DropForeignKey(
                name: "FK_Packages_Organizations_OrganizationId",
                table: "Packages");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentProviderAccounts_LegalEntities_LegalEntityId_Organiz~",
                table: "PaymentProviderAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentProviderAccounts_Locations_LocationId_OrganizationId",
                table: "PaymentProviderAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentProviderAccounts_Organizations_OrganizationId",
                table: "PaymentProviderAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionCorrections_Organizations_OrganizationId",
                table: "SessionCorrections");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionCorrections_Sessions_SessionId_OrganizationId",
                table: "SessionCorrections");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionParticipants_ClientPackages_ClientPackageId_Organiza~",
                table: "SessionParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionParticipants_Clients_ClientId_OrganizationId",
                table: "SessionParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionParticipants_Organizations_OrganizationId",
                table: "SessionParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionParticipants_Packages_PackageId_OrganizationId",
                table: "SessionParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionParticipants_Sessions_SessionId_OrganizationId",
                table: "SessionParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Clients_ClientId_OrganizationId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Locations_LocationId_OrganizationId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Organizations_OrganizationId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Packages_PackageId_OrganizationId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Trainers_TrainerId_OrganizationId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemSettings_Organizations_OrganizationId",
                table: "SystemSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerContractLocations_Locations_LocationId_OrganizationId",
                table: "TrainerContractLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerContractLocations_Organizations_OrganizationId",
                table: "TrainerContractLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerContractLocations_TrainerContracts_TrainerContractId~",
                table: "TrainerContractLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerContracts_Organizations_OrganizationId",
                table: "TrainerContracts");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerContracts_Trainers_TrainerId_OrganizationId",
                table: "TrainerContracts");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerLocations_Locations_LocationId_OrganizationId",
                table: "TrainerLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerLocations_Organizations_OrganizationId",
                table: "TrainerLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerLocations_Trainers_TrainerId_OrganizationId",
                table: "TrainerLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerMonthlySettlements_Organizations_OrganizationId",
                table: "TrainerMonthlySettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerMonthlySettlements_Trainers_TrainerId_OrganizationId",
                table: "TrainerMonthlySettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerRates_Organizations_OrganizationId",
                table: "TrainerRates");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerRates_Trainers_TrainerId_OrganizationId",
                table: "TrainerRates");

            migrationBuilder.DropForeignKey(
                name: "FK_Trainers_Organizations_OrganizationId",
                table: "Trainers");

            migrationBuilder.DropForeignKey(
                name: "FK_UserLegalConsents_LegalEntities_LegalEntityId_OrganizationId",
                table: "UserLegalConsents");

            migrationBuilder.DropForeignKey(
                name: "FK_UserLegalConsents_Organizations_OrganizationId",
                table: "UserLegalConsents");

            migrationBuilder.DropIndex(
                name: "IX_UserLegalConsents_LegalEntityId_OrganizationId",
                table: "UserLegalConsents");

            migrationBuilder.DropIndex(
                name: "IX_UserLegalConsents_OrganizationId",
                table: "UserLegalConsents");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Trainers_Id_OrganizationId",
                table: "Trainers");

            migrationBuilder.DropIndex(
                name: "IX_Trainers_OrganizationId",
                table: "Trainers");

            migrationBuilder.DropIndex(
                name: "IX_TrainerRates_OrganizationId",
                table: "TrainerRates");

            migrationBuilder.DropIndex(
                name: "IX_TrainerRates_TrainerId_OrganizationId",
                table: "TrainerRates");

            migrationBuilder.DropIndex(
                name: "IX_TrainerMonthlySettlements_OrganizationId",
                table: "TrainerMonthlySettlements");

            migrationBuilder.DropIndex(
                name: "IX_TrainerMonthlySettlements_TrainerId_OrganizationId",
                table: "TrainerMonthlySettlements");

            migrationBuilder.DropIndex(
                name: "IX_TrainerLocations_LocationId_OrganizationId",
                table: "TrainerLocations");

            migrationBuilder.DropIndex(
                name: "IX_TrainerLocations_OrganizationId",
                table: "TrainerLocations");

            migrationBuilder.DropIndex(
                name: "IX_TrainerLocations_TrainerId_OrganizationId",
                table: "TrainerLocations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TrainerContracts_Id_OrganizationId",
                table: "TrainerContracts");

            migrationBuilder.DropIndex(
                name: "IX_TrainerContracts_OrganizationId",
                table: "TrainerContracts");

            migrationBuilder.DropIndex(
                name: "IX_TrainerContracts_TrainerId_OrganizationId",
                table: "TrainerContracts");

            migrationBuilder.DropIndex(
                name: "IX_TrainerContractLocations_LocationId_OrganizationId",
                table: "TrainerContractLocations");

            migrationBuilder.DropIndex(
                name: "IX_TrainerContractLocations_OrganizationId",
                table: "TrainerContractLocations");

            migrationBuilder.DropIndex(
                name: "IX_TrainerContractLocations_TrainerContractId_OrganizationId",
                table: "TrainerContractLocations");

            migrationBuilder.DropIndex(
                name: "IX_SystemSettings_OrganizationId",
                table: "SystemSettings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Sessions_Id_OrganizationId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ClientId_OrganizationId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_LocationId_OrganizationId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_OrganizationId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_PackageId_OrganizationId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_TrainerId_OrganizationId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_SessionParticipants_ClientId_OrganizationId",
                table: "SessionParticipants");

            migrationBuilder.DropIndex(
                name: "IX_SessionParticipants_ClientPackageId_OrganizationId",
                table: "SessionParticipants");

            migrationBuilder.DropIndex(
                name: "IX_SessionParticipants_OrganizationId",
                table: "SessionParticipants");

            migrationBuilder.DropIndex(
                name: "IX_SessionParticipants_PackageId_OrganizationId",
                table: "SessionParticipants");

            migrationBuilder.DropIndex(
                name: "IX_SessionParticipants_SessionId_OrganizationId",
                table: "SessionParticipants");

            migrationBuilder.DropIndex(
                name: "IX_SessionCorrections_OrganizationId",
                table: "SessionCorrections");

            migrationBuilder.DropIndex(
                name: "IX_SessionCorrections_SessionId_OrganizationId",
                table: "SessionCorrections");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PaymentProviderAccounts_Id_OrganizationId",
                table: "PaymentProviderAccounts");

            migrationBuilder.DropIndex(
                name: "IX_PaymentProviderAccounts_LegalEntityId_OrganizationId",
                table: "PaymentProviderAccounts");

            migrationBuilder.DropIndex(
                name: "IX_PaymentProviderAccounts_LocationId_OrganizationId",
                table: "PaymentProviderAccounts");

            migrationBuilder.DropIndex(
                name: "IX_PaymentProviderAccounts_OrganizationId",
                table: "PaymentProviderAccounts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Packages_Id_OrganizationId",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_LocationId_OrganizationId",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_OrganizationId",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_OutlookOAuthChallenges_OrganizationId",
                table: "OutlookOAuthChallenges");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_OrganizationId",
                table: "Notifications");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MilestoneDefinitions_Id_OrganizationId",
                table: "MilestoneDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_MilestoneDefinitions_OrganizationId",
                table: "MilestoneDefinitions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Locations_Id_OrganizationId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_LegalEntityId_OrganizationId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_OrganizationId",
                table: "Locations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_LegalEntities_Id_OrganizationId",
                table: "LegalEntities");

            migrationBuilder.DropIndex(
                name: "IX_LegalEntities_OrganizationId",
                table: "LegalEntities");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_ClientId_OrganizationId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_LocationId_OrganizationId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_OrganizationId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_TrainerId_OrganizationId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_GroupEnrollmentRequests_ClientId_OrganizationId",
                table: "GroupEnrollmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_GroupEnrollmentRequests_ClientPackageId_OrganizationId",
                table: "GroupEnrollmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_GroupEnrollmentRequests_OrganizationId",
                table: "GroupEnrollmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_GroupEnrollmentRequests_SessionId_OrganizationId",
                table: "GroupEnrollmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_ExternalCalendarEvents_CalendarIntegrationId_OrganizationId",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_ExternalCalendarEvents_OrganizationId",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_ExternalCalendarEvents_SessionId_OrganizationId",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_CompanyExpenses_LegalEntityId_OrganizationId",
                table: "CompanyExpenses");

            migrationBuilder.DropIndex(
                name: "IX_CompanyExpenses_LocationId_OrganizationId",
                table: "CompanyExpenses");

            migrationBuilder.DropIndex(
                name: "IX_CompanyExpenses_OrganizationId",
                table: "CompanyExpenses");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Clients_Id_OrganizationId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_ActivePackageId_OrganizationId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_LocationId_OrganizationId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_OrganizationId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_TrainerId_OrganizationId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_ClientId_OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_ClientPackageId_OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_LegalEntityId_OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_LocationId_OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropIndex(
                name: "IX_ClientPayments_PaymentProviderAccountId_OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ClientPackages_Id_OrganizationId",
                table: "ClientPackages");

            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_ClientId_OrganizationId",
                table: "ClientPackages");

            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_LocationId_OrganizationId",
                table: "ClientPackages");

            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_OrganizationId",
                table: "ClientPackages");

            migrationBuilder.DropIndex(
                name: "IX_ClientPackages_PackageId_OrganizationId",
                table: "ClientPackages");

            migrationBuilder.DropIndex(
                name: "IX_ClientMilestones_ClientId_OrganizationId",
                table: "ClientMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ClientMilestones_MilestoneDefinitionId_OrganizationId",
                table: "ClientMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ClientMilestones_OrganizationId",
                table: "ClientMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ClientMilestones_RewardClaimedByTrainerId_OrganizationId",
                table: "ClientMilestones");

            migrationBuilder.DropIndex(
                name: "IX_ClientLocationMemberships_ClientId_OrganizationId",
                table: "ClientLocationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_ClientLocationMemberships_LocationId_OrganizationId",
                table: "ClientLocationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_ClientLocationMemberships_OrganizationId",
                table: "ClientLocationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_ClientEmailChangeRequests_ClientId_OrganizationId",
                table: "ClientEmailChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_ClientEmailChangeRequests_OrganizationId",
                table: "ClientEmailChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_ClientBalanceTransactions_ClientId_OrganizationId",
                table: "ClientBalanceTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ClientBalanceTransactions_ClientPackageId_OrganizationId",
                table: "ClientBalanceTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ClientBalanceTransactions_OrganizationId",
                table: "ClientBalanceTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ClientBalanceTransactions_SessionId_OrganizationId",
                table: "ClientBalanceTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ClientAuditEntries_ClientId_OrganizationId",
                table: "ClientAuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_ClientAuditEntries_OrganizationId",
                table: "ClientAuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_CalendarSubscriptions_CalendarIntegrationId_OrganizationId",
                table: "CalendarSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_CalendarSubscriptions_OrganizationId",
                table: "CalendarSubscriptions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CalendarIntegrations_Id_OrganizationId",
                table: "CalendarIntegrations");

            migrationBuilder.DropIndex(
                name: "IX_CalendarIntegrations_OrganizationId",
                table: "CalendarIntegrations");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEventLinks_CalendarIntegrationId_OrganizationId",
                table: "CalendarEventLinks");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEventLinks_OrganizationId",
                table: "CalendarEventLinks");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEventLinks_SessionId_OrganizationId",
                table: "CalendarEventLinks");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogEntries_OrganizationId",
                table: "ActivityLogEntries");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "UserLegalConsents");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Trainers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "TrainerRates");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "TrainerMonthlySettlements");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "TrainerLocations");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "TrainerContracts");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "TrainerContractLocations");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "SessionParticipants");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "SessionCorrections");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "PaymentProviderAccounts");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "OutlookOAuthChallenges");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "MilestoneDefinitions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "GroupEnrollmentRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ExternalCalendarEvents");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CompanyExpenses");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ClientPayments");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ClientPackages");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ClientMilestones");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ClientLocationMemberships");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ClientEmailChangeRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ClientBalanceTransactions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ClientAuditEntries");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CalendarSubscriptions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CalendarIntegrations");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CalendarEventLinks");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ActivityLogEntries");

            migrationBuilder.CreateIndex(
                name: "IX_UserLegalConsents_LegalEntityId",
                table: "UserLegalConsents",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerLocations_LocationId",
                table: "TrainerLocations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ClientId",
                table: "Sessions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_PackageId",
                table: "Sessions",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_ClientId",
                table: "SessionParticipants",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionParticipants_PackageId",
                table: "SessionParticipants",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCorrections_SessionId",
                table: "SessionCorrections",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_LegalEntityId",
                table: "Locations",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_ClientId",
                table: "Invitations",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_LocationId",
                table: "Invitations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupEnrollmentRequests_ClientId",
                table: "GroupEnrollmentRequests",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupEnrollmentRequests_ClientPackageId",
                table: "GroupEnrollmentRequests",
                column: "ClientPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCalendarEvents_SessionId",
                table: "ExternalCalendarEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ActivePackageId",
                table: "Clients",
                column: "ActivePackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_LocationId",
                table: "Clients",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_LocationId",
                table: "ClientPackages",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPackages_PackageId",
                table: "ClientPackages",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMilestones_ClientId",
                table: "ClientMilestones",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMilestones_MilestoneDefinitionId",
                table: "ClientMilestones",
                column: "MilestoneDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMilestones_RewardClaimedByTrainerId",
                table: "ClientMilestones",
                column: "RewardClaimedByTrainerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientLocationMemberships_LocationId",
                table: "ClientLocationMemberships",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptions_CalendarIntegrationId",
                table: "CalendarSubscriptions",
                column: "CalendarIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventLinks_CalendarIntegrationId",
                table: "CalendarEventLinks",
                column: "CalendarIntegrationId");
        }
    }
}
