public enum CharacterGender
{
    Male,
    Female
}

public static class CharacterGenderUtility
{
    public static string GetSkinId(CharacterGender gender)
    {
        return gender switch
        {
            CharacterGender.Female => "char_a_p1_0bas_humn_v02",
            _ => "char_a_p1_0bas_humn_v00"
        };
    }

    public static string GetDisplayName(CharacterGender gender)
    {
        return gender switch
        {
            CharacterGender.Female => "Feminino",
            _ => "Masculino"
        };
    }
}
