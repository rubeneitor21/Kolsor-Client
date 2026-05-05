using System.Collections;
using System.Collections.Generic;
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

    [Header("Filas de dados confirmados")]
    public Transform myKeptRowOrigin;
    public Transform enemyKeptRowOrigin;
    public float keptRowSpacing = 0.55f;
    public float keptYOffset = 0.4f;

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
        ClearBowlsAndKept();

        var gm = GameManager.Instance;
        if (gm == null) return;

        // Decorativos sincronizados solo si nadie ha empezado a confirmar todavía
        // Y si el jugador propio aún no ha tirado en esta tirada.
        bool nothingConfirmedYet = (gm.MyConfirmed.Count == 0 && gm.EnemyConfirmed.Count == 0);
        bool myBowlEmpty = (gm.MyDice == null || gm.MyDice.Count == 0);

        // Cuenco propio
        if (gm.MyDice != null && gm.MyDice.Count > 0 && gm.MyDiceRolled)
        {
            // Mis dados reales tras tirar
            for (int i = 0; i < gm.MyDice.Count && i < BowlOffsets.Length; i++)
            {
                Vector3 pos = myBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
                _myBowlObjects.Add(SpawnDice(gm.MyDice[i], pos, isMine: true, decorative: false));
            }
        }
        else if (nothingConfirmedYet && myBowlEmpty)
        {
            // Inicio de partida: decorativos sincronizados con el RoomId
            SpawnDecorativeBowls();
        }
        // En cualquier otro caso (por ejemplo "ya he confirmado pero aún no he
        // vuelto a tirar"), el cuenco propio queda vacío. Visualmente esto es
        // correcto: ya tiraste y los sobrantes se quedan ahí solo entre tirada
        // y tirada — pero como no tenemos los sobrantes en cliente todavía,
        // vacío durante el turno del rival.

        // Filas confirmadas
        SpawnConfirmedRow(gm.MyConfirmed, myKeptRowOrigin, _myConfirmedObjects, isMine: true);
        SpawnConfirmedRow(gm.EnemyConfirmed, enemyKeptRowOrigin, _enemyConfirmedObjects, isMine: false);
    }

    private void SpawnDecorativeBowls()
    {
        int seed = string.IsNullOrEmpty(GameData.RoomId) ? 0 : GameData.RoomId.GetHashCode();
        var rng = new System.Random(seed);

        var startBowlFaces = GenerateFaces(rng, 6);
        var secondBowlFaces = GenerateFaces(rng, 6);

        bool iAmPlayerStart = (GameData.MyId == GameData.PlayerStartId);
        var myDecorative = iAmPlayerStart ? startBowlFaces : secondBowlFaces;
        var enemyDecorative = iAmPlayerStart ? secondBowlFaces : startBowlFaces;

        SpawnFromFaces(myDecorative, myBowlCenter, _myBowlObjects, isMine: true, decorative: true);
        SpawnFromFaces(enemyDecorative, enemyBowlCenter, _enemyBowlObjects, isMine: false, decorative: true);
    }

    private void SpawnConfirmedRow(List<DiceData> dice, Transform origin, List<GameObject> list, bool isMine)
    {
        if (dice == null || origin == null) return;
        Vector3 dir = -origin.right;
        for (int i = 0; i < dice.Count; i++)
        {
            Vector3 pos = origin.position + dir * i * keptRowSpacing + Vector3.up * keptYOffset;
            list.Add(SpawnDice(dice[i], pos, isMine, decorative: false));
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
    public IEnumerator AnimateEnemyRoll(List<DiceData> finalDice)
    {
        Debug.Log($"[Board] AnimateEnemyRoll START | _enemyBowlObjects.Count={_enemyBowlObjects.Count} | finalDice.Count={(finalDice?.Count ?? 0)}");
        // Igual que con la mía: reusamos decorativos o creamos.
        if (_enemyBowlObjects.Count == 0)
            Debug.Log($"[Board] AnimateEnemyRoll: _enemyBowlObjects.Count={_enemyBowlObjects.Count} | finalDice.Count={finalDice.Count}");
        {
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

            Debug.Log($"[Board] AnimateEnemyRoll terminado, dados finales aplicados");
            for (int i = 0; i < _enemyBowlObjects.Count && i < finalDice.Count; i++)
            {
                Debug.Log($"[Board]   Enemy[{i}] = {finalDice[i].face} energy:{finalDice[i].energy}");
            }

            Debug.Log("[Board] AnimateEnemyRoll END");
        }

        yield return AnimateBowl(_enemyBowlObjects, enemyBowlCenter, finalDice);
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
            ctrl.ApplyVisual();
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

        // Si no se ha asignado el material en el Inspector, no creamos halo.
        // Esto evita que la build crashee si por algún motivo falta.
        if (energyHaloMaterial == null) return;

        var halo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        halo.name = "EnergyHalo";
        halo.transform.SetParent(diceObj.transform, worldPositionStays: false);
        halo.transform.localPosition = Vector3.zero;
        halo.transform.localRotation = Quaternion.identity;
        halo.transform.localScale = Vector3.one * 1.12f;

        var col = halo.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var haloRenderer = halo.GetComponent<Renderer>();
        haloRenderer.material = new Material(energyHaloMaterial);
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