# Decision: Docker Deployment Gap & Getting Started Guide

**Date**: 2025-05-27  
**Author**: Book (Documentation)  
**Status**: Recorded  
**Related**: Getting Started Guide (`docs/help/getting-started.md`)

---

## Problem

Thiccdal had no user-facing getting-started guide for operators/streamers to install and run the application. Additionally, while .NET Aspire is used for development orchestration, **no Dockerfile or docker-compose.yml assets exist** in the production codebase, leaving a gap between "building from source" and "containerized deployment."

---

## Decision

1. **Created `docs/help/getting-started.md`** — A comprehensive operator-focused guide covering:
   - Prerequisites and .NET 10 installation
   - Both pre-built release and source build options
   - SQLite database configuration and persistence
   - Step-by-step Twitch OAuth setup with credential registration
   - Local network and production deployment guidance
   - Troubleshooting and common configuration scenarios

2. **Documented the Docker deployment gap** — Rather than invent Docker assets that don't exist, the guide:
   - Clearly states **"Dockerfile and docker-compose.yml assets do not yet exist"**
   - Provides the **expected structure** for future Docker support
   - Lists **planned volume mounts** and **environment variables**
   - Includes steps to check if Docker support has been added
   - Keeps the guide useful for both current and future deployments

3. **Platform integration guidance** — Used the existing Twitch authentication flow as a template:
   - Links to [Connecting Thiccdal to Twitch](./connecting-to-twitch.md) for detailed OAuth steps
   - Explains configuration via `appsettings.json` and environment variables
   - Lists future integrations clearly marked as not yet enabled

---

## Rationale

### Why No Invented Docker Assets?

The instructions explicitly state: *"Do not invent unsupported setup flows. Base the document on what is actually present in the repo today."*

Creating fake Dockerfile/compose files would:
- ✗ Mislead operators into expecting Docker support that doesn't work
- ✗ Create technical debt if real assets are added later with different structure
- ✗ Violate the instruction to document current reality

Instead, documenting the **gap + planned approach** ensures:
- ✓ The guide is immediately usable for .NET-based deployments
- ✓ Future Docker work has clear guidance on expected structure
- ✓ Operators know what's available today and what's coming

### Why This Approach for Operators?

Thiccdal is used by **streamers and operators**, not just developers. The guide:
- Uses plain language ("control dashboard", "badge") instead of technical jargon
- Provides copy-paste configuration examples
- Explains **why** each setting matters (e.g., "RedirectUri must match both places")
- Includes troubleshooting for real operator scenarios (e.g., "Port already in use")

### Configuration Management

The guide documents both `appsettings.json` (current) and environment variables (planned for Docker):
- Allows operators to choose their comfort level
- Aligns with planned Docker deployment pattern
- Supports both development and production scenarios

---

## What's Documented

### ✓ Currently Available
- Installation from pre-built releases
- Building from source with .NET 10
- SQLite database configuration and auto-setup
- Twitch OAuth integration (step-by-step, with credential registration)
- Local and network-based deployment
- Background service execution (Windows)
- Logging configuration
- Troubleshooting for common issues

### ⚠️ Documented as Planned/Future
- Docker and docker-compose deployment (with expected structure)
- YouTube, Facebook, X (Twitter), Discord integrations (noted as "Coming soon")
- LinkedIn and TikTok (noted as "Awaiting API approval")

### ✗ Not Documented
- RTMP ingest setup (not yet exposed in configuration)
- Stream recording configuration (not yet exposed in configuration)
- Chatbot command creation (UI flow not yet reviewed)
- Overlay configuration (UI flow not yet reviewed)

---

## File Created

- **`docs/help/getting-started.md`** (16.5 KB)
  - Linked from README for discoverability
  - References existing platform guide (`connecting-to-twitch.md`)
  - Provides checklist for first-run validation

---

## Future Work

1. **When Docker support is added**:
   - Create `Dockerfile` and `docker-compose.yml` in repo root
   - Update "Docker Deployment" section with real instructions
   - Add "Verify Docker assets exist" check

2. **As other platforms are enabled**:
   - Create platform-specific guides (similar to `connecting-to-twitch.md`)
   - Update the "Future Platform Support" section

3. **As UI features stabilize**:
   - Add guides for chatbot, overlays, stream recording configuration

---

## Sign-Off

This decision document records:
- ✓ What is currently documented (and accurately reflects the repo)
- ✓ What gaps exist (and are clearly called out as "not yet available")
- ✓ Why Docker assets were not invented
- ✓ The expected structure for future Docker support

The guide is ready for operators and provides a foundation for future platform and deployment work.
