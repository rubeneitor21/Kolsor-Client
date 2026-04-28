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

    [Header("Fila de dados conservados")]
    public Transform myKeptRowOrigin;
    public Transform enemyKeptRowOrigin;
    public float keptRowSpacing = 0.55f;
    public float keptYOffset = 0.4f;

    private List<GameObject> _playerStones = new();
    private List<GameObject> _opponentStones = new();

    // Cuenco propio: dados que el jugador puede manipular en su tirada actual.
    private List<GameObject> _myBowlObjects = new();

    // Cuenco rival: dados decorativos (caras random) que se ven mientras
    // el rival no recibe del servidor sus dados reales.
    private List<GameObject> _enemyBowlObjects = new();

    // Filas confirmadas: dados ya guardados por cada jugador.
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

        // Llenamos los dos cuencos con dados decorativos al arrancar la escena.
        // Cuando llegue el primer game-rolls, RebuildAll los sustituirá por
        // los reales en el cuenco del jugador activo.
        SpawnDecorativeBowl(myBowlCenter, _myBowlObjects, isMine: true);
        SpawnDecorativeBowl(enemyBowlCenter, _enemyBowlObjects, isMine: false);

        GameManager.Instance?.NotifyBoardReady();
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
                var obj = Instantiate(stonePrefab, pos, Quaternion.identity);
                list.Add(obj);
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

    // ── Cuencos decorativos ──────────────────────────────────

    /// Llena un cuenco con 6 dados de caras aleatorias. Decorativos: no son
    /// clicables ni se envían al servidor. Solo simulan que los dados están
    /// físicamente en la mesa esperando a ser lanzados.
    private void SpawnDecorativeBowl(Transform center, List<GameObject> list, bool isMine)
    {
        if (center == null) return;

        foreach (var obj in list) if (obj) Destroy(obj);
        list.Clear();

        for (int i = 0; i < 6; i++)
        {
            var data = new DiceData
            {
                face = AllFaces[Random.Range(0, AllFaces.Length)],
                energy = false,
                kept = false,
                isMyDice = isMine
            };

            Vector3 pos = center.position + BowlOffsets[i] + Vector3.up * 0.3f;
            var diceObj = SpawnDice(data, pos, isMine);

            // Los decorativos no se pueden seleccionar: desactivamos el
            // controller que detecta interacción.
            var ctrl = diceObj.GetComponent<DiceController>();
            if (ctrl != null) ctrl.enabled = false;

            list.Add(diceObj);
        }
    }

    // ── Reconstrucción global ────────────────────────────────

    /// Reconstruye toda la mesa de dados desde el estado del GameManager.
    /// Se llama cada vez que llega un game-rolls.
    public void RebuildAll()
    {
        ClearAllDice();

        var gm = GameManager.Instance;
        if (gm == null) return;

        // 1. Cuenco propio
        if (gm.MyDice != null && gm.MyDice.Count > 0 && myBowlCenter != null)
        {
            // Es mi turno: dados reales del servidor, clicables
            for (int i = 0; i < gm.MyDice.Count && i < BowlOffsets.Length; i++)
            {
                Vector3 pos = myBowlCenter.position + BowlOffsets[i] + Vector3.up * 0.3f;
                var obj = SpawnDice(gm.MyDice[i], pos, isMine: true);
                _myBowlObjects.Add(obj);
            }
        }
        else
        {
            // No es mi turno: cuenco con dados decorativos.
            // El número que mostramos = 6 menos los que ya tengo confirmados.
            int remaining = 6 - (gm.MyConfirmed?.Count ?? 0);
            SpawnDecorativeBowlPartial(myBowlCenter, _myBowlObjects, isMine: true, count: remaining);
        }

        // 2. Cuenco rival: siempre decorativo (el servidor no nos manda
        //    los dados del rival). Cuántos quedan = 6 - confirmados del rival.
        int enemyRemaining = 6 - (gm.EnemyConfirmed?.Count ?? 0);
        SpawnDecorativeBowlPartial(enemyBowlCenter, _enemyBowlObjects, isMine: false, count: enemyRemaining);

        // 3. Fila confirmada propia
        if (gm.MyConfirmed != null && myKeptRowOrigin != null)
        {
            Vector3 dir = -myKeptRowOrigin.right;
            for (int i = 0; i < gm.MyConfirmed.Count; i++)
            {
                Vector3 pos = myKeptRowOrigin.position
                            + dir * i * keptRowSpacing
                            + Vector3.up * keptYOffset;
                var obj = SpawnDice(gm.MyConfirmed[i], pos, isMine: true);
                _myConfirmedObjects.Add(obj);
            }
        }

        // 4. Fila confirmada del rival
        if (gm.EnemyConfirmed != null && enemyKeptRowOrigin != null)
        {
            Vector3 dir = -enemyKeptRowOrigin.right;
            for (int i = 0; i < gm.EnemyConfirmed.Count; i++)
            {
                Vector3 pos = enemyKeptRowOrigin.position
                            + dir * i * keptRowSpacing
                            + Vector3.up * keptYOffset;
                var obj = SpawnDice(gm.EnemyConfirmed[i], pos, isMine: false);
                _enemyConfirmedObjects.Add(obj);
            }
        }
    }

    /// Versión de SpawnDecorativeBowl que pone N dados en lugar de 6.
    private void SpawnDecorativeBowlPartial(Transform center, List<GameObject> list, bool isMine, int count)
    {
        if (center == null) return;
        if (count <= 0) return;

        for (int i = 0; i < count && i < BowlOffsets.Length; i++)
        {
            var data = new DiceData
            {
                face = AllFaces[Random.Range(0, AllFaces.Length)],
                energy = false,
                kept = false,
                isMyDice = isMine
            };

            Vector3 pos = center.position + BowlOffsets[i] + Vector3.up * 0.3f;
            var diceObj = SpawnDice(data, pos, isMine);

            var ctrl = diceObj.GetComponent<DiceController>();
            if (ctrl != null) ctrl.enabled = false;

            list.Add(diceObj);
        }
    }

    /// Llamado al hacer clic en un dado del cuenco propio.
    public void RefreshKeptRows()
    {
        var gm = GameManager.Instance;
        if (gm?.MyDice == null || myKeptRowOrigin == null) return;

        Vector3 dir = -myKeptRowOrigin.right;
        int startSlot = gm.MyConfirmed?.Count ?? 0;

        int bowlSlot = 0;
        int keptSlot = 0;

        for (int i = 0; i < _myBowlObjects.Count && i < gm.MyDice.Count; i++)
        {
            var obj = _myBowlObjects[i];
            var d = gm.MyDice[i];
            Vector3 pos;

            if (!d.kept)
            {
                pos = myBowlCenter.position + BowlOffsets[bowlSlot] + Vector3.up * 0.3f;
                bowlSlot++;
            }
            else
            {
                pos = myKeptRowOrigin.position
                    + dir * (startSlot + keptSlot) * keptRowSpacing
                    + Vector3.up * keptYOffset;
                keptSlot++;
            }

            var ctrl = obj.GetComponent<DiceController>();
            ctrl?.SetRestPosition(pos);
            ctrl?.ApplyVisual();
        }
    }

    // ── Spawn helpers ────────────────────────────────────────

    private GameObject SpawnDice(DiceData data, Vector3 pos, bool isMine)
    {
        var obj = Instantiate(dicePrefab, pos, Quaternion.identity);
        ApplyDiceColor(obj, data);

        data.isMyDice = isMine;

        var ctrl = obj.GetComponent<DiceController>();
        if (ctrl == null) ctrl = obj.AddComponent<DiceController>();
        ctrl.enabled = true;
        ctrl.Init(data);
        ctrl.SetRestPosition(pos);
        ctrl.ApplyVisual();

        return obj;
    }

    private void ApplyDiceColor(GameObject obj, DiceData data)
    {
        var renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;

        var mat = new Material(renderer.material);

        if (FaceColors.TryGetValue(data.face, out Color color))
            mat.color = color;

        if (data.energy)
            mat.SetColor("_EmissionColor", new Color(0.8f, 0.6f, 0.0f) * 2f);

        renderer.material = mat;
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
}