using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RevitToIfcScheduler.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddDesignAutomationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExportSettingsJson",
                table: "IfcSettingsSets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OnlyExportVisibleElementsInView",
                table: "IfcSettingsSets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UserDefinedPsetsContent",
                table: "IfcSettingsSets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewId",
                table: "IfcSettingsSets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkItemId",
                table: "ConversionJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkItemReportUrl",
                table: "ConversionJobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExportSettingsJson",
                table: "IfcSettingsSets");

            migrationBuilder.DropColumn(
                name: "OnlyExportVisibleElementsInView",
                table: "IfcSettingsSets");

            migrationBuilder.DropColumn(
                name: "UserDefinedPsetsContent",
                table: "IfcSettingsSets");

            migrationBuilder.DropColumn(
                name: "ViewId",
                table: "IfcSettingsSets");

            migrationBuilder.DropColumn(
                name: "WorkItemId",
                table: "ConversionJobs");

            migrationBuilder.DropColumn(
                name: "WorkItemReportUrl",
                table: "ConversionJobs");
        }
    }
}
