// A subject quizzes belong to.

namespace CodeQuest.Models;

public sealed class Topic
{
    private int _topicId;
    private string _name = string.Empty;
    private string _category = string.Empty;
    private string _description = string.Empty;
    private DateTimeOffset _createdAt;

    public int TopicId
    {
        get => _topicId;
        set => _topicId = value;
    }

    public string Name
    {
        get => _name;
        set => _name = value;
    }

    public string Category
    {
        get => _category;
        set => _category = value;
    }

    public string Description
    {
        get => _description;
        set => _description = value;
    }

    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => _createdAt = value;
    }
}
