// =============================================================================
// CodeQuest — Phase P3: Core OOP Domain Layer
// OOP rubric: ENCAPSULATION — private fields, public properties.
// Look at: the private fields below and the public properties that expose them;
//          no public mutable field exists anywhere in this class.
// Table:   topics (database/schema.sql) — columns: topic_id, name, category,
//          description, created_at.
// =============================================================================

namespace CodeQuest.Models;

/// <summary>
/// One subject area a quiz can belong to (schema table <c>topics</c>).
/// </summary>
public sealed class Topic
{
    private int _topicId;
    private string _name = string.Empty;
    private string _category = string.Empty;
    private string _description = string.Empty;
    private DateTimeOffset _createdAt;

    /// <summary>Primary key of the topic (schema column <c>topic_id</c>).</summary>
    public int TopicId
    {
        get => _topicId;
        set => _topicId = value;
    }

    /// <summary>Subject name, unique in the schema (schema column <c>name</c>).</summary>
    public string Name
    {
        get => _name;
        set => _name = value;
    }

    /// <summary>
    /// Subject grouping (schema column <c>category</c>). The schema allows exactly
    /// two values — <c>Programming</c> and <c>Cybersecurity</c> — so the topic form
    /// (P8) uses a dropdown limited to those two, never free text (plan ruling R-2 #7).
    /// </summary>
    public string Category
    {
        get => _category;
        set => _category = value;
    }

    /// <summary>One-line description shown in the topic picker (schema column <c>description</c>).</summary>
    public string Description
    {
        get => _description;
        set => _description = value;
    }

    /// <summary>
    /// Creation timestamp (schema column <c>created_at</c>). The schema stores ISO-8601 UTC
    /// text; the data layer (P4) writes and reads the format <c>yyyy-MM-ddTHH:mm:ssZ</c>.
    /// </summary>
    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => _createdAt = value;
    }
}
