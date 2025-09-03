# Azure Cost Optimization Summary

## Executive Summary

This document outlines cost optimization strategies for the Intune Enrollment Notifier Azure Function application, with a focus on reducing operational costs while maintaining or improving functionality.

## Current Architecture Costs

### Monthly Cost Breakdown (Estimated)

| Resource | Current Cost | Optimized Cost | Savings |
|----------|-------------|----------------|---------|
| SendGrid Email Service | $0-$100 | $0 | $0-$100 |
| Azure Function (Consumption) | $5 | $2 | $3 |
| Azure Storage | $2 | $2 | $0 |
| Application Insights | $5 | $3 | $2 |
| Key Vault | $1 | $1 | $0 |
| **Total** | **$13-$113** | **$8** | **$5-$105** |

## Optimization Strategies

### 1. Replace SendGrid with Microsoft Teams

**Current State:**
- Using SendGrid for email notifications
- Cost varies by email volume ($0-$100/month)
- External dependency on third-party service

**Optimized State:**
- Use Microsoft Teams channel notifications
- Leverages existing Microsoft 365 licenses
- No additional cost
- Better integration with existing workflows

**Implementation:**
- ✅ Created `TeamsNotificationService.cs`
- ✅ Integrated with Microsoft Graph API
- ✅ Configurable notification type (Teams/Email/Both)
- ✅ Adaptive Cards for rich formatting

**Savings: $0-$100/month**

### 2. Optimize Function Timeout

**Current State:**
- Function timeout: 5 minutes
- Most functions complete in < 10 seconds
- Paying for unused compute time

**Optimized State:**
- Reduce timeout to 1-2 minutes
- Matches actual execution time
- Reduces compute costs

**Implementation:**
Update `host.json`:
```json
{
  "functionTimeout": "00:01:00"
}
```

**Savings: ~$3/month (60% reduction in compute costs)**

### 3. Application Insights Optimization

**Current State:**
- Full telemetry collection
- May include unnecessary data
- Sampling enabled but not optimized

**Optimized State:**
- Adjust sampling rate
- Exclude low-value telemetry
- Keep critical metrics

**Implementation:**
Update `host.json`:
```json
{
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "maxTelemetryItemsPerSecond": 5,
        "excludedTypes": "Request;Exception"
      }
    }
  }
}
```

**Savings: ~$2/month**

### 4. Storage Account Optimization

**Current State:**
- Using development storage setting in config
- Production should use Azure Storage

**Optimized State:**
- Use Standard LRS (Locally Redundant Storage)
- Enable lifecycle management
- Archive old logs after 30 days

**Implementation:**
- Update `AzureWebJobsStorage` to production storage account
- Configure lifecycle management policy
- Use cool tier for infrequent access

**Savings: Minimal, but improves reliability**

### 5. Retry Policy Optimization

**Current State:**
- Exponential backoff with 3 retries
- May cause unnecessary executions

**Optimized State:**
- Reduce max retry count to 2
- Adjust intervals based on actual needs

**Implementation:**
Update `host.json`:
```json
{
  "retry": {
    "strategy": "exponentialBackoff",
    "maxRetryCount": 2,
    "minimumInterval": "00:00:02",
    "maximumInterval": "00:00:15"
  }
}
```

**Savings: ~$0.50/month**

## Azure Resource Recommendations

### Function App Hosting Plan

**Current: Consumption Plan** ✅ (Optimal)
- Pay only for execution time
- Automatic scaling
- First 1M executions free
- $0.20 per million executions

**Keep Consumption Plan** - Most cost-effective for event-driven workloads

### Storage Account

**Recommendation: Standard LRS**
- Locally redundant storage
- Sufficient for function storage
- Cost: ~$0.02/GB/month

**Lifecycle Management:**
```json
{
  "rules": [
    {
      "name": "archiveOldLogs",
      "type": "Lifecycle",
      "definition": {
        "actions": {
          "baseBlob": {
            "tierToCool": {
              "daysAfterModificationGreaterThan": 30
            },
            "delete": {
              "daysAfterModificationGreaterThan": 90
            }
          }
        }
      }
    }
  ]
}
```

### Application Insights

**Recommendation: Basic Tier**
- 5 GB/month included free
- $2.30/GB after
- Typical usage: 1-2 GB/month

**Optimization:**
- Use sampling (already configured)
- Filter out verbose logs
- Focus on errors and critical events

### Key Vault

**Current: Standard Tier** ✅ (Optimal)
- $0.03 per 10,000 operations
- Minimal cost for this workload
- Essential for security

**Keep Standard Tier** - Cost is negligible

## Security & Cost Balance

### Secrets Management

**Current State:**
- SendGrid API key hardcoded in `azure-resources.parameters.json`
- Security risk and potential cost if exposed

**Optimized State:**
- All secrets in Azure Key Vault
- Use Managed Identity for access
- No secrets in code or config files

**Implementation:**
1. Store secrets in Key Vault:
   - `GRAPH-API-CLIENT-ID`
   - `GRAPH-API-TENANT-ID`
   - `GRAPH-API-CLIENT-SECRET`
   - `SendGridApiKey` (if still using email)

