# ⚠️ ARCHIVED DOCUMENT ⚠️

**This document describes database entities that were designed but never used.**

**Archived Date**: October 15, 2025  
**Reason**: All 20+ entity models described here are unused. Application calculates metrics via live GitLab API calls with no database storage.

**For current architecture**, see: [CURRENT_STATE.md](../CURRENT_STATE.md)

---

# PRD Entity Design Document

## Overview
This document outlines the new data model design aligned with the PRD requirements, focusing on core GitLab metrics entities while removing obsolete tables.

## Core Entities

### 1. Developer Identity (`developers`)
**Purpose**: Central developer identity with aliases support
```sql
CREATE TABLE developers (
    id BIGSERIAL PRIMARY KEY,
    gitlab_user_id BIGINT NOT NULL,
    primary_email VARCHAR(255) NOT NULL,
    primary_username VARCHAR(255) NOT NULL,
    display_name VARCHAR(255) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT uk_developers_gitlab_user_id UNIQUE (gitlab_user_id),
    CONSTRAINT uk_developers_primary_email UNIQUE (primary_email)
);

CREATE INDEX idx_developers_gitlab_user_id ON developers(gitlab_user_id);
CREATE INDEX idx_developers_primary_email ON developers(primary_email);
```

### 2. Developer Aliases (`developer_aliases`)
**Purpose**: Track multiple emails/usernames for the same developer
```sql
CREATE TABLE developer_aliases (
    id BIGSERIAL PRIMARY KEY,
    developer_id BIGINT NOT NULL REFERENCES developers(id),
    alias_type VARCHAR(50) NOT NULL, -- 'email', 'username'
    alias_value VARCHAR(255) NOT NULL,
    verified_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT uk_developer_aliases_value UNIQUE (alias_value, alias_type),
    FOREIGN KEY (developer_id) REFERENCES developers(id) ON DELETE CASCADE
);

CREATE INDEX idx_developer_aliases_developer_id ON developer_aliases(developer_id);
CREATE INDEX idx_developer_aliases_value ON developer_aliases(alias_value);
```

### 3. Projects (`projects`)
**Purpose**: GitLab project information
```sql
CREATE TABLE projects (
    id BIGINT PRIMARY KEY, -- GitLab project ID
    name VARCHAR(255) NOT NULL,
    path_with_namespace VARCHAR(500) NOT NULL,
    web_url VARCHAR(1000),
    default_branch VARCHAR(255),
    visibility_level VARCHAR(50),
    archived BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ingested_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT uk_projects_path UNIQUE (path_with_namespace)
);

CREATE INDEX idx_projects_name ON projects(name);
CREATE INDEX idx_projects_archived ON projects(archived);
CREATE INDEX idx_projects_created_at ON projects(created_at);
```

### 4. Commit Facts (`commit_facts`)
**Purpose**: Commit-level metrics and facts
```sql
CREATE TABLE commit_facts (
    id BIGSERIAL PRIMARY KEY,
    project_id BIGINT NOT NULL REFERENCES projects(id),
    developer_id BIGINT NOT NULL REFERENCES developers(id),
    sha VARCHAR(40) NOT NULL,
    committed_at TIMESTAMPTZ NOT NULL,
    lines_added INTEGER NOT NULL DEFAULT 0,
    lines_deleted INTEGER NOT NULL DEFAULT 0,
    files_changed INTEGER NOT NULL DEFAULT 0,
    is_signed BOOLEAN NOT NULL DEFAULT false,
    is_merge_commit BOOLEAN NOT NULL DEFAULT false,
    parent_count INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT uk_commit_facts_project_sha UNIQUE (project_id, sha)
) PARTITION BY RANGE (committed_at);

CREATE INDEX idx_commit_facts_developer_id ON commit_facts(developer_id);
CREATE INDEX idx_commit_facts_committed_at ON commit_facts(committed_at);
CREATE INDEX idx_commit_facts_project_id ON commit_facts(project_id);
```

