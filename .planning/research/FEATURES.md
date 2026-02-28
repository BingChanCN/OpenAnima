# Feature Research

**Domain:** Multi-Anima Architecture, i18n, and Module Ecosystem
**Researched:** 2026-02-28
**Confidence:** MEDIUM

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these = product feels incomplete.

#### Anima Management

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Create new Anima | Core concept of "multi-Anima" architecture | LOW | Simple instance creation with default name |
| List all Animas | Users need to see what exists | LOW | Sidebar list with names |
| Switch between Animas | Multi-instance pattern requires switching | LOW | Click to activate, UI updates to show active instance |
| Independent execution per Anima | Each instance must run separately | MEDIUM | Separate heartbeat loops, isolated module state |
| Persist Anima configuration | Users expect their work to survive restarts | MEDIUM | Save name, module connections, module configs to JSON per instance |
| Delete Anima | Users need cleanup capability | LOW | Confirmation dialog + file deletion |

#### Internationalization (i18n)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Language switcher UI | Standard i18n pattern | LOW | Dropdown or toggle in header/settings |
| Chinese/English UI text | Project requirement | MEDIUM | Resource files for all UI strings |
| Persist language preference | Users expect choice to survive reload | LOW | localStorage or config file |
| Immediate UI update on switch | Modern apps don't require reload | LOW | Reactive UI framework handles this |

#### Module Management

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| List installed modules | Users need to see what's available | LOW | Built-in + third-party distinction |
| Install module from .oamod | Core SDK workflow (v1.4 shipped this) | LOW | Already have package loading |
| Uninstall module | Cleanup capability | LOW | Remove from disk + registry |
| Enable/disable module | Standard plugin pattern (VSCode, JetBrains) | LOW | Toggle without uninstall |
| View module metadata | Users need info before using | LOW | Display name, version, author, description |
| Module status indicators | Users need to know what's active | LOW | Visual state: enabled/disabled/error |

#### Module Configuration

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Select module to show config | Standard node editor pattern (Blender, UE) | LOW | Click node → detail panel appears |
| Edit module-specific settings | Each module has different config needs | MEDIUM | Dynamic UI based on module schema |
| Persist module config per Anima | Settings must survive restart | MEDIUM | Save to Anima's config JSON |
| Config validation | Prevent invalid states | MEDIUM | Module defines validation rules |

#### Built-in Modules

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Fixed text output | Basic data source | LOW | Config: editable text field |
| Text concatenation | Common text operation | LOW | Two inputs → one output |
| Text split by delimiter | Common text operation | LOW | Config: delimiter string |
| Conditional branching | Flow control necessity | MEDIUM | Config: condition expression, two outputs |
| LLM module with config | Core AI capability | MEDIUM | Config: API URL, API key, model name |

### Differentiators (Competitive Advantage)

Features that set the product apart. Not required, but valuable.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Heartbeat as optional module | Flexibility — not all Animas need proactive loops | LOW | Moves from core to module, user decides |
| Per-Anima chat interface | Each instance has independent conversation | MEDIUM | Aligns with "digital life form" concept |
| Visual module status in editor | Real-time feedback on execution state | LOW | Already exists (v1.3), extend to new modules |
| Module dependency resolution | Auto-install required modules | HIGH | Complex graph resolution, defer to v2+ |
| Module configuration templates | Pre-filled configs for common use cases | MEDIUM | Nice-to-have, not launch-critical |
| Anima cloning | Duplicate existing setup | LOW | Copy config JSON, useful for experimentation |
| Module search/filter | Helps when module count grows | LOW | Simple text filter on name/description |
| Module categories/tags | Organize large module libraries | MEDIUM | Requires taxonomy design |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create problems.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Real-time config sync across Animas | "Keep settings consistent" | Violates instance isolation, creates coupling | Export/import config templates |
| Auto-update modules | "Always stay current" | Breaking changes break workflows, user loses control | Manual update with changelog review |
| Module marketplace backend | "Like VSCode marketplace" | Infrastructure complexity, moderation burden, security liability | Local .oamod file installation only |
| Nested Anima instances | "Anima managing other Animas" | Confusing mental model, unclear ownership, debugging nightmare | Single-level architecture, use modules for composition |
| Global module configuration | "Configure once, use everywhere" | Breaks per-Anima isolation, creates hidden dependencies | Per-Anima config with optional templates |
| Automatic module recommendations | "Suggest what to install" | Requires usage analytics, privacy concerns, biased results | User-driven discovery via documentation |

