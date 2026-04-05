[System.Serializable]
public class BuffEffect
{
    public float damageMultiplier = 1f;
    public float rangeMultiplier = 1f;
    public float fireRateMultiplier = 1f;
    public int bonusStartGold = 0;
    public int bonusLives = 0;
    public bool multiTarget = false;

    public static BuffEffect Default()
    {
        return new BuffEffect();
    }

    public void AddBuff(BuffEffect other)
    {
        damageMultiplier += (other.damageMultiplier - 1f);
        rangeMultiplier += (other.rangeMultiplier - 1f);
        fireRateMultiplier += (other.fireRateMultiplier - 1f);
        bonusStartGold += other.bonusStartGold;
        bonusLives += other.bonusLives;
        if (other.multiTarget) multiTarget = true;
    }
}
