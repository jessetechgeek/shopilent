# Shopilent E-Commerce Platform

![Unit Tests](https://github.com/jessetechgeek/shopilent/actions/workflows/unit-tests.yml/badge.svg)
![Infrastructure Tests](https://github.com/jessetechgeek/shopilent/actions/workflows/infrastructure-tests.yml/badge.svg)
![API & E2E Tests](https://github.com/jessetechgeek/shopilent/actions/workflows/api-e2e-tests.yml/badge.svg)
![GitHub last commit](https://img.shields.io/github/last-commit/jessetechgeek/shopilent)
![GitHub contributors](https://img.shields.io/github/contributors/jessetechgeek/shopilent)
![GitHub Issues](https://img.shields.io/github/issues/jessetechgeek/shopilent)
![GitHub Pull Requests](https://img.shields.io/github/issues-pr/jessetechgeek/shopilent)

Shopilent is a production-grade e-commerce platform built to demonstrate enterprise-level backend architecture and system design. It implements Clean Architecture, Domain-Driven Design, and CQRS patterns to solve real-world challenges in high-traffic commerce systems: data consistency, performance optimization, maintainability, and scalability.

## ⚡ TL;DR

- **Architecture**: Clean Architecture + DDD + CQRS
- **Performance**: EF Core (writes) + Dapper (reads), Redis caching, read replicas
- **Search**: Meilisearch for sub-millisecond product queries
- **Reliability**: Outbox pattern for event consistency, 4000+ tests
- **Scale**: 85+ API endpoints across 6 domains, 18 projects
- **Status**: Production-ready backend, customizable frontend

## 🎯 Why This Project Exists

Most e-commerce tutorials show basic CRUD operations. Shopilent demonstrates how to build systems that **actually scale in production**:

- **CQRS with optimized reads**: Separate write models (EF Core) from read models (Dapper) for performance
- **Event-driven consistency**: Reliable domain events using the Outbox pattern, not fire-and-forget
- **Distributed caching**: Redis integration with intelligent cache invalidation strategies
- **Search at scale**: Meilisearch integration for fast, typo-tolerant product search
- **Production-ready testing**: 4000+ comprehensive tests covering domain logic, application workflows, and infrastructure

This is not a tutorial project—it's a **working system** designed as both a learning resource and a foundation for real products.

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation Layer                   │
│              FastEndpoints API (85+ endpoints)          │
├─────────────────────────────────────────────────────────┤
│                   Application Layer                     │
│        CQRS Commands/Queries with MediatR Pipeline      │
│     (Logging, Validation, Caching Behaviors)            │
├─────────────────────────────────────────────────────────┤
│                     Domain Layer                        │
│   Aggregates │ Value Objects │ Domain Events │ Specs    │
├─────────────────────────────────────────────────────────┤
│                 Infrastructure Layer                    │
│  EF Core │ Redis │ S3 Storage │ Payments │ Meilisearch  │
└─────────────────────────────────────────────────────────┘
```

**18 projects** (12 source + 6 test) organized with clear separation of concerns and dependency inversion throughout.

## ⚡ Technical Highlights

### Scalability & Performance
- **Read/Write Separation**: Dedicated database connections for reads vs writes (CQRS)
- **Read Replicas Ready**: Infrastructure designed to support PostgreSQL read replicas
- **Distributed Caching**: Redis with pattern-based invalidation for consistency
- **Optimized Queries**: Dapper for complex reads, EF Core for transactional writes
- **Full-Text Search**: Meilisearch integration for sub-millisecond product searches

### Architecture & Design
- **Clean Architecture**: Zero dependencies from Domain → Application → Infrastructure
- **Domain-Driven Design**: Rich domain models with 40+ domain events
- **Event-Driven**: Outbox pattern ensures reliable event processing
- **CQRS Pattern**: Complete separation of commands and queries
- **Repository Pattern**: Explicit Read/Write repository separation

### Quality & Testing
- **4000+ Tests**: Comprehensive coverage across all layers
- **Unit Tests**: Pure domain logic testing without external dependencies
- **Integration Tests**: Full workflow testing with mocked infrastructure
- **Builder Pattern**: Fluent test data creation for readable tests
- **CI/CD Pipeline**: Automated testing on every commit

### Developer Experience
- **Docker Development**: Complete local environment with one command
- **Hot Reload**: Fast development cycle for API and frontend
- **Structured Logging**: Serilog with Seq for centralized log analysis
- **API Documentation**: Interactive Scalar documentation in development
- **Type Safety**: Strong typing throughout with C# 12 and TypeScript

## 🚀 Key Features

### Multi-Application Setup
- **REST API** (.NET 9): High-performance backend with 85+ FastEndpoints
- **Admin Panel** (React 19 + Vite): Modern admin dashboard
- **Customer App** (Next.js 15): Server-side rendered storefront
- **Shared Infrastructure**: PostgreSQL, Redis, MinIO, Seq

### Core Domains

| Domain | Features |
|--------|----------|
| **Catalog** | Product variants, hierarchical categories, flexible attributes |
| **Identity** | JWT authentication, role-based authorization, token refresh |
| **Sales** | Shopping cart, order processing, inventory tracking |
| **Payments** | Multi-provider support (Stripe), secure payment processing |
| **Shipping** | Address management, multiple shipping addresses per user |

## 📋 Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- [Node.js 18+](https://nodejs.org/) (for frontend development)

## 🛠️ Quick Start

### 1. Clone Repository
```bash
git clone https://github.com/jessetechgeek/Shopilent-API.git
cd Shopilent-API
```

### 2. Start Development Environment
```bash
# Start entire application stack (API, Client, Admin, and Infrastructure)
docker compose up -d
```

### 3. Initialize Database
```bash
# Apply database migrations (run after containers are up)
docker exec -it shopilent-api dotnet ef database update \
  --project Shopilent.Infrastructure.Persistence.PostgreSQL \
  --startup-project Shopilent.API
```

### 4. Access Applications

| Service | Port | URL |
|---------|------|-----|
| **API** | 9801 | http://localhost:9801 |
| **API Documentation** | 9801 | http://localhost:9801/scalar/v1 |
| **Customer App** (Next.js) | 9800 | http://localhost:9800 |
| **Admin Panel** (React) | 9802 | http://localhost:9802 |
| **Seq Logs** | 9803 | http://localhost:9803 |
| **MailHog** (Email Testing) | 9804 | http://localhost:9804 |
| **PostgreSQL** | 9851 | `localhost:9851` |
| **PostgreSQL Replica 1** | 9852 | `localhost:9852` |
| **PostgreSQL Replica 2** | 9853 | `localhost:9853` |
| **Meilisearch** | 9855 | http://localhost:9855 |
| **Redis** | 9856 | `localhost:9856` |
| **MinIO API** | 9858 | `localhost:9858` |
| **MinIO Console** | 9859 | http://localhost:9859 |

### 5. Local Development (Without Docker)

For faster development iteration:

```bash
# Start only infrastructure services
docker compose up -d postgres redis minio seq

# Run API locally with hot reload
dotnet run --project src/API/Shopilent.API

# Run Admin Panel
cd Shopilent.Admin && npm run dev

# Run Customer App
cd Shopilent.Client && npm run dev
```

## 🧪 Testing Strategy

Built with testing as a first-class concern:

- **Domain Tests**: Pure business rules without infrastructure dependencies
- **Application Tests**: CQRS handlers tested end-to-end with mocked infrastructure
- **Integration Tests**: Database and external provider integrations
- **Builder Pattern**: Expressive, readable test data creation
- **Mock Isolation**: External dependencies isolated with Moq

```bash
# Run all tests
dotnet test

# Run with coverage report
dotnet test --collect:"XPlat Code Coverage"
```

## 🔧 Common Development Tasks

### Build & Test
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/API/Shopilent.API

# Restore dependencies
dotnet restore
```

### Database Management
```bash
# Create new migration
dotnet ef migrations add <MigrationName> \
  --project src/Infrastructure/Shopilent.Infrastructure.Persistence.PostgreSQL \
  --startup-project src/API/Shopilent.API

# Update database
dotnet ef database update \
  --project src/Infrastructure/Shopilent.Infrastructure.Persistence.PostgreSQL \
  --startup-project src/API/Shopilent.API
```

### Frontend Development
```bash
# Admin Panel (React + Vite)
cd Shopilent.Admin
npm run dev        # Development server
npm run build      # Production build
npm run lint       # Lint code

# Customer App (Next.js)
cd Shopilent.Client
npm run dev        # Development server
npm run build      # Production build
npm run lint       # Lint code
```

## 📊 API Overview

85+ RESTful endpoints organized by domain:

| Domain | Endpoints | Purpose |
|--------|-----------|---------|
| **Catalog** | 33 | Products, categories, attributes, variants |
| **Sales** | 19 | Shopping cart, orders, order fulfillment |
| **Identity** | 8 | Authentication, registration, token management |
| **Users** | 8 | User administration, profiles, roles |
| **Payments** | 7 | Payment processing, payment methods |
| **Shipping** | 6 | Address management, shipping addresses |
| **Administration** | 2 | System administration, maintenance |
| **Search** | 2 | Product search, search indexing |

All endpoints include:
- JWT authentication with role-based authorization
- Comprehensive request validation with FluentValidation
- Consistent error handling and response formatting
- OpenAPI documentation for interactive testing

## 🔐 Security Features

- **JWT Authentication**: Secure token-based authentication with refresh tokens
- **Role-Based Authorization**: Granular permission control (Admin, Customer, Guest)
- **Input Validation**: Comprehensive validation with FluentValidation
- **SQL Injection Prevention**: Parameterized queries and ORM best practices
- **Password Security**: PBKDF2 hashing with salt
- **CORS Configuration**: Controlled cross-origin resource sharing

## 📈 Monitoring & Operations

- **Structured Logging**: Serilog with JSON formatting for machine-readable logs
- **Centralized Logging**: Seq integration for log aggregation and analysis
- **Health Checks**: Database and Redis connectivity monitoring
- **Performance Metrics**: Built-in ASP.NET Core metrics and diagnostics
- **Request Tracing**: Comprehensive request/response logging pipeline

## 🚀 Production Deployment

### Docker Deployment
```bash
# Build and run entire application stack
docker compose up -d

# View logs
docker compose logs -f

# Stop all services
docker compose down
```

### Manual Deployment
```bash
# Build API for production
dotnet publish src/API/Shopilent.API -c Release -o ./publish

# Build frontend applications
cd Shopilent.Admin && npm run build
cd Shopilent.Client && npm run build
```

### Configuration
The application uses environment variables for configuration. See `.env.example` for required settings:
- Database connection strings (with read replica support)
- Redis configuration
- JWT secrets and token lifetimes
- S3/MinIO storage credentials
- Payment provider API keys
- CORS allowed origins

## 🗂️ Project Structure

The solution contains **18 projects** organized by architectural layer:

### 📦 Core Layer (Business Logic)
```
src/Core/
├── Shopilent.Domain                → Entities, Value Objects, Domain Events, Specifications
└── Shopilent.Application           → CQRS Commands/Queries, MediatR Handlers, Validators
```

### 🔧 Infrastructure Layer (Technical Concerns)
```
src/Infrastructure/
├── Shopilent.Infrastructure                            → Domain Events, Email, Image Processing
├── Shopilent.Infrastructure.Persistence.PostgreSQL     → EF Core, Dapper, Repositories
├── Shopilent.Infrastructure.Cache.Redis                → Distributed Caching, Pattern Invalidation
├── Shopilent.Infrastructure.Identity                   → JWT Authentication & Authorization
├── Shopilent.Infrastructure.S3ObjectStorage            → Multi-provider Storage (S3, MinIO)
├── Shopilent.Infrastructure.Payments                   → Payment Providers (Stripe)
├── Shopilent.Infrastructure.Logging                    → Serilog, Seq Integration
├── Shopilent.Infrastructure.Search.Meilisearch         → Full-text Search Engine
└── Shopilent.Infrastructure.Realtime.SignalR           → Real-time Communication
```

### 🌐 Presentation Layer (User Interfaces)
```
src/API/
└── Shopilent.API                  → 85+ FastEndpoints, Validation, Auth

src/UI/
├── Shopilent.Admin                → React 19 Admin Dashboard
└── Shopilent.Client               → Next.js 15 Customer Storefront
```

### 🧪 Test Projects (Quality Assurance)
```
tests/
├── Shopilent.Domain.UnitTests                  → Pure domain logic tests
├── Shopilent.Application.UnitTests             → CQRS handler tests with mocks
├── Shopilent.Infrastructure.IntegrationTests   → Database & external service tests
├── Shopilent.API.IntegrationTests              → API endpoint integration tests
├── Shopilent.FunctionalTests                   → End-to-end workflow tests
└── Shopilent.ArchitectureTests                 → Architecture rule enforcement
```

## 📚 Technical Decisions

### Why Clean Architecture?
Ensures business logic remains independent of frameworks, databases, and external services. Makes the codebase testable, maintainable, and adaptable to changing requirements.

### Why CQRS?
Separates read and write concerns, allowing optimization of each independently. Write models focus on consistency and business rules, while read models optimize for query performance.

### Why Outbox Pattern?
Guarantees reliable event processing without distributed transactions. Events are stored in the same database transaction as entity changes, ensuring consistency.

### Why Domain Events?
Decouples domain logic from side effects (caching, notifications, search indexing). Aggregates remain focused on business rules while event handlers manage cross-cutting concerns.

## 🛣️ Project Status

**Current State**: Production-ready backend with 85 endpoints and 4000+ tests. Frontend applications serve as functional demos and can be customized to match specific client branding and UX requirements.

**Active Development**:
- Stabilizing existing features and fixing bugs
- Completing comprehensive test coverage
- Preparing for v1.0 stable release

**Future Enhancements** (Post-v1.0):
- Advanced search features with Meilisearch
- Real-time notifications with SignalR
- Enhanced analytics and reporting
- Multi-currency support
- Advanced promotion and discount system

This project is actively maintained and kept up-to-date with latest .NET releases.

## 🤝 Contributing

While this is primarily a portfolio project, suggestions and feedback are welcome! Feel free to:
- Open issues for bugs or suggestions
- Start discussions about architectural decisions
- Share how you're using this project as a learning resource

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

Built with industry-leading tools and frameworks:

**Backend:**
- [FastEndpoints](https://fast-endpoints.com/) - High-performance alternative to ASP.NET Core controllers
- [MediatR](https://github.com/jbogard/MediatR) - Elegant CQRS and mediator pattern implementation
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/) - Modern ORM for .NET
- [FluentValidation](https://fluentvalidation.net/) - Strongly-typed validation rules
- [Serilog](https://serilog.net/) - Flexible structured logging
- [Dapper](https://github.com/DapperLib/Dapper) - High-performance micro-ORM for optimized reads

**Frontend:**
- [React 19](https://reactjs.org/) - Modern UI library with latest features
- [Next.js 15](https://nextjs.org/) - Production-ready React framework with App Router
- [shadcn/ui](https://ui.shadcn.com/) - Re-usable component library built with Radix UI
- [Tailwind CSS 4](https://tailwindcss.com/) - Utility-first CSS framework
- [Vite](https://vitejs.dev/) - Next-generation frontend build tool

---

**Built by [Jesse](https://github.com/jessetechgeek)** - Senior Backend Engineer focused on scalable system design and clean architecture.