## Feature Dependencies

```
[Multi-Anima Architecture]
    ├──requires──> [Instance Isolation] (separate state per Anima)
    ├──requires──> [Configuration Persistence] (save/load per instance)
    └──requires──> [Instance Switcher UI] (sidebar navigation)

[Module Configuration UI]
    ├──requires──> [Module Selection] (click to select)
    ├──requires──> [Detail Panel] (right-side properties)
    └──requires──> [Config Persistence] (save to Anima config)

[Built-in Modules]
    ├──requires──> [Module SDK] (already shipped v1.4)
    ├──requires──> [Port System] (already shipped v1.3)
    └──requires──> [Configuration UI] (for editable modules)

[i18n]
    ├──requires──> [Resource Files] (translation strings)
    ├──requires──> [Language Switcher UI] (user control)
    └──requires──> [Persistence] (localStorage or config)

[Module Management]
    ├──requires──> [Module Registry] (already exists v1.0)
    ├──requires──> [Enable/Disable State] (new capability)
    └──enhances──> [Module Configuration] (can't configure disabled modules)

[Heartbeat as Module]
    ├──requires──> [Optional Module Pattern] (not auto-loaded)
    └──conflicts──> [Core Heartbeat] (must remove from core runtime)
```

### Dependency Notes

- **Multi-Anima requires Instance Isolation:** Each Anima must have completely separate module instances, heartbeat loops, and chat state. Shared state would violate the "independent agent" concept.
- **Module Configuration requires Detail Panel:** Standard node editor pattern (Blender, Unreal) — click node, properties appear on right.
- **Built-in Modules require Module SDK:** Already shipped in v1.4, so this is satisfied.
- **i18n requires Resource Files:** All UI strings must be externalized to support language switching.
- **Module Management enhances Module Configuration:** Disabled modules shouldn't show config UI.
- **Heartbeat as Module conflicts with Core Heartbeat:** Must refactor from core runtime to optional module.

## MVP Definition

### Launch With (v1.5)

Minimum viable product — what's needed to validate multi-Anima concept.

- [x] **Multi-Anima Architecture** — Core value proposition, must ship
  - Create/list/switch/delete Animas
  - Independent execution per instance
  - Configuration persistence
- [x] **i18n (Chinese/English)** — Project requirement, table stakes
  - Language switcher UI
  - Translated UI strings
  - Preference persistence
- [x] **Module Management** — Enables ecosystem growth
  - Install/uninstall from .oamod
  - Enable/disable toggle
  - Module metadata display
- [x] **Module Configuration UI** — Necessary for configurable modules
  - Click module → detail panel
  - Edit settings
  - Persist per Anima
- [x] **Essential Built-in Modules** — Demonstrate capabilities
  - Fixed text (data source)
  - Text concat (basic processing)
  - Conditional branch (flow control)
  - Configurable LLM (AI capability)
  - Optional heartbeat (proactive behavior)

### Add After Validation (v1.6+)

Features to add once core is working.

- [ ] **Anima cloning** — When users request "copy this setup"
- [ ] **Module search/filter** — When module count exceeds ~20
- [ ] **Text split/merge modules** — When users request more text operations
- [ ] **Module configuration templates** — When users request "save this config"
- [ ] **Module categories** — When organization becomes problem

### Future Consideration (v2+)

Features to defer until product-market fit is established.

- [ ] **Module dependency resolution** — Complex, wait for real dependency patterns
- [ ] **Module marketplace backend** — Infrastructure burden, validate local-first approach first
- [ ] **Nested Anima instances** — Unclear value, high complexity
- [ ] **Cross-Anima config sync** — Violates isolation, wait for user demand
- [ ] **Auto-update modules** — Control vs convenience tradeoff, validate manual approach first

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Create/switch Animas | HIGH | MEDIUM | P1 |
| Independent execution | HIGH | MEDIUM | P1 |
| Config persistence | HIGH | MEDIUM | P1 |
| Language switcher | HIGH | LOW | P1 |
| UI text translation | HIGH | MEDIUM | P1 |
| Install/uninstall modules | HIGH | LOW | P1 |
| Enable/disable modules | HIGH | LOW | P1 |
| Module metadata display | MEDIUM | LOW | P1 |
| Module config UI | HIGH | MEDIUM | P1 |
| Fixed text module | MEDIUM | LOW | P1 |
| Text concat module | MEDIUM | LOW | P1 |
| Conditional module | HIGH | MEDIUM | P1 |
| Configurable LLM module | HIGH | MEDIUM | P1 |
| Optional heartbeat module | MEDIUM | MEDIUM | P1 |
| Anima cloning | MEDIUM | LOW | P2 |
| Module search | MEDIUM | LOW | P2 |
| Text split module | LOW | LOW | P2 |
| Text merge module | LOW | LOW | P2 |
| Config templates | MEDIUM | MEDIUM | P2 |
| Module categories | LOW | MEDIUM | P2 |
| Dependency resolution | MEDIUM | HIGH | P3 |
| Marketplace backend | LOW | HIGH | P3 |

