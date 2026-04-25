# Proposal

## Title

Add AI market briefing

## Summary

Add an AI-powered briefing workflow that summarizes the latest market snapshots into a structured daily market briefing.

## Problem

The system can ingest and expose market snapshots, but it does not yet generate useful interpretation or natural-language summaries from the collected data.

## Goals

- add a briefing generation use case
- use latest market snapshots as input
- generate a structured market briefing
- integrate Semantic Kernel and Azure OpenAI through Infrastructure
- expose a manual briefing endpoint

## Non-goals

This change does not include:

- trading recommendations
- signal detection engine
- email delivery
- scheduling
- persistent briefing history
- frontend dashboard

## Expected outcome

A caller can trigger a briefing endpoint and receive an AI-generated daily market summary grounded in the latest stored snapshots.