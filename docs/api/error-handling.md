# API Error Handling

We follow the standard RFC 7807 **Problem Details** specification for returning errors from the REST API endpoints.

## ProblemDetails Response Format

Every error response contains a structured JSON payload with a stable `code` field that the mobile client can map to localized user-visible strings:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "The requested resource was not found.",
  "code": "USER_NOT_FOUND"
}
```

## Standard Status Codes

* **400 Bad Request:** Input validation errors (e.g. invalid phone number format).
* **401 Unauthorized:** Missing or expired authentication token.
* **403 Forbidden:** Valid token, but the user is missing necessary capabilities or trust levels. Returns a `missing[]` array in the response to list what's required.
* **404 Not Found:** Resource doesn't exist. For safety and privacy, some authorization failures will return 404 instead of 403 to avoid confirming the existence of a record.
* **409 Conflict:** Resource state mismatch or optimistic concurrency violation (e.g., trying to accept a help request that is already assigned).
* **429 Too Many Requests:** Brute force or rate limit throttling (e.g. requesting OTP codes too frequently).