**Priority key:**
- P1: Must have for v1.5 launch
- P2: Should have, add in v1.6+
- P3: Nice to have, future consideration (v2+)

## Competitor Feature Analysis

| Feature | VSCode Extensions | JetBrains Plugins | Unreal Blueprint | Our Approach |
|---------|-------------------|-------------------|------------------|--------------|
| Install/Uninstall | ✓ Marketplace + VSIX | ✓ Marketplace + ZIP/JAR | N/A (built-in) | .oamod file only (no marketplace) |
| Enable/Disable | ✓ Global + per-workspace | ✓ Individual + category | N/A | Per-Anima enable/disable |
| Configuration | ✓ Settings JSON | ✓ Settings dialog | ✓ Details panel | Details panel (like Unreal) |
| Metadata Display | ✓ Name, author, version, downloads, rating | ✓ Name, author, version, dependencies | ✓ Node properties | Name, author, version, description |
| Multi-Instance | ✓ Workspace-level | ✓ Project-level | ✓ Per-level | Per-Anima (stronger isolation) |
| i18n | ✓ Built-in | ✓ Built-in | ✓ Built-in | Chinese/English only (v1.5) |
| Module Status | ✓ Enabled/disabled/error | ✓ Enabled/disabled/bundled | ✓ Execution state | Enabled/disabled/error + real-time execution |

**Key Insights:**
- **Enable/disable is table stakes** — All major platforms support this
- **Details panel pattern is proven** — Unreal/Blender use right-side panel for node properties
- **Per-instance isolation is differentiator** — VSCode/JetBrains have workspace-level, we have Anima-level (stronger)
- **No marketplace is acceptable** — Can ship with local file installation only
- **Real-time execution status is differentiator** — Most platforms show static state, we show live execution

## Sources

### Multi-Agent Architecture
- [Multi-Agent Coordination Systems Enterprise Guide 2026](https://iterathon.tech/blog/multi-agent-coordination-systems-enterprise-guide-2026)
- [Building Better Systems: Single vs Multi-Agent Architecture (2026 Guide)](https://www.innervationai.com/blog/single-vs-multi-agent-architecture-2026-guide/)
- [The developer's guide to SaaS multi-tenant architecture — WorkOS](https://workos.com/blog/developers-guide-saas-multi-tenant-architecture)

### Plugin Management Patterns
- [PhpStorm Documentation — Enabling and Disabling Plugins](https://www.jetbrains.com/phpstorm/help/enabling-and-disabling-plugins.html)
- [Managing Extensions in Visual Studio Code](https://code.visualstudio.com/docs/editor/extension-marketplace)

### Node Editor Patterns
- [Details Panel in the Blueprints Visual Scripting Editor for Unreal Engine](https://dev.epicgames.com/documentation/en-us/unreal-engine/details-panel-in-the-blueprints-visual-scriting-editor-for-unreal-engine)
- [Blender Node Editor](https://www.avalab.org/blender-node-editor/)

### i18n Patterns
- [React i18next and correct way of changing language](https://stackoverflow.com/questions/35728632/react-i18next-and-correct-way-of-changing-language)
- [How to persist localisation in a vue app?](https://stackoverflow.com/questions/71224550/how-to-persist-localisation-in-a-vue-app)
- [7 Key Best Practices for Rails Internationalization](https://phrase.com/blog/posts/rails-i18n-best-practices/)

### Configuration Persistence
- [JSON Design Patterns: Best Practices for Structured Data](https://toolnest.io/blog/json-patterns)
- [JSON Best Practices: Complete Guide](https://jsonparser.com/json-best-practices)

---
*Feature research for: OpenAnima v1.5 Multi-Anima Architecture*
*Researched: 2026-02-28*