2. Update function to retrieve from Key Vault (already implemented)

3. Remove hardcoded secrets from config files

**Benefit:** Prevents potential security breaches that could lead to unexpected costs

## Monitoring & Cost Management

### Cost Alerts

Set up Azure Cost Management alerts:

1. **Budget Alert**: $20/month threshold
2. **Anomaly Alert**: Detect unusual spending
3. **Forecast Alert**: Projected overspend

### Key Metrics to Monitor

**Cost Metrics:**
- Daily spend by resource
- Function execution count
- Storage usage
- Application Insights data volume

**Performance Metrics:**
- Function execution time
- Success/failure rate
- API call latency

### Cost Management Queries

**Monthly Cost by Resource:**
```kusto
AzureDiagnostics
| where TimeGenerated > ago(30d)
| summarize Cost = sum(todouble(Cost)) by ResourceType
| order by Cost desc
```

**Function Execution Cost:**
```kusto
FunctionAppLogs
| where TimeGenerated > ago(30d)
| summarize 
    ExecutionCount = count(),
    AvgDuration = avg(DurationMs),
    TotalDuration = sum(DurationMs)
| extend EstimatedCost = (TotalDuration / 1000 / 3600) * 0.20
```

## Implementation Roadmap

### Phase 1: Immediate (Week 1)
- ✅ Implement Teams integration
- ✅ Update configuration files
- ✅ Add notification type selector
- [ ] Test Teams notifications
- [ ] Update Azure Function settings

### Phase 2: Transition (Week 2)
- [ ] Set notification type to "Both"
- [ ] Monitor both channels
- [ ] Verify Teams reliability
- [ ] Document any issues

### Phase 3: Optimization (Week 3)
- [ ] Switch to Teams-only
- [ ] Reduce function timeout
- [ ] Optimize Application Insights
- [ ] Update retry policies

### Phase 4: Cleanup (Week 4)
- [ ] Remove SendGrid dependency (optional)
- [ ] Cancel SendGrid subscription
- [ ] Archive old email templates
- [ ] Update documentation

## Cost Comparison Scenarios

### Scenario 1: Low Volume (10 enrollments/day)

**Before:**
- SendGrid: $0 (free tier)
- Azure: $13/month
- **Total: $13/month**

**After:**
- Teams: $0
- Azure: $8/month
- **Total: $8/month**
- **Savings: $5/month (38%)**

### Scenario 2: Medium Volume (100 enrollments/day)

**Before:**
- SendGrid: $19.95/month
- Azure: $13/month
- **Total: $32.95/month**

**After:**
- Teams: $0
- Azure: $8/month
- **Total: $8/month**
- **Savings: $24.95/month (76%)**

### Scenario 3: High Volume (500 enrollments/day)

**Before:**
- SendGrid: $79.95/month
- Azure: $20/month
- **Total: $99.95/month**

**After:**
- Teams: $0
- Azure: $10/month
- **Total: $10/month**
- **Savings: $89.95/month (90%)**

## Additional Cost Optimization Tips

### 1. Use Managed Identity
- Eliminates need for connection strings
- Reduces Key Vault operations
- More secure

### 2. Optimize Timer Trigger
- Current: Every 5 minutes
- Consider: Every 10-15 minutes if acceptable
- Reduces function executions by 50-66%

### 3. Batch Processing
- Process multiple events in single execution
- Reduces cold starts
- More efficient resource usage

### 4. Regional Deployment
- Deploy in same region as other resources
- Reduces data transfer costs
- Improves latency

### 5. Reserved Capacity
- If moving to Premium plan
- Commit to 1-3 year term
- Save up to 65%

## ROI Analysis

### Annual Cost Comparison

**Current (Email-based):**
- Year 1: $395 (assuming medium volume)
- Year 2: $395
- Year 3: $395
- **3-Year Total: $1,185**

**Optimized (Teams-based):**
- Year 1: $96
- Year 2: $96
- Year 3: $96
- **3-Year Total: $288**

**3-Year Savings: $897 (76% reduction)**

### Implementation Cost

- Development time: Already completed ✅
- Testing time: 2-4 hours
- Migration time: 1-2 hours
- **Total effort: 3-6 hours**

**Payback Period: Immediate** (no upfront costs)

## Conclusion

By implementing these optimizations, particularly the Teams integration, you can reduce operational costs by **76-90%** while improving functionality and user experience. The changes are low-risk with easy rollback options, making this a highly recommended optimization.

### Key Takeaways

1. **Teams integration eliminates SendGrid costs** ($0-$100/month savings)
2. **Function timeout optimization reduces compute costs** ($3/month savings)
3. **Application Insights optimization reduces monitoring costs** ($2/month savings)
4. **Total estimated savings: $5-$105/month** (38-90% reduction)
5. **Implementation is complete and ready for testing**

### Next Steps

1. Configure Azure AD app permissions
2. Get Teams channel IDs
3. Update configuration
4. Test with "Both" mode
5. Switch to "Teams" mode
6. Monitor costs and performance
