Things i learned from this project. This first section is for voice memos.
---------------------------
I was using an in-memory database to manage user sessions, which didn't really work out very well. I created a hosted service that was used to mark properties and fields in that database as ready for transfer. That hosted service would check a static concurrent queue on the user session class to see which rows in the database to mark ready to transfer as. My hosted services in general were dogshit because they were running on start async with a while loop instead of taking background service as a base class and creating a ongoing background service that runs that manages sessions, which I'll probably end up deleting because I'm going to do an in-memory cache for managing sessions. I'm trying to see if I should use redis just to implement it for the experience, but I probably won't do that. I wasn't using dependency injection correctly in the beginning. I didn't understand exactly how it worked with constructor injection, so I was creating instances within my classes. All of my methods were just passing pieces of random strings and ints around like nonstop instead of passing full sessions. Originally, in program CS, I had a lot of logic within the minimal APIs that I had set up. Which I then moved into groups, map groups, or endpoint groups in the separate classes. Entity framework can require unique keys. I was trying to do my own verification to see if there was any keys already generated when I was writing a new session, which was unnecessary. My project folder and file structure still doesn't work. My project folder and file structure still doesn't really seem right, but it's much better than it was. My secrets are stored in .NET user secrets database thingy. Before, I was just keeping them in app settings, but wasn't committing app settings to my repo. I'm not even using the development JSON app settings or app settings development. I'm just using app settings.json. I guess I am running in development, but I guess the whole separation between development and production is still a little not clear to me, but it is running in development. That's because of... Oh, it's all because it's running in development. That's because of... Oh, fuck, is it Kestrel? I don't know. Can you create this into a nice little note for me that shows the things i learned, Chat?
For security, originally, I was, well, I guess I still kind of am, but I'm creating keys and I'm storing the keys in plain text on the database. Obviously, this isn't the best, but just getting the basic functionality down was what I needed to do. I didn't have anti-forgery set up, so I set that up. I probably still need to look into anti-forgery a little bit more. I need to figure out what proper rate limiting would look like and how to actually benchmark that to see what we need. I wasn't using logging. I had a bunch of console write lines, so I didn't actually have a logger set up. Right now, my logging is set up for just a console profile. I probably should set up some other ways to log, especially once it gets finalized to a file or database or some other thing. I used Cursor to create all the pages. I just loaded Cursor up on my wwwroot folder, and then I just started going crazy with Cursor for a little bit to get the pages. I learned about middleware and what it does. I could probably still learn a little bit more about that and some of the other things that Microsoft offers for middleware and ASP.NET. I learned about Entity Framework migrations. Entity Framework uses providers to connect to databases, like Postgres or MySQL. I used ImageSharp to manipulate the images. I learned about how the EXIF metadata actually stores the orientation of a file. When you take it, the image takes a picture, and it doesn't actually rotate it. It's that EXIF image metadata that tells the device which way to display it. I had to do that with ImageSharp. I had to rotate it and save it as a new image. I learned about serialization and deserialization from JSON and creating models for post bodies, response and request bodies. I still need to figure out the best way for throwing exceptions, returning null, doing all that stuff. I still need to think about that and think about how I can return errors from within a method to a caller. Is it always the best way to do returning null? Should I return a tuple with the value that I'm looking for and then the actual error or result? Or should I just throw fucking exceptions whenever crazy shit happens? When should we change that or choose which one to use? I learned about password derivation, or sorry, derivation functions. Like, what is the name of it? Argon2. All right, that's good. Create this and do a little recap notes for me, too, or add it on to the original one.
I really need to start incorperating unit testing and Testability.
i learned how to use appsettings.json in different environments,
how to deploy to azure and set up managed identities for app service, azure sql, and azure blob storage.
set up logging for azure
how to set up a vpc to private routing
i learned about csrf
---------------------------


Notes on What I Learned While Building the System
1. Session Storage & In-Memory Databases

Using an in-memory EF database for user sessions is fragile and not suited for multi-instance or long-running scenarios.

A better approach is:

IMemoryCache for simple, single-instance workflows.

Redis (distributed cache) if scaling out or if you want the experience.

Sessions should be managed by a proper background service, not ad-hoc loops.

2. Hosted Services & Background Work

IHostedService implemented with a manual StartAsync + while(true) loop is brittle.

Using BackgroundService as the base class is the correct pattern:

Provides structured cancellation.

Stops cleanly on shutdown.

Fits into ASP.NET Core’s hosting model.

Background workers should not manage static global queues unless absolutely necessary.

3. Dependency Injection Principles

Constructor injection is the intended way to access services—do not instantiate dependencies manually inside classes.

Proper DI simplifies:

Testing

Lifetime management

Startup/shutdown behavior

Passing around primitive values (string, int) everywhere is a smell; passing domain objects (e.g., Session, DeviceContext, etc.) is cleaner.

4. Endpoint Structure & Minimal APIs

Putting business logic directly inside minimal API endpoints leads to clutter.

Moving endpoints into endpoint groups or organized classes is cleaner.

This improves:

Separation of concerns

Testability

Readability

Future maintainability

5. EF Core: Unique Keys & Unnecessary Checks

EF Core and PostgreSQL enforce uniqueness constraints.

Manual “pre-checking” for duplicate keys is unnecessary—and introduces race conditions.

Trust unique indexes and handle insert failures if they ever occur (extremely rare with GUIDs).

6. Secrets Management

Secrets belong in:

.NET user secrets (development)

Environment variables or vault provider (production)

