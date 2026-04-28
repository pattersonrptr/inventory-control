using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryControl.Migrations
{
    /// <inheritdoc />
    public partial class FixExternalMappingSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    CREATE SEQUENCE IF NOT EXISTS ""CategoryExternalMappings_Id_seq"";
                    SELECT setval('""CategoryExternalMappings_Id_seq""', COALESCE((SELECT MAX(""Id"") FROM ""CategoryExternalMappings""), 0) + 1, false);
                    ALTER TABLE ""CategoryExternalMappings"" ALTER COLUMN ""Id"" SET DEFAULT nextval('""CategoryExternalMappings_Id_seq""');

                    CREATE SEQUENCE IF NOT EXISTS ""ProductExternalMappings_Id_seq"";
                    SELECT setval('""ProductExternalMappings_Id_seq""', COALESCE((SELECT MAX(""Id"") FROM ""ProductExternalMappings""), 0) + 1, false);
                    ALTER TABLE ""ProductExternalMappings"" ALTER COLUMN ""Id"" SET DEFAULT nextval('""ProductExternalMappings_Id_seq""');
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    ALTER TABLE ""CategoryExternalMappings"" ALTER COLUMN ""Id"" DROP DEFAULT;
                    DROP SEQUENCE IF EXISTS ""CategoryExternalMappings_Id_seq"";

                    ALTER TABLE ""ProductExternalMappings"" ALTER COLUMN ""Id"" DROP DEFAULT;
                    DROP SEQUENCE IF EXISTS ""ProductExternalMappings_Id_seq"";
                ");
            }
        }
    }
}
