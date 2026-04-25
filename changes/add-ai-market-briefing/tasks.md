# Tasks

## Goal

Implement the first AI-generated market briefing workflow.

## Tasks

### 1. Add briefing models

- add MarketBriefingResult
- add any minimal input model if needed

### 2. Add application contracts

- add IMarketBriefingService
- add IMarketBriefingGenerator

### 3. Implement application service

- create MarketBriefingService
- read snapshots from IMarketSnapshotRepository
- pass snapshot data to IMarketBriefingGenerator
- return MarketBriefingResult

### 4. Implement Semantic Kernel generator

- create SemanticKernelMarketBriefingGenerator in Infrastructure
- use Azure OpenAI through Semantic Kernel
- keep prompt grounded in provided snapshots
- return structured output

### 5. Add configuration

- add required Azure OpenAI settings to appsettings example
- do not hardcode secrets

### 6. Wire dependency injection

- register MarketBriefingService
- register SemanticKernelMarketBriefingGenerator
- register Semantic Kernel dependencies as needed

### 7. Add API endpoint

- expose POST /api/briefing/run
- keep controller thin

### 8. Ensure solution builds

- run dotnet build

## Out of scope

The following are excluded:

- trading recommendations
- briefing persistence
- email delivery
- scheduler
- frontend