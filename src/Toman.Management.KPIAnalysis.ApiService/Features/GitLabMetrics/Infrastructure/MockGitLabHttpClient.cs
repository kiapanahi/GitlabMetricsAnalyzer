using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

/// <summary>
/// Mock implementation of GitLab HTTP client for development and testing.
/// Provides realistic test data without requiring actual GitLab API access.
/// </summary>
public sealed class MockGitLabHttpClient : IGitLabHttpClient
{
    private readonly List<GitLabUser> _users;
    private readonly List<GitLabProject> _projects;
    private readonly List<GitLabCommit> _commits;
    private readonly List<GitLabMergeRequest> _mergeRequests;
    private readonly List<GitLabPipeline> _pipelines;
    private readonly List<GitLabContributedProject> _contributedProjects;

    public MockGitLabHttpClient()
    {
        // Initialize mock data
        _users = CreateMockUsers();
        _projects = CreateMockProjects();
        _commits = CreateMockCommits();
        _mergeRequests = CreateMockMergeRequests();
        _pipelines = CreateMockPipelines();
        _contributedProjects = CreateMockContributedProjects();
    }

    /// <summary>
    /// Tests the connection (always returns true for mock).
    /// </summary>
    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Gets all accessible projects.
    /// </summary>
    public Task<IReadOnlyList<GitLabProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_projects.AsReadOnly() as IReadOnlyList<GitLabProject>);
    }

    /// <summary>
    /// Gets projects for a specific group (returns empty for mock).
    /// </summary>
    public Task<IReadOnlyList<GitLabProject>> GetGroupProjectsAsync(long groupId, CancellationToken cancellationToken = default)
    {
        // Mock implementation - return empty list
        return Task.FromResult(new List<GitLabProject>().AsReadOnly() as IReadOnlyList<GitLabProject>);
    }

    /// <summary>
    /// Gets commits for a specific project.
    /// </summary>
    public Task<IReadOnlyList<GitLabCommit>> GetCommitsAsync(long projectId, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
    {
        var projectCommits = _commits.Where(c => c.ProjectId == projectId).ToList();

        if (since.HasValue)
        {
            projectCommits = projectCommits.Where(c => c.CommittedDate >= since.Value.DateTime).ToList();
        }

        return Task.FromResult(projectCommits.AsReadOnly() as IReadOnlyList<GitLabCommit>);
    }

    /// <summary>
    /// Gets merge requests for a specific project.
    /// </summary>
    public Task<IReadOnlyList<GitLabMergeRequest>> GetMergeRequestsAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
    {
        var projectMrs = _mergeRequests.Where(mr => mr.ProjectId == projectId).ToList();

        if (updatedAfter.HasValue)
        {
            projectMrs = projectMrs.Where(mr => mr.UpdatedAt >= updatedAfter.Value.DateTime).ToList();
        }

        return Task.FromResult(projectMrs.AsReadOnly() as IReadOnlyList<GitLabMergeRequest>);
    }

    /// <summary>
    /// Gets commits for a specific merge request.
    /// </summary>
    public Task<IReadOnlyList<GitLabCommit>> GetMergeRequestCommitsAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default)
    {
        // Mock implementation - return some commits for the MR's source branch
        var mr = _mergeRequests.FirstOrDefault(m => m.ProjectId == projectId && m.Iid == mergeRequestIid);
        if (mr is null)
        {
            return Task.FromResult(Array.Empty<GitLabCommit>() as IReadOnlyList<GitLabCommit>);
        }

        // Return 1-5 commits for this MR, simulating commits in the MR
        var random = new Random((int)(projectId + mergeRequestIid));
        var commitCount = random.Next(1, 6);
        var mrCommits = new List<GitLabCommit>();

        for (int i = 0; i < commitCount; i++)
        {
            var commitDate = mr.CreatedAt?.AddHours(-random.Next(1, 24 * 7)); // Commits before MR creation
            mrCommits.Add(new GitLabCommit
            {
                Id = $"commit_{projectId}_{mergeRequestIid}_{i}",
                ShortId = $"abc{i}{mergeRequestIid}",
                Title = $"Commit {i + 1} for MR {mergeRequestIid}",
                Message = $"Work on feature\n\n- Implementation details",
                AuthorName = mr.Author?.Name ?? "Developer",
                AuthorEmail = mr.Author?.Email ?? "dev@example.com",
                CommitterName = mr.Author?.Name ?? "Developer",
                CommitterEmail = mr.Author?.Email ?? "dev@example.com",
                AuthoredDate = commitDate,
                CommittedDate = commitDate,
                Stats = new GitLabCommitStats
                {
                    Additions = random.Next(10, 100),
                    Deletions = random.Next(0, 50),
                    Total = random.Next(10, 150)
                },
                Status = "success",
                ProjectId = projectId
            });
        }

        // Sort by date to ensure first commit is the oldest
        return Task.FromResult(mrCommits.OrderBy(c => c.CommittedDate).ToList().AsReadOnly() as IReadOnlyList<GitLabCommit>);
    }

    /// <summary>
    /// Gets pipelines for a specific project.
    /// </summary>
    public Task<IReadOnlyList<GitLabPipeline>> GetPipelinesAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
    {
        var projectPipelines = _pipelines.Where(p => p.ProjectId == projectId).ToList();

        if (updatedAfter.HasValue)
        {
            projectPipelines = projectPipelines.Where(p => p.UpdatedAt >= updatedAfter.Value.DateTime).ToList();
        }

        return Task.FromResult(projectPipelines.AsReadOnly() as IReadOnlyList<GitLabPipeline>);
    }

    /// <summary>
    /// Gets all users.
    /// </summary>
    public Task<IReadOnlyList<GitLabUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_users.AsReadOnly() as IReadOnlyList<GitLabUser>);
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    public Task<GitLabUser?> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = _users.FirstOrDefault(u => u.Id == userId);
        return Task.FromResult(user);
    }

    /// <summary>
    /// Gets projects that a user has contributed to.
    /// </summary>
    public Task<IReadOnlyList<GitLabContributedProject>> GetUserContributedProjectsAsync(long userId, CancellationToken cancellationToken = default)
    {
        var contributedProjects = _contributedProjects.Where(cp => cp.Id != 0).ToList(); // Mock - return all for simplicity
        return Task.FromResult(contributedProjects.AsReadOnly() as IReadOnlyList<GitLabContributedProject>);
    }

    /// <summary>
    /// Gets user project contributions.
    /// </summary>
    public Task<IReadOnlyList<GitLabUserProjectContribution>> GetUserProjectContributionsAsync(long userId, string? userEmail = null, CancellationToken cancellationToken = default)
    {
        // Mock implementation - create contributions based on commits
        var contributions = new List<GitLabUserProjectContribution>();

        var userCommits = _commits.Where(c => c.AuthorEmail == userEmail).ToList();
        var projectGroups = userCommits.GroupBy(c => c.ProjectId);

        foreach (var group in projectGroups)
        {
            var project = _projects.FirstOrDefault(p => p.Id == group.Key);
            if (project != null)
            {
                contributions.Add(new GitLabUserProjectContribution
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    UserId = userId,
                    UserEmail = userEmail,
                    CommitsCount = group.Count(),
                    LastContribution = group.Max(c => c.CommittedDate)
                });
            }
        }

        return Task.FromResult(contributions.AsReadOnly() as IReadOnlyList<GitLabUserProjectContribution>);
    }

    /// <summary>
    /// Gets projects owned by a user.
    /// </summary>
    public Task<IReadOnlyList<GitLabProject>> GetUserProjectsAsync(long userId, CancellationToken cancellationToken = default)
    {
        // Mock - return projects where user is owner
        var userProjects = _projects.Where(p => p.Owner?.Id == userId).ToList();
        return Task.FromResult(userProjects.AsReadOnly() as IReadOnlyList<GitLabProject>);
    }

    /// <summary>
    /// Gets projects a user has contributed to based on activity.
    /// </summary>
    public Task<IReadOnlyList<GitLabProject>> GetUserProjectsByActivityAsync(long userId, string userEmail, CancellationToken cancellationToken = default)
    {
        // Mock - return projects where user has commits
        var userCommitProjectIds = _commits
            .Where(c => c.AuthorEmail == userEmail)
            .Select(c => c.ProjectId)
            .Distinct();

        var projects = _projects.Where(p => userCommitProjectIds.Contains(p.Id)).ToList();
        return Task.FromResult(projects.AsReadOnly() as IReadOnlyList<GitLabProject>);
    }

    /// <summary>
    /// Gets commits by user email for a specific project.
    /// </summary>
    public Task<IReadOnlyList<GitLabCommit>> GetCommitsByUserEmailAsync(long projectId, string userEmail, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
    {
        var commits = _commits
            .Where(c => c.ProjectId == projectId && c.AuthorEmail == userEmail)
            .ToList();

        if (since.HasValue)
        {
            commits = commits.Where(c => c.CommittedDate >= since.Value.DateTime).ToList();
        }

        return Task.FromResult(commits.AsReadOnly() as IReadOnlyList<GitLabCommit>);
    }

    /// <summary>
    /// Gets merge request notes for a specific project and merge request.
    /// </summary>
    public Task<IReadOnlyList<GitLabMergeRequestNote>> GetMergeRequestNotesAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default)
    {
        // Mock implementation - return empty list for now
        // In a real implementation, this would return mock notes
        return Task.FromResult(new List<GitLabMergeRequestNote>().AsReadOnly() as IReadOnlyList<GitLabMergeRequestNote>);
    }

    /// <summary>
    /// Gets contribution events for a user.
    /// </summary>
    public Task<IReadOnlyList<GitLabEvent>> GetUserEventsAsync(long userId, DateTimeOffset? after = null, DateTimeOffset? before = null, CancellationToken cancellationToken = default)
    {
        // Create mock push events for testing
        var now = DateTime.UtcNow;
        var events = new List<GitLabEvent>();

        // Generate some mock push events spread across different hours
        var user = _users.FirstOrDefault(u => u.Id == userId);
        if (user is not null)
        {
            var random = new Random((int)userId);
            var startDate = after?.DateTime ?? now.AddDays(-30);
            var endDate = before?.DateTime ?? now;

            // Create 20-50 push events spread across the time period
            var eventCount = random.Next(20, 51);
            for (var i = 0; i < eventCount; i++)
            {
                var eventDate = startDate.AddMinutes(random.Next(0, (int)(endDate - startDate).TotalMinutes));
                var project = _projects[random.Next(_projects.Count)];

                events.Add(new GitLabEvent
                {
                    Id = i + 1,
                    ActionName = "pushed",
                    TargetType = null,
                    CreatedAt = eventDate,
                    Project = new GitLabEventProject
                    {
                        Id = project.Id,
                        Name = project.Name,
                        Description = project.Description,
                        WebUrl = project.WebUrl,
                        PathWithNamespace = project.PathWithNamespace
                    },
                    Author = new GitLabEventAuthor
                    {
                        Id = userId,
                        Username = user.Username,
                        Name = user.Name
                    },
                    PushData = new GitLabPushData
                    {
                        CommitCount = random.Next(1, 6), // 1-5 commits per push
                        Action = "pushed",
                        RefType = "branch",
                        CommitFrom = Guid.NewGuid().ToString()[..8],
                        CommitTo = Guid.NewGuid().ToString()[..8],
                        Ref = "main",
                        CommitTitle = $"Mock commit {i + 1}"
                    }
                });
            }
        }

        return Task.FromResult(events.AsReadOnly() as IReadOnlyList<GitLabEvent>);
    }

    #region Mock Data Creation

    private List<GitLabUser> CreateMockUsers()
    {
        return new List<GitLabUser>
        {
            new GitLabUser
            {
                Id = 1,
                Username = "alice.dev",
                Email = "alice@example.com",
                Name = "Alice Developer",
                State = "active",
                CreatedAt = DateTime.Parse("2023-01-15"),
                IsAdmin = false,
                CanCreateGroup = true,
                CanCreateProject = true,
                TwoFactorEnabled = true,
                External = false,
                PrivateProfile = false
            },
            new GitLabUser
            {
                Id = 2,
                Username = "bob.smith",
                Email = "bob@example.com",
                Name = "Bob Smith",
                State = "active",
                CreatedAt = DateTime.Parse("2023-02-20"),
                IsAdmin = false,
                CanCreateGroup = true,
                CanCreateProject = true,
                TwoFactorEnabled = false,
                External = false,
                PrivateProfile = false
            },
            new GitLabUser
            {
                Id = 3,
                Username = "charlie.jones",
                Email = "charlie@example.com",
                Name = "Charlie Jones",
                State = "active",
                CreatedAt = DateTime.Parse("2023-03-10"),
                IsAdmin = false,
                CanCreateGroup = true,
                CanCreateProject = true,
                TwoFactorEnabled = true,
                External = false,
                PrivateProfile = false
            },
            new GitLabUser
            {
                Id = 4,
                Username = "diana.prince",
                Email = "diana@example.com",
                Name = "Diana Prince",
                State = "active",
                CreatedAt = DateTime.Parse("2023-04-05"),
                IsAdmin = false,
                CanCreateGroup = true,
                CanCreateProject = true,
                TwoFactorEnabled = false,
                External = false,
                PrivateProfile = false
            },
            new GitLabUser
            {
                Id = 5,
                Username = "eve.wilson",
                Email = "eve@example.com",
                Name = "Eve Wilson",
                State = "active",
                CreatedAt = DateTime.Parse("2023-05-12"),
                IsAdmin = false,
                CanCreateGroup = true,
                CanCreateProject = true,
                TwoFactorEnabled = true,
                External = false,
                PrivateProfile = false
            }
        };
    }

    private List<GitLabProject> CreateMockProjects()
    {
        var users = CreateMockUsers();
        return new List<GitLabProject>
        {
            new GitLabProject
            {
                Id = 1,
                Name = "web-api",
                NameWithNamespace = "company/web-api",
                Path = "web-api",
                PathWithNamespace = "company/web-api",
                Description = "Main web API service",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/web-api",
                ForksCount = 2,
                StarCount = 5,
                CreatedAt = DateTime.Parse("2023-01-20"),
                LastActivityAt = DateTime.Parse("2024-09-20"),
                Owner = users[0] // Alice
            },
            new GitLabProject
            {
                Id = 2,
                Name = "mobile-app",
                NameWithNamespace = "company/mobile-app",
                Path = "mobile-app",
                PathWithNamespace = "company/mobile-app",
                Description = "iOS and Android mobile application",
                DefaultBranch = "develop",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/mobile-app",
                ForksCount = 1,
                StarCount = 3,
                CreatedAt = DateTime.Parse("2023-02-15"),
                LastActivityAt = DateTime.Parse("2024-09-19"),
                Owner = users[1] // Bob
            },
            new GitLabProject
            {
                Id = 3,
                Name = "data-pipeline",
                NameWithNamespace = "company/data-pipeline",
                Path = "data-pipeline",
                PathWithNamespace = "company/data-pipeline",
                Description = "ETL data processing pipeline",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/data-pipeline",
                ForksCount = 0,
                StarCount = 2,
                CreatedAt = DateTime.Parse("2023-03-01"),
                LastActivityAt = DateTime.Parse("2024-09-18"),
                Owner = users[2] // Charlie
            },
            new GitLabProject
            {
                Id = 4,
                Name = "analytics-dashboard",
                NameWithNamespace = "company/analytics-dashboard",
                Path = "analytics-dashboard",
                PathWithNamespace = "company/analytics-dashboard",
                Description = "Business intelligence dashboard",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/analytics-dashboard",
                ForksCount = 3,
                StarCount = 8,
                CreatedAt = DateTime.Parse("2023-03-20"),
                LastActivityAt = DateTime.Parse("2024-09-17"),
                Owner = users[3] // Diana
            },
            new GitLabProject
            {
                Id = 5,
                Name = "infrastructure",
                NameWithNamespace = "company/infrastructure",
                Path = "infrastructure",
                PathWithNamespace = "company/infrastructure",
                Description = "Infrastructure as Code and deployment scripts",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/infrastructure",
                ForksCount = 1,
                StarCount = 4,
                CreatedAt = DateTime.Parse("2023-04-10"),
                LastActivityAt = DateTime.Parse("2024-09-16"),
                Owner = users[4] // Eve
            },
            new GitLabProject
            {
                Id = 6,
                Name = "documentation",
                NameWithNamespace = "company/documentation",
                Path = "documentation",
                PathWithNamespace = "company/documentation",
                Description = "Project documentation and guides",
                DefaultBranch = "main",
                Visibility = "internal",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/documentation",
                ForksCount = 0,
                StarCount = 1,
                CreatedAt = DateTime.Parse("2023-05-01"),
                LastActivityAt = DateTime.Parse("2024-09-15"),
                Owner = users[0] // Alice
            },
            new GitLabProject
            {
                Id = 7,
                Name = "testing-framework",
                NameWithNamespace = "company/testing-framework",
                Path = "testing-framework",
                PathWithNamespace = "company/testing-framework",
                Description = "Automated testing framework",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/testing-framework",
                ForksCount = 2,
                StarCount = 6,
                CreatedAt = DateTime.Parse("2023-05-15"),
                LastActivityAt = DateTime.Parse("2024-09-14"),
                Owner = users[1] // Bob
            },
            new GitLabProject
            {
                Id = 8,
                Name = "legacy-system",
                NameWithNamespace = "company/legacy-system",
                Path = "legacy-system",
                PathWithNamespace = "company/legacy-system",
                Description = "Legacy system maintenance",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = true,
                WebUrl = "https://gitlab.example.com/company/legacy-system",
                ForksCount = 0,
                StarCount = 0,
                CreatedAt = DateTime.Parse("2022-01-01"),
                LastActivityAt = DateTime.Parse("2023-12-31"),
                Owner = users[2] // Charlie
            },
            new GitLabProject
            {
                Id = 9,
                Name = "machine-learning",
                NameWithNamespace = "company/machine-learning",
                Path = "machine-learning",
                PathWithNamespace = "company/machine-learning",
                Description = "ML models and experiments",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/machine-learning",
                ForksCount = 1,
                StarCount = 7,
                CreatedAt = DateTime.Parse("2023-06-01"),
                LastActivityAt = DateTime.Parse("2024-09-13"),
                Owner = users[3] // Diana
            },
            new GitLabProject
            {
                Id = 10,
                Name = "security-tools",
                NameWithNamespace = "company/security-tools",
                Path = "security-tools",
                PathWithNamespace = "company/security-tools",
                Description = "Security scanning and compliance tools",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/security-tools",
                ForksCount = 0,
                StarCount = 3,
                CreatedAt = DateTime.Parse("2023-06-15"),
                LastActivityAt = DateTime.Parse("2024-09-12"),
                Owner = users[4] // Eve
            },
            new GitLabProject
            {
                Id = 11,
                Name = "customer-portal",
                NameWithNamespace = "company/customer-portal",
                Path = "customer-portal",
                PathWithNamespace = "company/customer-portal",
                Description = "Customer-facing web portal",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/customer-portal",
                ForksCount = 1,
                StarCount = 4,
                CreatedAt = DateTime.Parse("2023-07-01"),
                LastActivityAt = DateTime.Parse("2024-09-11"),
                Owner = users[0] // Alice
            },
            new GitLabProject
            {
                Id = 12,
                Name = "notification-service",
                NameWithNamespace = "company/notification-service",
                Path = "notification-service",
                PathWithNamespace = "company/notification-service",
                Description = "Email and SMS notification service",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/notification-service",
                ForksCount = 0,
                StarCount = 2,
                CreatedAt = DateTime.Parse("2023-07-15"),
                LastActivityAt = DateTime.Parse("2024-09-10"),
                Owner = users[1] // Bob
            },
            new GitLabProject
            {
                Id = 13,
                Name = "payment-gateway",
                NameWithNamespace = "company/payment-gateway",
                Path = "payment-gateway",
                PathWithNamespace = "company/payment-gateway",
                Description = "Payment processing integration",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/payment-gateway",
                ForksCount = 0,
                StarCount = 1,
                CreatedAt = DateTime.Parse("2023-08-01"),
                LastActivityAt = DateTime.Parse("2024-09-09"),
                Owner = users[2] // Charlie
            },
            new GitLabProject
            {
                Id = 14,
                Name = "reporting-engine",
                NameWithNamespace = "company/reporting-engine",
                Path = "reporting-engine",
                PathWithNamespace = "company/reporting-engine",
                Description = "Automated report generation",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/reporting-engine",
                ForksCount = 1,
                StarCount = 5,
                CreatedAt = DateTime.Parse("2023-08-15"),
                LastActivityAt = DateTime.Parse("2024-09-08"),
                Owner = users[3] // Diana
            },
            new GitLabProject
            {
                Id = 15,
                Name = "api-gateway",
                NameWithNamespace = "company/api-gateway",
                Path = "api-gateway",
                PathWithNamespace = "company/api-gateway",
                Description = "API gateway and routing service",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/api-gateway",
                ForksCount = 2,
                StarCount = 6,
                CreatedAt = DateTime.Parse("2023-09-01"),
                LastActivityAt = DateTime.Parse("2024-09-07"),
                Owner = users[4] // Eve
            }
        };
    }

    private List<GitLabCommit> CreateMockCommits()
    {
        var users = CreateMockUsers();
        var projects = CreateMockProjects();
        var commits = new List<GitLabCommit>();
        var random = new Random(42); // Fixed seed for consistent data

        // Generate commits for the last 6 months
        var startDate = DateTime.Now.AddMonths(-6);
        var endDate = DateTime.Now;

        foreach (var project in projects.Where(p => !p.Archived))
        {
            // Generate 10-50 commits per project
            var commitCount = random.Next(10, 51);

            for (int i = 0; i < commitCount; i++)
            {
                var user = users[random.Next(users.Count)];
                var commitDate = startDate.AddDays(random.Next((endDate - startDate).Days));

                var stats = new GitLabCommitStats
                {
                    Additions = random.Next(1, 100),
                    Deletions = random.Next(0, 50),
                    Total = 0
                };

                commits.Add(new GitLabCommit
                {
                    Id = $"commit_{project.Id}_{i}",
                    ShortId = $"abc123{i}",
                    Title = $"Commit {i} for {project.Name}",
                    Message = $"Implement feature {i}\n\n- Added new functionality\n- Fixed bug\n- Updated tests",
                    AuthorName = user.Name,
                    AuthorEmail = user.Email,
                    CommitterName = user.Name,
                    CommitterEmail = user.Email,
                    AuthoredDate = commitDate,
                    CommittedDate = commitDate.AddMinutes(random.Next(1, 60)),
                    Stats = stats,
                    Status = "success",
                    ProjectId = project.Id
                });

                // Calculate total
                stats.Total = stats.Additions + stats.Deletions;
            }
        }

        return commits;
    }

    private List<GitLabMergeRequest> CreateMockMergeRequests()
    {
        var users = CreateMockUsers();
        var projects = CreateMockProjects();
        var mergeRequests = new List<GitLabMergeRequest>();
        var random = new Random(42);

        var states = new[] { "opened", "closed", "merged" };

        foreach (var project in projects.Where(p => !p.Archived))
        {
            // Generate 3-15 MRs per project
            var mrCount = random.Next(3, 16);

            for (int i = 0; i < mrCount; i++)
            {
                var author = users[random.Next(users.Count)];
                var createdDate = DateTime.Now.AddDays(-random.Next(180));
                var state = states[random.Next(states.Length)];

                mergeRequests.Add(new GitLabMergeRequest
                {
                    Id = (project.Id * 1000) + i,
                    Iid = i + 1,
                    ProjectId = project.Id,
                    Title = $"MR {i + 1}: {project.Name} feature",
                    Description = $"Implementation of feature {i + 1} for {project.Name}",
                    State = state,
                    CreatedAt = createdDate,
                    UpdatedAt = createdDate.AddDays(random.Next(1, 30)),
                    MergedAt = state == "merged" ? (DateTime?)createdDate.AddDays(random.Next(1, 14)) : null,
                    ClosedAt = state == "closed" ? (DateTime?)createdDate.AddDays(random.Next(1, 14)) : null,
                    TargetBranch = "main",
                    SourceBranch = $"feature/{i + 1}",
                    Author = author,
                    WorkInProgress = false,
                    HasConflicts = false,
                    ChangesCount = random.Next(1, 20).ToString(),
                    MergeStatus = "can_be_merged",
                    WebUrl = $"https://gitlab.example.com/company/{project.Path}/-/merge_requests/{i + 1}"
                });
            }
        }

        return mergeRequests;
    }

    private List<GitLabPipeline> CreateMockPipelines()
    {
        var projects = CreateMockProjects();
        var pipelines = new List<GitLabPipeline>();
        var random = new Random(42);

        var statuses = new[] { "success", "failed", "running", "pending", "canceled" };
        var sources = new[] { "push", "web", "schedule", "api", "external" };

        foreach (var project in projects.Where(p => !p.Archived))
        {
            // Generate 5-25 pipelines per project
            var pipelineCount = random.Next(5, 26);

            for (int i = 0; i < pipelineCount; i++)
            {
                var createdDate = DateTime.Now.AddDays(-random.Next(180));

                pipelines.Add(new GitLabPipeline
                {
                    Id = (project.Id * 10000) + i,
                    ProjectId = project.Id,
                    Sha = $"sha_{project.Id}_{i}",
                    Ref = i % 3 == 0 ? "main" : $"feature/{i}",
                    Status = statuses[random.Next(statuses.Length)],
                    Source = sources[random.Next(sources.Length)],
                    CreatedAt = createdDate,
                    UpdatedAt = createdDate.AddMinutes(random.Next(1, 120)),
                    WebUrl = $"https://gitlab.example.com/company/{project.Path}/-/pipelines/{(project.Id * 10000) + i}"
                });
            }
        }

        return pipelines;
    }

    private List<GitLabContributedProject> CreateMockContributedProjects()
    {
        var projects = CreateMockProjects();
        var contributedProjects = new List<GitLabContributedProject>();

        foreach (var project in projects)
        {
            contributedProjects.Add(new GitLabContributedProject
            {
                Id = project.Id,
                Name = project.Name!,
                NameWithNamespace = project.NameWithNamespace!,
                Path = project.Path!,
                PathWithNamespace = project.PathWithNamespace!,
                Description = project.Description,
                DefaultBranch = project.DefaultBranch,
                WebUrl = project.WebUrl!,
                ForksCount = project.ForksCount
            });
        }

        return contributedProjects;
    }

    #endregion
}
