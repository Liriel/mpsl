# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MPS (Multiplayer Shopping List) is a real-time collaborative shopping list application with:
- **Backend**: ASP.NET Core 8.0 with Entity Framework Core, SQLite, and SignalR
- **Frontend**: Angular 17 with Angular Material UI
- **Authentication**: ASP.NET Core Identity with Microsoft Account integration
- **Deployment**: Multi-architecture Docker containers

## Essential Commands

### Backend Development
```bash
# Build and run
dotnet build
dotnet run

# Testing and database
dotnet test
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

### Frontend Development
```bash
cd ClientApp

# Development workflow
npm install
npm start              # Starts dev server on https://localhost:44443
npm run build          # Development build
npm run publish        # Production build for deployment
npm test
```

### Deployment
```bash
./publish.sh           # Builds and deploys multi-arch Docker images
```

## Architecture & Code Structure

### Backend Architecture
- **Repository Pattern**: `IRepository` and `PersistedRepository`
- **Generic Services**: `IEntityService<T>` for CRUD operations
- **Identity Services**: `IUserIdentityService` for user context
- **AutoMapper**: Object mapping between models and ViewModels
- **SignalR Hub**: `LiveHub` for real-time notifications

### Key Directories
```
Controllers/           # Web API controllers
Services/             # Business logic services  
Model/               # Entity models and DataContext
Infrastructure/      # Shared utilities and constants
ViewModels/         # Data transfer objects
Hubs/               # SignalR hubs for real-time updates
ClientApp/          # Angular frontend application
Tests/              # XUnit tests (minimal coverage currently)
```

### Database Models
Core entities: `ShoppingList`, `ShoppingListItem`, `Unit`, `Recommendation`, `ItemHistory`
- All entities track modification user/timestamp
- SQLite database with Entity Framework Code First
- Recommendation algorithm in `recommend.sql`

### Frontend Architecture
- Service-based with dependency injection
- Repository pattern via `WebRepo` service
- PWA capabilities with service worker
- Real-time updates via SignalR client

## Development Setup

### Configuration
- **Development URL**: https://localhost:44443 (Angular) with proxy to ASP.NET Core API
- **Database**: SQLite at `data/mps.db`
- **Authentication**: Requires Microsoft Account OAuth ClientId/ClientSecret in user secrets
- **CORS**: Configured for localhost:44443 and 192.168.0.96:44443

### Authentication Flow
Uses ASP.NET Core Identity with cookie-based authentication. Custom 401 handling returns API responses instead of redirects.

### Real-time Features
SignalR broadcasts changes to all connected clients through notification service pattern.

## Development Patterns

### Adding New Entities
1. Create model in `Model/` directory
2. Add DbSet to `DataContext`  
3. Create migration: `dotnet ef migrations add AddNewEntity`
4. Create service implementing `IEntityService<T>`
5. Create controller inheriting from appropriate base
6. Add corresponding Angular service and components

### Testing
- Backend: XUnit framework, run with `dotnet test`
- Frontend: Angular testing (Jasmine/Karma), currently disabled in angular.json

### Code Formatting
Uses EditorConfig in `ClientApp/.editorconfig`:
- 2-space indentation for most files
- Single quotes for TypeScript
- UTF-8 encoding

## Deployment Architecture

Multi-architecture Docker deployment supporting AMD64, ARM32v7, ARM64v8:
- Alpine Linux base images for minimal footprint
- Self-contained .NET deployments
- Private registry at `peach.heisl.org`
- Automated via `publish.sh` script

## Important Notes

- Frontend and backend run simultaneously during development with hot reloading
- Database migrations run automatically on startup
- All API responses are JSON (no MVC views)
- HTTPS certificates auto-generated via `aspnetcore-https.js`
- Tests currently have minimal coverage and should be expanded