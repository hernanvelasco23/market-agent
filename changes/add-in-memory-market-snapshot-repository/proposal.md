# Proposal

## Title

Add in-memory market snapshot repository

## Summary

Introduce the first concrete implementation of IMarketSnapshotRepository using in-memory storage.

This repository will allow the project to persist market snapshots during the current process lifetime without introducing SQL Server or database infrastructure yet.

## Problem

The ingestion workflow already has domain primitives, application contracts, and early market data providers, but it still lacks a concrete snapshot repository implementation.

Without a repository implementation, the ingestion flow cannot be executed end-to-end.

## Goals

- implement IMarketSnapshotRepository
- store MarketSnapshot instances in memory
- keep the implementation simple and test-friendly
- enable the next ingestion orchestration feature

## Non-goals

This change does not include:

- SQL Server persistence
- Entity Framework Core
- migrations
- historical query support beyond minimal storage
- API endpoints
- background jobs

## Expected outcome

After this change, the project should have a working in-memory repository that supports storing snapshots and enables the first end-to-end ingestion flow in later features.