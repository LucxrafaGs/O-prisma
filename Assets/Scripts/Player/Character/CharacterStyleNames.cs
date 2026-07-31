using System.Collections.Generic;

public static class CharacterStyleNames
{
    private static readonly Dictionary<string, string> Names = new()
    {
        { "boxr", "Boxer" },
        { "undi", "Roupa intima" },
        { "fstr", "Tunica fazendeiro" },
        { "pfpn", "Calca camponesa" },
        { "pfdr", "Vestido campones" },
        { "pfht", "Chapeu campones" },
        { "pfbn", "Chapeu bonnet" },
        { "pnty", "Chapeu pontudo" },
        { "rnht", "Chapeu chuva" },
        { "band", "Bandana" },
        { "angl", "Calca pescador" },
        { "bksm", "Avental ferreiro" },
        { "alch", "Jaleco alquimista" },
        { "bob1", "Cabelo Bob" },
        { "bob2", "Cabelo Bob 2" },
        { "dap1", "Cabelo Dapper" },
        { "flat", "Cabelo Flat" },
        { "fro1", "Cabelo Afro" },
        { "pon1", "Cabelo Rabo" },
        { "spk2", "Cabelo Spiky" },
        { "lnpl", "Capa longa" },
        { "mnpl", "Manto" },
        { "hdpl", "Capuz cima" },
        { "hddn", "Capuz baixo" },
        { "gogl", "Oculos" },
        { "humn", "Humano" },
        { "demn", "Demonio" },
        { "gbln", "Goblin" }
    };

    public static string GetStyleTitle(CharacterLayerType layer, string styleCode)
    {
        if (layer == CharacterLayerType.Skin)
        {
            return Names.TryGetValue(styleCode, out string skinName) ? skinName : styleCode.ToUpperInvariant();
        }

        return Names.TryGetValue(styleCode, out string mapped) ? mapped : styleCode.ToUpperInvariant();
    }
}
