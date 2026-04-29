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
        GameManager.Instance?.NotifyBoardReady();
        if (_myBowlObjects.Count == 0 && _enemyBowlObjects.Count == 0)
            RebuildAll();
    }

    // ── Reconstrucción global ────────────────────────────────

    public void RebuildAll()
    {
        ClearAllDice();

        var gm = GameManager.Instance;
        bool showMyReal = gm != null
                          && gm.MyDice != null
                          && gm.MyDice.Count > 0
                          && gm.MyDiceRolled;

        // Decorativos sincronizados vía RoomId
        int seed = string.IsNullOrEmpty(GameData.RoomId) ? 0 : GameData.RoomId.GetHashCode();
        var rng = new System.Random(seed);

        var startBowlFaces = GenerateFaces(rng, 6);
        var secondBowlFaces = GenerateFaces(rng, 6);

        bool iAmPlayerStart = (GameData.MyId == GameData.PlayerStartId);
        var myDecorative = iAmPlayerStart ? startBowlFaces : secondBowlFaces;
        var enemyDecorative = iAmPlayerStart ? secondBowlFaces : startBowlFaces;

        // 1. Cuenco propio
        if (showMyReal)
        {
            for (int i = 0; i < gm.MyDice.Count && i < BowlOffsets.Length; i++)
            {
                Vector3 pos = myBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
                var obj = SpawnDice(gm.MyDice[i], pos, isMine: true, decorative: false);
                _myBowlObjects.Add(obj);
            }
        }
        else
        {
            SpawnFromFaces(myDecorative, myBowlCenter, _myBowlObjects, isMine: true, decorative: true);
        }

        // 2. Cuenco rival: siempre decorativo
        SpawnFromFaces(enemyDecorative, enemyBowlCenter, _enemyBowlObjects, isMine: false, decorative: true);

        // 3. Filas confirmadas
        if (gm != null && myKeptRowOrigin != null && gm.MyConfirmed != null)
        {
            Vector3 dir = -myKeptRowOrigin.right;
            for (int i = 0; i < gm.MyConfirmed.Count; i++)
            {
                Vector3 pos = myKeptRowOrigin.position + dir * i * keptRowSpacing + Vector3.up * keptYOffset;
                var obj = SpawnDice(gm.MyConfirmed[i], pos, isMine: true, decorative: false);
                _myConfirmedObjects.Add(obj);
            }
        }

        if (gm != null && enemyKeptRowOrigin != null && gm.EnemyConfirmed != null)
        {
            Vector3 dir = -enemyKeptRowOrigin.right;
            for (int i = 0; i < gm.EnemyConfirmed.Count; i++)
            {
                Vector3 pos = enemyKeptRowOrigin.position + dir * i * keptRowSpacing + Vector3.up * keptYOffset;
                var obj = SpawnDice(gm.EnemyConfirmed[i], pos, isMine: false, decorative: false);
                _enemyConfirmedObjects.Add(obj);
            }
        }
    }

    /// Animación de tirada del jugador propio.
    public IEnumerator AnimateMyRoll(List<DiceData> finalDice)
    {
        if (finalDice == null || _myBowlObjects.Count == 0) yield break;

        float elapsed = 0f;
        float nextChange = 0f;

        while (elapsed < rollDuration)
        {
            if (elapsed >= nextChange)
            {
                for (int i = 0; i < _myBowlObjects.Count; i++)
                {
                    var randomFace = AllFaces[Random.Range(0, AllFaces.Length)];
                    var fakeData = new DiceData { face = randomFace, energy = false };
                    ApplyDiceColor(_myBowlObjects[i], fakeData);
                }
                nextChange = elapsed + rollFaceChangeRate;
            }

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

        // Al terminar: caras reales + DiceController activo y enlazado a MyDice
        // para que el clic funcione.
        var gm = GameManager.Instance;
        for (int i = 0; i < _myBowlObjects.Count && i < finalDice.Count; i++)
        {
            ApplyDiceColor(_myBowlObjects[i], finalDice[i]);
            Vector3 basePos = myBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
            _myBowlObjects[i].transform.position = basePos;

            // Conectamos el DiceController al DiceData real para que ToggleKeep
            // funcione sobre el dato correcto de gm.MyDice.
            var ctrl = _myBowlObjects[i].GetComponent<DiceController>();
            if (ctrl == null) ctrl = _myBowlObjects[i].AddComponent<DiceController>();
            ctrl.enabled = true;
            ctrl.Init(finalDice[i]);
            ctrl.SetRestPosition(basePos);
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

    private void SpawnFromFaces(List<DiceData> faces, Transform center, List<GameObject> list, bool isMine, bool decorative)
    {
        if (center == null) return;
        for (int i = 0; i < faces.Count && i < BowlOffsets.Length; i++)
        {
            Vector3 pos = center.position + BowlOffsets[i] + Vector3.up * 0.3f;
            list.Add(SpawnDice(faces[i], pos, isMine, decorative));
        }
    }

    /// Crea un dado en la posición indicada.
    /// Si decorative=true, el DiceController se desactiva (no se puede clicar).
    /// Si decorative=false, el DiceController queda activo y referenciado al
    /// DiceData real para que ToggleKeep funcione.
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

        // La emisión la gestiona DiceController.ApplyVisual (para kept).
        // Aquí solo nos aseguramos de que esté limpia al inicio.
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.black);

        // Borde dorado para dados con energía (sin tocar el material principal).
        ApplyEnergyHalo(obj, data.energy);
    }

    /// Añade o quita un halo dorado alrededor del dado.
    /// El halo es un cubo hijo ligeramente más grande con material semitransparente.
    private void ApplyEnergyHalo(GameObject diceObj, bool hasEnergy)
    {
        Transform existing = diceObj.transform.Find("EnergyHalo");

        if (!hasEnergy)
        {
            if (existing != null) Destroy(existing.gameObject);
            return;
        }

        if (existing != null) return; // ya tiene halo

        var halo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        halo.name = "EnergyHalo";
        halo.transform.SetParent(diceObj.transform, worldPositionStays: false);
        halo.transform.localPosition = Vector3.zero;
        halo.transform.localRotation = Quaternion.identity;
        halo.transform.localScale = Vector3.one * 1.12f; // 12% más grande que el dado

        // Quitamos el collider del halo para que no interfiera con los raycasts del clic.
        var col = halo.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Material dorado emisivo, semitransparente.
        var haloRenderer = halo.GetComponent<Renderer>();
        var haloMat = new Material(Shader.Find("Standard"));
        // Modo Transparent del Standard Shader
        haloMat.SetFloat("_Mode", 3f);
        haloMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        haloMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        haloMat.SetInt("_ZWrite", 0);
        haloMat.DisableKeyword("_ALPHATEST_ON");
        haloMat.EnableKeyword("_ALPHABLEND_ON");
        haloMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        haloMat.renderQueue = 3000;

        Color gold = new Color(1f, 0.78f, 0.2f, 0.35f);
        haloMat.color = gold;
        haloMat.EnableKeyword("_EMISSION");
        haloMat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.0f) * 1.2f);

        haloRenderer.material = haloMat;
        haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        haloRenderer.receiveShadows = false;
    }

    private void ClearAllDice()
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