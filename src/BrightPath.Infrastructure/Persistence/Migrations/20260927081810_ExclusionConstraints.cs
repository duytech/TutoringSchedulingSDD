using BrightPath.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrightPath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExclusionConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            // The slot columns exist only for the constraints below, so they are not mapped in EF.
            // Half-open [) ranges: a 09:00-10:00 lesson and a 10:00-11:00 lesson do not overlap.
            migrationBuilder.Sql(
                """
                ALTER TABLE sessions
                    ADD COLUMN slot tstzrange NOT NULL
                    GENERATED ALWAYS AS (tstzrange(starts_at, ends_at, '[)')) STORED;
                """);

            // The tables are still empty here (the seed comes later), so no backfill is needed.
            migrationBuilder.Sql("ALTER TABLE attendees ADD COLUMN slot tstzrange NOT NULL;");

            // attendees.slot is copied from the session on insert, so no insert path can forget it.
            // A session's time never changes after creation, so no UPDATE trigger is needed.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION attendees_copy_slot() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    SELECT slot INTO NEW.slot FROM sessions WHERE id = NEW.session_id;
                    RETURN NEW;
                END $$;

                CREATE TRIGGER trg_attendees_copy_slot
                    BEFORE INSERT ON attendees
                    FOR EACH ROW EXECUTE FUNCTION attendees_copy_slot();
                """);

            // Constraint names are stable: the API maps each one to a conflict kind.
            migrationBuilder.Sql(
                $"""
                ALTER TABLE sessions ADD CONSTRAINT ex_sessions_room_slot
                    EXCLUDE USING gist (room_id WITH =, slot WITH &&)
                    WHERE (cancelled_at IS NULL AND NOT legacy_violation);

                ALTER TABLE sessions ADD CONSTRAINT ex_sessions_tutor_slot
                    EXCLUDE USING gist (tutor_id WITH =, slot WITH &&)
                    WHERE (cancelled_at IS NULL AND NOT legacy_violation);

                ALTER TABLE attendees ADD CONSTRAINT ex_attendees_student_slot
                    EXCLUDE USING gist (student_id WITH =, slot WITH &&)
                    WHERE (status <> '{AttendeeStatus.Cancelled}' AND NOT legacy_violation);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE attendees DROP CONSTRAINT ex_attendees_student_slot;
                ALTER TABLE sessions DROP CONSTRAINT ex_sessions_tutor_slot;
                ALTER TABLE sessions DROP CONSTRAINT ex_sessions_room_slot;

                DROP TRIGGER trg_attendees_copy_slot ON attendees;
                DROP FUNCTION attendees_copy_slot();

                ALTER TABLE attendees DROP COLUMN slot;
                ALTER TABLE sessions DROP COLUMN slot;
                """);

            // Npgsql does not emit DROP EXTENSION here, so btree_gist stays installed. That is harmless:
            // Up uses CREATE EXTENSION IF NOT EXISTS.
            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");
        }
    }
}
