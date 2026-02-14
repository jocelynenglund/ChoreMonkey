# Security Vulnerability Scan Report

**Date:** February 14, 2026  
**Repository:** jocelynenglund/ChoreMonkey  
**Scan Type:** Comprehensive Security Assessment

## Executive Summary

A comprehensive security scan was performed on the ChoreMonkey repository, including:
- Dependency vulnerability checks (NuGet and npm packages)
- CodeQL static code analysis
- Manual security code review

**Overall Status:** ✅ No critical vulnerabilities found

## Scan Details

### 1. Dependency Vulnerability Scan

#### .NET NuGet Packages
**Status:** ✅ PASS - No vulnerabilities found

Packages scanned:
- Aspire.Hosting.AppHost v9.5.0
- Aspire.Hosting.NodeJs v9.5.2
- Aspire.Hosting.Testing v9.5.0
- Microsoft.Extensions.Http.Resilience v9.9.0
- Microsoft.Extensions.ServiceDiscovery v9.5.0
- OpenTelemetry.* v1.12.0 series
- Microsoft.AspNetCore.OpenApi v10.0.0-rc.1.25451.107
- FileEventStore v1.1.1
- xunit.v3 v3.0.1
- And others

**Result:** All packages checked against GitHub Advisory Database - no known vulnerabilities.

#### NPM Packages (nestle-together frontend)
**Status:** ✅ PASS - No vulnerabilities found

Major dependencies scanned:
- React v18.3.1
- @radix-ui/* components (various versions)
- @tanstack/react-query v5.83.0
- vite v7.3.1
- typescript v5.8.3
- And 60+ other dependencies

**Result:** All packages checked against GitHub Advisory Database - no known vulnerabilities.

### 2. CodeQL Static Analysis

**Status:** ✅ PASS - No alerts

Languages analyzed:
- C# (.NET 10.0)
- TypeScript/JavaScript (React/Vite)

**Result:** CodeQL found 0 security alerts across all analyzed code.

### 3. Manual Security Review

#### Cryptographic Security ✅ GOOD
- **PIN Hashing:** Uses PBKDF2 with SHA256, 100,000 iterations, and 16-byte salt
- **Random Number Generation:** Uses `RandomNumberGenerator.GetBytes()` (cryptographically secure)
- **Timing Attack Protection:** Uses `CryptographicOperations.FixedTimeEquals()` for hash comparison
- **Location:** `/ChoreMonkey.Core/Security/PinHasher.cs`

#### Authentication & Authorization ⚠️ RECOMMENDATION
- **Current Implementation:** PIN-based authentication for household access
- **Recommendation:** Consider implementing rate limiting on the PIN verification endpoint (`/api/households/{id}/access`) to prevent brute force attacks
- **Note:** Added security comment to the code highlighting this recommendation
- **Location:** `/ChoreMonkey.Core/Feature/AccessHousehold/Handler.cs`

#### Data Storage ✅ GOOD
- Uses event sourcing with FileEventStore
- No hardcoded secrets or credentials found
- Environment variable configuration for sensitive paths
- Uses User Secrets for development (UserSecretsId configured)

#### CORS Configuration ✅ ACCEPTABLE
- Properly configured with specific origins
- Includes localhost for development
- Uses credentials with known origins only
- **Location:** `/ChoreMonkey.ApiService/Program.cs`

#### Input Validation ✅ GOOD
- GUIDs validated through route constraints
- Nullable reference types enabled across all projects
- Using strongly-typed requests and responses

#### Frontend Security ✅ GOOD
- No dangerous HTML injection found in user code
- One instance of `dangerouslySetInnerHTML` in chart component (shadcn/ui library) - used only for CSS generation with sanitized IDs
- API URL configurable via environment variable
- No hardcoded secrets or API keys

## Recommendations

### Priority: Medium
1. **Implement Rate Limiting:** Add rate limiting to the PIN authentication endpoint to prevent brute force attacks. Consider using ASP.NET Core's built-in rate limiting middleware or a third-party solution.

   Example implementation:
   ```csharp
   builder.Services.AddRateLimiter(options =>
   {
       options.AddFixedWindowLimiter("pin-auth", opt =>
       {
           opt.Window = TimeSpan.FromMinutes(1);
           opt.PermitLimit = 5;
       });
   });
   ```

### Priority: Low
2. **Dependency Updates:** Keep monitoring dependencies for security updates, especially preview packages like Microsoft.AspNetCore.OpenApi (currently RC version).

3. **Security Headers:** Consider adding security headers (CSP, X-Frame-Options, etc.) to the web application for defense in depth.

## No Action Required

The following were reviewed and are secure:
- ✅ No SQL injection risks (using event sourcing, no direct SQL)
- ✅ No XSS vulnerabilities in custom code
- ✅ No exposed secrets or credentials
- ✅ Proper use of cryptographic functions
- ✅ No vulnerable dependencies
- ✅ HTTPS enforced in production (via Aspire configuration)
- ✅ CORS properly configured

## Conclusion

The ChoreMonkey repository demonstrates good security practices with no critical vulnerabilities discovered. The primary recommendation is to implement rate limiting on the PIN authentication endpoint to enhance protection against brute force attacks. All dependencies are up-to-date and free from known vulnerabilities as of the scan date.

**Next Steps:**
1. Review and consider implementing the rate limiting recommendation
2. Continue monitoring dependencies for security updates
3. Re-run security scans after significant code changes

---
*This report was generated by automated and manual security scanning tools.*
