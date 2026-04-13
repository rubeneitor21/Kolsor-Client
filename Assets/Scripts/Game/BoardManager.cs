using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject dicePrefab;
    public GameObject stonePrefab;

    [Header("Piedras de vida")]
    public Transform playerStonesOrigin;   // punto de inicio fila del jugador
    public Transform opponentStonesOrigin; // punto de inicio fila del rival

    private List<GameObject> _playerStones = new();
    private List<GameObject> _opponentStones = new();

    // Llama a esto desde Start() después de NotifyBoardReady()
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

    [Header("Posiciones")]
    public Transform myBowlCenter;
    public Transform enemyBowlCenter;

    // Colores por cara
    private static readonly Dictionary<DiceFace, Color> FaceColors = new()
    {
        { DiceFace.Axe,    new Color(0.80f, 0.15f, 0.15f) }, // rojo
        { DiceFace.Arrow,  new Color(0.15f, 0.60f, 0.80f) }, // azul
        { DiceFace.Helmet, new Color(0.60f, 0.60f, 0.60f) }, // gris
        { DiceFace.Shield, new Color(0.20f, 0.65f, 0.25f) }, // verde
        { DiceFace.Hand,   new Color(0.85f, 0.65f, 0.10f) }, // dorado
    };

    private List<GameObject> _myDiceObjects = new();
    private List<GameObject> _enemyDiceObjects = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        Debug.Log("[Board] BoardManager.Start() ejecutado");
        SpawnStones();
        GameManager.Instance?.NotifyBoardReady();
    }

    public void SpawnDice()
    {
        ClearDice();
        SpawnDiceForPlayer(GameManager.Instance.MyDice, myBowlCenter, _myDiceObjects);
        SpawnDiceForPlayer(GameManager.Instance.EnemyDice, enemyBowlCenter, _enemyDiceObjects);
    }

    private void SpawnDiceForPlayer(List<DiceData> dice, Transform center, List<GameObject> objects)
    {
        // Colocamos los 6 dados en dos filas de 3
        Vector3[] offsets = new Vector3[]
        {
            new Vector3(-0.55f, 0, -0.3f),
            new Vector3( 0.00f, 0, -0.3f),
            new Vector3( 0.55f, 0, -0.3f),
            new Vector3(-0.55f, 0,  0.3f),
            new Vector3( 0.00f, 0,  0.3f),
            new Vector3( 0.55f, 0,  0.3f),
        };

        for (int i = 0; i < dice.Count && i < offsets.Length; i++)
        {
            Vector3 pos = center.position + offsets[i] + Vector3.up * 0.3f;
            var obj = Instantiate(dicePrefab, pos, Quaternion.identity);
            ApplyDiceColor(obj, dice[i]);
            objects.Add(obj);
        }
    }

    private void ApplyDiceColor(GameObject obj, DiceData data)
    {
        var renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;

        var mat = new Material(renderer.material);

        if (FaceColors.TryGetValue(data.face, out Color color))
            mat.color = color;

        // Borde dorado si tiene energía
        if (data.energy)
            mat.SetColor("_EmissionColor", new Color(0.8f, 0.6f, 0.0f) * 2f);

        renderer.material = mat;
    }

    private void ClearDice()
    {
        foreach (var obj in _myDiceObjects) if (obj) Destroy(obj);
        foreach (var obj in _enemyDiceObjects) if (obj) Destroy(obj);
        _myDiceObjects.Clear();
        _enemyDiceObjects.Clear();
    }


}