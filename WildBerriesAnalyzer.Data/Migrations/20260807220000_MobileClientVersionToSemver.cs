using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WildBerriesAnalyzer.Data.Migrations
{
    /// <summary>
    /// На случай, если AddUserMobileClientVersion уже успели применить с int:
    /// приводит колонку к semver-строке и убирает DisplayVersion.
    /// </summary>
    [DbContext(typeof(WbDataBase))]
    [Migration("20260807220000_MobileClientVersionToSemver")]
    public partial class MobileClientVersionToSemver : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Users" DROP COLUMN IF EXISTS "MobileClientDisplayVersion";

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Users'
                          AND column_name = 'MobileClientVersion'
                          AND data_type = 'integer'
                    ) THEN
                        ALTER TABLE "Users"
                            ALTER COLUMN "MobileClientVersion" TYPE character varying(32)
                            USING (
                                CASE
                                    WHEN "MobileClientVersion" IS NULL THEN NULL
                                    ELSE "MobileClientVersion"::text
                                END
                            );
                    END IF;
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Обратный переход к int не поддерживаем.
        }
    }
}
