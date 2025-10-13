namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

/// <summary>
/// Represents a GitLab user.
/// </summary>
public class GitLabUser
{
    /// <summary>
    /// The user ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// The user's email.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The user's full name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The user's state (active, blocked, etc.).
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// The user's avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// The user's website URL.
    /// </summary>
    public string? WebUrl { get; set; }

    /// <summary>
    /// When the user was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Whether the user is admin.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Whether the user can create groups.
    /// </summary>
    public bool CanCreateGroup { get; set; }

    /// <summary>
    /// Whether the user can create projects.
    /// </summary>
    public bool CanCreateProject { get; set; }

    /// <summary>
    /// Two-factor authentication enabled.
    /// </summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>
    /// External user.
    /// </summary>
    public bool External { get; set; }

    /// <summary>
    /// Private profile.
    /// </summary>
    public bool PrivateProfile { get; set; }
}

/// <summary>
/// Represents a GitLab project.
/// </summary>
public class GitLabProject
{
    /// <summary>
    /// The project ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The project name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The project name with namespace.
    /// </summary>
    public string? NameWithNamespace { get; set; }

    /// <summary>
    /// The project path.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// The project path with namespace.
    /// </summary>
    public string? PathWithNamespace { get; set; }

    /// <summary>
    /// The project description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The default branch.
    /// </summary>
    public string? DefaultBranch { get; set; }

    /// <summary>
    /// The project visibility.
    /// </summary>
    public string? Visibility { get; set; }

    /// <summary>
    /// Whether the project is archived.
    /// </summary>
    public bool Archived { get; set; }

    /// <summary>
    /// The web URL.
    /// </summary>
    public string? WebUrl { get; set; }

    /// <summary>
    /// The SSH URL to the repository.
    /// </summary>
    public string? SshUrlToRepo { get; set; }

    /// <summary>
    /// The HTTP URL to the repository.
    /// </summary>
    public string? HttpUrlToRepo { get; set; }

    /// <summary>
    /// The avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Number of forks.
    /// </summary>
    public int ForksCount { get; set; }

    /// <summary>
    /// Number of stars.
    /// </summary>
    public int StarCount { get; set; }

    /// <summary>
    /// When the project was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// When the project was last updated.
    /// </summary>
    public DateTime? LastActivityAt { get; set; }

    /// <summary>
    /// The namespace information.
    /// </summary>
    public GitLabNamespace? Namespace { get; set; }

    /// <summary>
    /// The owner information.
    /// </summary>
    public GitLabUser? Owner { get; set; }
}

/// <summary>
/// Represents a GitLab commit.
/// </summary>
public class GitLabCommit
{
    /// <summary>
    /// The commit ID (SHA).
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// The short commit ID.
    /// </summary>
    public string? ShortId { get; set; }

    /// <summary>
    /// The commit title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The commit message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// The author name.
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    /// The author email.
    /// </summary>
    public string? AuthorEmail { get; set; }

    /// <summary>
    /// The committer name.
    /// </summary>
    public string? CommitterName { get; set; }

    /// <summary>
    /// The committer email.
    /// </summary>
    public string? CommitterEmail { get; set; }

    /// <summary>
    /// When the commit was authored.
    /// </summary>
    public DateTime? AuthoredDate { get; set; }

    /// <summary>
    /// When the commit was committed.
    /// </summary>
    public DateTime? CommittedDate { get; set; }

    /// <summary>
    /// The commit stats.
    /// </summary>
    public GitLabCommitStats? Stats { get; set; }

    /// <summary>
    /// The commit status.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// The project ID.
    /// </summary>
    public long? ProjectId { get; set; }
}

/// <summary>
/// Represents GitLab commit statistics.
/// </summary>
public class GitLabCommitStats
{
    /// <summary>
    /// Number of additions.
    /// </summary>
    public int Additions { get; set; }

    /// <summary>
    /// Number of deletions.
    /// </summary>
    public int Deletions { get; set; }

    /// <summary>
    /// Total changes.
    /// </summary>
    public int Total { get; set; }
}

/// <summary>
/// Represents a GitLab merge request.
/// </summary>
public class GitLabMergeRequest
{
    /// <summary>
    /// The merge request ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The merge request IID (internal ID within project).
    /// </summary>
    public long Iid { get; set; }

    /// <summary>
    /// The project ID.
    /// </summary>
    public long ProjectId { get; set; }

    /// <summary>
    /// The title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The state.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// When created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// When updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// When merged.
    /// </summary>
    public DateTime? MergedAt { get; set; }

    /// <summary>
    /// When closed.
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// The target branch.
    /// </summary>
    public string? TargetBranch { get; set; }

    /// <summary>
    /// The source branch.
    /// </summary>
    public string? SourceBranch { get; set; }