### 5. Merge Request Facts (`merge_request_facts`)
**Purpose**: MR-level metrics with timeline and flags
```sql
CREATE TABLE merge_request_facts (
    id BIGSERIAL PRIMARY KEY,
    project_id BIGINT NOT NULL REFERENCES projects(id),
    mr_iid INTEGER NOT NULL, -- Internal ID within project
    author_developer_id BIGINT NOT NULL REFERENCES developers(id),
    target_branch VARCHAR(255) NOT NULL,
    source_branch VARCHAR(255) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    merged_at TIMESTAMPTZ,
    closed_at TIMESTAMPTZ,
    first_review_at TIMESTAMPTZ,
    state VARCHAR(50) NOT NULL, -- 'opened', 'closed', 'merged'
    
    -- Metrics
    lines_added INTEGER NOT NULL DEFAULT 0,
    lines_deleted INTEGER NOT NULL DEFAULT 0,
    commits_count INTEGER NOT NULL DEFAULT 0,
    files_changed INTEGER NOT NULL DEFAULT 0,
    
    -- Timeline metrics (hours)
    cycle_time_hours DECIMAL(10,2), -- created to merged
    review_time_hours DECIMAL(10,2), -- created to first review
    
    -- Flags
    has_pipeline BOOLEAN NOT NULL DEFAULT false,
    is_draft BOOLEAN NOT NULL DEFAULT false,
    is_wip BOOLEAN NOT NULL DEFAULT false,
    has_conflicts BOOLEAN NOT NULL DEFAULT false,
    
    created_at_fact TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT uk_merge_request_facts_project_iid UNIQUE (project_id, mr_iid)
) PARTITION BY RANGE (created_at);

CREATE INDEX idx_merge_request_facts_author ON merge_request_facts(author_developer_id);
CREATE INDEX idx_merge_request_facts_state ON merge_request_facts(state);
CREATE INDEX idx_merge_request_facts_merged_at ON merge_request_facts(merged_at);
```

### 6. Pipeline Facts (`pipeline_facts`)
**Purpose**: Pipeline execution with merge request links
```sql
CREATE TABLE pipeline_facts (
    id BIGSERIAL PRIMARY KEY,
    project_id BIGINT NOT NULL REFERENCES projects(id),
    pipeline_id BIGINT NOT NULL, -- GitLab pipeline ID
    merge_request_fact_id BIGINT REFERENCES merge_request_facts(id),
    developer_id BIGINT NOT NULL REFERENCES developers(id),
    ref_name VARCHAR(255) NOT NULL, -- branch/tag name
    sha VARCHAR(40) NOT NULL,
    status VARCHAR(50) NOT NULL, -- 'success', 'failed', 'canceled', etc.
    source VARCHAR(50), -- 'push', 'merge_request_event', etc.
    created_at TIMESTAMPTZ NOT NULL,
    started_at TIMESTAMPTZ,
    finished_at TIMESTAMPTZ,
    duration_seconds INTEGER,
    
    created_at_fact TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT uk_pipeline_facts_project_pipeline UNIQUE (project_id, pipeline_id)
) PARTITION BY RANGE (created_at);

CREATE INDEX idx_pipeline_facts_developer_id ON pipeline_facts(developer_id);
CREATE INDEX idx_pipeline_facts_merge_request ON pipeline_facts(merge_request_fact_id);
CREATE INDEX idx_pipeline_facts_status ON pipeline_facts(status);
CREATE INDEX idx_pipeline_facts_created_at ON pipeline_facts(created_at);
```

### 7. Review Events (`review_events`)
**Purpose**: Track all review-related activities
```sql
CREATE TABLE review_events (
    id BIGSERIAL PRIMARY KEY,
    merge_request_fact_id BIGINT NOT NULL REFERENCES merge_request_facts(id),
    reviewer_developer_id BIGINT NOT NULL REFERENCES developers(id),
    event_type VARCHAR(50) NOT NULL, -- 'reviewed', 'approved', 'requested_changes', 'comment'
    occurred_at TIMESTAMPTZ NOT NULL,
    
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    FOREIGN KEY (merge_request_fact_id) REFERENCES merge_request_facts(id) ON DELETE CASCADE
) PARTITION BY RANGE (occurred_at);

CREATE INDEX idx_review_events_merge_request ON review_events(merge_request_fact_id);
CREATE INDEX idx_review_events_reviewer ON review_events(reviewer_developer_id);
CREATE INDEX idx_review_events_type ON review_events(event_type);
CREATE INDEX idx_review_events_occurred_at ON review_events(occurred_at);
```

