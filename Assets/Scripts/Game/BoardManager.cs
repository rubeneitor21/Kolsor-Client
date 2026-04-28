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

    [Header("Animación de tirada")]
    public float rollDuration = 1.0f;       // duración total de la animación
    public float rollFaceChangeRate = 0.05f; // cada cuántos segundos cambia la cara

    private List<GameObject> _playerStones = new();
    private List<GameObject> _opponentStones = new();
    private List<GameObject> _myBowlObjects = new();
    private List<GameObject> _enemyBowlObjects = new();

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
        GameManager.Instance?.NotifyBoardReady();
        if (_myBowlObjects.Count == 0 && _enemyBowlObjects.Count == 0)
            RebuildAll();
    }

    // ── Reconstrucción global ────────────────────────────────

    /// Reconstruye los dos cuencos.
    /// IMPORTANTE: el cuenco propio solo muestra dados REALES si el jugador
    /// ya pulsó espacio (MyDiceRolled = true). Mientras no, todos los cuencos
    /// muestran decorativos sincronizados vía RoomId.
    public void RebuildAll()
    {
        ClearAllDice();

        var gm = GameManager.Instance;
        bool showMyReal = gm != null
                          && gm.MyDice != null
                          && gm.MyDice.Count > 0
                          && gm.MyDiceRolled;

        // RNG sembrado con el roomId: ambos clientes generan las mismas caras.
        int seed = string.IsNullOrEmpty(GameData.RoomId)
            ? 0
            : GameData.RoomId.GetHashCode();
        var rng = new System.Random(seed);

        // Para mantener sincronía entre ambos clientes, generamos SIEMPRE
        // las caras decorativas en el mismo orden:
        //   primer set de 6 = cuenco del playerStart
        //   segundo set de 6 = cuenco del playerSecond
        // Cada cliente luego decide cuál de esos sets corresponde a "mi" cuenco
        // y cuál al "rival" según si es playerStart o no.

        var startBowlFaces = GenerateFaces(rng, 6);
        var secondBowlFaces = GenerateFaces(rng, 6);

        bool iAmPlayerStart = (GameData.MyId == GameData.PlayerStartId);
        var myDecorative = iAmPlayerStart ? startBowlFaces : secondBowlFaces;
        var enemyDecorative = iAmPlayerStart ? secondBowlFaces : startBowlFaces;

        // Cuenco propio
        if (showMyReal)
        {
            // El jugador ya tiró: mostramos los dados reales del servidor
            for (int i = 0; i < gm.MyDice.Count && i < BowlOffsets.Length; i++)
            {
                Vector3 pos = myBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
                _myBowlObjects.Add(SpawnDice(gm.MyDice[i], pos));
            }
        }
        else
        {
            // No ha tirado todavía: decorativos sincronizados
            SpawnFromFaces(myDecorative, myBowlCenter, _myBowlObjects);
        }

        // Cuenco rival: siempre decorativos (nunca recibimos sus dados reales)
        SpawnFromFaces(enemyDecorative, enemyBowlCenter, _enemyBowlObjects);
    }

    /// Animación de tirada de los dados propios. Va cambiando rápidamente
    /// las caras y al final deja las caras reales (las de MyDice).
    public IEnumerator AnimateMyRoll(List<DiceData> finalDice)
    {
        if (finalDice == null || _myBowlObjects.Count == 0) yield break;

        float elapsed = 0f;
        float nextChange = 0f;

        while (elapsed < rollDuration)
        {
            if (elapsed >= nextChange)
            {
                // En cada cambio, ponemos caras random a todos los dados
                for (int i = 0; i < _myBowlObjects.Count; i++)
                {
                    var randomFace = AllFaces[Random.Range(0, AllFaces.Length)];
                    var fakeData = new DiceData { face = randomFace, energy = false };
                    ApplyDiceColor(_myBowlObjects[i], fakeData);
                }
                nextChange = elapsed + rollFaceChangeRate;
            }

            // Pequeño bote vertical para dar sensación de movimiento
            float bounce = Mathf.Abs(Mathf.Sin(elapsed * 20f)) * 0.15f;
            for (int i = 0; i < _myBowlObjects.Count; i++)
            {
                if (_myBowlObjects[i] == null) continue;
                Vector3 basePos = myBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
                _myBowlObjects[i].transform.position = basePos + Vector3.up * bounce;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Al terminar: dejamos los dados con las caras reales y posición de reposo
        for (int i = 0; i < _myBowlObjects.Count && i < finalDice.Count; i++)
        {
            ApplyDiceColor(_myBowlObjects[i], finalDice[i]);
            Vector3 basePos = myBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
            _myBowlObjects[i].transform.position = basePos;
        }
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

    private void SpawnFromFaces(List<DiceData> faces, Transform center, List<GameObject> list)
    {
        if (center == null) return;
        for (int i = 0; i < faces.Count && i < BowlOffsets.Length; i++)
        {
            Vector3 pos = center.position + BowlOffsets[i] + Vector3.up * 0.3f;
            list.Add(SpawnDice(faces[i], pos));
        }
    }

    private GameObject SpawnDice(DiceData data, Vector3 pos)
    {
        var obj = Instantiate(dicePrefab, pos, Quaternion.identity);
        ApplyDiceColor(obj, data);
        return obj;
    }

    private void ApplyDiceColor(GameObject obj, DiceData data)
    {
        var renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;
        var mat = renderer.material;
        if (FaceColors.TryGetValue(data.face, out Color color)) mat.color = color;
        if (data.energy) mat.SetColor("_EmissionColor", new Color(0.8f, 0.6f, 0.0f) * 2f);
        else mat.SetColor("_EmissionColor", Color.black);
    }

    private void ClearAllDice()
    {
        foreach (var obj in _myBowlObjects) if (obj) Destroy(obj);
        foreach (var obj in _enemyBowlObjects) if (obj) Destroy(obj);
        _myBowlObjects.Clear();
        _enemyBowlObjects.Clear();
    }

    // ── Piedras de vida ──────────────────────────────────────

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