    /// <summary>
    /// The author.
    /// </summary>
    public GitLabUser? Author { get; set; }

    /// <summary>
    /// The assignee.
    /// </summary>
    public GitLabUser? Assignee { get; set; }

    /// <summary>
    /// The assignees.
    /// </summary>
    public List<GitLabUser>? Assignees { get; set; }

    /// <summary>
    /// The reviewers.
    /// </summary>
    public List<GitLabUser>? Reviewers { get; set; }

    /// <summary>
    /// Whether it's a work in progress.
    /// </summary>
    public bool WorkInProgress { get; set; }

    /// <summary>
    /// Whether it has conflicts.
    /// </summary>
    public bool HasConflicts { get; set; }

    /// <summary>
    /// The changes count.
    /// </summary>
    public string? ChangesCount { get; set; }

    /// <summary>
    /// The merge status.
    /// </summary>
    public string? MergeStatus { get; set; }

    /// <summary>
    /// The web URL.
    /// </summary>
    public string? WebUrl { get; set; }

    /// <summary>
    /// The labels.
    /// </summary>
    public List<string>? Labels { get; set; }

    /// <summary>
    /// Whether the merge request should be squashed when merged.
    /// </summary>
    public bool Squash { get; set; }
}

/// <summary>
/// Represents a GitLab pipeline.
/// </summary>
public class GitLabPipeline
{
    /// <summary>
    /// The pipeline ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The project ID.
    /// </summary>
    public long ProjectId { get; set; }

    /// <summary>
    /// The SHA.
    /// </summary>
    public string? Sha { get; set; }

    /// <summary>
    /// The ref (branch/tag).
    /// </summary>
    public string? Ref { get; set; }

    /// <summary>
    /// The status.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// The source.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// When created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// When updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// The web URL.
    /// </summary>
    public string? WebUrl { get; set; }

    /// <summary>
    /// The user who triggered the pipeline.
    /// </summary>
    public GitLabUser? User { get; set; }
}

/// <summary>
/// Represents a GitLab user project contribution.
/// </summary>
public class GitLabUserProjectContribution
{
    /// <summary>
    /// The project ID.
    /// </summary>
    public long ProjectId { get; set; }

    /// <summary>
    /// The project name.
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// The user ID.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// The user name.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// The user email.
    /// </summary>
    public string? UserEmail { get; set; }

    /// <summary>
    /// Number of commits.
    /// </summary>
    public int CommitsCount { get; set; }

    /// <summary>
    /// Number of merge requests created.
    /// </summary>
    public int MergeRequestsCreated { get; set; }

    /// <summary>
    /// Number of merge requests reviewed.
    /// </summary>
    public int MergeRequestsReviewed { get; set; }

    /// <summary>
    /// Last contribution date.
    /// </summary>
    public DateTime? LastContribution { get; set; }
}

/// <summary>
/// Represents a GitLab merge request note/discussion from the API.
/// </summary>
public class GitLabMergeRequestNote
{
    /// <summary>
    /// The note ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The type of the noteable (e.g., "MergeRequest").
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// The body of the note.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// The attachment (if any).
    /// </summary>
    public string? Attachment { get; set; }

    /// <summary>
    /// The author of the note.
    /// </summary>
    public GitLabUser? Author { get; set; }

    /// <summary>
    /// When the note was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// When the note was updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Whether this is a system-generated note.
    /// </summary>
    public bool System { get; set; }

    /// <summary>
    /// The noteable ID (merge request IID).
    /// </summary>
    public long NoteableId { get; set; }

    /// <summary>
    /// The noteable IID.
    /// </summary>
    public long NoteableIid { get; set; }

    /// <summary>
    /// The noteable type.
    /// </summary>
    public string? NoteableType { get; set; }

    /// <summary>
    /// The position (for code review comments).
    /// </summary>
    public object? Position { get; set; }

    /// <summary>
    /// Whether the note is resolvable.
    /// </summary>
    public bool Resolvable { get; set; }

    /// <summary>
    /// Whether the note is resolved.
    /// </summary>
    public bool Resolved { get; set; }

    /// <summary>
    /// Who resolved the note.
    /// </summary>
    public GitLabUser? ResolvedBy { get; set; }

    /// <summary>
    /// The discussion ID.
    /// </summary>
    public string? DiscussionId { get; set; }

    /// <summary>
    /// Whether this is a note on a commit.
    /// </summary>
    public bool NoteOnCommit { get; set; }
}

/// <summary>
/// Represents a GitLab user event (contribution event).
/// </summary>
public class GitLabEvent
{
    /// <summary>
    /// The event ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The project associated with the event.
    /// </summary>
    public GitLabEventProject? Project { get; set; }

    /// <summary>
    /// The action name (e.g., "pushed", "opened", "closed", etc.).
    /// </summary>
    public string? ActionName { get; set; }

