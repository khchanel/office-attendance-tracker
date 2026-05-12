# Office Attendance Tracker

## Project Overview
A .NET 8 application that automatically tracks office attendance by detecting presence on configured office networks (CIDR matching). Two deployment modes: Windows Desktop app (system tray) and Windows Service.

## Architecture
- **Core** — Domain logic, shared models (`AttendanceService`, `AppSettings`, `AttendanceRecord`, `ComplianceStatus`, file stores, network detection)
- **Desktop** — WinForms tray app with custom `SettingsManager` and `SettingsForm`
- **Service** — Windows Service with `BackgroundService` Worker
- **Test** — MSTest + Moq unit tests

## Key Concepts
- `IsDayOff` flag on `AttendanceRecord` excludes days from both attendance count and total business days denominator
- Business days = Mon-Fri minus `IsDayOff` weekday records
- `ComplianceStatus` enum: `Secured` (met entire month), `Compliant` (met rolling), `Warning` (achievable), `Critical` (impossible)
- Compliance threshold defaults to 50% of business days
- Mockable `IDateTimeProvider` for testable date-dependent logic
- File stores support CSV and JSON formats with atomic write patterns

## Coding Standards
- Target .NET 8, use C# 12 features where appropriate
- PascalCase for public members, camelCase with underscore for private fields
- Use collection expressions `[]` for empty collections
- Prefer `var` when type is obvious
- Use nullable reference types
- XML documentation for public APIs
- No emojis in code
- CRLF line endings, UTF-8 encoding
- KISS and YAGNI principles

## Architecture Principles
- Business logic belongs in Core, UI logic stays in Desktop
- Configuration mechanisms are deployment-specific, models are shared
- Network operations abstracted through interfaces
- Dependency injection via constructor injection
- Desktop uses custom settings manager with JSON persistence
- Service uses standard `IConfiguration` with `appsettings.json`
- Both support runtime customization

## Testing
- MSTest + Moq
- Mock `IDateTimeProvider` for date-dependent tests
- Mock `IAttendanceRecordStore` (don't hit real file I/O)
- Mock `INetworkInfoProvider` (don't hit real network)
- One assert per test method, Arrange-Act-Assert pattern
- Focus on business logic in Core services

## Common Tasks
- Adding settings: add to `AppSettings` in Core, update UI in Desktop, update `appsettings.json` example in Service
- Adding services: define interface in Core, implement in Core, register in DI, add tests
- Build: `dotnet build`
- Test: `dotnet test`
- Publish Desktop: `dotnet publish OfficeAttendanceTracker.Desktop -c Release -o ./publish/desktop`
- Publish Service: `dotnet publish OfficeAttendanceTracker.Service -c Release -o ./publish/service`

## Pitfalls to Avoid
- Don't put business logic in UI classes
- Don't hardcode paths or configuration values
- Don't ignore cancellation tokens in async operations
- Don't skip validation for user input
- Don't forget to dispose resources
- Don't make breaking changes without considering both Desktop and Service
