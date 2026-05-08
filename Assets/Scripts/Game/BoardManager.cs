using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }

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
    public float confirmAnimDuration = 0.6f;

    [Header("Material del halo dorado")]
    public Material energyHaloMaterial;

    private List<GameObject> _playerStones = new();
    private List<GameObject> _opponentStones = new();
    private List<GameObject> _myBowlObjects = new();
    private List<GameObject> _enemyBowlObjects = new();
    private List<GameObject> _myConfirmedObjects = new();
    private List<GameObject> _enemyConfirmedObjects = new();
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

        // Posiciones del preview row para mis nuevos dados confirmados
        var myNewStarts = new List<Vector3>();
        if (myKeptRowOrigin != null)
        {
            var op = myKeptRowOrigin.position;
            int newCount = (gm.MyConfirmed?.Count ?? 0) - prevMyConfirmed;
            for (int i = 0; i < newCount; i++)
                myNewStarts.Add(new Vector3(op.x, 0.5f, op.z - i * keptRowSpacing));
        }

        // Posiciones del bowl del rival para sus nuevos dados confirmados
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

        SpawnConfirmedRow(gm.MyConfirmed, myKeptRowOrigin, _myConfirmedObjects, myStarts);
        SpawnConfirmedRow(gm.EnemyConfirmed, enemyKeptRowOrigin, _enemyConfirmedObjects, enemyStarts);
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
                                   List<GameObject> list, List<Vector3> startPositions)
    {
        if (dice == null || previewOrigin == null) return;
        var p = previewOrigin.position;
        Vector3 confirmedOrigin = new Vector3(p.x, p.y, confirmedRowZ);
        for (int i = 0; i < dice.Count; i++)
        {
            Vector3 endPos = confirmedOrigin + new Vector3(0f, 0f, -1f) * i * keptRowSpacing + Vector3.up * keptYOffset;
            bool hasStart = startPositions != null && i < startPositions.Count && startPositions[i] != Vector3.zero;
            Vector3 startPos = hasStart ? startPositions[i] : endPos;
            var obj = SpawnDice(dice[i], startPos, isMine: false, decorative: true);
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

        var ctrl = obj.GetComponent<DiceController>();
        if (ctrl == null) ctrl = obj.AddComponent<DiceController>();

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

    private void ApplyDiceColor(GameObject obj, DiceData data)
    {
        var renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;
        var mat = renderer.material;
        if (FaceColors.TryGetValue(data.face, out Color color)) mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.black);
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

        // Usamos esfera en lugar de cubo: una esfera alrededor de un cubo
        // da efecto de aura/halo orgánico. Un cubo dentro de otro cubo
        // simplemente parece el mismo dado un poco más grande.
        var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        halo.name = "EnergyHalo";
        halo.transform.SetParent(diceObj.transform, worldPositionStays: false);
        halo.transform.localPosition = Vector3.zero;
        halo.transform.localRotation = Quaternion.identity;
        halo.transform.localScale = Vector3.one * 1.5f;

        var col = halo.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Tomamos el shader del propio dado en lugar de buscarlo por nombre.
        // Shader.Find("Standard") puede devolver null en la build si Unity no
        // incluye ese shader con ese nombre exacto → resultado: material morado.
        // Usando el material del dado garantizamos que el shader ya está incluido.
        var diceRenderer = diceObj.GetComponent<Renderer>();
        var mat = new Material(diceRenderer.material);

        // Modo Transparent
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;

        // Color dorado semitransparente — alfa 0.35 da el efecto de aura sin tapar el dado
        mat.color = new Color(1f, 0.78f, 0.20f, 0.35f);

        // Emisión dorada suave para que brille un poco
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.4f, 0.28f, 0f));

        var haloRenderer = halo.GetComponent<Renderer>();
        haloRenderer.material = mat;
        haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        haloRenderer.receiveShadows = false;
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
        _moveCoroutines.Clear(); // los objetos se destruyen, las coroutines mueren con ellos
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
                list.Add(Instantiate(stonePrefab, pos, Quaternion.identity));
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