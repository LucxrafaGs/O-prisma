using System;
using System.Collections.Generic;

/// <summary>
/// Runtime backpack state for Prisma. Six free slots, no weight, no expansion.
/// Fixed items sit outside the free inventory.
/// </summary>
public static class PlayerInventory
{
    public const int FreeSlotCount = 6;

    public sealed class ItemStack
    {
        public string Id;
        public string DisplayName;
        public string Kind; // small | consumable | key | quest
        public string Description;
    }

    public sealed class FixedItem
    {
        public string Id;
        public string DisplayName;
        public bool Unlocked;
        public string Description;
    }

    private static readonly ItemStack[] FreeSlots = new ItemStack[FreeSlotCount];

    private static readonly List<FixedItem> FixedItems = new()
    {
        new FixedItem { Id = "lanterna", DisplayName = "Lanterna", Unlocked = true, Description = "Ilumina cantos escuros." },
        new FixedItem { Id = "camera", DisplayName = "Câmera", Unlocked = true, Description = "Registra o que os olhos duvidam." },
        new FixedItem { Id = "toca_fitas", DisplayName = "Toca-fitas", Unlocked = true, Description = "Aparelho portátil de cassetes." },
        new FixedItem { Id = "cracha", DisplayName = "Crachá escolar", Unlocked = false, Description = "Ainda não entregue." },
        new FixedItem { Id = "carteira", DisplayName = "Carteira", Unlocked = true, Description = "Documentos e pouco dinheiro." },
        new FixedItem { Id = "mapa", DisplayName = "Mapa de Pedra Branca", Unlocked = true, Description = "Locais descobertos à mão." }
    };

    public static IReadOnlyList<ItemStack> GetFreeSlots() => FreeSlots;

    public static IReadOnlyList<FixedItem> GetFixedItems() => FixedItems;

    public static bool TryAdd(ItemStack item)
    {
        if (item == null)
            return false;

        for (int i = 0; i < FreeSlots.Length; i++)
        {
            if (FreeSlots[i] != null)
                continue;

            FreeSlots[i] = item;
            return true;
        }

        return false;
    }

    public static ItemStack RemoveAt(int slot)
    {
        if (slot < 0 || slot >= FreeSlots.Length)
            return null;

        ItemStack item = FreeSlots[slot];
        FreeSlots[slot] = null;
        return item;
    }

    public static void UnlockFixed(string id)
    {
        foreach (FixedItem item in FixedItems)
        {
            if (item.Id == id)
                item.Unlocked = true;
        }
    }
}

public static class AgendaJournal
{
    public sealed class ScheduleEntry
    {
        public string Time;
        public string Title;
    }

    public sealed class NoteEntry
    {
        public string DateLabel;
        public string Category;
        public string Text;
    }

    public static string WeekdayLabel = "Segunda-feira";
    public static string MonthLabel = "Março";
    public static int DayOfMonth = 3;

    public static readonly List<ScheduleEntry> TodaySchedule = new()
    {
        new ScheduleEntry { Time = "08:00", Title = "Matemática" },
        new ScheduleEntry { Time = "10:00", Title = "História" },
        new ScheduleEntry { Time = "14:00", Title = "Clube de Música" },
        new ScheduleEntry { Time = "18:00", Title = "Festival da Praça" }
    };

    public static readonly List<NoteEntry> Notes = new()
    {
        new NoteEntry
        {
            DateLabel = "02/03",
            Category = "Pista",
            Text = "Um folder rasgado menciona 'Projeto Prisma' no verso da biblioteca."
        },
        new NoteEntry
        {
            DateLabel = "03/03",
            Category = "Foto",
            Text = "Fotografia da quadra após o horário — sombra que não deveria estar ali."
        }
    };

    public static void AddNote(string category, string text)
    {
        Notes.Insert(0, new NoteEntry
        {
            DateLabel = DateTime.Now.ToString("dd/MM"),
            Category = category,
            Text = text
        });
    }
}

public static class PeopleJournal
{
    public enum RelationState
    {
        Desconhecido,
        Conhecido,
        Colega,
        Amigo,
        Confidente
    }

