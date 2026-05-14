using System.Collections.Generic;

/// Datos estáticos de los 8 dioses: nombres, descripciones, costes y efectos por tier.
/// Sincronizados con GOD_COSTS y la lógica de commands.ts en el servidor.
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
        public TierInfo[] Tiers; // índice 0 = tier 1, índice 1 = tier 2, índice 2 = tier 3
    }

    public static readonly Dictionary<string, GodInfo> All = new()
    {
        ["ThorsStrike"] = new GodInfo
        {
            InternalName = "ThorsStrike",
            DisplayName = "Golpe de Thor",
            Description = "Inflige daño al adversario tras la fase de resolución.",
            Priority = 6,
            Tiers = new[] {
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
            Tiers = new[] {
                new TierInfo { Cost = 6,  EffectText = "Hachas ×1.5" },
                new TierInfo { Cost = 10, EffectText = "Hachas ×2"   },
                new TierInfo { Cost = 18, EffectText = "Hachas ×3"   },
            }
        },
        ["SkadisHunt"] = new GodInfo
        {
            InternalName = "SkadisHunt",
            DisplayName = "Caza de Skadi",
            Description = "Multiplica el daño de tus Flechas antes de la resolución.",
            Priority = 2,
            Tiers = new[] {
                new TierInfo { Cost = 6,  EffectText = "Flechas ×2" },
                new TierInfo { Cost = 10, EffectText = "Flechas ×3" },
                new TierInfo { Cost = 14, EffectText = "Flechas ×4" },
            }
        },
        ["LokisTrick"] = new GodInfo
        {
            InternalName = "LokisTrick",
            DisplayName = "Trampa de Loki",
            Description = "Cancela dados del adversario antes de la resolución.",
            Priority = 1,
            Tiers = new[] {
                new TierInfo { Cost = 3, EffectText = "Cancela 1 dado rival"  },
                new TierInfo { Cost = 6, EffectText = "Cancela 2 dados rivales" },
                new TierInfo { Cost = 9, EffectText = "Cancela 3 dados rivales" },
            }
        },
        ["BragisVerve"] = new GodInfo
        {
            InternalName = "BragisVerve",
            DisplayName = "Brío de Bragi",
            Description = "Gana tokens extra por cada Mano confirmada.",
            Priority = 5,
            Tiers = new[] {
                new TierInfo { Cost = 4,  EffectText = "+2 tokens por Mano" },
                new TierInfo { Cost = 8,  EffectText = "+3 tokens por Mano" },
                new TierInfo { Cost = 12, EffectText = "+4 tokens por Mano" },
            }
        },
        ["IdunsRejuvenat"] = new GodInfo
        {
            InternalName = "IdunsRejuvenat",
            DisplayName = "Rejuvenecimiento de Idun",
            Description = "Recupera puntos de vida tras la resolución.",
            Priority = 5,
            Tiers = new[] {
                new TierInfo { Cost = 4,  EffectText = "Recupera 2 de vida" },
                new TierInfo { Cost = 7,  EffectText = "Recupera 4 de vida" },
                new TierInfo { Cost = 10, EffectText = "Recupera 6 de vida" },
            }
        },
        ["MimirsWisdom"] = new GodInfo
        {
            InternalName = "MimirsWisdom",
            DisplayName = "Sabiduría de Mimir",
            Description = "Gana tokens por cada punto de daño recibido.",
            Priority = 5,
            Tiers = new[] {
                new TierInfo { Cost = 3, EffectText = "+1 token por daño recibido" },
                new TierInfo { Cost = 5, EffectText = "+2 tokens por daño recibido" },
                new TierInfo { Cost = 7, EffectText = "+3 tokens por daño recibido" },
            }
        },
        ["VarsBond"] = new GodInfo
        {
            InternalName = "VarsBond",
            DisplayName = "Pacto de Var",
            Description = "Recupera vida por cada token que gaste el adversario.",
            Priority = 5,
            Tiers = new[] {
                new TierInfo { Cost = 10, EffectText = "+1 vida por token rival" },
                new TierInfo { Cost = 14, EffectText = "+2 vida por token rival" },
                new TierInfo { Cost = 18, EffectText = "+3 vida por token rival" },
            }
        },
    };

    /// Devuelve el tier más alto que el jugador puede pagar (1-3), o 0 si ninguno.
    /// Mismo algoritmo que getAffordableTier() en el servidor.
    public static int GetAffordableTier(string godName, int energy)
    {
        if (!All.TryGetValue(godName, out var info)) return 0;
        for (int t = info.Tiers.Length - 1; t >= 0; t--)
            if (energy >= info.Tiers[t].Cost) return t + 1;
        return 0;
    }

    public static bool CanAffordAny(string godName, int energy)
        => GetAffordableTier(godName, energy) > 0;
}