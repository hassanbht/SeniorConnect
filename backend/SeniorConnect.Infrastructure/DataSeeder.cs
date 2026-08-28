using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Infrastructure;

public static class DataSeeder
{
    public static async Task SeedInitialDataAsync(SeniorConnectDbContext db, CancellationToken cancellationToken = default)
    {
        // 1. Seed Activity Categories (Reference Taxonomy: P1-15, P2-06, PSG-06)
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

        // 2. Seed Pilot Organizations (P2-01)
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
