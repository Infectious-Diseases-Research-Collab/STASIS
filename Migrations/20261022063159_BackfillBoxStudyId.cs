using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STASIS.Migrations
{
    /// <inheritdoc />
    public partial class BackfillBoxStudyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfills tbl_Boxes.StudyID for boxes that already existed before that column was
            // added and never got written through ValidateAndAssignBoxStudyAsync. For each box,
            // if its existing specimens all share exactly one distinct StudyID, set the box's
            // StudyID to that value. Boxes whose specimens span more than one distinct StudyID
            // (c > 1) are left NULL deliberately -- that's a real data-integrity problem (a
            // single physical box holding specimens from more than one study) that needs manual
            // cleanup, not something this migration should guess at. Boxes with zero specimens
            // are also left NULL -- correctly, since they are genuinely unclaimed. Boxes that
            // already have a StudyID (e.g. set going forward by ValidateAndAssignBoxStudyAsync)
            // are left untouched via the "StudyID IS NULL" guard.
            migrationBuilder.Sql(@"
                UPDATE ""tbl_Boxes"" b SET ""StudyID"" = s.sid
                FROM (SELECT ""BoxID"", MIN(""StudyID"") AS sid, COUNT(DISTINCT ""StudyID"") AS c
                      FROM ""tbl_Specimens"" WHERE ""BoxID"" IS NOT NULL GROUP BY ""BoxID"") s
                WHERE b.""BoxID"" = s.""BoxID"" AND s.c = 1 AND b.""StudyID"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately a no-op: there is no safe way to "unbackfill" -- we cannot distinguish
            // a StudyID this migration just set from one ValidateAndAssignBoxStudyAsync had
            // already set for the same box before this migration ran. This matches how
            // irreversible data migrations are typically handled elsewhere in this codebase
            // (a real reversal is not attempted rather than faked).
        }
    }
}
