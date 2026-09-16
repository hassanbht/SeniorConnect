using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Geography.Domain;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Profiles.Domain;

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

        // 2b. Seed Referral Directory for the pilot region (P2-06, PSG-06)
        //
        // Deliberately incomplete: only groups with a confidently-sourced,
        // verified-format contact are seeded. "nursing_blocked" and
        // "heavy_construction_blocked" are skipped — the founder's own
        // research turned up conflicting/unverified coverage claims for
        // those two (see BUILD-CHECKLIST.md PSG-06). Seed those once a real
        // phone call confirms a provider, per this project's own P0-01/P0-02
        // "verify with a real call" standard — do not add scraped business
        // listings here without that call.
        if (!await db.ReferralDirectories.AnyAsync(cancellationToken))
        {
            var referralProviders = new List<ReferralDirectory>
            {
                ReferralDirectory.Create(
                    referralGroup: "Gesundheitsberatung & Notdienst 1450 / Rettungsdienst 144",
                    regionCode: "AT",
                    name: "Rettungsdienst (Notruf)",
                    phone: "144",
                    noteKey: "referral.medical.rettung"),
                ReferralDirectory.Create(
                    referralGroup: "Gesundheitsberatung & Notdienst 1450 / Rettungsdienst 144",
                    regionCode: "AT",
                    name: "Gesundheitsberatung Österreich",
                    phone: "1450",
                    noteKey: "referral.medical.gesundheitsberatung"),
                ReferralDirectory.Create(
                    referralGroup: "Staatliche Schuldenberatung & Arbeiterkammer (AK)",
                    regionCode: "703",
                    name: "Schuldnerberatung Tirol",
                    phone: "+43 512 577649",
                    website: "https://www.sbtirol.at",
                    address: "Wilhelm-Greil-Straße 23, 6020 Innsbruck"),
                ReferralDirectory.Create(
                    referralGroup: "Staatliche Schuldenberatung & Arbeiterkammer (AK)",
                    regionCode: "703",
                    name: "Arbeiterkammer Tirol",
                    phone: "0800 22 55 22",
                    website: "https://tirol.arbeiterkammer.at",
                    address: "Maximilianstraße 7, 6020 Innsbruck")
            };

            await db.ReferralDirectories.AddRangeAsync(referralProviders, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        // 3. Seed Pilot Organizations (P2-01)
        if (!await db.Organizations.AnyAsync(cancellationToken))
        {
            // Real named pilot partner per ADR-020 (MVP lockdown): Freiwilligenzentrum
            // Innsbruck-Land, Dorfplatz 2, 6175 Kematen in Tirol.
            // Contacts: Lea Gohm, Veronika Schneider.
            var org = Organization.Create(
                name: "Freiwilligenzentrum Innsbruck-Land",
                type: OrganizationType.Association,
                legalName: "Freiwilligenzentrum Innsbruck-Land",
                supportEmail: "fwz@regio-il.at",
                supportPhone: "+43 5232 27702");

            await db.Organizations.AddAsync(org, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            var branch = OrganizationBranch.Create(
                organizationId: org.Id,
                name: "Freiwilligenzentrum Innsbruck-Land (Hauptsitz)",
                address: "Dorfplatz 2",
                postalCode: "6175",
                city: "Kematen in Tirol",
                latitude: 47.2600,
                longitude: 11.2433);

            await db.OrganizationBranches.AddAsync(branch, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        // 3b. Seed Pilot Intake Form for FWZ Innsbruck-Land (ADR-021 / P2-40)
        var fwzOrg = await db.Organizations.FirstOrDefaultAsync(
            o => o.Name == "Freiwilligenzentrum Innsbruck-Land", cancellationToken);
        if (fwzOrg is not null && !await db.OrganizationIntakeForms.AnyAsync(f => f.OrganizationId == fwzOrg.Id, cancellationToken))
        {
            await SeedFwzIntakeFormAsync(db, fwzOrg.Id, cancellationToken);
        }

        // 4. Seed Reference Interests (matching the 8 standard Bereiche from FWZ volunteer intake form)
        if (!await db.Interests.AnyAsync(cancellationToken))
        {
            var interests = new List<Interest>
            {
                Interest.Create("social", "interest.social"),
                Interest.Create("nature", "interest.nature"),
                Interest.Create("e_volunteering", "interest.e_volunteering"),
                Interest.Create("climate_sustainability", "interest.climate_sustainability"),
                Interest.Create("crafts_creative", "interest.crafts_creative"),
                Interest.Create("arts_culture", "interest.arts_culture"),
                Interest.Create("volunteer_pool", "interest.volunteer_pool"),
                Interest.Create("tutoring", "interest.tutoring")
            };

            await db.Interests.AddRangeAsync(interests, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        // 5. Seed Reference Languages
        if (!await db.Languages.AnyAsync(cancellationToken))
        {
            var languages = new List<Language>
            {
                Language.Create("de", "language.de"),
                Language.Create("en", "language.en"),
                Language.Create("fa", "language.fa"),
                Language.Create("ar", "language.ar"),
                Language.Create("uk", "language.uk"),
                Language.Create("tr", "language.tr")
            };

            await db.Languages.AddRangeAsync(languages, cancellationToken);
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

    private static async Task SeedFwzIntakeFormAsync(SeniorConnectDbContext db, Guid organizationId, CancellationToken cancellationToken)
    {
        var form = OrganizationIntakeForm.Create(
            organizationId,
            FormType.Volunteer,
            "FWZ Innsbruck-Land: Interesse für Freiwilligentätigkeit",
            "Standard-Aufnahmeformular für Freiwillige nach Vorlage FWZ Innsbruck-Land");

        await db.OrganizationIntakeForms.AddAsync(form, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var section1 = IntakeFormSection.Create(form.Id, "Bereiche", "In welchen Bereichen möchten Sie sich engagieren?", 1);
        var section2 = IntakeFormSection.Create(form.Id, "Personengruppen", "Mit welchen Personengruppen möchten Sie arbeiten?", 2);
        var section3 = IntakeFormSection.Create(form.Id, "Zeitaufwand", "Wie viel Zeit können Sie einbringen?", 3);
        var section4 = IntakeFormSection.Create(form.Id, "Fähigkeiten & Anmerkungen", "Haben Sie besondere Fähigkeiten oder Anmerkungen?", 4);
        var section5 = IntakeFormSection.Create(form.Id, "Strafrechtliche Unbescholtenheit", "Bestätigung der Unbescholtenheit", 5);
        var section6 = IntakeFormSection.Create(form.Id, "Datenschutz", "Einwilligung zur Datenverarbeitung", 6);

        await db.IntakeFormSections.AddRangeAsync([section1, section2, section3, section4, section5, section6], cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var bereicheArray = new[] { "Soziales", "Natur", "E-Volunteering", "Klima und Nachhaltigkeit", "Handwerkliches / Kreatives", "Kunst und Kultur", "Freiwilligenpool", "Lernbetreuung" };
        var personengruppenArray = new[] { "Geflüchtete / Personen mit Migrationshintergrund", "Familien", "Senior:innen", "Menschen mit Behinderung", "Kinder und Jugendliche", "Sonstige" };
        var zeitaufwandArray = new[] { "einmalig", "regelmäßig (pro Woche)", "regelmäßig (pro Monat)", "regelmäßig (pro Jahr)", "Stundenanzahl", "Tageszeit", "Wochentag(e)", "Flexibel", "WhatsApp-Zustimmung zur Kontaktaufnahme" };

        var bereicheOptions = System.Text.Json.JsonSerializer.Serialize(bereicheArray);
        var personengruppenOptions = System.Text.Json.JsonSerializer.Serialize(personengruppenArray);
        var zeitaufwandOptions = System.Text.Json.JsonSerializer.Serialize(zeitaufwandArray);

        var fields = new List<IntakeFormField>
        {
            IntakeFormField.Create(section1.Id, "bereiche", "intake.field.bereiche", FieldType.MultiChoice, true, bereicheOptions, 1),
            IntakeFormField.Create(section2.Id, "personengruppen", "intake.field.personengruppen", FieldType.MultiChoice, true, personengruppenOptions, 1),
            IntakeFormField.Create(section3.Id, "zeitaufwand", "intake.field.zeitaufwand", FieldType.MultiChoice, true, zeitaufwandOptions, 1),
            IntakeFormField.Create(section4.Id, "faehigkeiten", "intake.field.faehigkeiten", FieldType.Textarea, false, null, 1),
            IntakeFormField.Create(section5.Id, "strafrechtliche_unbescholtenheit", "intake.field.strafrechtliche_unbescholtenheit", FieldType.Boolean, true, null, 1),
            IntakeFormField.Create(section6.Id, "gdpr_consent", "intake.field.gdpr_consent", FieldType.Boolean, true, null, 1),
            IntakeFormField.Create(section6.Id, "event_invitation_opt_in", "intake.field.event_invitation_opt_in", FieldType.Boolean, false, null, 2)
        };

        await db.IntakeFormFields.AddRangeAsync(fields, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
