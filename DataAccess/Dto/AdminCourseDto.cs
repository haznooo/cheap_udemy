namespace DataAccess.Dto
{
    // Admin-only projection of a course for the moderation list. Extends the public
    // CourseDto with the tombstone columns (deleted_at/removal_reason) so an admin can
    // tell a taken-down/soft-deleted course from a live one — the public CourseDto only
    // exposes `status`, which stays at its pre-takedown value on a tombstoned course.
    // These two fields must NEVER appear on an anonymous/student-facing course response;
    // those keep returning the base CourseDto, which has no tombstone fields.
    public class AdminCourseDto : CourseDto
    {
        public DateTime? deleted_at { get; set; }
        public string? removal_reason { get; set; }
    }
}
