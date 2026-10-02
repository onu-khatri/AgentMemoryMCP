# External repository and release decisions

Status: **approved for local prerelease and migration**. This record intentionally contains no credentials, tokens, signing keys, feed API keys, or secret-bearing endpoints.

| Decision | Approved value | State |
|---|---|---|
| Git host and organization/owner | GitHub / `onu-khatri` | Approved on 2026-09-29 |
| Repository URL | `https://github.com/onu-khatri/OnuObservability.git` | Approved on 2026-09-29 for repository and package metadata |
| Local checkout location | `D:\RND\ONU\OnuObservability`, alongside `D:\RND\ONU\AgentMemoryMCP` | Approved by the user and moved to an independent local Git repository on 2026-10-01 |
| NuGet feed | Isolated filesystem feed at `D:\RND\ONU\OnuObservability\artifacts\packages`; AgentMemoryMCP reaches it through `../OnuObservability/artifacts/packages` | Approved on 2026-09-29 for local development and exact-package validation only; path updated for the sibling repository on 2026-10-01 |
| Promotion channels | Local validation channel only | Approved on 2026-09-29; external prerelease and stable promotion remain unapproved |
| Package owner(s) | `onu-khatri` | Approved on 2026-09-29 |
| SPDX license identifier | `MIT` | Approved on 2026-09-29 |
| Support contact/URL | `https://github.com/onu-khatri/OnuObservability/issues` | Approved on 2026-09-29 |
| Security contact/policy URL | Repository `SECURITY.md` and `https://github.com/onu-khatri/OnuObservability/security/advisories/new` | Approved on 2026-09-29 |
| Signing mechanism | Unsigned for the local-only feed | Approved on 2026-09-29; external signing remains a future promotion decision |
| SBOM/provenance mechanism | Repository SHA-256 manifest, SPDX 2.3 SBOM, and SLSA-shaped provenance evidence | Approved on 2026-09-29 |
| Initial prerelease version | `0.1.0-alpha.1` | Approved on 2026-09-29 for the local feed |
| Current immutable migration candidate | `0.1.0-alpha.9` | Local-only successor that moves reviewed outbound HTTP wiring into Hosting and formalizes consumer deployment-asset ownership; external publication remains unapproved |
| Supported target frameworks | Approved 2026-09-30 | All projects and packages target `net10.0` only; no .NET 8 support matrix |

The standalone local repository is initialized on branch `main` at the approved sibling checkout path. It has not been committed or pushed to GitHub. Packages may be published only to the approved ignored filesystem feed and consumed from there for migration validation. External publication, external signing, stable promotion, and pushing the repository remain unapproved actions.
