# Requests Folder

This folder contains HTTP request files (`.http`) for testing API endpoints using VS Code REST Client extension or similar tools.

## Active HTTP Request Files

- **`gitlab.http`** - GitLab API endpoints testing
- **`commit-time-analysis.http`** - Commit time analysis endpoint tests
- **`code-characteristics.http`** - Code characteristics endpoint tests
- **`quality-metrics.http`** - Quality metrics endpoint tests

## Usage

These files can be used with:
- **VS Code REST Client** extension
- **IntelliJ HTTP Client**
- Any tool that supports `.http` file format

## Archived Requests

Feature request markdown files (`000-*.md`, `001-*.md`, `002-*.md`) have been moved to `docs/archived/` as they described features that were either:
- Already implemented
- Never implemented (obsolete)
- Superseded by the current architecture

See [docs/archived/README.md](../docs/archived/README.md) for more information.

---

**For API endpoint documentation**, see:
- [docs/ENDPOINT_AUDIT.md](../docs/ENDPOINT_AUDIT.md) - Complete endpoint inventory
- [docs/API_USAGE_GUIDE.md](../docs/API_USAGE_GUIDE.md) - API usage examples
- [docs/CURRENT_STATE.md](../docs/CURRENT_STATE.md) - Current architecture
