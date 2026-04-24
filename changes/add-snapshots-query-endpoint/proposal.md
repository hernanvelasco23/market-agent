# Proposal

## Title

Add snapshots query endpoint

## Summary

Expose a read endpoint to inspect market snapshots stored during the current application lifetime.

## Problem

The ingestion endpoint returns an execution summary, but there is no way to inspect the snapshots persisted by the repository.

## Goals

- expose a GET endpoint for stored snapshots
- add read capability to the snapshot repository abstraction
- keep the implementation simple and in-memory
- provide a better visible demo of the ingestion pipeline

## Non-goals

This change does not include:

- SQL Server persistence
- pagination
- filtering
- authentication
- dashboards
- signal detection
- AI briefings

## Expected outcome

After running ingestion, a caller can retrieve the snapshots stored in memory through the API.