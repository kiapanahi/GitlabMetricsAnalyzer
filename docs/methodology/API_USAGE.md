# Methodology API Usage Examples

This document provides practical examples for using the methodology API endpoints to support executive dashboard requirements.

## Basic API Usage

### Get Methodology for a Specific Metric

```bash
# Get detailed methodology for ProductivityScore
curl -X GET "https://your-api-domain/api/methodology/productivityscore" \
  -H "Accept: application/json"
```

**Response:**
```json
{
  "metricName": "ProductivityScore",
  "definition": "Composite score measuring developer output and efficiency across multiple dimensions",
  "calculation": "Step-by-step calculation:\n1. Calculate commits per day...",
  "dataSources": [
    {
      "source": "GitLab commit history",
      "type": "exact",
      "description": "Direct API access to commit data"
    }
  ],
  "limitations": [
    "Does not account for commit/MR complexity or size"
  ],
  "interpretation": {
    "ranges": {
      "0-2.9": "Below expectations - significant improvement needed",
      "7.5-10.0": "Exceeding expectations - excellent performance"
    },
    "notes": []
  },
  "industryContext": "Based on research from Google's DORA metrics...",
  "lastUpdated": "2024-12-01T00:00:00Z",
  "version": "2.1"
}
```

### Get Executive Summary

```bash
# Get high-level overview for executive briefings
curl -X GET "https://your-api-domain/api/methodology/executive-summary" \
  -H "Accept: application/json"
```

**Response:**
```json
{
  "lastUpdated": "2024-12-01T10:00:00Z",
  "totalMetrics": 4,
  "keyLimitations": [
    "Metrics reflect GitLab activity only, not overall developer impact"
  ],
  "dataFreshness": "Metrics calculated from real-time GitLab API data",
  "contactInformation": "For methodology questions, contact VP Engineering",
  "metrics": [
    {
      "name": "ProductivityScore",
      "definition": "Composite score measuring developer output and efficiency...",
      "keyLimitations": ["Does not account for commit/MR complexity"],
      "version": "2.1"
    }
  ]
}
```

### Get Footnote for UI Display

```bash
# Get concise footnote text for dashboard metric display
curl -X GET "https://your-api-domain/api/methodology/footnote/productivityscore" \
  -H "Accept: application/json"
```

**Response:**
```json
{
  "metricName": "productivityscore",
  "footnote": "Calculated using weighted algorithm combining commit frequency, MR throughput, and pipeline success rate",
  "methodologyLink": "/api/methodology/productivityscore"
}
```

## Frontend Integration Examples

### React Component with Methodology Support

```tsx
interface MetricCardProps {
  value: number;
  title: string;
  metricName: string;
}

function MetricCard({ value, title, metricName }: MetricCardProps) {
  const [footnote, setFootnote] = useState<string>('');
  const [methodologyLink, setMethodologyLink] = useState<string>('');

  useEffect(() => {
    fetch(`/api/methodology/footnote/${metricName.toLowerCase()}`)
      .then(res => res.json())
      .then(data => {
        setFootnote(data.footnote);
        setMethodologyLink(data.methodologyLink);
      });
  }, [metricName]);

  return (
    <div className="metric-card">
      <h3>{title}</h3>
      <div className="metric-value">{value}</div>
      <div className="methodology-footnote">
        <small>{footnote}</small>
        <a href={methodologyLink} target="_blank" className="methodology-link">
          View Methodology
        </a>
      </div>
    </div>
  );
}

// Usage
<MetricCard 
  value={8.2} 
  title="Productivity Score" 
  metricName="ProductivityScore" 
/>
```

### Executive Dashboard Hook

```typescript
// Custom hook for executive methodology data
export function useExecutiveMethodology() {
  const [summary, setSummary] = useState<ExecutiveSummaryResponse | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchSummary = async () => {
      try {
        const response = await fetch('/api/methodology/executive-summary');
        const data = await response.json();
        setSummary(data);
      } catch (error) {
        console.error('Failed to load methodology summary:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchSummary();
  }, []);

  return { summary, loading };
}

// Usage in executive dashboard
function ExecutiveDashboard() {
  const { summary, loading } = useExecutiveMethodology();

  if (loading) return <LoadingSpinner />;

  return (
    <div className="executive-dashboard">
      <h1>Developer Productivity Metrics</h1>
      
      <div className="methodology-overview">
        <h2>Methodology Overview</h2>
        <p>Last Updated: {summary?.lastUpdated}</p>
        <p>Total Metrics: {summary?.totalMetrics}</p>
        
        <div className="key-limitations">
          <h3>Key Limitations</h3>
          <ul>
            {summary?.keyLimitations.map((limitation, index) => (
              <li key={index}>{limitation}</li>
            ))}
          </ul>
        </div>
      </div>

      {/* Metric cards with methodology integration */}
      <div className="metrics-grid">
        {/* Your metric cards here */}
      </div>
    </div>
  );
}
```

