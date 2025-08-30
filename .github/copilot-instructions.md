You are building a tool to measure engineering delivery speed metrics using GitLab data. 
The tool will query the GitLab API and calculate metrics that the VP of Engineering can use to track delivery performance. 

## Context
- Source of truth: GitLab repositories, merge requests, commits, and pipelines
- Programming language: Python
- Stack: pandas for processing, and optional ClickHouse/Postgres for storage

## Required Features
1. **Pipeline Metrics**
   - Pipeline Success Rate (successful / total pipelines)
   - Mean Time to Green (time from failed pipeline on default branch → next green)
   - Average Pipeline Duration
   - Broken Main Minutes (accumulated time main branch was red)

2. **Delivery Flow Metrics**
   - Deployment Frequency (count of successful deploy pipelines per service per week)
   - Lead Time for Change (first commit timestamp in MR → successful deploy pipeline timestamp)
   - Cycle Time (MR open → merge), with optional breakdown:
     * Review Pickup Time (open → first comment/review)
     * Review Active Time (first review → merge)
   - Throughput (count of merged MRs per week)
   - Batch Size (lines of code changed per MR)

3. **Stability Metrics**
   - Rollback/Revert Rate (count of reverted MRs or rollback pipelines)

## Architecture
- Config file for:
  * GitLab API token (in `.env` file)
  * Group/project IDs to monitor
  * Branch naming (main/master) and deploy stage patterns
- Collector module to call GitLab REST API (projects, pipelines, merge requests, commits)
- Transformer module to calculate metrics
- Storage module (initially CSV/JSON, later pluggable DB)
- Unit tests with mocked GitLab API responses

## Coding style

- Always make sure theres a python virtual environment activated at `.venv`
- For environement variable use the .env file
- Assume your scripts are running on windows and powershell.
- Inject environment variable powershell style

Start by scaffolding the project structure with clear TODOs, then progressively implement the GitLab API clients and metric calculations.