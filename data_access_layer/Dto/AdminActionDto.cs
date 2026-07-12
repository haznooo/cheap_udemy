using System.Text.Json;

namespace DataAccess.Dto
{
    // Read-model for the admin audit log (GET api/admin/actions). old_value/new_value
    // are JSONB snapshots and serialize to the client as raw JSON.
    public class AdminActionDto
    {
        public int Id { get; set; }
        public int AdminId { get; set; }
        public string AdminUsername { get; set; }
        public string ActionType { get; set; }
        public string TargetTable { get; set; }
        public int TargetId { get; set; }
        public JsonDocument? OldValue { get; set; }
        public JsonDocument? NewValue { get; set; }
        public DateTime PerformedAt { get; set; }
    }
}
