# Archived Documentation

This folder contains documentation that describes **obsolete architecture** or **unimplemented features** from earlier design phases.

## Why These Documents Are Archived

During the Architecture Consolidation project (Epic #106), we discovered that the application evolved away from the originally designed database-centric architecture toward a **live API-based approach**.

These documents describe features and architecture that were:
- Designed but **never implemented**
- Implemented but later **removed or replaced**
- No longer relevant to the **current architecture**

## Current Architecture

For accurate, up-to-date architecture documentation, see:
- **[CURRENT_STATE.md](../CURRENT_STATE.md)** - Complete architecture overview
- **[API_USAGE_GUIDE.md](../API_USAGE_GUIDE.md)** - API endpoints and usage
- **[CONFIGURATION_GUIDE.md](../CONFIGURATION_GUIDE.md)** - Configuration settings

## Archived Documents

### DATA_RESEEDING_GUIDE.md
**Archived**: October 15, 2025  
**Reason**: Describes database reseeding workflows that were never implemented. The application uses live GitLab API calls instead of storing data in a database.

**Historical Context**: Originally designed for a data collection/storage approach where GitLab data would be periodically ingested into PostgreSQL. This approach was abandoned in favor of real-time metrics calculation.

---

### PRD_ENTITY_DESIGN.md
**Archived**: October 15, 2025  
**Reason**: Describes database entity models (Raw, Fact, PRD, Operational, Dimensional) that were designed but never used. All 20+ entity models and 30+ migrations are unused.

**Historical Context**: Part of the original database-centric design. The entities were defined and migrations created, but no code ever inserted or queried this data.

---

## When to Reference Archived Docs

These documents may still be useful for:
- **Historical reference** - Understanding design decisions and evolution
- **Learning** - Seeing alternative approaches that were considered
- **Future features** - If we decide to add data persistence later

However, they should **NOT** be used as:
- Current architecture reference
- Implementation guides
- Operational runbooks

---

**For questions about archived documents**: See Epic #106 (Architecture Consolidation & Cleanup)  
**Last Updated**: October 15, 2025
