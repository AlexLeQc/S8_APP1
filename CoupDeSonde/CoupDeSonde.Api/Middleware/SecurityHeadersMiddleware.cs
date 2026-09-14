#nullable enable

namespace CoupDeSonde.Api.Middleware;

/// <summary>
/// Middleware that injects defensive HTTP security headers into every outbound response.
/// </summary>
/// <remarks>
/// <para>
/// Addresses the following security concerns from <c>gei771.pdf</c> and the OWASP Secure Headers Project:
/// </para>
/// <list type="bullet">
///   <item><description><b>X-Content-Type-Options: nosniff</b> — Prevents MIME-type sniffing by the browser, blocking content-type confusion attacks.</description></item>
///   <item><description><b>X-Frame-Options: DENY</b> — Blocks the page from being rendered inside &lt;iframe&gt;, &lt;frame&gt;, or &lt;object&gt; elements, mitigating clickjacking.</description></item>
///   <item><description><b>Content-Security-Policy</b> — <c>default-src 'none'</c> restricts all resource loading. <c>frame-ancestors 'none'</c> is the CSP equivalent of X-Frame-Options. Suitable for a pure JSON API that loads no scripts, images, or stylesheets.</description></item>
///   <item><description><b>Strict-Transport-Security (HSTS)</b> — Forces all future requests to use HTTPS for at least one year (<c>max-age=31536000</c>), including subdomains. This is a Deliverable #1 requirement in <c>gei771.pdf</c>.</description></item>
///   <item><description><b>Referrer-Policy: no-referrer</b> — Prevents leaking URL information in the Referer header to third-party origins.</description></item>
///   <item><description><b>X-XSS-Protection: 0</b> — Disables the legacy browser XSS auditor (deprecated and known to introduce vulnerabilities in some browsers).</description></item>
///   <item><description><b>Permissions-Policy</b> — Disables browser features irrelevant to a voting API (camera, microphone, geolocation).</description></item>
/// </list>
/// </remarks>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    // Strict Content-Security-Policy for a pure JSON REST API endpoint:
    // No scripts, images, stylesheets, fonts, frames, or connections to external origins.
    private const string ContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; form-action 'none'";

    // HSTS: enforce HTTPS for 1 year, covering all subdomains.
    // Deliverable #1 requirement (gei771.pdf).
    private const string StrictTransportSecurity =
        "max-age=31536000; includeSubDomains";

    private const string PermissionsPolicy =
        "camera=(), microphone=(), geolocation=(), payment=()";

    /// <summary>
    /// Initializes a new instance of <see cref="SecurityHeadersMiddleware"/>.
    /// </summary>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Injects security headers into the response, then forwards to the next middleware.
    /// Headers are added before <c>await _next(context)</c> to ensure they appear even
    /// if a downstream middleware short-circuits the pipeline.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Attach headers before the response begins so they cannot be stripped
        // by downstream pipeline stages that might short-circuit.
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Prevent MIME-type sniffing attacks.
            headers["X-Content-Type-Options"] = "nosniff";

            // Prevent clickjacking via iframe embedding.
            headers["X-Frame-Options"] = "DENY";

            // Full CSP — deny all resource loading for this pure JSON API.
            headers["Content-Security-Policy"] = ContentSecurityPolicy;

            // HSTS — enforce HTTPS for 1 year on all subdomains.
            headers["Strict-Transport-Security"] = StrictTransportSecurity;

            // Suppress Referer header on all outbound navigations.
            headers["Referrer-Policy"] = "no-referrer";

            // Disable legacy XSS auditor (deprecated; known to introduce new bugs).
            headers["X-XSS-Protection"] = "0";

            // Restrict browser feature access.
            headers["Permissions-Policy"] = PermissionsPolicy;

            // Remove the Server header to reduce fingerprinting surface.
            headers.Remove("Server");

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
