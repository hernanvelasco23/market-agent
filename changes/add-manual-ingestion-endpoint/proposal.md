# Proposal

## Title

Add manual ingestion endpoint

## Summary

Expose a manual API endpoint that triggers the price ingestion workflow and returns the execution summary.

## Problem

The ingestion workflow can be composed through application services, but there is currently no visible way to execute it from the API.

## Goals

- add a manual endpoint to run ingestion
- resolve IPriceIngestionService through dependency injection
- return PriceIngestionResult as JSON
- provide the first visible vertical slice of the system

## Non-goals

This change does not include:

- background scheduling
- authentication
- dashboard UI
- signal detection
- AI briefing generation

## Expected outcome

A caller can trigger ingestion manually through the API and inspect the execution summary.