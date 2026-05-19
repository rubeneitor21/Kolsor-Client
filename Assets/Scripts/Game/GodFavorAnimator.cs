using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Animaciones de favores divinos.
///
/// PRE-armas  (antes de PlayResolution): halo rojo en los dados afectados.
/// POST-armas (después de PlayResolution):
///   · Brunhild / Skadi: disparan armas extra en orden (defensores primero, luego piedras).
///     Las piedras desaparecen al impacto. Destruye los halos al terminar.
///   · Thor  : flechas sobre las piedras restantes del rival.
///   · Idun  : halo verde sobre las piedras propias.
public class GodFavorAnimator : MonoBehaviour
{
    public static GodFavorAnimator Instance { get; private set; }

    [Header("Timings")]
    public float haloHoldTime = 0.5f;  // pausa con halo antes de que vuelen armas originales
    public float arrowPause = 0.25f; // pausa entre flechas de Thor
    public float idunHoldTime = 1.0f;

    // Halos creados en PRE, destruidos en POST
    private readonly List<GameObject> _startHalos = new();
    private readonly List<GameObject> _secondHalos = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── PRE-armas: solo halos (sin armas) ───────────────────────────────────

    public IEnumerator PlayPreWeaponAnimations(
        string startGod, int startTier,
        string secondGod, int secondTier,
        List<DiceData> startDice, List<DiceData> secondDice,
        List<GameObject> startObjs, List<GameObject> secondObjs,
        bool iAmStart)
    {
        var bm = BoardManager.Instance;
        if (bm == null) yield break;

        if (IsMultiplier(startGod))
            AddHalos(_startHalos, startDice, startObjs, FaceFor(startGod), bm);

        if (IsMultiplier(secondGod))
            AddHalos(_secondHalos, secondDice, secondObjs, FaceFor(secondGod), bm);

        if (_startHalos.Count > 0 || _secondHalos.Count > 0)
            yield return new WaitForSeconds(haloHoldTime);

        // Idun: piedras de curación parpadean ANTES de que vuelen las armas
        if (startGod == "IdunsRejuvenat")
        {
            yield return AnimateIdun(invokerIsStart: true, iAmStart, bm, startTier);
            yield return new WaitForSeconds(0.2f);
        }
        if (secondGod == "IdunsRejuvenat")
            yield return AnimateIdun(invokerIsStart: false, iAmStart, bm, secondTier);
    }

    // ── POST-armas: destruir halos de Brunhild/Skadi; luego Thor; Idun ───────

    public IEnumerator PlayPostWeaponAnimations(
        string startGod, int startTier,
        string secondGod, int secondTier,
        List<DiceData> startDice, List<DiceData> secondDice,
        List<GameObject> startObjs, List<GameObject> secondObjs,
        bool iAmStart)
    {
        var bm = BoardManager.Instance;
        if (bm == null) yield break;

        // Destruir halos ahora que ya se animaron todas las armas
        DestroyList(_startHalos);
        DestroyList(_secondHalos);

        // Idun ya se animó en PRE; aquí solo Thor
        if (startGod == "ThorsStrike")
        {
            yield return AnimateThor(invokerIsStart: true, iAmStart, bm);
            yield return new WaitForSeconds(0.3f);
        }
        if (secondGod == "ThorsStrike")
        {
            yield return AnimateThor(invokerIsStart: false, iAmStart, bm);
            yield return new WaitForSeconds(0.3f);
        }

        // (Idun se procesa en PRE, no aquí)
    }

    // ── Helpers de clasificación ─────────────────────────────────────────────

    private static bool IsMultiplier(string g) => g == "BrunhildsFury" || g == "SkadisHunt";
    private static DiceFace FaceFor(string g) => g == "BrunhildsFury" ? DiceFace.Axe : DiceFace.Arrow;

