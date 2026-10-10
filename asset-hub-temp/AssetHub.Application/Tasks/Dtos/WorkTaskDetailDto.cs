using System;
using System.Collections.Generic;

namespace AssetHub.Application.Tasks.Dtos;

public class WorkTaskDetailDto : WorkTaskDto
{
    public List<TaskStatusHistoryDto> History { get; set; } = new();
    public List<TaskCommentDto> Comments { get; set; } = new();
}

public class TaskStatusHistoryDto
{
    public Guid Id { get; set; }
    public string? FromState { get; set; }
    public string ToState { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? ChangedByName { get; set; }
}

public class TaskCommentDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
}
