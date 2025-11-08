using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure.DTOs;

namespace KuriousLabs.Management.KPIAnalysis.Tests.TestFixtures;

/// <summary>
/// Deterministic test fixtures for Jira data covering comprehensive edge cases.
/// All data uses fixed seeds to ensure reproducible test outcomes.
/// </summary>
public static class JiraTestFixtures
{
    /// <summary>
    /// Fixed date for deterministic test data generation
    /// </summary>
    public static readonly DateTime FixedBaseDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Creates test Jira users with various patterns
    /// </summary>
    public static List<JiraUser> CreateTestUsers()
    {
        return
        [
            new JiraUser(
                AccountId: "user-001",
                DisplayName: "Alice Developer",
                EmailAddress: "alice@example.com",
                Active: true,
                AccountType: "atlassian"
            ),
            new JiraUser(
                AccountId: "user-002",
                DisplayName: "Bob Reviewer",
                EmailAddress: "bob@example.com",
                Active: true,
                AccountType: "atlassian"
            ),
            new JiraUser(
                AccountId: "user-003",
                DisplayName: "Charlie Manager",
                EmailAddress: "charlie@example.com",
                Active: true,
                AccountType: "atlassian"
            ),
            new JiraUser(
                AccountId: "bot-001",
                DisplayName: "Automation Bot",
                EmailAddress: "bot@example.com",
                Active: true,
                AccountType: "app" // Bot account
            ),
            new JiraUser(
                AccountId: "user-inactive",
                DisplayName: "Former Employee",
                EmailAddress: "former@example.com",
                Active: false, // Inactive user
                AccountType: "atlassian"
            )
        ];
    }

    /// <summary>
    /// Creates test Jira projects with different configurations
    /// </summary>
    public static List<JiraProject> CreateTestProjects()
    {
        return
        [
            new JiraProject(
                Id: "10001",
                Key: "MAIN",
                Name: "Main Service",
                ProjectTypeKey: "software",
                Lead: CreateTestUsers()[0]
            ),
            new JiraProject(
                Id: "10002",
                Key: "LEG",
                Name: "Legacy System",
                ProjectTypeKey: "software",
                Lead: CreateTestUsers()[2]
            ),
            new JiraProject(
                Id: "10003",
                Key: "OPS",
                Name: "Operations",
                ProjectTypeKey: "service_desk",
                Lead: CreateTestUsers()[1]
            )
        ];
    }

