# Project Retrospective & Learning Notes

This document summarizes my notes on the key technical lessons learned during development.

---

## 1. Session Management & State

### What I Tried
- Used an **in-memory database** to manage user sessions.
- Implemented a **hosted service** that:
  - Monitored a static `ConcurrentQueue` on the session class.
  - Marked session records as “ready for transfer.”

### Issues Identified
- In-memory storage was fragile and not well-suited for session lifecycle management.
- Static shared state created tight coupling and complexity.
- Hosted services were incorrectly implemented:
  - Used `StartAsync` with infinite `while` loops.
  - Did not inherit from `BackgroundService`.

### What I Learned
- Long-running background work should use `BackgroundService`.
- Session management should be centralized and lifecycle-aware.
- Passing around primitive values (`string`, `int`) instead of domain objects (e.g., `Session`) leads to brittle code.

### Next Steps
- Replace current approach with an **in-memory cache abstraction**.
- Possibly experiment with **Redis** for learning purposes, but not required for current scope.

---

## 2. Dependency Injection (DI)

### Early Mistakes
- Did not understand constructor injection.
- Manually instantiated services inside classes.

### Improvements
- Learned how ASP.NET Core DI works with constructor injection.
- Reduced direct instantiation and improved testability.

### Takeaway
- DI is foundational in ASP.NET Core—misusing it cascades into poor architecture.
- Services should depend on abstractions, not concrete implementations.

---

## 3. API & Application Structure

### Initial State
- Significant business logic lived directly inside **Minimal API endpoints** in `Program.cs`.

### Refactor
- Moved logic into:
  - Endpoint groups
  - Mapped route groups
  - Dedicated classes

### Lessons
- Thin endpoints + thick services are easier to test and maintain.
- `Program.cs` should focus on configuration, not behavior.

---

## 4. Entity Framework (EF Core)

### Mistakes
- Attempted manual uniqueness checks for keys before inserts.
- Did not initially understand EF’s constraints and key handling.

### What I Learned
- EF Core enforces uniqueness through:
  - Primary keys
  - Database constraints
- Manual verification was unnecessary and error-prone.
- Learned how **EF migrations** work.
- Understood that EF uses **providers** (e.g., SQL Server, PostgreSQL, MySQL).

---

## 5. Configuration & Environments

### Early Confusion
- Used only `appsettings.json`.
- Did not fully understand environment separation.
- Unclear why the app was always running in development.

### Improvements
- Learned how environment-specific configuration works:
  - `appsettings.Development.json`
  - `ASPNETCORE_ENVIRONMENT`
- Secrets moved to **.NET User Secrets** instead of config files.

### Takeaway
- Environment separation is critical for security and deployment clarity.
- Configuration should be layered and environment-aware.

---

## 6. Security Learnings

### Early State
- API keys stored in **plain text** in the database.
- No CSRF protection.
- No rate limiting.

### Improvements
- Added **anti-forgery (CSRF)** protection.
- Learned about **password derivation functions** (Argon2).
- Recognized risks of plaintext secrets.

### Open Questions
- Proper approach to:
  - Rate limiting
  - Benchmarking request limits
  - Secure key storage (hashing, rotation)

---

## 7. Logging & Observability

### Initial Approach
- Relied on `Console.WriteLine`.

### Improvements
- Integrated ASP.NET Core logging.
- Currently logging to console.
- Learned how to configure logging for **Azure App Services**.

### Next Steps
- Add structured logging.
- Log to file, database, or centralized logging platform.

---

## 8. Middleware & ASP.NET Core Internals

### What I Learned
- What middleware is and how request pipelines work.
- How ASP.NET Core processes requests sequentially.
- There is still more to explore with built-in middleware offerings.

---

## 9. Frontend & Tooling

### UI Generation
- Used **Cursor** to rapidly scaffold pages in `wwwroot`.

### Image Processing
- Used **ImageSharp** for image manipulation.
- Learned about **EXIF orientation metadata**:
  - Images are not physically rotated by default.
  - Orientation is stored in metadata.
- Implemented rotation and saved normalized images.

---

## 10. Serialization & Error Handling

### Learned
- JSON serialization/deserialization.
- Creating models for:
  - Request bodies
  - Response bodies

### Open Design Questions
- Error-handling strategy:
  - Return `null`?
  - Return `(Result, Error)` tuples?
  - Throw exceptions?
- When exceptions are appropriate vs. control-flow signaling.

---

## 11. Testing & Testability

### Current Gap
- No meaningful unit tests.
- Architecture initially not test-friendly.

### Takeaway
- Testability must be designed early.
- DI, abstractions, and clear boundaries are prerequisites for unit testing.

---

## 12. Cloud & Deployment (Azure)

### What I Learned
- Deploying ASP.NET Core apps to **Azure App Service**.
- Using **Managed Identities** for:
  - Azure SQL
  - Azure Blob Storage
- Configuring Azure logging.
- Setting up **private networking / VNet routing**.

---

## 13. Summary: Key Takeaways

- Early architectural shortcuts compound quickly.
- ASP.NET Core strongly encourages:
  - Dependency Injection
  - Clear separation of concerns
  - Environment-based configuration
- Security and observability should not be afterthoughts.
- Passing domain objects instead of primitives simplifies logic.
- Background work must follow framework patterns.
- Testability is a design concern, not an add-on.

---

## 14. Next Focus Areas

- Proper session/cache abstraction
- Unit and integration testing
- Error-handling conventions
- Rate limiting strategy
- Secure secret handling
- Cleaner project structure

---

## Final Recap

This project served as a hands-on crash course in real-world ASP.NET Core development. Many early decisions were suboptimal, but each mistake directly led to a deeper understanding of framework conventions, security practices, and system design. The codebase is significantly cleaner, more secure, and more maintainable than when it started—and the remaining gaps are now clearly identified.
