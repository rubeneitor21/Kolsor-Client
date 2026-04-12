[System.Serializable]
public class DiceData
{
    public DiceFace face;
    public bool energy;
    public bool kept;       // el jugador lo ha guardado
    public bool isMyDice;   // true = mío, false = del rival
}

public enum DiceFace
{
    Axe,
    Arrow,
    Helmet,
    Shield,
    Hand
}