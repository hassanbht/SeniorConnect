using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Geography.Domain;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Infrastructure;

public static class DataSeeder
{
    public static async Task SeedInitialDataAsync(SeniorConnectDbContext db, CancellationToken cancellationToken = default)
    {
        // 1. Seed Austrian Administrative Units (P1-15b)
        if (!await db.AustrianAdministrativeUnits.AnyAsync(cancellationToken))
        {
            var units = CreateAustrianAdministrativeUnits();
            await db.AustrianAdministrativeUnits.AddRangeAsync(units, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        // 2. Seed Activity Categories (Reference Taxonomy: P1-15, P2-06, PSG-06)
        if (!await db.ActivityCategories.AnyAsync(cancellationToken))
        {
            var categories = new List<ActivityCategory>
            {
                CreateCategory("shopping", "help.category.shopping", 1, isBlocked: false),
                CreateCategory("doctor", "help.category.doctor", 2, isBlocked: false),
                CreateCategory("authority", "help.category.authority", 2, isBlocked: false),
                CreateCategory("accompaniment", "help.category.accompaniment", 1, isBlocked: false),
                CreateCategory("home_small", "help.category.home_small", 2, isBlocked: false),
                CreateCategory("language_practice", "help.category.language_practice", 1, isBlocked: false),
                CreateCategory("newcomer_orientation", "help.category.newcomer_orientation", 1, isBlocked: false),
                CreateCategory("mentoring", "help.category.mentoring", 2, isBlocked: false),
                CreateCategory("gardening", "help.category.gardening", 1, isBlocked: false),
                CreateCategory("digital_support", "help.category.digital_support", 1, isBlocked: false),
                CreateCategory("reading", "help.category.reading", 1, isBlocked: false),

                // Blocked categories with designated Austrian referral providers (BR-SCOPE-01..05)
                CreateCategory(
                    "nursing_blocked",
                    "help.category.nursing_blocked",
                    5,
                    isBlocked: true,
                    referralGroup: "Mobile Hauskrankenpflege & Heimhilfe (Rotes Kreuz, Caritas, Volkshilfe, Hilfswerk)"),
                CreateCategory(
                    "medical_blocked",
                    "help.category.medical_blocked",
                    5,
                    isBlocked: true,
                    referralGroup: "Gesundheitsberatung & Notdienst 1450 / Rettungsdienst 144"),
                CreateCategory(
                    "heavy_construction_blocked",
                    "help.category.heavy_construction_blocked",
                    4,
                    isBlocked: true,
                    referralGroup: "Gewerbliche Handwerksbetriebe & Baumeister"),
                CreateCategory(
                    "financial_advice_blocked",
                    "help.category.financial_advice_blocked",
                    4,
                    isBlocked: true,
                    referralGroup: "Staatliche Schuldenberatung & Arbeiterkammer (AK)")
            };

            await db.ActivityCategories.AddRangeAsync(categories, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        // 3. Seed Pilot Organizations (P2-01)
        if (!await db.Organizations.AnyAsync(cancellationToken))
        {
            var org = Organization.Create(
                name: "Mitanand Nachbarschaftshilfe Pilot Salzburg",
                type: OrganizationType.Association,
                legalName: "Verein Mitanand Österreich",
                supportEmail: "kontakt@mitanand-pilot.at",
                supportPhone: "+43 662 123456");

            await db.Organizations.AddAsync(org, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static List<AustrianAdministrativeUnit> CreateAustrianAdministrativeUnits()
    {
        var units = new List<AustrianAdministrativeUnit>();

        // All 9 Bundesländer with their codes (Statistik Austria codes)
        var bundeslaender = new[]
        {
            new { Code = "1", Name = "Burgenland" },
            new { Code = "2", Name = "Kärnten" },
            new { Code = "3", Name = "Niederösterreich" },
            new { Code = "4", Name = "Oberösterreich" },
            new { Code = "5", Name = "Salzburg" },
            new { Code = "6", Name = "Steiermark" },
            new { Code = "7", Name = "Tirol" },
            new { Code = "8", Name = "Vorarlberg" },
            new { Code = "9", Name = "Wien" }
        };

        foreach (var bl in bundeslaender)
        {
            // Add Bundesland level entry
            units.Add(AustrianAdministrativeUnit.Create(
                bundeslandCode: bl.Code,
                bundeslandName: bl.Name,
                bezirkCode: "",
                bezirkName: "",
                gemeindeCode: "",
                gemeindeName: "",
                postalCode: "",
                localityName: "",
                latitude: 0,
                longitude: 0));
        }

        // Pilot region: Tirol (7) - Innsbruck-Land (703) with key Gemeinden
        // This is a representative subset for the pilot
        var tirolGemeinden = new[]
        {
            new { GemeindeCode = "70320", GemeindeName = "Kematen in Tirol", PLZ = "6175", Lat = 47.2600, Lon = 11.2433, Locality = "Kematen in Tirol" },
            new { GemeindeCode = "70321", GemeindeName = "Zirl", PLZ = "6170", Lat = 47.2719, Lon = 11.2336, Locality = "Zirl" },
            new { GemeindeCode = "70322", GemeindeName = "Völs", PLZ = "6176", Lat = 47.2481, Lon = 11.3092, Locality = "Völs" },
            new { GemeindeCode = "70101", GemeindeName = "Innsbruck", PLZ = "6020", Lat = 47.2692, Lon = 11.4041, Locality = "Innsbruck" },
            new { GemeindeCode = "70101", GemeindeName = "Innsbruck", PLZ = "6010", Lat = 47.2692, Lon = 11.4041, Locality = "Innsbruck" },
            new { GemeindeCode = "70101", GemeindeName = "Innsbruck", PLZ = "6060", Lat = 47.2692, Lon = 11.4041, Locality = "Innsbruck" },
        };

        foreach (var g in tirolGemeinden)
        {
            units.Add(AustrianAdministrativeUnit.Create(
                bundeslandCode: "7",
                bundeslandName: "Tirol",
                bezirkCode: "703",
                bezirkName: "Innsbruck-Land",
                gemeindeCode: g.GemeindeCode,
                gemeindeName: g.GemeindeName,
                postalCode: g.PLZ,
                localityName: g.Locality,
                latitude: g.Lat,
                longitude: g.Lon));
        }

        // Also add Innsbruck-Stadt (Bezirk 701) entries
        var innsbruckGemeinden = new[]
        {
            new { GemeindeCode = "70101", GemeindeName = "Innsbruck", PLZ = "6020", Lat = 47.2692, Lon = 11.4041, Locality = "Innsbruck" },
        };

        foreach (var g in innsbruckGemeinden)
        {
            units.Add(AustrianAdministrativeUnit.Create(
                bundeslandCode: "7",
                bundeslandName: "Tirol",
                bezirkCode: "701",
                bezirkName: "Innsbruck",
                gemeindeCode: g.GemeindeCode,
                gemeindeName: g.GemeindeName,
                postalCode: g.PLZ,
                localityName: g.Locality,
                latitude: g.Lat,
                longitude: g.Lon));
        }

        return units;
    }

    private static ActivityCategory CreateCategory(
        string code,
        string nameKey,
        int defaultSafetyLevel,
        bool isBlocked,
        string? referralGroup = null)
    {
        var category = (ActivityCategory)Activator.CreateInstance(typeof(ActivityCategory), nonPublic: true)!;
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(category, Guid.CreateVersion7());
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Code))!.SetValue(category, code);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.NameKey))!.SetValue(category, nameKey);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.DefaultSafetyLevel))!.SetValue(category, defaultSafetyLevel);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.IsBlocked))!.SetValue(category, isBlocked);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.ReferralGroup))!.SetValue(category, referralGroup);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.IsActive))!.SetValue(category, true);
        return category;
    }
}
