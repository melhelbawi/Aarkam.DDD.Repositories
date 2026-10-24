namespace Aarkam.Domain.Repositories
{
    /// <summary>
    /// Represents a single filter request from the client.
    /// </summary>
    public class FilterItem
    {
        /// <summary>
        /// Name of the property to filter on (e.g. "Name", "Email", "Status").
        /// </summary>
        public string Property { get; set; } = string.Empty;

        /// <summary>
        /// Value to filter by. Can be null when evaluating null checks (e.g. ISNULL, ISNOTNULL).
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// Filter operator. Supported values: EQ, NEQ, CONTAINS, STARTSWITH, ENDSWITH, ISNULL, ISNOTNULL, IN.
        /// Defaults to "EQ" (equals).
        /// </summary>
        public string Operator { get; set; } = "EQ";

        /// <summary>
        /// Optional: For IN operator (multiple values).
        /// </summary>
        public string[]? Values { get; set; }
    }
}
