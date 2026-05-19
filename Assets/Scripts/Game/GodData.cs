using System.Collections.Generic;

/// Datos estáticos de los 4 dioses disponibles.
/// Sincronizados con GOD_COSTS en commands.ts.
public static class GodData
{
    public class TierInfo
    {
        public int Cost;
        public string EffectText;
    }

    public class GodInfo
    {
        public string InternalName;
        public string DisplayName;
        public string Description;
        public int Priority;
        public TierInfo[] Tiers; // índice 0 = tier 1, 1 = tier 2, 2 = tier 3
    }

    public static readonly Dictionary<string, GodInfo> All = new()
    {
        ["ThorsStrike"] = new GodInfo
        {
            InternalName = "ThorsStrike",
            DisplayName = "Golpe de Thor",
            Description = "Inflige daño al adversario tras la fase de resolución.",
            Priority = 6,
            Tiers = new[]
            {
                new TierInfo { Cost = 4,  EffectText = "Inflige 2 de daño" },
                new TierInfo { Cost = 8,  EffectText = "Inflige 5 de daño" },
                new TierInfo { Cost = 12, EffectText = "Inflige 8 de daño" },
            }
        },
        ["BrunhildsFury"] = new GodInfo
        {
            InternalName = "BrunhildsFury",
            DisplayName = "Furia de Brunilda",
            Description = "Multiplica el daño de tus Hachas antes de la resolución.",
            Priority = 2,
            Tiers = new[]
            {
                new TierInfo { Cost = 6,  EffectText = "Hachas \u00D72"  },
                new TierInfo { Cost = 10, EffectText = "Hachas \u00D73"  },
                new TierInfo { Cost = 14, EffectText = "Hachas \u00D74"  },
            }
        },
        ["SkadisHunt"] = new GodInfo
        {
            InternalName = "SkadisHunt",
            DisplayName = "Caza de Skadi",
            Description = "Multiplica el daño de tus Flechas antes de la resolución.",
            Priority = 2,
            Tiers = new[]
            {
                new TierInfo { Cost = 6,  EffectText = "Flechas \u00D72" },
                new TierInfo { Cost = 10, EffectText = "Flechas \u00D73" },
                new TierInfo { Cost = 14, EffectText = "Flechas \u00D74" },
            }
        },
        ["IdunsRejuvenat"] = new GodInfo
        {
            InternalName = "IdunsRejuvenat",
            DisplayName = "Rejuvenecimiento de Idun",
            Description = "Recupera vida al final de la resolución.",
            Priority = 4,
            Tiers = new[]
            {
                new TierInfo { Cost = 4,  EffectText = "Cura 2 de vida" },
                new TierInfo { Cost = 7,  EffectText = "Cura 4 de vida" },
                new TierInfo { Cost = 10, EffectText = "Cura 6 de vida" },
            }
        },
    };

    /// Devuelve true si el jugador puede pagar al menos el tier 1 del dios indicado.
    public static bool CanAffordAny(string godName, int energy)
    {
        if (!All.TryGetValue(godName, out var info)) return false;
        return energy >= info.Tiers[0].Cost;
    }

    /// Devuelve el tier más alto que el jugador puede pagar (1-3), o 0 si ninguno.
    public static int GetAffordableTier(string godName, int energy)
    {
        if (!All.TryGetValue(godName, out var info)) return 0;
        for (int t = info.Tiers.Length - 1; t >= 0; t--)
            if (energy >= info.Tiers[t].Cost) return t + 1;
        return 0;
    }
}