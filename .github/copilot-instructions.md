# GitHub Copilot Instructions for Office Attendance Tracker

## Project Overview

This is a .NET 8 application that automatically tracks office attendance by detecting presence on configured office networks. The solution supports two deployment modes: a Windows Desktop application with GUI and a Windows Service for background operation.

## Architecture Principles

### Project Structure
- **Core Project**: Contains domain logic, services, and shared models
- **Desktop Project**: Windows Forms application for interactive use
- **Service Project**: Windows Service for background operation
- **Test Project**: Unit tests using MSTest and Moq

### Separation of Concerns
- Business logic belongs in Core
- UI logic stays in Desktop project
- Configuration mechanisms are deployment-specific, models are shared
- Network operations are abstracted through interfaces

## Coding Standards

### C# Conventions
- Use C# 12 features where appropriate
- Target .NET 8
- Follow standard .NET naming conventions (PascalCase for public members, camelCase with underscore for private fields)
- Use collection expressions `[]` for empty collections
- Prefer `var` for local variables when type is obvious
- Use nullable reference types appropriately

### Code Style
- Use XML documentation for public APIs
- Keep methods focused and single-purpose
- Prefer dependency injection over static dependencies
- Use interfaces for testability and flexibility

### Line Endings and Encoding
- Use CRLF line endings (Windows standard)
- Use UTF-8 encoding for all text files

## Design Patterns

### Dependency Injection
- Register services in Program.cs
- Use constructor injection for dependencies
- Prefer transient or singleton lifetimes as appropriate

### Configuration
- Desktop uses custom settings manager with JSON persistence
- Service uses standard IConfiguration with appsettings.json
- Settings model is shared in Core project
- Both support runtime customization

### Testing
- Write unit tests for business logic
- Use Moq for mocking dependencies
- Focus on testing behavior, not implementation
- Prefer readable test names that describe the scenario

## Common Tasks

### Adding New Settings
1. Add property to shared settings model in Core
2. Update default factory methods if needed
3. Add UI controls in Desktop settings form (if user-configurable)
4. Update Service configuration file example
5. Consider validation requirements

### Adding New Services
1. Define interface in Core project
2. Implement concrete class in Core
3. Register in dependency injection container
4. Add unit tests for the service
5. Document public APIs

### Network Detection
- Network operations use abstracted interfaces
- Detection logic filters virtual/container networks
- Uses Windows routing table to identify primary routes
- Supports CIDR notation for network configuration
- IPv4 only currently supported

### File Storage
- Support both CSV and JSON formats
- Use atomic write patterns for data safety
- Implement auto-save with timers
- Explicit initialization pattern for stores

## Best Practices

### Error Handling
- Catch specific exceptions where possible
- Log errors appropriately (Debug.WriteLine for now)
- Provide user-friendly error messages in UI
- Don't let background operations crash the application

### Performance
- Use minimal polling intervals
- Avoid blocking UI operations
- Lazy-load resources when appropriate
- Dispose resources properly

### Validation
- Validate configuration early (at startup)
- Provide clear validation error messages
- Use domain-specific validation (e.g., CIDR format)
- Validate in both UI and service layers

## Testing Guidelines

### What to Test
- Business logic in Core services
- Configuration validation
- Data transformations
- Edge cases and error conditions

### What Not to Test
- UI rendering (WinForms)
- File I/O operations (mock the store)
- Network operations (mock the provider)
- Third-party library behavior

### Mock Conventions
- Use descriptive mock setup
- One Assert per test method
- Arrange-Act-Assert pattern
- Descriptive test names

## Documentation

### Code Documentation
- XML comments for public APIs
- Inline comments only for complex logic
- Avoid obvious comments
- Keep documentation current with code

### User Documentation
- README focuses on essentials
- Avoid implementation details in user docs
- Provide examples for common scenarios
- Keep configuration examples up to date

## Specific Conventions

### Settings Management
- Desktop: UI-driven with events for runtime updates
- Service: Configuration file with restart requirement
- Both use shared model from Core
- Defaults are defined per deployment type

### Background Processing
- Use BackgroundService for Worker implementations
- Implement proper cancellation support
- Handle exceptions gracefully
- Don't block shutdown

### UI Patterns
- Use dependency injection for forms
- Separate UI logic from business logic
- Validate input before saving
- Provide visual feedback for actions

## Common Pitfalls to Avoid

- Don't put business logic in UI classes
- Don't hardcode paths or configuration values
- Don't ignore cancellation tokens in async operations
- Don't create Settings models in multiple projects
- Don't skip validation for user input
- Don't forget to dispose resources
- Don't make breaking changes without considering both Desktop and Service

## When in Doubt

- Follow existing patterns in the codebase
- Prefer simplicity over cleverness
- Consider testability when designing features
- Keep UI and business logic separated
- Write tests for new business logic
- Document public APIs with XML comments