### 8. Developer Metrics Aggregates (`developer_metrics_aggregates`)
**Purpose**: Pre-calculated aggregated metrics by time periods
```sql
CREATE TABLE developer_metrics_aggregates (
    id BIGSERIAL PRIMARY KEY,
    developer_id BIGINT NOT NULL REFERENCES developers(id),
    period_type VARCHAR(20) NOT NULL, -- 'daily', 'weekly', 'monthly', 'quarterly'
    period_start DATE NOT NULL,
    period_end DATE NOT NULL,
    
    -- Commit metrics
    commits_count INTEGER NOT NULL DEFAULT 0,
    lines_added INTEGER NOT NULL DEFAULT 0,
    lines_deleted INTEGER NOT NULL DEFAULT 0,
    files_changed INTEGER NOT NULL DEFAULT 0,
    
    -- MR metrics
    mrs_created INTEGER NOT NULL DEFAULT 0,
    mrs_merged INTEGER NOT NULL DEFAULT 0,
    mrs_reviewed INTEGER NOT NULL DEFAULT 0,
    avg_cycle_time_hours DECIMAL(10,2),
    
    -- Pipeline metrics
    pipelines_triggered INTEGER NOT NULL DEFAULT 0,
    successful_pipelines INTEGER NOT NULL DEFAULT 0,
    pipeline_success_rate DECIMAL(5,4),
    
    -- Collaboration metrics
    reviews_given INTEGER NOT NULL DEFAULT 0,
    unique_collaborators INTEGER NOT NULL DEFAULT 0,
    
    calculated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT uk_developer_metrics_period UNIQUE (developer_id, period_type, period_start)
) PARTITION BY RANGE (period_start);

CREATE INDEX idx_dev_metrics_agg_developer ON developer_metrics_aggregates(developer_id);
CREATE INDEX idx_dev_metrics_agg_period ON developer_metrics_aggregates(period_type, period_start);
```

## Tables to Remove

### Obsolete Tables (Per PRD)
1. `raw_issue` - Issues not in PRD scope
2. `fact_git_hygiene` - Git hygiene not in PRD scope  
3. `fact_release` / `dim_release` - Release tracking not in PRD scope
4. All issue-related columns from existing tables

## Partitioning Strategy

### Range Partitioning by Date
- `commit_facts` - partitioned by `committed_at` (monthly partitions)
- `merge_request_facts` - partitioned by `created_at` (monthly partitions)  
- `pipeline_facts` - partitioned by `created_at` (monthly partitions)
- `review_events` - partitioned by `occurred_at` (monthly partitions)
- `developer_metrics_aggregates` - partitioned by `period_start` (yearly partitions)

### Partition Management
```sql
-- Example: Create monthly partitions for commit_facts
CREATE TABLE commit_facts_2024_01 PARTITION OF commit_facts
    FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');

CREATE TABLE commit_facts_2024_02 PARTITION OF commit_facts
    FOR VALUES FROM ('2024-02-01') TO ('2024-03-01');
-- ... continue for other months
```

## Migration Strategy

### Phase 1: Create New Tables
1. Create all new PRD entities
2. Set up partitioning
3. Create indexes

### Phase 2: Data Migration
1. Migrate existing data to new schema
2. Update ETL processes to use new tables
3. Verify data integrity

### Phase 3: Remove Obsolete Tables  
1. Drop issue-related tables
2. Drop hygiene and release tables
3. Clean up old references

### Phase 4: Update Application
1. Update DbContext with new entities
2. Update services to use new schema
3. Update APIs if needed

## Performance Considerations

### Indexing Strategy
- Primary keys on all tables
- Foreign key indexes for joins
- Composite indexes for common query patterns
- Partial indexes for filtered queries

### Query Optimization
- Partition elimination for date-based queries  
- Covering indexes for read-heavy queries
- Materialized views for complex aggregations

This design focuses on the core GitLab metrics while providing better performance through partitioning and cleaner entity relationships aligned with the PRD requirements.