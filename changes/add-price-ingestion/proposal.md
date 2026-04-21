# Proposal

## Title

Add daily price ingestion for the initial market watchlist

## Summary

Introduce the first market data ingestion workflow for Market Agent.

This change adds the ability to retrieve daily price data for a fixed initial watchlist, normalize the results into a common internal format, and persist market snapshots for later signal detection and briefing generation.

## Problem

Market Agent cannot generate meaningful signals or daily briefings without a reliable source of structured market data.

Before any signal detection or AI summarization can happen, the system needs a repeatable ingestion flow that collects and stores price information for the monitored assets.

## Goals

- ingest daily price data for a fixed watchlist
- support multiple asset types through a normalized internal model
- persist market snapshots for historical traceability
- prepare the system for later signal detection features

## Non-goals

This change does not include:

- signal detection
- AI-generated briefings
- user-defined watchlists
- real-time streaming
- advanced technical indicators
- news or sentiment enrichment

## Initial watchlist

The first version will support a small curated set of assets, such as:

- MELI
- NU
- NVDA
- MSFT
- AMD
- SPY
- BTC
- ETH
- MEP

## Expected outcome

After this change, the system should be able to execute a daily ingestion flow and store normalized market snapshots for the initial watchlist.

These stored snapshots will become the source of truth for future signal detection and daily briefing generation.