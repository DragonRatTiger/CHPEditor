namespace CHPEditor
{
    public enum StateNames : int
    {
        Neutral,
        Second,
        Ojama,
        Miss,
        Standing,
        Fever,
        Great,
        Good,
        PlayerHitsBad,
        PlayerHitsFever,
        PlayerHitsGreat,
        PlayerHitsGood,
        State13,
        Dance,
        Win,
        Lose,
        FeverWin,
        AttackedByOjama,

        MINIMUM = Neutral,
        MAXIMUM = AttackedByOjama,
        COUNT = MAXIMUM + 1,
        UNKNOWN = -1
    }
}
