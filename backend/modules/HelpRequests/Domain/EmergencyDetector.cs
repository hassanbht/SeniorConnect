using System;
using System.Collections.Generic;

namespace SeniorConnect.Modules.HelpRequests.Domain;

public static class EmergencyDetector
{
    private static readonly HashSet<string> EmergencyKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "112", "144", "notarzt", "rettung", "atemnot", "herzinfarkt", "schlaganfall",
        "sturz", "bewusstlos", "blutung", "schwere schmerzen", "notfall", "ambulance",
        "emergency", "unconscious", "chest pain", "bleeding", "stroke"
    };

    public static bool IsEmergency(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var normalized = text.ToLowerInvariant();
        foreach (var keyword in EmergencyKeywords)
        {
            if (normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
