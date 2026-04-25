# Design

## Overview

This change adds the first AI workflow to Market Agent.

The AI layer must summarize existing market snapshots. It must not invent market data or generate trading advice.

## Application contracts

Add:

- IMarketBriefingService
- IMarketBriefingGenerator

The service coordinates the use case.
The generator abstracts the LLM implementation.

## Infrastructure implementation

Add an Azure OpenAI + Semantic Kernel implementation of IMarketBriefingGenerator.

Suggested type:

- SemanticKernelMarketBriefingGenerator

## Input

The briefing should use snapshots retrieved from IMarketSnapshotRepository.

## Output

Create a simple MarketBriefingResult model containing:

- GeneratedAtUtc
- Summary
- Highlights
- Risks
- WatchItems

## Endpoint

Add:

POST /api/briefing/run

The endpoint should:

- call IMarketBriefingService
- return MarketBriefingResult as JSON

## Rules

- AI must only summarize provided snapshots
- no buy/sell recommendations
- no unsupported claims
- keep output structured
- keep Application independent from Azure and Semantic Kernel

## Configuration

Infrastructure may read:

- Azure OpenAI endpoint
- deployment name
- API key

from configuration.

## Initial simplifications

- no database persistence for briefings
- no email delivery
- no scheduler
- no signal engine
- no historical comparison yet