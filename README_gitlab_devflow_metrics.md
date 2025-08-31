# GitLab Dev Flow Metrics (Local CSV only)

This script queries your self-managed GitLab and produces:

- Per-project CSVs of merged MRs with metrics
- A portfolio `_summary.csv` with per-project rollups

## Configuration

Set these environment variables **in your user profile** (already assumed set):

- `TOMAN_GITLAB_API_URL`   → e.g., `https://gitlab.example.com`
- `TOMAN_GITLAB_API_TOKEN` → your personal access token with API scope

Optional runtime flags:

- `--group-path "parent/subgroup"` to restrict projects
- `--days 90` to change the lookback (default 90)

## Run

```powershell
python -m venv .venv
. .venv/Scripts/Activate.ps1
pip install requests
python gitlab_devflow_metrics.py --days 90
```

All outputs go to `outputs/`:

- `<namespace__project>.csv` (MR-level facts)
- `_summary.csv` (rollups)

## Metric definitions

- **MTTM**: from last “marked ready” system note (or creation if none) → merged.
- **TTFR**: creation → first non-author human comment.
- **Review rounds**: a reviewer comment followed by an author commit (counted per cycle).
- **PR size**: number of **files changed** (XS ≤3, S 4–10, M 11–25, L 26–50, XL >50).