    /// <summary>
    /// Creates test Jira issues covering various states and patterns
    /// </summary>
    public static List<JiraIssue> CreateTestIssues()
    {
        var users = CreateTestUsers();
        var projects = CreateTestProjects();
        var issues = new List<JiraIssue>();

        // Standard feature issue - completed
        issues.Add(new JiraIssue(
            Id: "1001",
            Key: "MAIN-101",
            Fields: new JiraIssue.JiraIssueFields(
                Summary: "Implement user authentication endpoint",
                Created: FixedBaseDate.AddDays(-30),
                Updated: FixedBaseDate.AddDays(-10),
                ResolutionDate: FixedBaseDate.AddDays(-10),
                Status: new JiraIssue.JiraStatus(
                    Name: "Done",
                    StatusCategory: new JiraIssue.JiraStatusCategory(Key: "done")
                ),
                IssueType: new JiraIssue.JiraIssueType(
                    Name: "Story",
                    Subtask: false
                ),
                Priority: new JiraIssue.JiraPriority(Name: "Medium"),
                Assignee: users[0],
                Reporter: users[2],
                Project: projects[0],
                Description: "Add JWT-based authentication to the API"
            )
        ));

        // Bug - resolved quickly (hotfix)
        issues.Add(new JiraIssue(
            Id: "1002",
            Key: "MAIN-102",
            Fields: new JiraIssue.JiraIssueFields(
                Summary: "Critical security vulnerability in auth",
                Created: FixedBaseDate.AddDays(-5),
                Updated: FixedBaseDate.AddDays(-5).AddHours(3),
                ResolutionDate: FixedBaseDate.AddDays(-5).AddHours(3),
                Status: new JiraIssue.JiraStatus(
                    Name: "Done",
                    StatusCategory: new JiraIssue.JiraStatusCategory(Key: "done")
                ),
                IssueType: new JiraIssue.JiraIssueType(
                    Name: "Bug",
                    Subtask: false
                ),
                Priority: new JiraIssue.JiraPriority(Name: "Highest"),
                Assignee: users[0],
                Reporter: users[1],
                Project: projects[0],
                Description: "Security scan detected critical vulnerability"
            )
        ));

        // In Progress feature
        issues.Add(new JiraIssue(
            Id: "1003",
            Key: "MAIN-103",
            Fields: new JiraIssue.JiraIssueFields(
                Summary: "Add OAuth2 integration",
                Created: FixedBaseDate.AddDays(-15),
                Updated: FixedBaseDate.AddDays(-2),
                ResolutionDate: null, // Not resolved yet
                Status: new JiraIssue.JiraStatus(
                    Name: "In Progress",
                    StatusCategory: new JiraIssue.JiraStatusCategory(Key: "indeterminate")
                ),
                IssueType: new JiraIssue.JiraIssueType(
                    Name: "Story",
                    Subtask: false
                ),
                Priority: new JiraIssue.JiraPriority(Name: "High"),
                Assignee: users[0],
                Reporter: users[2],
                Project: projects[0],
                Description: "Support OAuth2 authentication flow"
            )
        ));

        // Backlog item - not started
        issues.Add(new JiraIssue(
            Id: "1004",
            Key: "MAIN-104",
            Fields: new JiraIssue.JiraIssueFields(
                Summary: "Implement SAML SSO",
                Created: FixedBaseDate.AddDays(-60),
                Updated: FixedBaseDate.AddDays(-60),
                ResolutionDate: null,
                Status: new JiraIssue.JiraStatus(
                    Name: "To Do",
                    StatusCategory: new JiraIssue.JiraStatusCategory(Key: "new")
                ),
                IssueType: new JiraIssue.JiraIssueType(
                    Name: "Story",
                    Subtask: false
                ),
                Priority: new JiraIssue.JiraPriority(Name: "Low"),
                Assignee: null, // Unassigned
                Reporter: users[2],
                Project: projects[0],
                Description: "Add SAML SSO support for enterprise customers"
            )
        ));

        // Epic - not an individual issue
        issues.Add(new JiraIssue(
            Id: "1005",
            Key: "MAIN-105",
            Fields: new JiraIssue.JiraIssueFields(
                Summary: "Authentication & Authorization Epic",
                Created: FixedBaseDate.AddDays(-90),
                Updated: FixedBaseDate.AddDays(-10),
                ResolutionDate: null,
                Status: new JiraIssue.JiraStatus(
                    Name: "In Progress",
                    StatusCategory: new JiraIssue.JiraStatusCategory(Key: "indeterminate")
                ),
                IssueType: new JiraIssue.JiraIssueType(
                    Name: "Epic",
                    Subtask: false
                ),
                Priority: new JiraIssue.JiraPriority(Name: "High"),
                Assignee: users[2],
                Reporter: users[2],
                Project: projects[0],
                Description: "Complete auth system overhaul"
            )
        ));

        // Sub-task completed
        issues.Add(new JiraIssue(
            Id: "1006",
            Key: "MAIN-106",
            Fields: new JiraIssue.JiraIssueFields(
                Summary: "Write unit tests for auth endpoint",
                Created: FixedBaseDate.AddDays(-28),
                Updated: FixedBaseDate.AddDays(-12),
                ResolutionDate: FixedBaseDate.AddDays(-12),
                Status: new JiraIssue.JiraStatus(
                    Name: "Done",
                    StatusCategory: new JiraIssue.JiraStatusCategory(Key: "done")
                ),
                IssueType: new JiraIssue.JiraIssueType(
                    Name: "Sub-task",
                    Subtask: true // Sub-task
                ),
                Priority: new JiraIssue.JiraPriority(Name: "Medium"),
                Assignee: users[0],
                Reporter: users[0],
                Project: projects[0],
                Description: "Add comprehensive unit test coverage"
            )
        ));

        // Long-running bug (old, still open)
        issues.Add(new JiraIssue(
            Id: "1007",
            Key: "LEG-201",
            Fields: new JiraIssue.JiraIssueFields(
                Summary: "Performance degradation in legacy module",
                Created: FixedBaseDate.AddDays(-180),
                Updated: FixedBaseDate.AddDays(-90),
                ResolutionDate: null,
                Status: new JiraIssue.JiraStatus(
                    Name: "Open",
                    StatusCategory: new JiraIssue.JiraStatusCategory(Key: "new")
                ),
                IssueType: new JiraIssue.JiraIssueType(
                    Name: "Bug",
                    Subtask: false
                ),
                Priority: new JiraIssue.JiraPriority(Name: "Medium"),
                Assignee: users[1],
                Reporter: users[2],
                Project: projects[1],
                Description: "System slows down under heavy load"
            )
        ));

        // Task resolved as duplicate
        issues.Add(new JiraIssue(
            Id: "1008",
            Key: "MAIN-107",
            Fields: new JiraIssue.JiraIssueFields(
                Summary: "Add user authentication",
                Created: FixedBaseDate.AddDays(-35),
                Updated: FixedBaseDate.AddDays(-34),
                ResolutionDate: FixedBaseDate.AddDays(-34),
                Status: new JiraIssue.JiraStatus(
                    Name: "Closed",
                    StatusCategory: new JiraIssue.JiraStatusCategory(Key: "done")
                ),
                IssueType: new JiraIssue.JiraIssueType(
                    Name: "Task",
                    Subtask: false
                ),
                Priority: new JiraIssue.JiraPriority(Name: "Medium"),
                Assignee: null,
                Reporter: users[0],
                Project: projects[0],
                Description: "Duplicate of MAIN-101"
            )
        ));

        // Bug resolved as Won't Fix
        issues.Add(new JiraIssue(
            Id: "1009",
            Key: "LEG-202",
            Fields: new JiraIssue.JiraIssueFields(
                Summary: "Outdated UI component",
                Created: FixedBaseDate.AddDays(-120),
                Updated: FixedBaseDate.AddDays(-100),
                ResolutionDate: FixedBaseDate.AddDays(-100),
                Status: new JiraIssue.JiraStatus(
                    Name: "Won't Do",
                    StatusCategory: new JiraIssue.JiraStatusCategory(Key: "done")
                ),
                IssueType: new JiraIssue.JiraIssueType(
                    Name: "Improvement",
                    Subtask: false
                ),
                Priority: new JiraIssue.JiraPriority(Name: "Low"),
                Assignee: users[1],
                Reporter: users[2],
                Project: projects[1],
                Description: "Legacy system to be deprecated"
            )
        ));

        // Recent issue created this week
        issues.Add(new JiraIssue(
            Id: "1010",
            Key: "MAIN-108",
            Fields: new JiraIssue.JiraIssueFields(
                Summary: "Optimize database queries",
                Created: FixedBaseDate.AddDays(-3),
                Updated: FixedBaseDate.AddDays(-2),
                ResolutionDate: null,
                Status: new JiraIssue.JiraStatus(
                    Name: "To Do",
                    StatusCategory: new JiraIssue.JiraStatusCategory(Key: "new")
                ),
                IssueType: new JiraIssue.JiraIssueType(
                    Name: "Improvement",
                    Subtask: false
                ),
                Priority: new JiraIssue.JiraPriority(Name: "Medium"),
                Assignee: users[0],
                Reporter: users[2],
                Project: projects[0],
                Description: "Improve query performance"
            )
        ));

        return issues;
    }