    /// <summary>
    /// The target type (e.g., "MergeRequest", "Issue", etc.).
    /// </summary>
    public string? TargetType { get; set; }

    /// <summary>
    /// The author (user who performed the action).
    /// </summary>
    public GitLabEventAuthor? Author { get; set; }

    /// <summary>
    /// When the event was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Push data (only present for push events).
    /// </summary>
    public GitLabPushData? PushData { get; set; }
}

/// <summary>
/// Represents push data in a GitLab event.
/// </summary>
public class GitLabPushData
{
    /// <summary>
    /// Number of commits in the push.
    /// </summary>
    public int CommitCount { get; set; }

    /// <summary>
    /// The action (e.g., "pushed").
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// The reference type (e.g., "branch").
    /// </summary>
    public string? RefType { get; set; }

    /// <summary>
    /// The commit SHA before the push.
    /// </summary>
    public string? CommitFrom { get; set; }

    /// <summary>
    /// The commit SHA after the push.
    /// </summary>
    public string? CommitTo { get; set; }

    /// <summary>
    /// The reference (branch name).
    /// </summary>
    public string? Ref { get; set; }

    /// <summary>
    /// The commit title.
    /// </summary>
    public string? CommitTitle { get; set; }
}

/// <summary>
/// Represents a simplified project in an event.
/// </summary>
public class GitLabEventProject
{
    /// <summary>
    /// The project ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The project name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The project description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The project web URL.
    /// </summary>
    public string? WebUrl { get; set; }

    /// <summary>
    /// The project path with namespace.
    /// </summary>
    public string? PathWithNamespace { get; set; }
}

/// <summary>
/// Represents a simplified author in an event.
/// </summary>
public class GitLabEventAuthor
{
    /// <summary>
    /// The author ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The author username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// The author name.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Represents changes (diff stats) for a merge request.
/// </summary>
public class GitLabMergeRequestChanges
{
    /// <summary>
    /// Total number of lines added across all changes.
    /// </summary>
    public int Additions { get; set; }

    /// <summary>
    /// Total number of lines deleted across all changes.
    /// </summary>
    public int Deletions { get; set; }

    /// <summary>
    /// Total changes (additions + deletions).
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// List of changed files with individual stats.
    /// </summary>
    public List<GitLabMergeRequestChange>? Changes { get; set; }
}

/// <summary>
/// Represents a single file change in a merge request.
/// </summary>
public class GitLabMergeRequestChange
{
    /// <summary>
    /// The old file path (null for new files).
    /// </summary>
    public string? OldPath { get; set; }

    /// <summary>
    /// The new file path (null for deleted files).
    /// </summary>
    public string? NewPath { get; set; }

    /// <summary>
    /// Whether the file is new.
    /// </summary>
    public bool NewFile { get; set; }

    /// <summary>
    /// Whether the file was renamed.
    /// </summary>
    public bool RenamedFile { get; set; }

    /// <summary>
    /// Whether the file was deleted.
    /// </summary>
    public bool DeletedFile { get; set; }
}

/// <summary>
/// Represents a GitLab discussion (threaded comments).
/// </summary>
public class GitLabDiscussion
{
    /// <summary>
    /// The discussion ID.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Whether this discussion is for a single comment.
    /// </summary>
    public bool IndividualNote { get; set; }

    /// <summary>
    /// The notes in this discussion.
    /// </summary>
    public List<GitLabMergeRequestNote>? Notes { get; set; }
}

/// <summary>
/// Represents merge request approval information.
/// </summary>
public class GitLabMergeRequestApprovals
{
    /// <summary>
    /// The merge request ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The merge request IID.
    /// </summary>
    public long Iid { get; set; }

    /// <summary>
    /// The project ID.
    /// </summary>
    public long ProjectId { get; set; }

    /// <summary>
    /// Number of approvals required.
    /// </summary>
    public int ApprovalsRequired { get; set; }

    /// <summary>
    /// Number of approvals given.
    /// </summary>
    public int ApprovalsLeft { get; set; }

    /// <summary>
    /// Whether the MR is approved.
    /// </summary>
    public bool Approved { get; set; }

    /// <summary>
    /// List of users who approved.
    /// </summary>
    public List<GitLabApprover>? ApprovedBy { get; set; }

    /// <summary>
    /// List of suggested approvers.
    /// </summary>
    public List<GitLabUser>? SuggestedApprovers { get; set; }

    /// <summary>
    /// List of approvers.
    /// </summary>
    public List<GitLabUser>? Approvers { get; set; }
}

/// <summary>
/// Represents an approver with timestamp.
/// </summary>
public class GitLabApprover
{
    /// <summary>
    /// The user who approved.
    /// </summary>
    public GitLabUser? User { get; set; }
}
