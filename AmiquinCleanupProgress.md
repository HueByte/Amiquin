# Amiquin Repository Cleanup & Improvement Progress

**Started:** 2025-01-09  
**Goal:** Modernize, clean, and optimize the Amiquin Discord bot codebase

## Overview
This document tracks the progress of a comprehensive cleanup and improvement effort for the Amiquin Discord bot project. The goal is to remove unused functionality, improve code structure, enhance services, and ensure consistency across the codebase.

## Task Categories

### 🧹 Code Cleanup
- [ ] Scan repository for unused services/functionalities
- [ ] Remove unused types across codebase
- [ ] Replace hardcoded values with constants from Constants.cs
- [ ] Ensure project structure consistency

### 🏗️ Architecture Improvements
- [ ] Move Configuration files from Options/Configuration/ to Options/ (structure cleanup)
- [ ] Improve BotContextAccessor service
- [ ] Ensure ChatSemaphoreManager integration in conversation processing
- [ ] Review and improve CommandHandlerService
- [ ] Update ServerMetaService with proper caching strategy

### 🔄 Service Modernization
- [ ] Improve CleanerService implementation
- [ ] Replace PerformanceAnalyzer with Jiro.Shared library version
- [ ] Improve ExternalProcessRunner services
- [ ] Review PersonaService for new conversation system
- [ ] Evaluate MessageCacheService - improve or remove
- [ ] Upgrade StatisticsCollector service

### 🐳 DevOps & Tooling
- [ ] Optimize Dockerfile
- [ ] Update and improve setup-project.ps1 script

## Analysis Notes

### Current State Assessment

#### Services Review
1. **CleanerService** (`Amiquin.Core.Cleaner.CleanerService.cs`)
   - Basic implementation with message cache clearing
   - Uses MemoryCache introspection (accessing .Count, .Keys)
   - Could be enhanced with more comprehensive cleanup operations
   - Frequency: 1 hour (3600 seconds)

2. **Options Structure** (`Amiquin.Core.Options/`)
   - Configuration files scattered in subfolder `Configuration/`
   - Files to move: ChatOptions, DataPathOptions, DiscordOptions, JobManagerOptions, LLMOptions, VoiceOptions
   - Target: Flatten structure to main Options folder

3. **BotContextAccessor** (`Services.BotContext.BotContextAccessor.cs`)
   - Simple class for context management
   - Missing thread safety considerations
   - Could benefit from improved lifecycle management
   - Uses hardcoded defaults that should use Constants

4. **ChatSemaphoreManager** (`Services.BotSemaphores.ChatSemaphoreManager.cs`)
   - Well-implemented semaphore management
   - Uses ConcurrentDictionary for thread-safe operations
   - Should verify integration with conversation processing

5. **PerformanceAnalyzer** (`Services.BotSession.PerformanceAnalyzer.cs`)
   - Platform-specific implementations (Windows/Linux)
   - Complex CPU and memory monitoring
   - Should be replaced with Jiro.Shared equivalent
   - Factory pattern implementation

6. **CommandHandlerService** (`Services.CommandHandler.CommandHandlerService.cs`)
   - Comprehensive command handling
   - Good error handling and logging
   - Uses BotContextAccessor and ServerMetaService
   - Proper async/await patterns

7. **ServerMetaService** (`Services.Meta.ServerMetaService.cs`)
   - Complex caching and database operations
   - Thread-safe with semaphores
   - Good implementation but could be optimized
   - Cache TTL: 30 minutes

8. **PersonaService** (`Services.Persona.PersonaService.cs`)
   - AI-powered persona management
   - News integration for dynamic mood
   - Caching with 1-day TTL
   - Should integrate better with new conversation system

9. **MessageCacheService** (`Services.MessageCache.MessageCacheService.cs`)
   - In-memory message caching
   - Database persistence
   - 5-day cache TTL for messages
   - Mixed value - evaluate if needed with new systems

10. **StatisticsCollector** (`Services.StatisticsCollector.StatisticsCollector.cs`)
    - Comprehensive bot statistics
    - Uses PerformanceAnalyzer (needs updating)
    - Good metrics collection
    - 5-minute frequency

### Constants Analysis
- Well-structured constants organization
- Environment variables with AMQ_ prefix
- Cache keys, AI models, persona keywords defined
- Some hardcoded values still exist in services

### Setup Script Analysis (`scripts/setup-project.ps1`)
- Comprehensive setup script
- Interactive and non-interactive modes
- Creates .env and appsettings.json
- Good structure but could be enhanced

## Implementation Strategy

### Phase 1: Repository Scan & Unused Code Removal
1. Analyze project dependencies and references
2. Identify unused services, types, and files
3. Remove dead code while preserving functionality

### Phase 2: Structure Optimization
1. Move Configuration files to proper location
2. Update imports and references
3. Verify project builds successfully

### Phase 3: Service Improvements
1. Replace PerformanceAnalyzer with Jiro.Shared version
2. Enhance CleanerService with better operations
3. Improve BotContextAccessor thread safety
4. Review ExternalProcessRunner implementation

### Phase 4: Integration Verification
1. Ensure ChatSemaphoreManager is properly used
2. Update PersonaService for conversation system
3. Evaluate MessageCacheService necessity
4. Upgrade StatisticsCollector dependencies

### Phase 5: Constants & Configuration
1. Replace remaining hardcoded values
2. Update setup script improvements
3. Optimize Docker configuration

## Progress Log

### 2025-01-09
- Initial analysis completed
- Todo list created with 16 main tasks
- Progress tracking document established
- Ready to begin Phase 1 implementation

---

## Next Actions
1. Start with repository scan for unused services
2. Begin with CleanerService improvements
3. Move Configuration files to proper structure
4. Continue with systematic service improvements

*This document will be updated as progress is made on each task.*