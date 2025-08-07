A) If you fork the state-machine extension, add these features
Context: ManagedCode.Orleans.StateMachine exists, is early (NuGet 0.1.x), and is a good base to extend. 
nuget.org
GitHub

1) Event-sourcing–native FSM (first-class with JournaledGrain)
Transition = Event: built-in option so every state transition emits a domain event (ControlActivated, CheckCompleted, …) and calls RaiseEvent/ConfirmEvents automatically. Include dedupe keys for idempotency. (Orleans event-sourcing model.) 
Microsoft Learn
+1

Outbox integration: after ConfirmEvents, publish to Orleans Streams using an outbox pattern to guarantee at-least once side-effects.

2) Timers/Reminders & temporal guards
Durable timeouts per state (within 5d move to Failed) using Reminders; non-durable retry timers for short loops. Expose a fluent API: .WithTimeout(TimeSpan...) → Transition(...). 
Microsoft Learn
sergeybykov.github.io

3) Hierarchical & orthogonal states
Support sub-states (e.g., Operating.{Monitoring|Paused}) and orthogonal regions (e.g., Operating × EvidenceRetention). Keep serialization deterministic so it plays nicely with snapshots.

4) Sagas & compensations across grains
Lightweight saga DSL for multi-grain flows (e.g., posting an invoice ⇒ creating a JE ⇒ running a control check), with compensation steps and correlation IDs.

5) Versioned state machines (blue/green)
Version the FSM definition separately from grain state, allow shadow runs (evaluate new FSM side-by-side without committing), plus a migrate hook to upgrade live instances safely.

6) Typed triggers, guards, and effects
Strongly-typed guards/effects returning ValueTask, optional policy decorators (circuit-breaker, retry, time budget).

7) Observability & audit
OpenTelemetry spans for Enter/Exit/Transition, metrics (transition counts, time-in-state, failure reasons), and structured audit log (who/when/why) to satisfy ICFR traceability. (General Orleans docs; add OTel in your fork.) 
Microsoft Learn

8) Dev-experience
State diagram export (Mermaid/PlantUML) from the live FSM;

Replay tester to feed event trails and assert transitions;

Hot reload of FSM config in non-prod;

Minimal Roslyn source-gen to create typed trigger methods from a JSON/YAML FSM spec.

If upstream is receptive, you could PR “event-sourcing hooks + reminders/timeouts + telemetry”; otherwise keep it in your fork and maintain parity with the base package.