using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrightPath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_students", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tutors",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<string>(type: "text", nullable: false),
                    room_id = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    moved_to_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    legacy_violation = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.CheckConstraint("ck_sessions_duration", "ends_at - starts_at IN (interval '60 minutes', interval '90 minutes')");
                    table.ForeignKey(
                        name: "fk_sessions_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sessions_sessions_moved_to_session_id",
                        column: x => x.moved_to_session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sessions_tutors_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "tutors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<string>(type: "text", nullable: true),
                    chargeable = table.Column<bool>(type: "boolean", nullable: false),
                    legacy_violation = table.Column<bool>(type: "boolean", nullable: false),
                    source_lesson_id = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendees", x => x.id);
                    table.CheckConstraint("ck_attendees_cancelled_by", "cancelled_by IS NULL OR cancelled_by IN ('family', 'tutor', 'centre')");
                    table.CheckConstraint("ck_attendees_cancelled_consistent", "(status = 'cancelled') = (cancelled_at IS NOT NULL)");
                    table.CheckConstraint("ck_attendees_status", "status IN ('booked', 'cancelled', 'no_show')");
                    table.ForeignKey(
                        name: "fk_attendees_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendees_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<string>(type: "text", nullable: true),
                    after_cutoff = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_changes", x => x.id);
                    table.CheckConstraint("ck_booking_changes_changed_by", "changed_by IS NULL OR changed_by IN ('family', 'tutor', 'centre')");
                    table.CheckConstraint("ck_booking_changes_kind", "kind IN ('created', 'cancelled', 'moved')");
                    table.ForeignKey(
                        name: "fk_booking_changes_attendees_attendee_id",
                        column: x => x.attendee_id,
                        principalTable: "attendees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_booking_changes_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "rooms",
                column: "id",
                values: new object[]
                {
                    "R1",
                    "R2",
                    "R3",
                    "R4",
                    "R5",
                    "R6"
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendees_session_id_student_id",
                table: "attendees",
                columns: new[] { "session_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendees_source_lesson_id",
                table: "attendees",
                column: "source_lesson_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendees_student_id",
                table: "attendees",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_changes_attendee_id",
                table: "booking_changes",
                column: "attendee_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_changes_session_id_changed_at",
                table: "booking_changes",
                columns: new[] { "session_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sessions_moved_to_session_id",
                table: "sessions",
                column: "moved_to_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_room_id",
                table: "sessions",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_tutor_id",
                table: "sessions",
                column: "tutor_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_name",
                table: "students",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_changes");

            migrationBuilder.DropTable(
                name: "attendees");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "students");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropTable(
                name: "tutors");
        }
    }
}