    private static void AddHalos(List<GameObject> list,
        List<DiceData> dice, List<GameObject> objs, DiceFace face, BoardManager bm)
    {
        for (int i = 0; i < dice.Count && i < objs.Count; i++)
        {
            if (dice[i].face != face || objs[i] == null) continue;
            var h = bm.ApplyGodHalo(objs[i]);
            if (h != null) list.Add(h);
        }
    }

    private static void DestroyList(List<GameObject> list)
    {
        foreach (var o in list) if (o) Destroy(o);
        list.Clear();
    }

    // ── Thor ─────────────────────────────────────────────────────────────────

    private IEnumerator AnimateThor(bool invokerIsStart, bool iAmStart, BoardManager bm)
    {
        bool targetIsPlayer = invokerIsStart ? !iAmStart : iAmStart;
        var stones = targetIsPlayer ? bm.GetPlayerStones() : bm.GetOpponentStones();

        var gm = GameManager.Instance;
        int finalLife = (invokerIsStart == iAmStart) ? gm.OpponentLife : gm.MyLife;
        int stonesToRemove = Mathf.Max(0, stones.Count - finalLife);

        if (stonesToRemove == 0 || ResolutionAnimator.Instance == null) yield break;

        bool attackerIsPlayer = (invokerIsStart == iAmStart);
        Vector3? figurePos = bm.GetGodFigurePosition("ThorsStrike", attackerIsPlayer);

        for (int i = 0; i < stonesToRemove; i++)
        {
            var stone = stones.Count > 0 ? stones[stones.Count - 1] : null;
            if (stone == null) break;

            Vector3 target = stone.transform.position;
            Vector3 spawn = figurePos.HasValue
                ? new Vector3(figurePos.Value.x, 0.9f, figurePos.Value.z)
                : new Vector3(target.x, 0.9f, target.z + (attackerIsPlayer ? -3f : 3f));

            yield return ResolutionAnimator.Instance.FireArrowAt(spawn, target, attackerIsPlayer);
            stones.Remove(stone);
            Destroy(stone);
            yield return new WaitForSeconds(arrowPause);
        }
    }

    // ── Idun ─────────────────────────────────────────────────────────────────

    private IEnumerator AnimateIdun(bool invokerIsStart, bool iAmStart, BoardManager bm, int tier)
    {
        bool isMyStones = (invokerIsStart == iAmStart);

        int[] heals = { 2, 4, 6 };
        int healAmount = (tier >= 1 && tier <= 3) ? heals[tier - 1] : 2;

        // Añadir piedras reales a la lista de BoardManager (persistirán durante la animación)
        var healed = bm.SpawnHealStones(isMyStones, healAmount);
        if (healed.Count == 0) yield break;

        // Parpadeo igual que las fichas robadas (3 veces, 0.1 s on/off)
        for (int b = 0; b < 3; b++)
        {
            foreach (var s in healed)
            {
                if (s == null) continue;
                var rend = s.GetComponent<Renderer>();
                if (rend != null) rend.enabled = false;
            }
            yield return new WaitForSeconds(0.1f);
            foreach (var s in healed)
            {
                if (s == null) continue;
                var rend = s.GetComponent<Renderer>();
                if (rend != null) rend.enabled = true;
            }
            yield return new WaitForSeconds(0.1f);
        }

        yield return new WaitForSeconds(0.3f);
        // Las piedras se quedan — SpawnStones al final de la resolución las gestionará
    }

    // ── LerpScale (para Idun si se añade fade) ───────────────────────────────

    private IEnumerator LerpScale(List<GameObject> objs, Vector3 from, Vector3 to, float dur)
    {
        float elapsed = 0f;
        while (elapsed < dur)
        {
            float t = elapsed / dur;
            foreach (var o in objs) if (o) o.transform.localScale = Vector3.Lerp(from, to, t);
            elapsed += Time.deltaTime; yield return null;
        }
        foreach (var o in objs) if (o) o.transform.localScale = to;
    }
}