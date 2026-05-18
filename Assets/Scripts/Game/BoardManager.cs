using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }

    public List<GameObject> GetPlayerStones() => _playerStones;
    public List<GameObject> GetOpponentStones() => _opponentStones;
    public List<GameObject> GetPlayerTokens() => _myTokenObjects;
    public List<GameObject> GetOpponentTokens() => _enemyTokenObjects;

    [Header("Materiales de caras de dado")]
    public Material matHacha;
    public Material matFlecha;
    public Material matCasco;
    public Material matEscudo;
    public Material matMano;

    [Header("Texturas de caras de dado")]
    public Texture2D texFlecha;
    public Texture2D texHacha;
    public Texture2D texCasco;
    public Texture2D texEscudo;
    public Texture2D texMano;

    [Header("Prefabs")]
    public GameObject dicePrefab;
    public GameObject stonePrefab;

    [Header("Piedras de vida")]
    public Transform playerStonesOrigin;
    public Transform opponentStonesOrigin;

    [Header("Posiciones cuencos")]
    public Transform myBowlCenter;
    public Transform enemyBowlCenter;

    [Header("Fila de preview de selección (Z: -3)")]
    public Transform myKeptRowOrigin;
    public Transform enemyKeptRowOrigin;
    public float keptRowSpacing = 0.55f;
    public float keptYOffset = 0.4f;
    // La fila de dados se extiende siempre en el eje X del mundo, igual que BowlOffsets.
    [Tooltip("Z de la fila definitiva de dados confirmados (ambos jugadores)")]
    public float confirmedRowZ = 3.0f;

    [Header("Animación de tirada")]
    public float rollDuration = 1.0f;
    public float rollFaceChangeRate = 0.05f;

    [Header("Animación de confirmación")]
    public float confirmAnimDuration = 0f; // la animación real la hace SpawnConfirmedRow+MoveObject

    [Header("Figuras de dioses")]
    // Orden: [0]BrunhildsFury [1]SkadisHunt [2]ThorsStrike [3]LokisTrick
    //        [4]BragisVerve  [5]IdunsRejuvenat [6]MimirsWisdom [7]VarsBond
    public GameObject[] godPrefabs;
    public float godFigureScale = 1f;

    public Vector3[] myGodPositions = { new Vector3(-2f, 0.15f, -2f), new Vector3(-2f, 0.15f, -3f) };
    public Vector3 myGodRotation = new Vector3(-90f, -90f, 0f);
    public Vector3[] enemyGodPositions = { new Vector3(2f, 0.15f, 2f), new Vector3(2f, 0.15f, 3f) };
    public Vector3 enemyGodRotation = new Vector3(-90f, 90f, 0f);

    [Header("Fichas de energía")]
    public GameObject tokenPrefab;
    public Transform myTokenOrigin;
    public Transform enemyTokenOrigin;
    public float tokenSpacing = 0.32f;


    [Header("Material del halo dorado")]
    public Material energyHaloMaterial;

    private List<GameObject> _playerStones = new();
    private List<GameObject> _opponentStones = new();
    private List<GameObject> _myBowlObjects = new();
    private List<GameObject> _enemyBowlObjects = new();
    private List<GameObject> _myConfirmedObjects = new();
    private List<GameObject> _enemyConfirmedObjects = new();
    private List<GameObject> _myGodObjects = new();
    private List<GameObject> _enemyGodObjects = new();
    private List<GameObject> _myTokenObjects = new();
    private List<GameObject> _enemyTokenObjects = new();
    // Mapa nombre de dios → índice de prefab en godPrefabs[]
    private static readonly System.Collections.Generic.Dictionary<string, int> GodPrefabIndex =
        new System.Collections.Generic.Dictionary<string, int>
    {
        {"BrunhildsFury", 0}, {"SkadisHunt", 1}, {"ThorsStrike", 2}, {"LokisTrick",    3},
        {"BragisVerve",   4}, {"IdunsRejuvenat", 5}, {"MimirsWisdom", 6}, {"VarsBond", 7}
    };

    // Orden de seleccion: indices de _myBowlObjects en el orden en que el jugador los selecciono.
    private List<int> _selectionOrder = new();
    // Coroutines activas de movimiento por objeto, para cancelarlas antes de iniciar una nueva.
    private Dictionary<GameObject, Coroutine> _moveCoroutines = new();

    private static readonly Vector3[] BowlOffsets =
    {
        new Vector3(-0.55f, 0, -0.3f),
        new Vector3( 0.00f, 0, -0.3f),
        new Vector3( 0.55f, 0, -0.3f),
        new Vector3(-0.55f, 0,  0.3f),
        new Vector3( 0.00f, 0,  0.3f),
        new Vector3( 0.55f, 0,  0.3f),
    };

    private static readonly Dictionary<DiceFace, Color> FaceColors = new()
    {
        { DiceFace.Axe,    new Color(0.80f, 0.15f, 0.15f) },
        { DiceFace.Arrow,  new Color(0.15f, 0.60f, 0.80f) },
        { DiceFace.Helmet, new Color(0.60f, 0.60f, 0.60f) },
        { DiceFace.Shield, new Color(0.20f, 0.65f, 0.25f) },
        { DiceFace.Hand,   new Color(0.85f, 0.65f, 0.10f) },
    };

    private static readonly DiceFace[] AllFaces =
        { DiceFace.Axe, DiceFace.Arrow, DiceFace.Helmet, DiceFace.Shield, DiceFace.Hand };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        Debug.Log("[Board] BoardManager.Start()");
        SpawnStones();
        // GameManager.Start ya llama a RebuildAll, no hace falta aquí
    }

    public void RebuildAll()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // ── Capturar posiciones antes de destruir ───────────────
        // Mis dados: los seleccionados están en el preview row.
        int prevMyConfirmed = _myConfirmedObjects.Count;
        int prevEnemyConfirmed = _enemyConfirmedObjects.Count;

        // Snapshot de posiciones reales antes de destruir los objetos.
        // Usamos _selectionOrder para saber qué bowl-objects están en el preview row:
        //   - Si el dado fue seleccionado manualmente → está en el preview row (Z: -2)
        //   - Si fue auto-confirmado en tirada 3   → sigue en el bowl
        // De esta forma los dados siempre animan desde donde están físicamente.
        var selectionOrderSnapshot = new List<int>(_selectionOrder);
        var myBowlPositions = _myBowlObjects
            .Select(o => o != null ? o.transform.position : Vector3.zero)
            .ToList();

        int newMyCount = (gm.MyConfirmed?.Count ?? 0) - prevMyConfirmed;
        var myNewStarts = new List<Vector3>();
        for (int i = 0; i < newMyCount; i++)
        {
            // Si tenemos orden de selección, el slot i-ésimo corresponde al dado en _selectionOrder[i]
            int bowlIdx = i < selectionOrderSnapshot.Count ? selectionOrderSnapshot[i] : i;
            myNewStarts.Add(bowlIdx < myBowlPositions.Count ? myBowlPositions[bowlIdx] : Vector3.zero);
        }

        // Dados del rival: sus posiciones actuales en el bowl
        var enemyNewStarts = _enemyBowlObjects
            .Where(o => o != null)
            .Select(o => o.transform.position)
            .ToList();

        ClearBowlsAndKept();

        // ── Cuenco propio ──────────────────────────────────────
        bool myBowlEmpty = (gm.MyDice == null || gm.MyDice.Count == 0);

        if (gm.MyDice != null && gm.MyDice.Count > 0 && gm.MyDiceRolled)
        {
            // Dados reales de mi tirada actual (clicables para seleccionar)
            for (int i = 0; i < gm.MyDice.Count && i < BowlOffsets.Length; i++)
            {
                Vector3 pos = myBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
                _myBowlObjects.Add(SpawnDice(gm.MyDice[i], pos, isMine: true, decorative: false));
            }
        }
        else if (gm.MySurvivors != null && gm.MySurvivors.Count > 0)
        {
            // Sobrantes de la última confirmación: visibles mientras es el turno del rival.
            for (int i = 0; i < gm.MySurvivors.Count && i < BowlOffsets.Length; i++)
            {
                Vector3 pos = myBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
                _myBowlObjects.Add(SpawnDice(gm.MySurvivors[i], pos, isMine: false, decorative: true));
            }
        }
        else if (myBowlEmpty && gm.MyConfirmed.Count == 0)
        {
            // Estado inicial (nadie ha confirmado nada aún): decorativos de mi cuenco.
            var (myFaces, _) = GetDecorativeFaces();
            SpawnFromFaces(myFaces, myBowlCenter, _myBowlObjects, isMine: true, decorative: true);
        }
        // En cualquier otro caso el cuenco propio queda vacío (esperando tirar).

        // ── Cuenco rival ────────────────────────────────────────
        if (gm.EnemyCurrentBowl != null && gm.EnemyCurrentBowl.Count > 0)
        {
            // El rival ha tirado: mostramos sus dados actuales (o sobrantes).
            for (int i = 0; i < gm.EnemyCurrentBowl.Count && i < BowlOffsets.Length; i++)
            {
                Vector3 pos = enemyBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
                _enemyBowlObjects.Add(SpawnDice(gm.EnemyCurrentBowl[i], pos, isMine: false, decorative: true));
            }
        }
        else if (gm.EnemyConfirmed.Count == 0)
        {
            // El rival no ha confirmado nada aún: decorativos.
            // Cubre el inicio de partida Y el estado "yo confirmé, rival aún no tira".
            var (_, enemyFaces) = GetDecorativeFaces();
            SpawnFromFaces(enemyFaces, enemyBowlCenter, _enemyBowlObjects, isMine: false, decorative: true);
        }
        // Si el rival ya confirmó y EnemyCurrentBowl está vacío → cuenco vacío.

        // ── Filas de dados confirmados ───────────────────────────
        // Mis dados: los ya confirmados aparecen en su sitio; los nuevos animán desde preview.
        // Mis confirmados: dado ya confirmado antes → sin animación (Vector3.zero).
        //                   dado nuevo           → animar desde preview row.
        var myStarts = new List<Vector3>();
        for (int i = 0; i < (gm.MyConfirmed?.Count ?? 0); i++)
        {
            int newIdx = i - prevMyConfirmed;
            myStarts.Add(i < prevMyConfirmed || newIdx >= myNewStarts.Count
                ? Vector3.zero
                : myNewStarts[newIdx]);
        }

        // Dados del rival: igual, pero los nuevos parten del bowl del rival.
        var enemyStarts = new List<Vector3>();
        for (int i = 0; i < (gm.EnemyConfirmed?.Count ?? 0); i++)
        {
            int newIdx = i - prevEnemyConfirmed;
            enemyStarts.Add(i < prevEnemyConfirmed || newIdx >= enemyNewStarts.Count
                ? Vector3.zero
                : enemyNewStarts[newIdx]);
        }

        SpawnConfirmedRow(gm.MyConfirmed, myKeptRowOrigin, _myConfirmedObjects, myStarts, isMine: false);
        SpawnConfirmedRow(gm.EnemyConfirmed, enemyKeptRowOrigin, _enemyConfirmedObjects, enemyStarts, isMine: true);

        SpawnTokens(gm.MyEnergy, gm.OpponentEnergy);
    }

    /// Devuelve las caras decorativas (mías, del rival) generadas con la semilla
    /// del RoomId. Ambos clientes obtienen el mismo resultado porque comparten el RoomId.
    private (List<DiceData> myFaces, List<DiceData> enemyFaces) GetDecorativeFaces()
    {
        int seed = string.IsNullOrEmpty(GameData.RoomId) ? 0 : GameData.RoomId.GetHashCode();
        var rng = new System.Random(seed);
        var startFaces = GenerateFaces(rng, 6);
        var secondFaces = GenerateFaces(rng, 6);
        bool iAmStart = (GameData.MyId == GameData.PlayerStartId);
        return iAmStart ? (startFaces, secondFaces) : (secondFaces, startFaces);
    }

    private void SpawnDecorativeBowls()
    {
        var (myFaces, enemyFaces) = GetDecorativeFaces();
        SpawnFromFaces(myFaces, myBowlCenter, _myBowlObjects, isMine: true, decorative: true);
        SpawnFromFaces(enemyFaces, enemyBowlCenter, _enemyBowlObjects, isMine: false, decorative: true);
    }

    /// Crea la fila de dados confirmados en la posición definitiva.
    /// El origen de preview está en Z: -3; la posición definitiva se obtiene
    /// negando la Z para que quede en Z: +3 (el lado lejano del tablero).
    /// Crea la fila de dados confirmados animando cada dado desde su posición anterior.
    /// startPositions: dónde estaba cada dado antes de confirmar.
    ///   - Mis dados: posiciones del preview row (Z: -2)
    ///   - Dados del rival: posiciones del bowl del rival
    ///   - Si startPositions[i] == Vector3.zero → sin animación (dado ya estaba confirmado)
    private void SpawnConfirmedRow(List<DiceData> dice, Transform previewOrigin,
                               List<GameObject> list, List<Vector3> startPositions,
                               bool isMine)
    {
        if (dice == null || previewOrigin == null) return;
        var p = previewOrigin.position;
        Vector3 confirmedOrigin = new Vector3(p.x, p.y, confirmedRowZ);

        Quaternion baseRot = isMine
            ? Quaternion.identity
            : Quaternion.Euler(0f, 180f, 0f);

        for (int i = 0; i < dice.Count; i++)
        {
            Vector3 endPos = confirmedOrigin + new Vector3(0f, 0f, -1f) * i * keptRowSpacing + Vector3.up * keptYOffset;
            bool hasStart = startPositions != null && i < startPositions.Count && startPositions[i] != Vector3.zero;
            Vector3 startPos = hasStart ? startPositions[i] : endPos;
            var obj = SpawnDice(dice[i], startPos, isMine: false, decorative: true);
            if (!isMine && FaceRotations.TryGetValue(dice[i].face, out Vector3 faceRot))
                obj.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(faceRot);
            list.Add(obj);
            if (hasStart) MoveObject(obj, endPos);
        }
    }

    /// Animación de mi tirada con caras reales finales.
    public IEnumerator AnimateMyRoll(List<DiceData> finalDice)
    {
        // Si el cuenco aún tiene decorativos, los reusamos.
        // Si está vacío (caso típico tras una confirmación), los creamos.
        if (_myBowlObjects.Count == 0)
        {
            for (int i = 0; i < finalDice.Count && i < BowlOffsets.Length; i++)
            {
                var startData = new DiceData
                {
                    face = AllFaces[Random.Range(0, AllFaces.Length)],
                    energy = false
                };
                Vector3 pos = myBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
                _myBowlObjects.Add(SpawnDice(startData, pos, isMine: true, decorative: true));
            }
        }

        yield return AnimateBowl(_myBowlObjects, myBowlCenter, finalDice);

        // Tras la animación, los dados ya tienen las caras reales.
        // Ahora hay que conectarles el DiceData real para que sean clicables.
        for (int i = 0; i < _myBowlObjects.Count && i < finalDice.Count; i++)
        {
            var ctrl = _myBowlObjects[i].GetComponent<DiceController>();
            if (ctrl == null) ctrl = _myBowlObjects[i].AddComponent<DiceController>();
            ctrl.enabled = true;
            ctrl.Init(finalDice[i]);
            Vector3 basePos = myBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
            ctrl.SetRestPosition(basePos);
            ctrl.ApplyVisual();
        }
    }

    /// Animación de la tirada del rival con caras reales finales.
    /// BUGFIX (Bug 3 + Bug 4): limpiamos el cuenco rival antes de crear dados nuevos.
    /// El problema original era que el bloque de creación de dados no estaba dentro
    /// del "if (_enemyBowlObjects.Count == 0)" por una llave mal colocada, causando
    /// que se añadieran 6 dados nuevos encima de los 6 decorativos existentes (12 total).
    /// Los 6 sobrantes quedaban con colores aleatorios de la animación, superpuestos
    /// visualmente a los que sí tenían las caras correctas.
    public IEnumerator AnimateEnemyRoll(List<DiceData> finalDice)
    {
        Debug.Log($"[Board] AnimateEnemyRoll START | _enemyBowlObjects.Count={_enemyBowlObjects.Count} | finalDice.Count={(finalDice?.Count ?? 0)}");

        // Destruimos los objetos existentes (decorativos o de tiradas anteriores)
        // antes de crear los nuevos para la animación.
        foreach (var obj in _enemyBowlObjects) if (obj) Destroy(obj);
        _enemyBowlObjects.Clear();

        // Creamos los dados de animación (caras aleatorias que luego se resuelven)
        for (int i = 0; i < finalDice.Count && i < BowlOffsets.Length; i++)
        {
            var startData = new DiceData
            {
                face = AllFaces[Random.Range(0, AllFaces.Length)],
                energy = false
            };
            Vector3 pos = enemyBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
            _enemyBowlObjects.Add(SpawnDice(startData, pos, isMine: false, decorative: true));
        }

        yield return AnimateBowl(_enemyBowlObjects, enemyBowlCenter, finalDice);

        Debug.Log($"[Board] AnimateEnemyRoll END — dados finales aplicados:");
        for (int i = 0; i < _enemyBowlObjects.Count && i < finalDice.Count; i++)
        {
            Debug.Log($"[Board]   Enemy[{i}] = {finalDice[i].face} energy:{finalDice[i].energy}");
        }
    }

    /// Animación común para cualquier cuenco.
    private IEnumerator AnimateBowl(List<GameObject> objects, Transform center, List<DiceData> finalDice)
    {
        if (objects == null || objects.Count == 0 || center == null) yield break;
        if (finalDice == null) finalDice = new List<DiceData>();

        int count = Mathf.Min(objects.Count, BowlOffsets.Length);

        float elapsed = 0f;
        float nextChange = 0f;

        while (elapsed < rollDuration)
        {
            if (elapsed >= nextChange)
            {
                for (int i = 0; i < count; i++)
                {
                    if (objects[i] == null) continue;
                    var randomFace = AllFaces[Random.Range(0, AllFaces.Length)];
                    var fakeData = new DiceData { face = randomFace, energy = false };
                    ApplyDiceColor(objects[i], fakeData);
                }
                nextChange = elapsed + rollFaceChangeRate;
            }

            float bounce = Mathf.Abs(Mathf.Sin(elapsed * 20f)) * 0.15f;
            for (int i = 0; i < count; i++)
            {
                if (objects[i] == null) continue;
                Vector3 basePos = center.position + BowlOffsets[i] + Vector3.up * 0.3f;
                objects[i].transform.position = basePos + Vector3.up * bounce;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Caras reales al final, solo en los índices que existen en ambas listas
        int finalCount = Mathf.Min(count, finalDice.Count);
        for (int i = 0; i < finalCount; i++)
        {
            if (objects[i] == null) continue;
            ApplyDiceColor(objects[i], finalDice[i]);
            Vector3 basePos = center.position + BowlOffsets[i] + Vector3.up * 0.3f;
            objects[i].transform.position = basePos;
        }
    }

    /// Animación de los dados kept moviéndose a la fila confirmada.
    /// Si wasMe = true, anima los míos; si no, los del rival.
    public IEnumerator AnimateConfirmation(bool wasMe)
    {
        // Por simplicidad y robustez, hacemos una pausa breve para que el
        // jugador vea visualmente que algo pasa, y luego RebuildAll
        // colocará todo en su sitio.
        yield return new WaitForSeconds(confirmAnimDuration);
    }

    // ── Selección interactiva ─────────────────────────────────

    /// Recalcula las posiciones de todos los dados del cuenco propio:
    /// - Los marcados como kept se mueven a la fila de preview (myKeptRowOrigin, Z: -3).
    /// - Los no marcados vuelven a su posición de reposo en el cuenco.
    /// Llamado desde DiceController.ToggleKeep().
    public void RefreshSelectionRow()
    {
        // ── Actualizar orden de selección ──────────────────────
        _selectionOrder.RemoveAll(idx =>
        {
            if (idx >= _myBowlObjects.Count || _myBowlObjects[idx] == null) return true;
            var c = _myBowlObjects[idx].GetComponent<DiceController>();
            return c == null || c.Data == null || !c.Data.kept;
        });
        for (int i = 0; i < _myBowlObjects.Count; i++)
        {
            if (_selectionOrder.Contains(i)) continue;
            var c = _myBowlObjects[i]?.GetComponent<DiceController>();
            if (c != null && c.Data != null && c.Data.kept) _selectionOrder.Add(i);
        }

        // ── Diagnóstico ────────────────────────────────────────
        if (myKeptRowOrigin == null)
        {
            Debug.LogError("[Board] myKeptRowOrigin es NULL — asígnalo en el Inspector.");
            return;
        }
        Debug.Log($"[Board] RefreshSelectionRow | origin={myKeptRowOrigin.position} | seleccionados={_selectionOrder.Count} | bowlObjects={_myBowlObjects.Count}");

        Vector3 origin = myKeptRowOrigin.position;

        // ── Colocar dados seleccionados ─────────────────────────
        // Snap instantáneo (sin coroutines) para eliminar conflictos de timing.
        // Espaciado a lo largo del eje X del mundo (igual que BowlOffsets).
        for (int slot = 0; slot < _selectionOrder.Count; slot++)
        {
            var obj = _myBowlObjects[_selectionOrder[slot]];
            if (obj == null) continue;
            Vector3 target = new Vector3(
                origin.x,
                0.5f,  // por encima del tablero
                origin.z - slot * keptRowSpacing
            );
            Debug.Log($"[Board]   slot={slot} → target={target} | obj={obj.name}");
            MoveObject(obj, target);
        }

        // ── Devolver no seleccionados al cuenco ─────────────────
        for (int i = 0; i < _myBowlObjects.Count; i++)
        {
            if (_selectionOrder.Contains(i)) continue;
            var obj = _myBowlObjects[i];
            if (obj == null) continue;
            var ctrl = obj.GetComponent<DiceController>();
            if (ctrl == null) continue;
            StartCoroutine(MoveDiceTo(obj, ctrl.RestPosition));
        }
    }

    /// Inicia el movimiento suave de un objeto, cancelando cualquier coroutine previa.
    /// Usado por AnimateBowl y MoveDiceTo — NO por RefreshSelectionRow (que hace snap).
    private void MoveObject(GameObject obj, Vector3 target)
    {
        if (_moveCoroutines.TryGetValue(obj, out var prev) && prev != null)
            StopCoroutine(prev);
        _moveCoroutines[obj] = StartCoroutine(MoveDiceTo(obj, target));
    }

    /// Devuelve el DiceController bajo el cursor de pantalla.
    /// Combina raycast preciso con fallback de distancia en pantalla para
    /// colliders pequeños o vistos en ángulo.
    public DiceController GetHoveredDie(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return null;

        // Intento 1: raycast normal (fiable si el cursor está sobre la cara del dado)
        Ray ray = cam.ScreenPointToRay(screenPos);
        var hits = Physics.RaycastAll(ray, 100f);
        foreach (var hit in hits)
        {
            var ctrl = hit.collider.GetComponent<DiceController>();
            if (ctrl != null && ctrl.enabled && ctrl.Data != null)
                return ctrl;
        }

        // Intento 2: proximidad en pantalla para los dados del cuenco.
        // Util cuando el collider es pequeño o la cámara lo ve muy de lado.
        const float pixelThreshold = 45f;
        DiceController closest = null;
        float closestDist = pixelThreshold;

        foreach (var obj in _myBowlObjects)
        {
            if (obj == null) continue;
            var ctrl = obj.GetComponent<DiceController>();
            if (ctrl == null || !ctrl.enabled || ctrl.Data == null) continue;

            Vector3 sp = cam.WorldToScreenPoint(obj.transform.position);
            if (sp.z < 0) continue; // detrás de la cámara
            float d = Vector2.Distance(screenPos, new Vector2(sp.x, sp.y));
            if (d < closestDist) { closestDist = d; closest = ctrl; }
        }
        return closest;
    }

    private IEnumerator MoveDiceTo(GameObject obj, Vector3 target)
    {
        const float duration = 0.18f;
        float elapsed = 0f;
        Vector3 start = obj != null ? obj.transform.position : target;
        while (elapsed < duration)
        {
            if (obj == null) yield break;
            obj.transform.position = Vector3.Lerp(start, target, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (obj != null) obj.transform.position = target;
    }

    // ── Dioses y fichas ──────────────────────────────────────

    /// Instancia las 2 figuras de dios de cada jugador según sus dioses seleccionados.
    public void SpawnGodFigures()
    {
        ClearGodFigures();

        Debug.Log($"[Board] SpawnGodFigures | prefabs={godPrefabs?.Length} | MyGods={string.Join(",", GameData.MySelectedGods ?? new string[0])} | EnemyGods={string.Join(",", GameData.OpponentSelectedGods ?? new string[0])}");

        if (godPrefabs == null || godPrefabs.Length < 8) { Debug.LogWarning("[Board] godPrefabs no asignados o faltan prefabs"); return; }

        string[] myGods = GameData.MySelectedGods;
        string[] enemyGods = GameData.OpponentSelectedGods;

        if (myGods == null || myGods.Length < 2) { Debug.LogWarning($"[Board] MySelectedGods vacío o insuficiente: {myGods?.Length}"); return; }
        if (enemyGods == null || enemyGods.Length < 2) { Debug.LogWarning($"[Board] OpponentSelectedGods vacío o insuficiente: {enemyGods?.Length}"); return; }

        SpawnGodFigure(myGods[0], myGodPositions, 0, myGodRotation, _myGodObjects, isInteractable: false);
        SpawnGodFigure(myGods[1], myGodPositions, 1, myGodRotation, _myGodObjects, isInteractable: false);
        SpawnGodFigure(enemyGods[0], enemyGodPositions, 0, enemyGodRotation, _enemyGodObjects, isInteractable: false);
        SpawnGodFigure(enemyGods[1], enemyGodPositions, 1, enemyGodRotation, _enemyGodObjects, isInteractable: false);

        Debug.Log($"[Board] SpawnGodFigures completado | myGods={_myGodObjects.Count} | enemyGods={_enemyGodObjects.Count}");
    }

    /// Activa la interacción en las figuras propias para la fase god-favor.
    public void EnableGodFavorInteraction()
    {
        foreach (var obj in _myGodObjects)
        {
            if (obj == null) continue;
            var ctrl = obj.GetComponent<GodFavorController>();
            if (ctrl != null) ctrl.IsInteractable = true;
        }
    }

    /// Desactiva la interacción en todas las figuras (tras elegir o al resolver).
    public void DisableGodFavorInteraction()
    {
        foreach (var obj in _myGodObjects)
        {
            if (obj == null) continue;
            var ctrl = obj.GetComponent<GodFavorController>();
            if (ctrl != null) ctrl.IsInteractable = false;
        }
    }

    private void SpawnGodFigure(string godName, Vector3[] positions, int slot,
                                 Vector3 rotEuler, List<GameObject> list, bool isInteractable)
    {
        if (!GodPrefabIndex.TryGetValue(godName, out int prefabIdx))
        {
            Debug.LogWarning($"[Board] Dios '{godName}' no encontrado en GodPrefabIndex");
            return;
        }
        if (prefabIdx < 0 || prefabIdx >= godPrefabs.Length || godPrefabs[prefabIdx] == null)
        {
            Debug.LogWarning($"[Board] Prefab para '{godName}' (idx={prefabIdx}) no asignado en Inspector");
            return;
        }
        if (slot >= positions.Length)
        {
            Debug.LogWarning($"[Board] Slot {slot} fuera de rango");
            return;
        }

        Vector3 pos = positions[slot];
        Quaternion rot = Quaternion.Euler(rotEuler);
        var obj = Instantiate(godPrefabs[prefabIdx], pos, rot);
        obj.transform.localScale = Vector3.one * godFigureScale;

        if (obj.GetComponent<Collider>() == null)
            obj.AddComponent<BoxCollider>();

        var ctrl = obj.AddComponent<GodFavorController>();
        ctrl.GodName = godName;
        ctrl.IsInteractable = isInteractable;
        list.Add(obj);
    }


    /// Instancia las fichas doradas de energía de cada jugador.
    public void SpawnTokens(int myCount, int enemyCount)
    {
        if (tokenPrefab == null) return;
        ClearTokens();
        SpawnTokenPiles(myCount, myTokenOrigin, _myTokenObjects);
        SpawnTokenPiles(enemyCount, enemyTokenOrigin, _enemyTokenObjects);
    }

    private void SpawnTokenPiles(int count, Transform origin, List<GameObject> list)
    {
        if (origin == null || count <= 0) return;

        int stackSize = 5;
        float deltaX = 0.05f;   // +X por cada token dentro del montón
        float deltaY = 0.15f;   // +Y por cada token dentro del montón
        float pileGapZ = 0.4f;    // separación entre montones (en Z)

        Quaternion rot = Quaternion.Euler(-90f, 0f, 0f);

        for (int i = 0; i < count; i++)
        {
            int pile = i / stackSize;
            int indexInPile = i % stackSize;

            Vector3 pos = origin.position
            + new Vector3((indexInPile + 1) * deltaX,
                          (indexInPile + 1) * deltaY,
                           pile * pileGapZ);

            var token = Instantiate(tokenPrefab, pos, rot);
            token.transform.localScale = Vector3.one;
            list.Add(token);
        }
    }

    public void ClearGodFigures()
    {
        foreach (var o in _myGodObjects) if (o) Destroy(o);
        foreach (var o in _enemyGodObjects) if (o) Destroy(o);
        _myGodObjects.Clear();
        _enemyGodObjects.Clear();
    }

    private void ClearTokens()
    {
        foreach (var o in _myTokenObjects) if (o) Destroy(o);
        foreach (var o in _enemyTokenObjects) if (o) Destroy(o);
        _myTokenObjects.Clear();
        _enemyTokenObjects.Clear();
    }

    /// Devuelve el GodFavorController bajo el cursor (solo los interactuables del jugador local).
    public GodFavorController GetHoveredGod(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(screenPos);
        var hits = Physics.RaycastAll(ray, 100f);
        foreach (var hit in hits)
        {
            var ctrl = hit.collider.GetComponent<GodFavorController>();
            if (ctrl != null && ctrl.IsInteractable) return ctrl;
        }
        return null;
    }

    // ── Resolución visual ─────────────────────────────────────
    // Layout por zonas a lo largo del eje Z:
    //   Zona 1: My Axe    <-->  Enemy Helmet  (misma Z, X distinto)
    //   Zona 2: My Arrow  <-->  Enemy Shield
    //   Zona 3: Hand (ambos)
    //   Zona 4: Enemy Axe <-->  My Helmet
    //   Zona 5: Enemy Arrow <-> My Shield
    // Si un lado del matchup no tiene dado, el slot queda vacío.

    public IEnumerator AnimateResolution()
    {
        var gm = GameManager.Instance;
        if (gm == null) yield break;

        bool iAmStart = (GameData.MyId == GameData.PlayerStartId);

        List<DiceData> startDice = iAmStart ? gm.MyConfirmed : gm.EnemyConfirmed;
        List<DiceData> secondDice = iAmStart ? gm.EnemyConfirmed : gm.MyConfirmed;
        List<GameObject> startObjs = iAmStart ? _myConfirmedObjects : _enemyConfirmedObjects;
        List<GameObject> secondObjs = iAmStart ? _enemyConfirmedObjects : _myConfirmedObjects;

        float myX = myKeptRowOrigin?.position.x ?? -0.5f;
        float enemyX = enemyKeptRowOrigin?.position.x ?? 0.5f;
        float startX = iAmStart ? myX : enemyX;
        float secondX = iAmStart ? enemyX : myX;
        float y = (myKeptRowOrigin?.position.y ?? 0f) + keptYOffset;
        float z = 2.5f;

        var startIdx = GetIndicesByType(startDice);
        var secondIdx = GetIndicesByType(secondDice);
        var startMoves = new Dictionary<int, Vector3>();
        var secondMoves = new Dictionary<int, Vector3>();

        void PlaceColumn(List<int> startList, List<int> secondList)
        {
            int slots = Mathf.Max(startList.Count, secondList.Count);
            if (slots == 0) return;
            for (int i = 0; i < slots; i++)
            {
                if (i < startList.Count)
                    startMoves[startList[i]] = new Vector3(startX, y, z);
                if (i < secondList.Count)
                    secondMoves[secondList[i]] = new Vector3(secondX, y, z);
                z -= keptRowSpacing;
            }
            z -= 0.2f;
        }

        PlaceColumn(startIdx[DiceFace.Axe], secondIdx[DiceFace.Helmet]);
        PlaceColumn(startIdx[DiceFace.Arrow], secondIdx[DiceFace.Shield]);
        PlaceColumn(startIdx[DiceFace.Helmet], secondIdx[DiceFace.Axe]);
        PlaceColumn(startIdx[DiceFace.Shield], secondIdx[DiceFace.Arrow]);
        PlaceColumn(startIdx[DiceFace.Hand], secondIdx[DiceFace.Hand]);

        for (int i = 0; i < startDice.Count && i < startObjs.Count; i++)
        {
            if (startObjs[i] == null) continue;
            if (FaceRotations.TryGetValue(startDice[i].face, out Vector3 rot))
                startObjs[i].transform.rotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(rot);
        }
        for (int i = 0; i < secondDice.Count && i < secondObjs.Count; i++)
        {
            if (secondObjs[i] == null) continue;
            if (FaceRotations.TryGetValue(secondDice[i].face, out Vector3 rot))
                secondObjs[i].transform.rotation = Quaternion.Euler(rot);
        }

        // Mostrar fichas PRE-manos y quitar halos de energy ANTES de mover los dados
        // (valores ya corregidos en HandleGodFavor: energía base + dados con energy:true)
        Debug.Log($"[Board] AnimateResolution SpawnTokens | MyEnergyPreHands:{gm.MyEnergyPreHands} OppEnergyPreHands:{gm.OpponentEnergyPreHands}");
        SpawnTokens(gm.MyEnergyPreHands, gm.OpponentEnergyPreHands);
        ClearEnergyHalos();

        foreach (var kv in startMoves)
            if (kv.Key < startObjs.Count && startObjs[kv.Key] != null)
                MoveObject(startObjs[kv.Key], kv.Value);
        foreach (var kv in secondMoves)
            if (kv.Key < secondObjs.Count && secondObjs[kv.Key] != null)
                MoveObject(secondObjs[kv.Key], kv.Value);

        yield return new WaitForSeconds(0.5f);

        // Animación de armas
        if (ResolutionAnimator.Instance != null)
            yield return ResolutionAnimator.Instance.PlayResolution(
                startDice, secondDice, startObjs, secondObjs, iAmStart);

        yield return new WaitForSeconds(0.5f);
    }

    private void ClearEnergyHalos()
    {
        var allConfirmed = new List<GameObject>(_myConfirmedObjects);
        allConfirmed.AddRange(_enemyConfirmedObjects);

        foreach (var obj in allConfirmed)
        {
            if (obj == null) continue;
            foreach (Transform t in obj.GetComponentsInChildren<Transform>())
            {
                if (t != null && t.name == "EnergyHalo")
                {
                    Destroy(t.gameObject);
                    break;
                }
            }
        }
    }
    private void PlaceZone(
    List<int> attackerIdx, List<int> defenderIdx,
    float attackerX, float defenderX, float y, ref float z,
    Dictionary<int, Vector3> attackerMoves,
    Dictionary<int, Vector3> defenderMoves)
    {
        int slots = Mathf.Max(attackerIdx.Count, defenderIdx.Count);
        if (slots == 0) return;
        Debug.Log($"[Board] PlaceZone | attackers:{attackerIdx.Count} defenders:{defenderIdx.Count} | z={z:F2} | attackerX={attackerX:F2} defenderX={defenderX:F2}");
        for (int i = 0; i < slots; i++)
        {
            if (i < attackerIdx.Count)
            {
                attackerMoves[attackerIdx[i]] = new Vector3(attackerX, y, z);
                Debug.Log($"[Board]   attacker[{attackerIdx[i]}] → ({attackerX:F2}, {y:F2}, {z:F2})");
            }
            if (i < defenderIdx.Count)
            {
                defenderMoves[defenderIdx[i]] = new Vector3(defenderX, y, z);
                Debug.Log($"[Board]   defender[{defenderIdx[i]}] → ({defenderX:F2}, {y:F2}, {z:F2})");
            }
            z -= keptRowSpacing;
        }
        z -= 0.15f;
    }

    private void PlaceHands(
        List<int> startHands, List<int> secondHands,
        float startX, float secondX, float y, ref float z,
        Dictionary<int, Vector3> startMoves,
        Dictionary<int, Vector3> secondMoves)
    {
        foreach (var idx in startHands) { startMoves[idx] = new Vector3(startX, y, z); z -= keptRowSpacing; }
        foreach (var idx in secondHands) { secondMoves[idx] = new Vector3(secondX, y, z); z -= keptRowSpacing; }
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

    // ── Helpers ──────────────────────────────────────────────

    private List<DiceData> GenerateFaces(System.Random rng, int count)
    {
        var list = new List<DiceData>();
        for (int i = 0; i < count; i++)
        {
            list.Add(new DiceData
            {
                face = AllFaces[rng.Next(AllFaces.Length)],
                energy = false
            });
        }
        return list;
    }

    private void SpawnFromFaces(List<DiceData> faces, Transform center, List<GameObject> list, bool isMine, bool decorative)
    {
        if (center == null) return;
        for (int i = 0; i < faces.Count && i < BowlOffsets.Length; i++)
        {
            Vector3 pos = center.position + BowlOffsets[i] + Vector3.up * 0.3f;
            list.Add(SpawnDice(faces[i], pos, isMine, decorative));
        }
    }

    private GameObject SpawnDice(DiceData data, Vector3 pos, bool isMine, bool decorative)
    {
        var obj = Instantiate(dicePrefab, pos, Quaternion.identity);
        ApplyDiceColor(obj, data);
        // ... resto igual


        var ctrl = obj.GetComponent<DiceController>()
                    ?? obj.GetComponentInChildren<DiceController>();
        if (ctrl == null) ctrl = obj.AddComponent<DiceController>();
        Debug.Log($"[Board] SpawnDice | ctrl={ctrl != null} | decorative={decorative}"); // ← nuevo

        if (decorative)
        {
            ctrl.enabled = false;
        }
        else
        {
            data.isMyDice = isMine;
            ctrl.enabled = true;
            ctrl.Init(data);
            ctrl.SetRestPosition(pos);
            // No llamamos ApplyVisual aquí: ApplyDiceColor ya ha puesto el color y la
            // emisión correctos. ApplyVisual solo se llama en runtime al togglear la
            // selección (DiceController.ToggleKeep), no al instanciar el dado.
        }
        return obj;
    }

    private static readonly Dictionary<DiceFace, Vector3> FaceRotations = new()
{
    { DiceFace.Axe,    new Vector3(  0,  0,   0) },
    { DiceFace.Hand,   new Vector3( 90,  0,   0) },
    { DiceFace.Shield, new Vector3(180,  0,   0) },
    { DiceFace.Helmet, new Vector3(  0,  0,  90) },
    { DiceFace.Arrow,  new Vector3(  0,  0, -90) },
};

    private void ApplyDiceColor(GameObject obj, DiceData data)
    {
        var renderer = obj.GetComponent<Renderer>()
                    ?? obj.GetComponentInChildren<Renderer>();
        if (renderer == null) return;

        if (FaceRotations.TryGetValue(data.face, out Vector3 rot))
            obj.transform.rotation = Quaternion.Euler(rot);

        var mats = renderer.materials;
        foreach (var mat in mats)
        {
            if (mat == null) continue;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
        renderer.materials = mats;
        ApplyEnergyHalo(obj, data.energy);
    }
    private void ApplyEnergyHalo(GameObject diceObj, bool hasEnergy)
    {
        Transform existing = diceObj.transform.Find("EnergyHalo");
        if (!hasEnergy)
        {
            if (existing != null) Destroy(existing.gameObject);
            return;
        }
        if (existing != null) return;

        // Obtener renderer PRIMERO — lo necesitamos para bounds y material
        var diceRenderer = diceObj.GetComponent<Renderer>()
                        ?? diceObj.GetComponentInChildren<Renderer>();
        if (diceRenderer == null) return;

        var meshObj = diceRenderer.gameObject;

        var halo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        halo.name = "EnergyHalo";
        halo.transform.SetParent(meshObj.transform, worldPositionStays: false);
        halo.transform.localPosition = Vector3.zero;
        halo.transform.localRotation = Quaternion.identity;

        var bounds = diceRenderer.bounds;
        float worldSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 1.08f;
        Vector3 ps = meshObj.transform.lossyScale;
        halo.transform.localScale = new Vector3(
            worldSize / ps.x,
            worldSize / ps.y,
            worldSize / ps.z
        );

        var col = halo.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var mat = new Material(diceRenderer.material);
        mat.mainTexture = null;
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 3f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3001;
        mat.color = new Color(1f, 0.75f, 0.1f, 0.15f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.8f, 0.55f, 0f) * 1.5f);

        var rend = halo.GetComponent<Renderer>();
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }

    private void ClearBowlsAndKept()
    {
        foreach (var obj in _myBowlObjects) if (obj) Destroy(obj);
        foreach (var obj in _enemyBowlObjects) if (obj) Destroy(obj);
        foreach (var obj in _myConfirmedObjects) if (obj) Destroy(obj);
        foreach (var obj in _enemyConfirmedObjects) if (obj) Destroy(obj);
        _myBowlObjects.Clear();
        _enemyBowlObjects.Clear();
        _myConfirmedObjects.Clear();
        _enemyConfirmedObjects.Clear();
        _selectionOrder.Clear();
        _moveCoroutines.Clear();
        // ClearTokens() eliminado — los tokens los gestiona SpawnTokens explícitamente
    }

    public void SpawnStones(int playerCount = 15, int opponentCount = 15)
    {
        ClearStones();
        SpawnStoneRow(playerCount, playerStonesOrigin, _playerStones);
        SpawnStoneRow(opponentCount, opponentStonesOrigin, _opponentStones);
    }

    private void SpawnStoneRow(int count, Transform origin, List<GameObject> list)
    {
        int columns = 3;
        float spacingX = 0.32f;
        float spacingZ = 0.32f;
        int index = 0;
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                if (index >= count) break;
                Vector3 pos = origin.position
                    + Vector3.right * col * spacingX
                    + Vector3.forward * row * spacingZ;
                list.Add(Instantiate(stonePrefab, pos, Quaternion.Euler(90f, 0f, 0f)));
                index++;
            }
        }
    }

    private void ClearStones()
    {
        foreach (var obj in _playerStones) if (obj) Destroy(obj);
        foreach (var obj in _opponentStones) if (obj) Destroy(obj);
        _playerStones.Clear();
        _opponentStones.Clear();
    }
}