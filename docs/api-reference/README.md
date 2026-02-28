# API Reference

API reference for OpenAnima module development.

## Core Interfaces

- **[IModule](IModule.md)** — Base contract for all modules (lifecycle methods)
- **[IModuleExecutor](IModuleExecutor.md)** — Extends IModule with execution capabilities
- **[ITickable](ITickable.md)** — Heartbeat interface for periodic execution
- **[IEventBus](IEventBus.md)** — Pub/sub system for module communication

## Port System

- **[Port System](port-system.md)** — Port types, attributes, and EventBus usage patterns

## Common Patterns

- **[Common Patterns](common-patterns.md)** — Code examples for typical module topologies (source, transform, sink, heartbeat)