## Search Functionality

```bash
# Search for metrics related to "pipeline"
curl -X GET "https://your-api-domain/api/methodology/search?q=pipeline" \
  -H "Accept: application/json"
```

**Use Cases:**
- Help executives find specific metric information quickly
- Support dashboard search functionality
- Enable contextual help systems

## Change Log and Audit Trail

### Get Change History

```bash
# Get all methodology changes
curl -X GET "https://your-api-domain/api/methodology/changelog" \
  -H "Accept: application/json"

# Get changes for specific metric
curl -X GET "https://your-api-domain/api/methodology/changelog/productivityscore" \
  -H "Accept: application/json"
```

### Get Audit Trail

```bash
# Get audit trail for compliance
curl -X GET "https://your-api-domain/api/methodology/audit-trail" \
  -H "Accept: application/json"

# Get audit trail for specific metric in date range
curl -X GET "https://your-api-domain/api/methodology/audit-trail?metricName=productivityscore&fromDate=2024-01-01T00:00:00Z&toDate=2024-12-31T23:59:59Z" \
  -H "Accept: application/json"
```

## Board Presentation Support

### Generate Methodology Report

```typescript
// Function to generate comprehensive methodology report for board meetings
async function generateMethodologyReport() {
  const [allMethodologies, changeLog, summary] = await Promise.all([
    fetch('/api/methodology/').then(res => res.json()),
    fetch('/api/methodology/changelog').then(res => res.json()),
    fetch('/api/methodology/executive-summary').then(res => res.json())
  ]);

  return {
    summary,
    methodologies: allMethodologies,
    recentChanges: changeLog.slice(0, 5), // Last 5 changes
    auditCompliance: true,
    generatedAt: new Date().toISOString()
  };
}
```

### PDF Export Support

```typescript
// Generate PDF-ready methodology documentation
async function exportMethodologyToPDF(metricName: string) {
  const methodology = await fetch(`/api/methodology/${metricName}`)
    .then(res => res.json());
  
  // Use your PDF generation library (e.g., jsPDF, Puppeteer)
  const pdfContent = {
    title: `${methodology.metricName} Methodology`,
    sections: [
      { heading: 'Definition', content: methodology.definition },
      { heading: 'Calculation', content: methodology.calculation },
      { heading: 'Data Sources', content: methodology.dataSources },
      { heading: 'Limitations', content: methodology.limitations },
      { heading: 'Interpretation', content: methodology.interpretation }
    ],
    footer: `Version ${methodology.version} | Last Updated: ${methodology.lastUpdated}`
  };

  return generatePDF(pdfContent);
}
```

## Performance Optimization

### Caching Strategy

```typescript
// Cache methodology data for better performance
const methodologyCache = new Map<string, MethodologyInfo>();
const CACHE_DURATION = 5 * 60 * 1000; // 5 minutes

async function getCachedMethodology(metricName: string): Promise<MethodologyInfo> {
  const cacheKey = `methodology-${metricName}`;
  const cached = methodologyCache.get(cacheKey);
  
  if (cached && Date.now() - cached.cachedAt < CACHE_DURATION) {
    return cached;
  }

  const methodology = await fetch(`/api/methodology/${metricName}`)
    .then(res => res.json());
  
  methodology.cachedAt = Date.now();
  methodologyCache.set(cacheKey, methodology);
  
  return methodology;
}
```

## Error Handling

```typescript
// Robust error handling for methodology API calls
async function safeGetMethodology(metricName: string) {
  try {
    const response = await fetch(`/api/methodology/${metricName}`);
    
    if (!response.ok) {
      if (response.status === 404) {
        return {
          error: 'Methodology not found',
          fallback: 'See general methodology documentation'
        };
      }
      throw new Error(`HTTP ${response.status}`);
    }
    
    return await response.json();
  } catch (error) {
    console.error('Methodology API error:', error);
    return {
      error: 'Unable to load methodology',
      fallback: 'Contact engineering team for methodology details'
    };
  }
}
```

## Integration Checklist

- [ ] Add methodology footnotes to all metric displays
- [ ] Implement search functionality for quick methodology lookup
- [ ] Create executive summary dashboard section
- [ ] Add methodology links to metric hover states
- [ ] Implement PDF export for board presentations
- [ ] Set up caching for methodology data
- [ ] Add error handling with graceful fallbacks
- [ ] Create mobile-friendly methodology views
- [ ] Implement methodology change notifications
- [ ] Add audit trail reporting for compliance

This comprehensive API enables executives to confidently explain, defend, and trust the metrics presented in the dashboard while maintaining full audit compliance and transparency.