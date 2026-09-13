using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Geography.Application;

namespace SeniorConnect.Api.Endpoints;

public static class GeographyEndpoints
{
    public static IEndpointRouteBuilder MapGeographyEndpoints(this IEndpointRouteBuilder app)
    {
        // Public reference endpoints (no auth required)
        var refGroup = app.MapGroup("/api/v1/reference/austria")
            .WithTags("Austrian Geography Reference");

        refGroup.MapGet("/bundeslaender", async (
            IGeographyReferenceService refReader,
            CancellationToken ct) =>
        {
            var result = await refReader.GetBundeslaenderAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("GetBundeslaender")
        .Produces<IReadOnlyList<BundeslandDto>>(StatusCodes.Status200OK);

        refGroup.MapGet("/bezirke", async (
            string bundeslandCode,
            IGeographyReferenceService refReader,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(bundeslandCode))
            {
                return Results.BadRequest("bundeslandCode is required");
            }
            var result = await refReader.GetBezirkeAsync(bundeslandCode, ct);
            return result.ToHttpResult();
        })
        .WithName("GetBezirke")
        .Produces<IReadOnlyList<BezirkDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        refGroup.MapGet("/gemeinden", async (
            string bezirkCode,
            IGeographyReferenceService refReader,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(bezirkCode))
            {
                return Results.BadRequest("bezirkCode is required");
            }
            var result = await refReader.GetGemeindenAsync(bezirkCode, ct);
            return result.ToHttpResult();
        })
        .WithName("GetGemeinden")
        .Produces<IReadOnlyList<GemeindeDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        refGroup.MapGet("/lookup", async (
            string plz,
            IGeographyReferenceService refReader,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(plz))
            {
                return Results.BadRequest("plz is required");
            }
            var result = await refReader.LookupByPlzAsync(plz, ct);
            return result.ToHttpResult();
        })
        .WithName("LookupByPlz")
        .Produces<IReadOnlyList<GemeindeDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // Authenticated geocoding endpoint
        var geoAuthGroup = app.MapGroup("/api/v1/reference")
            .WithTags("Reference Data")
            .RequireAuthorization();

        geoAuthGroup.MapPost("/geocode-address", async (
            GeocodeAddressRequest request,
            ClaimsPrincipal user,
            IGeocodingProvider geocodingProvider,
            IStoreLocationService storeLocationService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await geocodingProvider.GeocodeAsync(request.Address, "de", ct);
            if (result.IsFailure) return result.ToHttpResult();

            var geocode = result.Value!;
            
            // Store the geocoded location if user requested it
            if (request.PersistToProfile)
            {
                var storeResult = await storeLocationService.StoreGeocodedLocationAsync(
                    userId.Value,
                    geocode.AddressLine,
                    geocode.PostalCode,
                    geocode.City,
                    geocode.Latitude,
                    geocode.Longitude,
                    geocode.GemeindeCode,
                    ct);
                
                if (storeResult.IsFailure) return storeResult.ToHttpResult();
            }

            return Results.Ok(new GeocodeAddressResponse(
                AddressLine: geocode.AddressLine,
                PostalCode: geocode.PostalCode,
                City: geocode.City,
                Latitude: geocode.Latitude,
                Longitude: geocode.Longitude,
                GemeindeCode: geocode.GemeindeCode,
                GemeindeName: geocode.GemeindeName,
                BezirkCode: geocode.BezirkCode,
                BezirkName: geocode.BezirkName,
                BundeslandCode: geocode.BundeslandCode,
                BundeslandName: geocode.BundeslandName,
                Confidence: geocode.Confidence));
        })
        .WithName("GeocodeAddress")
        .Produces<GeocodeAddressResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Authenticated discovery endpoints
        var discoveryGroup = app.MapGroup("/api/v1/discovery")
            .WithTags("Local Discovery")
            .RequireAuthorization();

        discoveryGroup.MapGet("/nearby-organizations", async (
            double latitude,
            double longitude,
            double radiusKm,
            IProximityService proximityService,
            CancellationToken ct) =>
        {
            if (radiusKm <= 0 || radiusKm > 100)
            {
                return Results.BadRequest("radiusKm must be between 0 and 100");
            }

            var result = await proximityService.FindNearbyOrganizationsAsync(latitude, longitude, radiusKm, ct);
            return result.ToHttpResult();
        })
        .WithName("FindNearbyOrganizations")
        .Produces<IReadOnlyList<ProximityResult>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        discoveryGroup.MapGet("/nearby-volunteers", async (
            double latitude,
            double longitude,
            double radiusKm,
            IProximityService proximityService,
            CancellationToken ct) =>
        {
            if (radiusKm <= 0 || radiusKm > 100)
            {
                return Results.BadRequest("radiusKm must be between 0 and 100");
            }

            var result = await proximityService.FindNearbyVolunteersAsync(latitude, longitude, radiusKm, ct);
            return result.ToHttpResult();
        })
        .WithName("FindNearbyVolunteers")
        .Produces<IReadOnlyList<ProximityResult>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        discoveryGroup.MapGet("/nearby-requests", async (
            double latitude,
            double longitude,
            double radiusKm,
            IProximityService proximityService,
            CancellationToken ct) =>
        {
            if (radiusKm <= 0 || radiusKm > 100)
            {
                return Results.BadRequest("radiusKm must be between 0 and 100");
            }

            var result = await proximityService.FindNearbyRequestsAsync(latitude, longitude, radiusKm, ct);
            return result.ToHttpResult();
        })
        .WithName("FindNearbyRequests")
        .Produces<IReadOnlyList<ProximityResult>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        discoveryGroup.MapGet("/nearest-towns", async (
            double latitude,
            double longitude,
            double? maxDistanceKm,
            int? maxResults,
            IProximityService proximityService,
            CancellationToken ct) =>
        {
            var result = await proximityService.GetNearestTownsAsync(
                latitude, longitude, maxDistanceKm ?? 50, maxResults ?? 10, ct);
            return result.ToHttpResult();
        })
        .WithName("GetNearestTowns")
        .Produces<IReadOnlyList<NearbyTownDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}

public sealed record GeocodeAddressRequest(
    string Address,
    bool PersistToProfile = false);

public sealed record GeocodeAddressResponse(
    string AddressLine,
    string PostalCode,
    string City,
    double Latitude,
    double Longitude,
    string GemeindeCode,
    string GemeindeName,
    string BezirkCode,
    string BezirkName,
    string BundeslandCode,
    string BundeslandName,
    double Confidence);