Storing secrets in appsettings.json—even uncommitted—is not good practice.

Using user secrets was the correct move.

7. Configuration Environments

ASP.NET Core supports environment-specific app settings:

appsettings.json

appsettings.Development.json

appsettings.Production.json

The environment (e.g., Development) is controlled by:

The ASPNETCORE_ENVIRONMENT variable

Kestrel profiles

Launch settings

You’re currently always running in Development, which explains why only appsettings.json is loading.

8. Project Structure

Early project structure was unclear, but reorganizing into:

separate folders for endpoints,

services,

models,

domain logic,

and persistence
was a significant improvement.

Still evolving, but much closer to a clean, maintainable design.

9. General Architecture Lessons

Keeping secrets (keys) out of the DB and storing PBKDF2 hashes is correct.

Using a proper session model and passing full session objects rather than short-lived fragments improves clarity.

Avoiding global state and static structures leads to safer concurrency behavior.

Understanding where to store plaintext keys (short-lived cache, never DB) clarified the system’s security posture.









Extended Notes on What I Learned During the Project
1. Security Foundations & Key Handling

Initially stored plaintext keys in the database; now understand:

Plaintext secrets must never be persisted long-term.

Use PBKDF2 or Argon2id to store only derived hashes.

Store only the hash, not the key itself.

Learned the importance of anti-forgery protection for web endpoints.

Need to deepen understanding of anti-forgery mechanisms:

Tokens, cookie+request-token pairs, SameSite handling, etc.

Need to research and benchmark rate limiting:

Identify endpoints that need protection.

Use built-in ASP.NET rate-limiting middleware or a gateway-level solution.

Determine thresholds experimentally (load testing, profiling).

Moving away from console WriteLine toward proper structured logging.

2. Logging & Monitoring

Replaced ad-hoc console messages with ASP.NET Core’s logging abstractions.

Currently using ConsoleLogger; will eventually need:

File-based logging

Database or external logging provider (Seq, Elasticsearch, Application Insights)

Structured logs for easier debugging & correlation

Learned the value of centralized logging for production-readiness.

3. Hosted Services & Background Work

Old pattern:

Wrote StartAsync loops with manual while(true) polling.

Used static queues and mutated shared in-memory structures.

New understanding:

Should extend BackgroundService for long-running tasks.

Proper cancellation tokens, graceful shutdown, and DI integration.

Avoid global static state; prefer registered services and caches.

4. Dependency Injection & Application Structure

Initially misunderstood DI and created dependencies manually.

Learned:

Constructor injection is the correct and idiomatic pattern.

Services should be registered and controlled by the DI container.

Avoid passing random primitive values everywhere—prefer domain models.

Project structure improvements:

Reduced “god-methods” inside Program.cs/minimal APIs.

Moved endpoints into endpoint groups or organized classes.

Better separation of concerns between endpoints, services, and persistence.

5. Entity Framework, Databases & Migrations

Learned about EF Migrations:

Create, update, and version database schema safely.

Understood that EF Core uses database providers (PostgreSQL, MySQL, SQL Server).

Realized:

DB-level uniqueness constraints are authoritative—no need for manual duplicate checks.

GUIDs as primary keys are fine; no pre-checking required.

Learned how to manage schemas cleanly instead of ad-hoc SQL.

6. Environment Configuration & Secrets

Began with secrets in appsettings.json (bad practice).

Moved to .NET User Secrets for development.

Understand the distinction between:

appsettings.json

appsettings.Development.json

appsettings.Production.json

Controlled by ASPNETCORE_ENVIRONMENT

Environment-specific configuration is still becoming clearer but improved significantly.

7. Image Processing & EXIF Metadata

Learned how EXIF orientation works:

Photos aren’t physically rotated; orientation metadata tells devices how to display them.

Used ImageSharp to:

Read EXIF metadata

Correct image orientation

Write out new processed image

Gained familiarity with server-side image manipulation workflows.

8. Serialization, Deserialization & API Models

Learned to define request/response models for Minimal APIs.

JSON serialization:

How ASP.NET Core handles JSON automatically.

When to define custom models.

Clean separation between transport-layer DTOs and domain entities.

Clearer understanding of how to structure API boundaries.

9. Error Handling & Method Design

Still exploring the best patterns for error signaling, including:

Returning null

Returning a tuple (success, value, error)

Returning a result object (Result<T>)

Throwing exceptions for unexpected failures

Understanding that:

Exceptions are for exceptional conditions, not normal flow.

Null returns often hide errors.

Result types can make error handling explicit and structured.

10. Cryptography & Secure Hashing

Learned about password/key derivation functions:

PBKDF2

Argon2id

scrypt, bcrypt

Understood how PBKDF2 works:

Salt

Iterations

Output size

Why PBKDF2 outputs are stored as a formatted string

Gained awareness of stronger options (Argon2id) and why PBKDF2 remains viable.

11. General ASP.NET Concepts

Learned about middleware and what it does:

Request pipeline

Ordering significance

Cross-cutting concerns (CORS, auth, rate-limiting, logging, etc.)

Still room to explore more built-in middleware features.

Combined Reflection

Overall, you learned a lot of fundamentals across:

Security design

Key management

ASP.NET Core architecture

Dependency injection

Logging and monitoring

EF Core and migrations

Image processing

JSON serialization

Background services

Proper use of cryptographic primitives

Environment configuration

You moved from a prototype-style codebase (globals, manual instantiation, logic in Program.cs, plaintext secrets) toward a more real production-grade architecture that is secure, structured, and maintainable.