    /// <summary>
    /// Creates Jira search results for testing pagination
    /// </summary>
    public static JiraSearchResult CreateSearchResult(List<JiraIssue> issues, int startAt = 0, int maxResults = 50)
    {
        var pagedIssues = issues.Skip(startAt).Take(maxResults).ToList();
        return new JiraSearchResult(
            StartAt: startAt,
            MaxResults: maxResults,
            Total: issues.Count,
            Issues: pagedIssues
        );
    }

    /// <summary>
    /// Creates Jira server info for health checks
    /// </summary>
    public static JiraServerInfo CreateServerInfo()
    {
        return new JiraServerInfo(
            BaseUrl: "https://jira.tomanpay.net",
            Version: "9.12.4",
            BuildNumber: 912004,
            ServerTitle: "Jira"
        );
    }

    /// <summary>
    /// Creates all test fixtures in a consistent, deterministic way
    /// Each property returns fresh instances to avoid tracking conflicts
    /// </summary>
    public static class CompleteFixture
    {
        public static List<JiraUser> Users => CreateTestUsers();
        public static List<JiraProject> Projects => CreateTestProjects();
        public static List<JiraIssue> Issues => CreateTestIssues();
        public static JiraServerInfo ServerInfo => CreateServerInfo();

        /// <summary>
        /// Get issues filtered by project key
        /// </summary>
        public static List<JiraIssue> IssuesForProject(string projectKey)
        {
            return CreateTestIssues()
                .Where(i => i.Fields.Project.Key == projectKey)
                .ToList();
        }

        /// <summary>
        /// Get issues assigned to a specific user
        /// </summary>
        public static List<JiraIssue> IssuesForUser(string accountId)
        {
            return CreateTestIssues()
                .Where(i => i.Fields.Assignee?.AccountId == accountId)
                .ToList();
        }

        /// <summary>
        /// Get resolved issues only
        /// </summary>
        public static List<JiraIssue> ResolvedIssues =>
            CreateTestIssues()
                .Where(i => i.Fields.ResolutionDate.HasValue)
                .ToList();

        /// <summary>
        /// Get open issues only
        /// </summary>
        public static List<JiraIssue> OpenIssues =>
            CreateTestIssues()
                .Where(i => !i.Fields.ResolutionDate.HasValue)
                .ToList();
    }
}
