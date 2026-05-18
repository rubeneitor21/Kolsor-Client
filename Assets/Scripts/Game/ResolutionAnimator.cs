using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResolutionAnimator : MonoBehaviour
{
    public static ResolutionAnimator Instance { get; private set; }

    [Header("Prefabs de armas (FBX)")]
    public GameObject arrowPrefab;
    public GameObject axePrefab;
    public GameObject shieldPrefab;
    public GameObject helmetPrefab;

    [Header("Mano (sprite)")]
    public Sprite handSprite;

    [Header("Ajustes de animación")]
    public float projectileSpeed = 2.5f;
    public float impactPauseSecs = 0.6f;
    public float zonePauseSecs = 0.25f;

    [Header("Escalas")]
    public float weaponScale = 0.8f;
    public float handScale = 0.05f;

    public System.Action OnAnimationComplete;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── AttackEvent ─────────────────────────────────────────────────────────

    private class AttackEvent
    {
        public float sortZ;
        public GameObject attackerDiceObj;
        public GameObject defenderDiceObj;
        public GameObject projectilePfb;
        public GameObject defenderPfb;
        public bool attacksStone;
        public bool isHandEffect;
        public bool handTargetIsPlayer;
        public bool targetIsPlayer;
        public bool attackerIsPlayer;  // para rotación correcta
        public DiceFace attackerFace;      // para rotación del proyectil
        public DiceFace defenderFace;      // para rotación del defensor
    }

    // ── Rotaciones por arma y propietario ────────────────────────────────────

    private static Quaternion GetWeaponRotation(DiceFace face, bool isPlayer)
    {
        if (isPlayer)
        {
            return face switch
            {
                DiceFace.Shield => Quaternion.Euler(0, 0, 90),
                DiceFace.Helmet => Quaternion.Euler(-90, 90, 0),
                DiceFace.Axe => Quaternion.Euler(0, 90, 0),
                DiceFace.Arrow => Quaternion.Euler(-90, 90, 0),
                _ => Quaternion.identity
            };
        }
        else
        {
            return face switch
            {
                DiceFace.Helmet => Quaternion.Euler(-90, 0, -90),
                DiceFace.Arrow => Quaternion.Euler(-90, -90, 0),
                DiceFace.Shield => Quaternion.Euler(0, 0, -90),
                DiceFace.Axe => Quaternion.Euler(0, -90, 0),
                _ => Quaternion.identity
            };
        }
    }

    private IEnumerator FlyToArc(GameObject obj, Vector3 target, float arcHeight = 0.5f)
    {
        if (obj == null) yield break;
        float dist = Vector3.Distance(obj.transform.position, target);
        float dur = Mathf.Max(0.1f, dist / projectileSpeed);
        float elapsed = 0f;
        Vector3 start = obj.transform.position;

        while (elapsed < dur)
        {
            if (obj == null) yield break;
            float t = elapsed / dur;
            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y += arcHeight * Mathf.Sin(t * Mathf.PI);
            obj.transform.position = pos;
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (obj != null) obj.transform.position = target;
    }

    // ── Punto de entrada ─────────────────────────────────────────────────────

    public IEnumerator PlayResolution(
        List<DiceData> startDice, List<DiceData> secondDice,
        List<GameObject> startObjs, List<GameObject> secondObjs,
        bool iAmStart)
    {
        var bm = BoardManager.Instance;
        if (bm == null) { OnAnimationComplete?.Invoke(); yield break; }

        var startIdx = GetIndicesByType(startDice);
        var secondIdx = GetIndicesByType(secondDice);
        var events = new List<AttackEvent>();

        bool startIsPlayer = iAmStart;
        bool secondIsPlayer = !iAmStart;

        // Flechas start vs Escudos second
        CollectZone(startIdx[DiceFace.Arrow], startObjs, arrowPrefab, DiceFace.Arrow,
                    secondIdx[DiceFace.Shield], secondObjs, shieldPrefab, DiceFace.Shield,
                    targetIsPlayer: !iAmStart, attackerIsPlayer: startIsPlayer, events);

        int arrowsBlocked = Mathf.Min(startIdx[DiceFace.Arrow].Count, secondIdx[DiceFace.Shield].Count);
        for (int i = arrowsBlocked; i < startIdx[DiceFace.Arrow].Count; i++)
            CollectAttack(startIdx[DiceFace.Arrow][i], startObjs, arrowPrefab, DiceFace.Arrow,
                          true, targetIsPlayer: !iAmStart, attackerIsPlayer: startIsPlayer, events);

        // Hachas start vs Cascos second
        CollectZone(startIdx[DiceFace.Axe], startObjs, axePrefab, DiceFace.Axe,
                    secondIdx[DiceFace.Helmet], secondObjs, helmetPrefab, DiceFace.Helmet,
                    targetIsPlayer: !iAmStart, attackerIsPlayer: startIsPlayer, events);

        int axesBlocked = Mathf.Min(startIdx[DiceFace.Axe].Count, secondIdx[DiceFace.Helmet].Count);
        for (int i = axesBlocked; i < startIdx[DiceFace.Axe].Count; i++)
            CollectAttack(startIdx[DiceFace.Axe][i], startObjs, axePrefab, DiceFace.Axe,
                          true, targetIsPlayer: !iAmStart, attackerIsPlayer: startIsPlayer, events);

        // Flechas second vs Escudos start
        CollectZone(secondIdx[DiceFace.Arrow], secondObjs, arrowPrefab, DiceFace.Arrow,
                    startIdx[DiceFace.Shield], startObjs, shieldPrefab, DiceFace.Shield,
                    targetIsPlayer: iAmStart, attackerIsPlayer: secondIsPlayer, events);

        int arrowsBlocked2 = Mathf.Min(secondIdx[DiceFace.Arrow].Count, startIdx[DiceFace.Shield].Count);
        for (int i = arrowsBlocked2; i < secondIdx[DiceFace.Arrow].Count; i++)
            CollectAttack(secondIdx[DiceFace.Arrow][i], secondObjs, arrowPrefab, DiceFace.Arrow,
                          true, targetIsPlayer: iAmStart, attackerIsPlayer: secondIsPlayer, events);

        // Hachas second vs Cascos start
        CollectZone(secondIdx[DiceFace.Axe], secondObjs, axePrefab, DiceFace.Axe,
                    startIdx[DiceFace.Helmet], startObjs, helmetPrefab, DiceFace.Helmet,
                    targetIsPlayer: iAmStart, attackerIsPlayer: secondIsPlayer, events);

        int axesBlocked2 = Mathf.Min(secondIdx[DiceFace.Axe].Count, startIdx[DiceFace.Helmet].Count);
        for (int i = axesBlocked2; i < secondIdx[DiceFace.Axe].Count; i++)
            CollectAttack(secondIdx[DiceFace.Axe][i], secondObjs, axePrefab, DiceFace.Axe,
                          true, targetIsPlayer: iAmStart, attackerIsPlayer: secondIsPlayer, events);

        // Manos start
        foreach (int idx in startIdx[DiceFace.Hand])
        {
            if (idx >= startObjs.Count || startObjs[idx] == null) continue;
            events.Add(new AttackEvent
            {
                sortZ = startObjs[idx].transform.position.z,
                attackerDiceObj = startObjs[idx],
                isHandEffect = true,
                handTargetIsPlayer = !iAmStart
            });
        }

        // Manos second
        foreach (int idx in secondIdx[DiceFace.Hand])
        {
            if (idx >= secondObjs.Count || secondObjs[idx] == null) continue;
            events.Add(new AttackEvent
            {
                sortZ = secondObjs[idx].transform.position.z,
                attackerDiceObj = secondObjs[idx],
                isHandEffect = true,
                handTargetIsPlayer = iAmStart
            });
        }

        events.Sort((a, b) => b.sortZ.CompareTo(a.sortZ));

        foreach (var ev in events)
        {
            if (ev.isHandEffect)
                yield return AnimateHandEffect(ev.attackerDiceObj, ev.handTargetIsPlayer, bm);
            else
                yield return ExecuteEvent(ev, bm);

            // Si algún jugador se queda sin piedras → terminar animación ahora mismo
            if (bm.GetPlayerStones().Count == 0 || bm.GetOpponentStones().Count == 0)
            {
                OnAnimationComplete?.Invoke();
                yield break;
            }
        }

        OnAnimationComplete?.Invoke();
    }

    // ── Constructores de eventos ─────────────────────────────────────────────

    private void CollectZone(
        List<int> atkIdx, List<GameObject> atkObjs, GameObject atkPfb, DiceFace atkFace,
        List<int> defIdx, List<GameObject> defObjs, GameObject defPfb, DiceFace defFace,
        bool targetIsPlayer, bool attackerIsPlayer, List<AttackEvent> events)
    {
        int pairs = Mathf.Min(atkIdx.Count, defIdx.Count);
        for (int i = 0; i < pairs; i++)
        {
            var atkObj = atkIdx[i] < atkObjs.Count ? atkObjs[atkIdx[i]] : null;
            var defObj = defIdx[i] < defObjs.Count ? defObjs[defIdx[i]] : null;
            if (atkObj == null) continue;
            events.Add(new AttackEvent
            {
                sortZ = atkObj.transform.position.z,
                attackerDiceObj = atkObj,
                defenderDiceObj = defObj,
                projectilePfb = atkPfb,
                defenderPfb = defPfb,
                attacksStone = false,
                isHandEffect = false,
                targetIsPlayer = targetIsPlayer,
                attackerIsPlayer = attackerIsPlayer,
                attackerFace = atkFace,
                defenderFace = defFace
            });
        }
    }

    private void CollectAttack(
        int atkIdx, List<GameObject> atkObjs, GameObject atkPfb, DiceFace atkFace,
        bool attacksStone, bool targetIsPlayer, bool attackerIsPlayer,
        List<AttackEvent> events)
    {
        var atkObj = atkIdx < atkObjs.Count ? atkObjs[atkIdx] : null;
        if (atkObj == null) return;
        events.Add(new AttackEvent
        {
            sortZ = atkObj.transform.position.z,
            attackerDiceObj = atkObj,
            projectilePfb = atkPfb,
            attacksStone = attacksStone,
            isHandEffect = false,
            targetIsPlayer = targetIsPlayer,
            attackerIsPlayer = attackerIsPlayer,
            attackerFace = atkFace
        });
    }

    // ── Ejecución de un evento ───────────────────────────────────────────────

    private IEnumerator ExecuteEvent(AttackEvent ev, BoardManager bm)
    {
        if (ev.attackerDiceObj == null) yield break;

        Vector3 dicePos = ev.attackerDiceObj.transform.position;
        Vector3 spawnPos = new Vector3(dicePos.x, 0.9f, dicePos.z);
        Quaternion projRot = GetWeaponRotation(ev.attackerFace, ev.attackerIsPlayer);

        Vector3 targetPos = Vector3.zero;
        GameObject defenderVisual = null;

        if (!ev.attacksStone)
        {
            if (ev.defenderDiceObj != null)
            {
                targetPos = ev.defenderDiceObj.transform.position;
                targetPos.y = 0.9f;
                Quaternion defRot = GetWeaponRotation(ev.defenderFace, !ev.attackerIsPlayer);
                defenderVisual = SpawnWeapon(ev.defenderPfb, targetPos, defRot);
            }
        }
        else
        {
            var stones = ev.targetIsPlayer ? bm.GetPlayerStones() : bm.GetOpponentStones();
            var stone = GetLast(stones);
            targetPos = stone != null ? stone.transform.position : dicePos + Vector3.forward;
        }

        GameObject projectile = SpawnWeapon(ev.projectilePfb, spawnPos, projRot);

        if (ev.attacksStone)
            yield return FlyToArc(projectile, targetPos);
        else
            yield return FlyTo(projectile, targetPos);

        yield return new WaitForSeconds(impactPauseSecs);

        if (projectile != null) Destroy(projectile);
        if (defenderVisual != null) Destroy(defenderVisual);

        // Eliminar la piedra impactada
        if (ev.attacksStone)
        {
            var stones = ev.targetIsPlayer ? bm.GetPlayerStones() : bm.GetOpponentStones();
            var stone = GetLast(stones);
            if (stone != null)
            {
                stones.Remove(stone);
                Destroy(stone);
            }
        }

        StartCoroutine(AnimateDiceReturn(ev.attackerDiceObj));
        if (ev.defenderDiceObj != null)
            StartCoroutine(AnimateDiceReturn(ev.defenderDiceObj));

        yield return new WaitForSeconds(zonePauseSecs);
    }

    // ── Manos ────────────────────────────────────────────────────────────────

    private IEnumerator AnimateHandEffect(GameObject diceObj, bool targetIsPlayer, BoardManager bm)
    {
        if (diceObj == null) yield break;

        var tokens = targetIsPlayer ? bm.GetPlayerTokens() : bm.GetOpponentTokens();
        var token = GetLast(tokens);

        if (token != null)
        {
            // Parpadeo del token robado
            var rend = token.GetComponent<Renderer>();
            for (int i = 0; i < 3; i++)
            {
                if (rend != null) rend.enabled = false;
                yield return new WaitForSeconds(0.1f);
                if (rend != null) rend.enabled = true;
                yield return new WaitForSeconds(0.1f);
            }

            // Quitar de la lista del rival
            tokens.Remove(token);
            token.SetActive(false);

            // Añadir token a la pila del atacante
            var destTokens = targetIsPlayer ? bm.GetOpponentTokens() : bm.GetPlayerTokens();
            var destOrigin = targetIsPlayer ? bm.enemyTokenOrigin : bm.myTokenOrigin;

            if (bm.tokenPrefab != null && destOrigin != null)
            {
                int count = destTokens.Count;
                int pile = count / 5;
                int idx = count % 5;
                Vector3 pos = destOrigin.position
                                    + new Vector3((idx + 1) * 0.05f,
                                                  (idx + 1) * 0.15f,
                                                   pile * 0.4f);
                var newToken = Object.Instantiate(
                    bm.tokenPrefab, pos, Quaternion.Euler(-90f, 0f, 0f));
                newToken.transform.localScale = Vector3.one;
                destTokens.Add(newToken);
            }
        }

        StartCoroutine(AnimateDiceReturn(diceObj));
        yield return new WaitForSeconds(zonePauseSecs);
    }

    // ── Animaciones ──────────────────────────────────────────────────────────

    private IEnumerator FlyTo(GameObject obj, Vector3 target)
    {
        if (obj == null) yield break;
        float dist = Vector3.Distance(obj.transform.position, target);
        float dur = Mathf.Max(0.1f, dist / projectileSpeed);
        float elapsed = 0f;
        Vector3 start = obj.transform.position;

        while (elapsed < dur)
        {
            if (obj == null) yield break;
            obj.transform.position = Vector3.Lerp(start, target, elapsed / dur);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (obj != null) obj.transform.position = target;
    }

    private IEnumerator AnimateDiceReturn(GameObject diceObj)
    {
        if (diceObj == null) yield break;
        float elapsed = 0f;
        float dur = 0.35f;
        Vector3 start = diceObj.transform.position;
        Vector3 target = start + Vector3.down * 1.5f;

        while (elapsed < dur)
        {
            if (diceObj == null) yield break;
            diceObj.transform.position = Vector3.Lerp(start, target, elapsed / dur);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (diceObj != null) diceObj.SetActive(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private GameObject SpawnWeapon(GameObject prefab, Vector3 pos, Quaternion rotation)
    {
        if (prefab == null) return null;
        var obj = Instantiate(prefab, pos, rotation);
        obj.transform.localScale = Vector3.one * weaponScale;
        foreach (var col in obj.GetComponentsInChildren<Collider>())
            col.enabled = false;
        return obj;
    }

    private GameObject SpawnHandProjectile(Vector3 pos)
    {
        var obj = new GameObject("HandProjectile");
        obj.transform.position = pos;
        obj.transform.localScale = Vector3.one * handScale;
        if (handSprite != null)
        {
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = handSprite;
            obj.AddComponent<BillboardFaceCamera>();
        }
        return obj;
    }

    private GameObject GetLast(List<GameObject> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i] != null) return list[i];
        return null;
    }

    private static Dictionary<DiceFace, List<int>> GetIndicesByType(List<DiceData> dice)
    {
        var result = new Dictionary<DiceFace, List<int>>
        {
            { DiceFace.Axe,    new List<int>() },
            { DiceFace.Arrow,  new List<int>() },
            { DiceFace.Hand,   new List<int>() },
            { DiceFace.Shield, new List<int>() },
            { DiceFace.Helmet, new List<int>() },
        };
        if (dice == null) return result;
        for (int i = 0; i < dice.Count; i++)
            if (result.ContainsKey(dice[i].face)) result[dice[i].face].Add(i);
        return result;
    }
}

public class BillboardFaceCamera : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}