    public sealed class PersonCard
    {
        public string Id;
        public string Name;
        public string Role;
        public RelationState Relation;
        public string Birthday;
        public string Likes;
        public string Notes;
        public bool Discovered;
    }

    public static readonly List<PersonCard> People = new()
    {
        new PersonCard
        {
            Id = "helena",
            Name = "Helena",
            Role = "3º Ano B",
            Relation = RelationState.Amigo,
            Birthday = "Aniversário desconhecido",
            Likes = "Gosta de fotografia",
            Notes = "Costuma estudar na biblioteca.",
            Discovered = true
        },
        new PersonCard
        {
            Id = "lucas",
            Name = "Lucas",
            Role = "3º Ano A",
            Relation = RelationState.Colega,
            Birthday = "Aniversário desconhecido",
            Likes = "Ainda não descoberto",
            Notes = "Costuma ficar na quadra depois da aula.",
            Discovered = true
        }
    };

    public static IReadOnlyList<PersonCard> KnownPeopleList()
    {
        List<PersonCard> known = new();
        foreach (PersonCard person in People)
        {
            if (person.Discovered)
                known.Add(person);
        }

        return known;
    }

    public static string RelationLabel(RelationState state)
    {
        return state switch
        {
            RelationState.Desconhecido => "Desconhecido",
            RelationState.Conhecido => "Conhecido",
            RelationState.Colega => "Colega",
            RelationState.Amigo => "Amigo",
            RelationState.Confidente => "Confidente",
            _ => state.ToString()
        };
    }
}

public static class IslandMapJournal
{
    public sealed class PlaceMark
    {
        public string Id;
        public string Name;
        public string District;
        public bool Discovered;
        public string Annotation;
        public string PlayerMark;
    }

    public static readonly List<PlaceMark> Places = new()
    {
        new PlaceMark
        {
            Id = "escola",
            Name = "Escola Municipal",
            District = "Centro",
            Discovered = true,
            Annotation = "Seu ponto de partida quase todos os dias."
        },
        new PlaceMark
        {
            Id = "praca",
            Name = "Praça Central",
            District = "Centro",
            Discovered = true,
            Annotation = "Festival da Praça marcado para o fim da tarde."
        },
        new PlaceMark
        {
            Id = "quadra",
            Name = "Quadra",
            District = "Escola",
            Discovered = true,
            Annotation = "Lucas costuma ficar na quadra depois da aula."
        },
        new PlaceMark
        {
            Id = "farol",
            Name = "Farol",
            District = "Costa Norte",
            Discovered = false,
            Annotation = string.Empty
        }
    };

    public static IReadOnlyList<PlaceMark> DiscoveredPlacesList()
    {
        List<PlaceMark> discovered = new();
        foreach (PlaceMark place in Places)
        {
            if (place.Discovered)
                discovered.Add(place);
        }

        return discovered;
    }
}

public static class WalkmanLibrary
{
    public sealed class Tape
    {
        public string Id;
        public string Title;
        public string Kind; // musica | entrevista | relato | ruido
        public bool Unlocked;
        public string Blurb;
    }

    public static readonly List<Tape> Tapes = new()
    {
        new Tape
        {
            Id = "abertura",
            Title = "Tema de Pedra Branca",
            Kind = "musica",
            Unlocked = true,
            Blurb = "Melodia suave da manhã na ilha."
        },
        new Tape
        {
            Id = "estatica",
            Title = "Fita sem rótulo",
            Kind = "ruido",
            Unlocked = false,
            Blurb = "Ainda não encontrada."
        },
        new Tape
        {
            Id = "entrevista_radio",
            Title = "Entrevista da rádio local",
            Kind = "entrevista",
            Unlocked = false,
            Blurb = "Ainda não encontrada."
        }
    };

    public static string ActiveTapeId = "abertura";

    public static IReadOnlyList<Tape> UnlockedTapesList()
    {
        List<Tape> unlocked = new();
        foreach (Tape tape in Tapes)
        {
            if (tape.Unlocked)
                unlocked.Add(tape);
        }

        return unlocked;
    }
}
