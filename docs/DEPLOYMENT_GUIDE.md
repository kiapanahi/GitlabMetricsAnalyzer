# Deployment Guide

This guide provides comprehensive deployment instructions for the GitLab Metrics Analyzer system across different environments and platforms.

## Table of Contents
- [Deployment Overview](#deployment-overview)
- [Prerequisites](#prerequisites)
- [Local Development Deployment](#local-development-deployment)
- [Docker Deployment](#docker-deployment)
- [Kubernetes Deployment](#kubernetes-deployment)
- [Production Deployment](#production-deployment)
- [Database Setup](#database-setup)
- [Security Hardening](#security-hardening)
- [Monitoring Setup](#monitoring-setup)
- [Backup and Recovery](#backup-and-recovery)
- [Troubleshooting](#troubleshooting)

## Deployment Overview

The GitLab Metrics Analyzer v1 system consists of:
- **.NET 9 Web API**: Main application with REST endpoints
- **PostgreSQL Database**: Time-series data storage with partitioning
- **.NET Aspire**: Local development orchestration (optional)
- **Manual Trigger System**: No automatic scheduling in v1

### Architecture Components
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Load Balancer │    │   GitLab API    │    │    Monitoring   │
│    (Optional)   │    │                 │    │   (Prometheus)  │
└─────────┬───────┘    └─────────┬───────┘    └─────────────────┘
          │                      │                      │
┌─────────▼────────────────────────▼──────────────────────▼─────┐
│                GitLab Metrics Analyzer                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐   │
│  │   Web API   │  │  Background │  │    Data Export      │   │
│  │ (REST/HTTP) │  │  Collection │  │   (JSON/CSV/Excel)  │   │
│  └─────────────┘  └─────────────┘  └─────────────────────┘   │
└───────────────────────────┬───────────────────────────────────┘
                            │
                   ┌────────▼────────┐
                   │   PostgreSQL    │
                   │   (Partitioned) │
                   └─────────────────┘
```

## Prerequisites

### System Requirements
- **Operating System**: Linux (Ubuntu 20.04+), Windows Server 2019+, or macOS 12+
- **Runtime**: .NET 9 Runtime/SDK
- **Database**: PostgreSQL 13+ with partitioning support
- **Memory**: Minimum 2GB RAM, recommended 4GB+
- **Storage**: 10GB+ for application, additional storage for database
- **Network**: HTTP/HTTPS access to GitLab instance

### GitLab Requirements
- **GitLab Version**: 13.0+ (API v4 support)
- **Personal Access Token**: With `api` scope
- **Network Access**: API endpoints accessible from deployment environment
- **Rate Limits**: Understanding of GitLab API rate limiting

## Local Development Deployment

### Using .NET Aspire (Recommended)

1. **Install Prerequisites**:
   ```bash
   # Install .NET 9 SDK
   curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version latest
   
   # Install Aspire workload
   dotnet workload install aspire
   ```

2. **Clone and Configure**:
   ```bash
   git clone https://github.com/kiapanahi/GitlabMetricsAnalyzer.git
   cd GitlabMetricsAnalyzer
   
   # Configure GitLab connection
   export GitLab__Token="glpat-your-token-here"
   export GitLab__BaseUrl="https://your-gitlab-instance.com/"
   ```

3. **Start Development Environment**:
   ```bash
   # Start with Aspire (includes database)
   aspire run
   
   # Application will be available at:
   # - API: http://localhost:5000
   # - Aspire Dashboard: http://localhost:15000
   ```

### Manual Development Setup

1. **Setup PostgreSQL**:
   ```bash
   # Using Docker
   docker run --name postgres-dev \
     -e POSTGRES_DB=GitLabMetrics \
     -e POSTGRES_USER=dev_user \
     -e POSTGRES_PASSWORD=dev_password \
     -p 5432:5432 -d postgres:15
   ```

2. **Configure Connection**:
   ```json
   // appsettings.Development.json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=GitLabMetrics;Username=dev_user;Password=dev_password"
     },
     "GitLab": {
       "BaseUrl": "https://your-gitlab-instance.com/",
       "Token": "glpat-your-token-here"
     }
   }
   ```

3. **Run Application**:
   ```bash
   cd src/Toman.Management.KPIAnalysis.ApiService
   dotnet run
   ```

## Docker Deployment

### Single Container Deployment

1. **Create Dockerfile** (already included):
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
   WORKDIR /app
   EXPOSE 8080
   
   FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
   WORKDIR /src
   COPY . .
   RUN dotnet restore
   RUN dotnet build -c Release -o /app/build
   
   FROM build AS publish
   RUN dotnet publish -c Release -o /app/publish
   
   FROM base AS final
   WORKDIR /app
   COPY --from=publish /app/publish .
   ENTRYPOINT ["dotnet", "Toman.Management.KPIAnalysis.ApiService.dll"]
   ```

2. **Build Image**:
   ```bash
   docker build -t gitlab-metrics-analyzer:latest .
   ```

3. **Run Container**:
   ```bash
   docker run -d \
     --name gitlab-metrics \
     -p 5000:8080 \
     -e GitLab__Token="glpat-your-token" \
     -e GitLab__BaseUrl="https://your-gitlab.com/" \
     -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Database=GitLabMetrics;Username=user;Password=pass" \
     -v /data/exports:/data/exports \
     gitlab-metrics-analyzer:latest
   ```

### Docker Compose Deployment

Create `docker-compose.yml`:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15
    container_name: gitlab-metrics-postgres
    environment:
      POSTGRES_DB: GitLabMetrics
      POSTGRES_USER: metrics_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./scripts/init-db.sql:/docker-entrypoint-initdb.d/init-db.sql
    ports:
      - "5432:5432"
    networks:
      - gitlab-metrics

  app:
    build: .
    container_name: gitlab-metrics-app
    depends_on:
      - postgres
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=GitLabMetrics;Username=metrics_user;Password=${POSTGRES_PASSWORD}"
      GitLab__Token: ${GITLAB_TOKEN}
      GitLab__BaseUrl: ${GITLAB_BASE_URL}
      Exports__Directory: /data/exports
      ASPNETCORE_ENVIRONMENT: Production
    volumes:
      - ./exports:/data/exports
      - ./logs:/app/logs
    ports:
      - "5000:8080"
    networks:
      - gitlab-metrics
    restart: unless-stopped

volumes:
  postgres_data:

networks:
  gitlab-metrics:
    driver: bridge
```

Create `.env` file:
```env
POSTGRES_PASSWORD=secure_password_here
GITLAB_TOKEN=glpat-your-token-here
GITLAB_BASE_URL=https://your-gitlab-instance.com/
```

Deploy:
```bash
docker-compose up -d
```

## Kubernetes Deployment

### Namespace and ConfigMap

```yaml
# namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: gitlab-metrics
---
# configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: gitlab-metrics-config
  namespace: gitlab-metrics
data:
  appsettings.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "AllowedHosts": "*",
      "Processing": {
        "MaxDegreeOfParallelism": 4,
        "BackfillDays": 180
      },
      "Exports": {
        "Directory": "/data/exports"
      }
    }
```

### Secrets

```yaml
# secrets.yaml
apiVersion: v1
kind: Secret
metadata:
  name: gitlab-metrics-secrets
  namespace: gitlab-metrics
type: Opaque
data:
  gitlab-token: Z2xwYXQteW91ci10b2tlbi1oZXJl  # base64 encoded
  db-password: c2VjdXJlX3Bhc3N3b3Jk  # base64 encoded
```

### PostgreSQL Deployment

```yaml
# postgres-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgres
  namespace: gitlab-metrics
spec:
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
      - name: postgres
        image: postgres:15
        env:
        - name: POSTGRES_DB
          value: GitLabMetrics
        - name: POSTGRES_USER
          value: metrics_user
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: gitlab-metrics-secrets
              key: db-password
        ports:
        - containerPort: 5432
        volumeMounts:
        - name: postgres-storage
          mountPath: /var/lib/postgresql/data
      volumes:
      - name: postgres-storage
        persistentVolumeClaim:
          claimName: postgres-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
  namespace: gitlab-metrics
spec:
  selector:
    app: postgres
  ports:
  - port: 5432
    targetPort: 5432
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-pvc
  namespace: gitlab-metrics
spec:
  accessModes:
  - ReadWriteOnce
  resources:
    requests:
      storage: 100Gi
```

### Application Deployment

```yaml
# app-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: gitlab-metrics-app
  namespace: gitlab-metrics
spec:
  replicas: 2
  selector:
    matchLabels:
      app: gitlab-metrics-app
  template:
    metadata:
      labels:
        app: gitlab-metrics-app
    spec:
      containers:
      - name: app
        image: gitlab-metrics-analyzer:latest
        env:
        - name: ConnectionStrings__DefaultConnection
          value: "Host=postgres;Database=GitLabMetrics;Username=metrics_user;Password=$(DB_PASSWORD)"
        - name: GitLab__Token
          valueFrom:
            secretKeyRef:
              name: gitlab-metrics-secrets
              key: gitlab-token
        - name: GitLab__BaseUrl
          value: "https://your-gitlab-instance.com/"
        - name: DB_PASSWORD
          valueFrom:
            secretKeyRef:
              name: gitlab-metrics-secrets
              key: db-password
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        ports:
        - containerPort: 8080
        livenessProbe:
          httpGet:
            path: /alive
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 15
          periodSeconds: 10
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        volumeMounts:
        - name: config-volume
          mountPath: /app/appsettings.json
          subPath: appsettings.json
        - name: exports-volume
          mountPath: /data/exports
      volumes:
      - name: config-volume
        configMap:
          name: gitlab-metrics-config
      - name: exports-volume
        persistentVolumeClaim:
          claimName: exports-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: gitlab-metrics-service
  namespace: gitlab-metrics
spec:
  selector:
    app: gitlab-metrics-app
  ports:
  - port: 80
    targetPort: 8080
  type: ClusterIP
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: exports-pvc
  namespace: gitlab-metrics
spec:
  accessModes:
  - ReadWriteMany
  resources:
    requests:
      storage: 50Gi
```

### Ingress Configuration

```yaml
# ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: gitlab-metrics-ingress
  namespace: gitlab-metrics
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  tls:
  - hosts:
    - gitlab-metrics.company.com
    secretName: gitlab-metrics-tls
  rules:
  - host: gitlab-metrics.company.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: gitlab-metrics-service
            port:
              number: 80
```

Deploy to Kubernetes:
```bash
kubectl apply -f namespace.yaml
kubectl apply -f configmap.yaml
kubectl apply -f secrets.yaml
kubectl apply -f postgres-deployment.yaml
kubectl apply -f app-deployment.yaml
kubectl apply -f ingress.yaml
```

## Production Deployment

### Production Checklist

#### Infrastructure
- [ ] Load balancer configured with health checks
- [ ] SSL/TLS certificates installed
- [ ] Database backup strategy implemented
- [ ] Monitoring and alerting configured
- [ ] Log aggregation setup
- [ ] Security scanning completed

#### Configuration
- [ ] Production connection strings configured
- [ ] Secrets properly managed (Azure Key Vault, AWS Secrets Manager)
- [ ] Resource limits configured
- [ ] Performance tuning applied
- [ ] Error handling configured
- [ ] CORS policies set

#### Security
- [ ] Network security groups configured
- [ ] Database access restricted
- [ ] API authentication implemented (if required)
- [ ] Security headers configured
- [ ] Vulnerability scanning completed

### Production Configuration Example

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Toman.Management.KPIAnalysis": "Information"
    }
  },
  "AllowedHosts": "gitlab-metrics.company.com",
  "GitLab": {
    "BaseUrl": "https://gitlab.company.com/",
    "TimeoutSeconds": 60,
    "RetryCount": 5,
    "RateLimitPerSecond": 5
  },
  "Processing": {
    "MaxDegreeOfParallelism": 8,
    "BackfillDays": 365,
    "BatchSize": 200,
    "MemoryLimitMB": 4096
  },
  "Exports": {
    "Directory": "/data/exports",
    "RetentionDays": 90,
    "MaxFileSizeMB": 500
  },
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:443"
      }
    }
  }
}
```

## Database Setup

### Initial Database Configuration

```sql
-- Create database and user
CREATE DATABASE GitLabMetrics;
CREATE USER metrics_prod WITH PASSWORD 'secure_production_password';
GRANT ALL PRIVILEGES ON DATABASE GitLabMetrics TO metrics_prod;

-- Connect to database
\c GitLabMetrics

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";

-- Create partitioning function
CREATE OR REPLACE FUNCTION create_monthly_partition(table_name text, start_date date)
RETURNS void AS $$
DECLARE
    partition_name text;
    end_date date;
BEGIN
    partition_name := table_name || '_' || to_char(start_date, 'YYYY_MM');
    end_date := start_date + interval '1 month';
    
    EXECUTE format('CREATE TABLE IF NOT EXISTS %I PARTITION OF %I
                    FOR VALUES FROM (%L) TO (%L)',
                   partition_name, table_name, start_date, end_date);
END;
$$ LANGUAGE plpgsql;
```

### Automated Partition Management

Create monthly partitions script:

```bash
#!/bin/bash
# create-partitions.sh - Create future partitions

DB_HOST="localhost"
DB_NAME="GitLabMetrics"
DB_USER="metrics_prod"

# Create partitions for next 6 months
for i in {0..5}; do
    MONTH_START=$(date -d "$(date +'%Y-%m-01') + ${i} months" +'%Y-%m-01')
    
    psql -h $DB_HOST -d $DB_NAME -U $DB_USER -c \
        "SELECT create_monthly_partition('commit_facts', '$MONTH_START');"
    psql -h $DB_HOST -d $DB_NAME -U $DB_USER -c \
        "SELECT create_monthly_partition('merge_request_facts', '$MONTH_START');"
    psql -h $DB_HOST -d $DB_NAME -U $DB_USER -c \
        "SELECT create_monthly_partition('pipeline_facts', '$MONTH_START');"
done
```

Add to crontab:
```bash
# Run monthly partition creation on the 15th of each month
0 2 15 * * /path/to/create-partitions.sh
```

## Security Hardening

### Network Security

```yaml
# network-policy.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: gitlab-metrics-network-policy
  namespace: gitlab-metrics
spec:
  podSelector:
    matchLabels:
      app: gitlab-metrics-app
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    ports:
    - protocol: TCP
      port: 8080
  egress:
  - to:
    - podSelector:
        matchLabels:
          app: postgres
    ports:
    - protocol: TCP
      port: 5432
  - to: []  # Allow GitLab API access
    ports:
    - protocol: TCP
      port: 443
```

### Security Headers Configuration

```csharp
// In Program.cs or Startup.cs
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'");
    
    await next();
});
```

## Monitoring Setup

### Health Check Endpoints

The application includes built-in health checks:
- `/health` - Application readiness
- `/alive` - Application liveness

### Prometheus Metrics Integration

Add to `Program.cs`:
```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddPrometheusExporter();
    });
```

### Monitoring Configuration

```yaml
# monitoring.yaml
apiVersion: v1
kind: ServiceMonitor
metadata:
  name: gitlab-metrics-monitor
  namespace: gitlab-metrics
spec:
  selector:
    matchLabels:
      app: gitlab-metrics-app
  endpoints:
  - port: http
    path: /metrics
```

### Grafana Dashboard

Create dashboard with key metrics:
- Collection run success rate
- API response times
- Database query performance
- Memory and CPU usage
- Data quality scores

## Backup and Recovery

### Database Backup Strategy

```bash
#!/bin/bash
# backup-database.sh - Daily database backup

BACKUP_DIR="/backups/gitlab-metrics"
DATE=$(date +%Y%m%d_%H%M%S)
DB_HOST="localhost"
DB_NAME="GitLabMetrics"
DB_USER="metrics_prod"

# Create backup directory
mkdir -p $BACKUP_DIR

# Perform backup
pg_dump -h $DB_HOST -U $DB_USER -d $DB_NAME \
    --verbose --format=custom \
    --file="$BACKUP_DIR/gitlab_metrics_$DATE.dump"

# Compress backup
gzip "$BACKUP_DIR/gitlab_metrics_$DATE.dump"

# Clean old backups (keep 30 days)
find $BACKUP_DIR -name "*.dump.gz" -mtime +30 -delete

echo "Backup completed: gitlab_metrics_$DATE.dump.gz"
```

### Configuration Backup

```bash
#!/bin/bash
# backup-config.sh - Backup configuration files

CONFIG_BACKUP_DIR="/backups/config"
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p $CONFIG_BACKUP_DIR

# Backup Kubernetes manifests
kubectl get all,ingress,secrets,configmaps -n gitlab-metrics -o yaml > \
    "$CONFIG_BACKUP_DIR/k8s-config-$DATE.yaml"

# Backup Docker Compose files
tar -czf "$CONFIG_BACKUP_DIR/docker-config-$DATE.tar.gz" \
    docker-compose.yml .env

echo "Configuration backup completed"
```

### Recovery Procedures

#### Database Recovery
```bash
# Stop application
kubectl scale deployment gitlab-metrics-app --replicas=0 -n gitlab-metrics

# Restore database
pg_restore -h localhost -U metrics_prod -d GitLabMetrics \
    --verbose --clean --if-exists \
    /backups/gitlab-metrics/gitlab_metrics_20240115_120000.dump.gz

# Restart application
kubectl scale deployment gitlab-metrics-app --replicas=2 -n gitlab-metrics
```

## Troubleshooting

### Common Issues

#### Application Won't Start

**Symptoms**:
- Container exits immediately
- Health checks fail
- Connection errors in logs

**Diagnosis**:
```bash
# Check container logs
docker logs gitlab-metrics-app

# Check Kubernetes logs
kubectl logs -f deployment/gitlab-metrics-app -n gitlab-metrics

# Check configuration
kubectl describe configmap gitlab-metrics-config -n gitlab-metrics
```

**Resolution**:
1. Verify database connectivity
2. Check GitLab token validity
3. Validate configuration format
4. Ensure required directories exist

#### Performance Issues

**Symptoms**:
- Slow API responses
- High memory usage
- Database connection timeouts

**Diagnosis**:
```bash
# Check resource usage
kubectl top pods -n gitlab-metrics

# Check database performance
SELECT query, mean_exec_time, calls 
FROM pg_stat_statements 
ORDER BY mean_exec_time DESC 
LIMIT 10;

# Check application metrics
curl http://localhost:5000/metrics
```

**Resolution**:
1. Increase resource limits
2. Optimize database queries
3. Tune garbage collection settings
4. Review processing configuration

#### Collection Failures

**Symptoms**:
- Collections fail with API errors
- Partial data collection
- Rate limiting errors

**Diagnosis**:
```bash
# Check collection runs
curl "http://localhost:5000/gitlab-metrics/collect/runs?limit=10"

# Review GitLab API response headers
curl -I "https://gitlab.company.com/api/v4/projects"
```

**Resolution**:
1. Check GitLab API rate limits
2. Verify token permissions
3. Adjust parallelism settings
4. Implement exponential backoff

### Emergency Contacts

- **DevOps Team**: devops@company.com
- **Database Admin**: dba@company.com  
- **Security Team**: security@company.com
- **On-call Engineer**: +1-555-0123

### Recovery Time Objectives (RTO)

- **Critical Issues**: 4 hours
- **Data Loss**: 24 hours recovery
- **Planned Maintenance**: 2 hours downtime window

This deployment guide should be updated as the system evolves and new deployment patterns are established.