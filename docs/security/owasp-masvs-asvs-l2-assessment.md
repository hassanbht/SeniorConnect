# OWASP ASVS L2 & MASVS v2 Self-Assessment (P7-10)

> **Projekt:** SeniorConnect Platform  
> **Umfang:** ASP.NET Core 10 Modular Monolith API & Flutter Cross-Platform Client  
> **Bewertungsstandard:** OWASP Application Security Verification Standard (ASVS) 4.0 Level 2 & OWASP Mobile Application Security (MASVS) v2.0  
> **Status:** COMPLIANT / VERIFIED  
> **Datum:** August 2026

---

## 1. Backend ASVS Level 2 Verification Matrix

| Kapitel | ASVS Anforderung | Implementierungsdetails in SeniorConnect | Status |
| :--- | :--- | :--- | :---: |
| **V1: Architecture & Threat Modeling** | Modulare Grenzen, Bounded Contexts, Least Privilege | Modular Monolith mit strikter Assembly- und Entity-Isolation. Revisionssichere Architecture Tests (`SeniorConnect.ArchitectureTests`). | **Pass** |
| **V2: Authentication** | Passwortlose Authentifizierung, sichere OTP-Verfahren, Brute-Force-Schutz | SMS/E-Mail OTP mit 5-Minuten-Gültigkeit, Bcrypt-Hashing, Rate-Limiter (`auth_policy`, 5 Req/Min). Keine Passwörter in Senior-/Volunteer-Screens (`BR-AUTH-03`). | **Pass** |
| **V3: Session Management** | JWT Bearer Tokens mit kryptografischer Signatur, Rotation & Revocation | HS256/RS256 JWT, kurze Token-Lebensdauer, Refresh-Token-Rotation mit Widerrufs-Blacklist. | **Pass** |
| **V4: Access Control** | Server-seitige Autorisierung, Tenant-Isolation, Capabilities | Keine clientseitige Trust-Level-Autorisierung (`BR-TRUST-04`). Automatischer EF Core Query-Filter für `TenantId` und `OrganizationId`. | **Pass** |
| **V5: Validation & Encoding** | Typisierte Request-Validierung, SQL Injection Schutz | EF Core Parameterized Queries, strenge DTO-Validierung mit `Result<T>` ProblemDetails. | **Pass** |
| **V7: Error Handling & Logging** | Einheitliche Fehlerverträge (RFC 9457), kein PII-Leakage | ProblemDetails mit stabilen Error-Codes. Anonymisierte strukturierte Logs mit `CorrelationIdMiddleware`. | **Pass** |
| **V8: Data Protection** | DSGVO 2-Stufen-Löschung, Safeguarding-Trennung | Getrennte Datenbank (`SafeguardingDbContext`) für Schutzfälle. Automatischer Tier-2 Anonymisierungs-Job. Keine Speicherung von Ausweisscans (`BR-TRUST-05`). | **Pass** |
| **V13: API & Web Services** | RESTful Verträge, Rate Limiting, Idempotency | ASP.NET Core Minimal APIs, OpenAPI/Scalar, `IdempotencyMiddleware` für Mutationen. | **Pass** |

---

## 2. Mobile MASVS v2 Verification Matrix

| Bereich | MASVS Anforderung | Implementierung im Flutter Client | Status |
| :--- | :--- | :--- | :---: |
| **MASVS-STORAGE** | Sichere lokale Datenspeicherung | Sichere Speicherung von Session-Tokens via `flutter_secure_storage` (Keychain / EncryptedSharedPreferences). Kein lokales Cachen von Safeguarding-Details. | **Pass** |
| **MASVS-CRYPTO** | Verwendung robuster Kryptografie | TLS 1.3 / HTTPS für alle Netzwerkverbindungen. Keine hardcodierten Keys oder sensiblen Zertifikate im Quellcode. | **Pass** |
| **MASVS-AUTH** | Biometrie / OTP Integration | Nahtloser Login ohne statische Passwörter. Automatische Session-Abmeldung bei Inaktivität. | **Pass** |
| **MASVS-NETWORK** | Verschlüsselte Übertragung | Erzwungenes HTTPS/TLS 1.3. Keine unsicheren Klartext-HTTP-Aufrufe. | **Pass** |
| **MASVS-PLATFORM** | Berechtigungsminimierung & OS-Integration | Keine Hintergrund-Standortverfolgung (`AGENTS.md`). Standortzugriff nur mit ausdrücklicher Berechtigung bei Bedarf. | **Pass** |
| **MASVS-CODE** | Code-Qualität & Dependency Hygiene | Strikte Linter-Regeln (`flutter_lints`), keine Drittanbieter-Werbe- oder Tracking-SDKs (`BR-NOTIFY-01`). | **Pass** |

---

## 3. Fazit

Die SeniorConnect-Plattform erfüllt sämtliche relevanten Prüfpunkte des OWASP ASVS Level 2 für Webdienste sowie des OWASP MASVS v2 für mobile Apps im Kontext schutzbedürftiger Personen und gemeinnütziger Nachbarschaftshilfe.
