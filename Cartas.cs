// Versus Bot — lo que el bot sabe de las cartas: qué necesita cada una y qué la contrarresta.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using System;
using System.Collections.Generic;
using Il2CppReloaded.Gameplay;

namespace VersusBot;

internal static class Cartas
{
    private static readonly SeedType[] Hongos =
    {
        SeedType.Puffshroom, SeedType.Sunshroom, SeedType.Fumeshroom, SeedType.Hypnoshroom, SeedType.Scaredyshroom,
        SeedType.Iceshroom, SeedType.Doomshroom, SeedType.Seashroom, SeedType.Magnetshroom, SeedType.Gloomshroom
    };

    // No hay agua ni techo en el tablero de Versus: estas cartas no tienen dónde servir.
    private static readonly SeedType[] SinUso =
    {
        SeedType.Lilypad, SeedType.Tanglekelp, SeedType.Seashroom, SeedType.Cattail, SeedType.Flowerpot, SeedType.GoldMagnet
    };

    private static readonly SeedType[] ZombisDeMetal =
    {
        SeedType.ZombiePail, SeedType.ZombieScreenDoor, SeedType.ZombieFootball, SeedType.ZombieLadder,
        SeedType.ZombiePogo, SeedType.ZombieJackInTheBox, SeedType.ZombieTrashCan, SeedType.ZombieDigger
    };

    private static readonly SeedType[] ZombisMuyFuertes =
    {
        SeedType.ZombiePail, SeedType.ZombieScreenDoor, SeedType.ZombieFootball,
        SeedType.ZombieTrashCan, SeedType.Zomboni, SeedType.ZombieGargantuar
    };

    // Ataque que no depende de otra carta.
    private static readonly SeedType[] AtaqueIndependiente =
    {
        SeedType.Peashooter, SeedType.Snowpea, SeedType.Repeater, SeedType.Threepeater, SeedType.Splitpea,
        SeedType.Cactus, SeedType.Starfruit, SeedType.Cabbagepult, SeedType.Kernelpult, SeedType.Melonpult
    };

    private sealed class Contrarresto
    {
        public SeedType[] Zombis;
        public SeedType[] Plantas;
    }

    // En orden de urgencia: sin respuesta, un globo pasa por encima de toda la defensa.
    private static readonly Contrarresto[] Contrarrestos =
    {
        new Contrarresto { Zombis = new[] { SeedType.ZombieBalloon }, Plantas = new[] { SeedType.Cactus, SeedType.Blover } },
        new Contrarresto { Zombis = new[] { SeedType.ZombieDigger }, Plantas = new[] { SeedType.Magnetshroom, SeedType.Splitpea, SeedType.Starfruit } },
        new Contrarresto { Zombis = ZombisDeMetal, Plantas = new[] { SeedType.Magnetshroom } },
        new Contrarresto { Zombis = new[] { SeedType.ZombieBungee, SeedType.ZombieCatapult }, Plantas = new[] { SeedType.Umbrella } },
        new Contrarresto { Zombis = new[] { SeedType.ZombiePolevaulter, SeedType.ZombiePogo }, Plantas = new[] { SeedType.Tallnut } },
        new Contrarresto { Zombis = new[] { SeedType.Zomboni }, Plantas = new[] { SeedType.Spikeweed, SeedType.Spikerock } }
    };

    // Nivel extremo: solo lo que la papa, la explosiva y la nuez no resuelven. Globo y excavador se responden
    // con un atacante, que además sirve de defensa normal.
    internal static List<SeedType[]> RespuestasMaestras(HashSet<SeedType> zombisRival, HashSet<SeedType> propias)
    {
        var pendientes = new List<SeedType[]>();
        if (zombisRival.Contains(SeedType.ZombieBalloon) && !propias.Contains(SeedType.Cactus))
        {
            pendientes.Add(new[] { SeedType.Cactus });
        }
        if (zombisRival.Contains(SeedType.ZombieDigger) && !propias.Contains(SeedType.Splitpea) && !propias.Contains(SeedType.Starfruit))
        {
            pendientes.Add(new[] { SeedType.Splitpea, SeedType.Starfruit });
        }
        if ((zombisRival.Contains(SeedType.ZombieBungee) || zombisRival.Contains(SeedType.ZombieCatapult)) && !propias.Contains(SeedType.Umbrella))
        {
            pendientes.Add(new[] { SeedType.Umbrella });
        }
        return pendientes;
    }

    internal static bool EsHongo(SeedType tipo)
    {
        return Array.IndexOf(Hongos, tipo) >= 0;
    }

    internal static bool EsDeMetal(SeedType tipo)
    {
        return Array.IndexOf(ZombisDeMetal, tipo) >= 0;
    }

    internal static bool HayZombiMuyFuerte(HashSet<SeedType> cartas)
    {
        foreach (var zombi in ZombisMuyFuertes)
        {
            if (cartas.Contains(zombi))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool NoSirve(SeedType tipo)
    {
        return Array.IndexOf(SinUso, tipo) >= 0;
    }

    internal static bool EsAtaqueIndependiente(SeedType tipo)
    {
        return Array.IndexOf(AtaqueIndependiente, tipo) >= 0;
    }

    // La carta que debe ir en el mazo para que esta sirva: el café para los hongos de día, o la planta que se mejora.
    internal static SeedType? Requisito(SeedType tipo)
    {
        if (EsHongo(tipo) && tipo != SeedType.Gloomshroom) return SeedType.InstantCoffee;
        switch (tipo)
        {
            case SeedType.Gatlingpea: return SeedType.Repeater;
            case SeedType.Twinsunflower: return SeedType.Sunflower;
            case SeedType.Gloomshroom: return SeedType.Fumeshroom;
            case SeedType.Wintermelon: return SeedType.Melonpult;
            case SeedType.Spikerock: return SeedType.Spikeweed;
            case SeedType.Cobcannon: return SeedType.Kernelpult;
            default: return null;
        }
    }

    // Un zombi está contrarrestado si el rival lleva alguna planta que lo anula.
    internal static bool EstaContrarrestado(SeedType zombi, HashSet<SeedType> plantasRival)
    {
        foreach (var contrarresto in Contrarrestos)
        {
            if (Array.IndexOf(contrarresto.Zombis, zombi) < 0)
            {
                continue;
            }
            foreach (var planta in contrarresto.Plantas)
            {
                if (plantasRival.Contains(planta))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Plantas que responden a zombis conocidos del rival que este mazo todavía no contrarresta.
    // Se devuelven todas: si una no se puede elegir, porque no cabe, se prueba con la siguiente.
    internal static List<SeedType[]> RespuestasPendientes(HashSet<SeedType> zombisRival, HashSet<SeedType> propias)
    {
        var pendientes = new List<SeedType[]>();
        foreach (var contrarresto in Contrarrestos)
        {
            var aparece = false;
            foreach (var zombi in contrarresto.Zombis)
            {
                aparece |= zombisRival.Contains(zombi);
            }
            var cubierto = false;
            foreach (var planta in contrarresto.Plantas)
            {
                cubierto |= propias.Contains(planta);
            }
            if (aparece && !cubierto)
            {
                pendientes.Add(contrarresto.Plantas);
            }
        }
        return pendientes;
    }
}
