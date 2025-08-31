# GitLab CI Health Metrics (Local CSV only)

This script pulls CI health data from your self-hosted GitLab and writes local CSVs per project under `outputs/ci/`.

## Metrics per project

- **Success rate**: success / total pipelines
- **Pipeline duration**: mean, p50, p90 (seconds)
- **Pipeline queue time**: mean, p50, p90 (seconds), computed from job `queued_duration` (if present) or `started_at - created_at`
- **Job outcomes**: total/success/failed/canceled/skipped
- **Per-stage averages**: average job duration per stage (written to a separate `__stages.csv`)

## Configuration (environment)

Set in your user profile (assumed already set):

- `TOMAN_GITLAB_API_URL`   → e.g., `https://gitlab.example.com`
- `TOMAN_GITLAB_API_TOKEN` → PAT with API scope

## Run

```powershell
python -m venv .venv
. .venv/Scripts/Activate.ps1
pip install requests
python gitlab_ci_health_metrics.py --days 30            # all projects you have access to
python gitlab_ci_health_metrics.py --group-path toman   # scope to a group (includes subgroups)
```

## Output files (examples)

- `outputs/ci/acme__payments.csv` – pipeline-level facts (one row per pipeline)
- `outputs/ci/acme__payments__stages.csv` – per-stage average job duration
- `outputs/ci/_summary.csv` – per-project rollups

> Tip: In Grafana (or Excel), chart `duration_p90_sec` and `queue_p90_sec` over time by regenerating the CSVs daily and appending with a date column (simple cron wrapper).
