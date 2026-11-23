// Versus Bot — reglas de los mapas de noche (1.1.0). © 2026 Angel. MIT.
// El juego decide todo lo de la noche por el fondo del tablero (Night o Fog):
// setas despiertas, sin caída del cielo, niebla en Fog y tumbas al azar en Night.
// Reglas pedidas para Versus: sin niebla, sin tumbas al azar, cielo al doble de lento
// (solo los cerebros; el sol de las plantas cae como de día) y tumbas que dan cerebros el doble de rápido.
using System;
using HarmonyLib;
using Il2CppReloaded.Gameplay;

namespace VersusBot;

internal static class ReglasDeNoche
{
    // De noche: los cerebros del cielo caen 1 de cada FactorCielo veces (el sol, igual que de día);
    // las tumbas producen FactorTumbas veces más rápido.
    internal const int FactorCielo = 2;
    internal const int FactorTumbas = 2;

    internal static bool Activas(Board tablero) =>
        MapaVersus.Noche && tablero != null && MapaVersus.EsVersus(tablero.mApp);
}

// La carta fija del jugador de plantas: el Girasol de día, la Seta solar de noche.
// En Versus el juego pone el Girasol directo en el espacio 1 del banco (no pasa por la lista
// de cartas elegidas) y lo bloquea en el selector con SeedNotAllowedToPick.
// Aquí se cambia ese espacio según el mapa; el bloqueo del selector está en ParcheNenufarPermitido.
internal static class CartaFija
{
    internal static SeedType Actual => MapaVersus.Noche ? SeedType.Sunshroom : SeedType.Sunflower;

    internal static void Ajustar(SeedChooserScreen selector)
    {
        var tablero = selector?.mBoard;
        if (tablero == null || !MapaVersus.EsVersus(tablero.mApp) || tablero.SeedBanks == null)
        {
            return;
        }
        var quitar = MapaVersus.Noche ? SeedType.Sunflower : SeedType.Sunshroom;
        // Versus tiene 2 jugadores: el banco de las plantas es el que tiene la carta fija en el espacio 1.
        for (var jugador = 0; jugador <= 1; jugador++)
        {
            var banco = tablero.SeedBanks[jugador];
            var paquetes = banco?.mSeedPackets;
            if (paquetes == null || paquetes.Length == 0 || paquetes[0] == null || paquetes[0].mPacketType != quitar)
            {
                continue;
            }
            paquetes[0].SetPacketType(Actual, SeedType.None);
            VersusBotMod.Log.Msg("Carta fija de las plantas: " + quitar + " → " + Actual + " (jugador " + (jugador + 1) + ")");
        }
    }
}

// Sin niebla en la piscina de noche.
[HarmonyPatch(typeof(Board), nameof(Board.StageHasFog))]
internal static class ParcheSinNiebla
{
    private static void Postfix(Board __instance, ref bool __result)
    {
        if (__result && ReglasDeNoche.Activas(__instance))
        {
            __result = false;
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// Sin tumbas al azar al empezar el jardín de noche: en Versus las tumbas son del bando zombi.
[HarmonyPatch(typeof(Board), nameof(Board.StageHasGraveStones))]
internal static class ParcheSinTumbasAlAzar
{
    private static void Postfix(Board __instance, ref bool __result)
    {
        if (__result && ReglasDeNoche.Activas(__instance))
        {
            __result = false;
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// De noche el juego no deja caer nada del cielo. Durante la caída se le muestra el fondo de día
// (jardín o piscina) para que caiga como de día. El sol de las plantas cae igual que de día; los
// cerebros, que caen a la vez y con el mismo contador, se retiran en 1 de cada 2 caídas.
[HarmonyPatch(typeof(Board), "UpdateSunSpawning")]
internal static class ParcheCieloDeNoche
{
    private struct Estado
    {
        internal bool Activo;
        internal BackgroundType Fondo;
    }

    private static int _caidas;
    // Mientras dura la caída actual: ¿se retiran los cerebros que aparezcan?
    internal static bool SinCerebrosAhora { get; private set; }

    private static void Prefix(Board __instance, out Estado __state)
    {
        __state = default;
        if (!ReglasDeNoche.Activas(__instance))
        {
            return;
        }
        __state.Activo = true;
        __state.Fondo = __instance.mBackground;
        __instance.mBackground = MapaVersus.Piscina ? BackgroundType.Pool : BackgroundType.Day;
        // El juego baja el contador de 1 en 1 y suelta las monedas cuando llega a 0.
        if (__instance.mSunCountDown == 1)
        {
            _caidas++;
            SinCerebrosAhora = _caidas % ReglasDeNoche.FactorCielo != 0;
        }
    }

    private static void Postfix(Board __instance, Estado __state)
    {
        SinCerebrosAhora = false;
        if (__state.Activo)
        {
            __instance.mBackground = __state.Fondo;
        }
    }

    // Si algo falla, el fondo vuelve a ser el de noche igual.
    private static Exception Finalizer(Board __instance, Estado __state, Exception __exception)
    {
        SinCerebrosAhora = false;
        if (__state.Activo && __instance != null)
        {
            __instance.mBackground = __state.Fondo;
        }
        return MapaVersus.Registrar(__exception);
    }
}

// Retira los cerebros del cielo en las caídas que tocan sin cerebros (ver ParcheCieloDeNoche).
[HarmonyPatch(typeof(Board), nameof(Board.AddCoin))]
internal static class ParcheCerebrosDeNoche
{
    private static void Postfix(Coin __result, CoinType theCoinType)
    {
        if (ParcheCieloDeNoche.SinCerebrosAhora && theCoinType == CoinType.Brain && __result != null)
        {
            __result.Die();
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// En los mapas de noche las setas siempre están despiertas: cualquier orden de dormir se anula.
[HarmonyPatch(typeof(Plant), nameof(Plant.SetSleeping))]
internal static class ParcheSetasDespiertas
{
    private static void Prefix(Plant __instance, ref bool theIsAsleep)
    {
        if (theIsAsleep && ReglasDeNoche.Activas(__instance.mBoard))
        {
            theIsAsleep = false;
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// En los mapas de noche las setas solares nacen grandes (dan 25 en vez de 15).
// El juego las planta pequeñas con 12000 ciclos (120 s) para crecer; aquí se deja en 1 y
// crecen enseguida con su propia animación.
[HarmonyPatch(typeof(Plant), nameof(Plant.PlantInitialize))]
internal static class ParcheSetaSolarGrande
{
    private const int CicloParaCrecer = 1;

    private static void Postfix(Plant __instance)
    {
        if (__instance.mSeedType == SeedType.Sunshroom && __instance.mState == PlantState.SunshroomSmall
            && __instance.mStateCountdown > CicloParaCrecer && ReglasDeNoche.Activas(__instance.mBoard))
        {
            __instance.mStateCountdown = CicloParaCrecer;
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// Tumbas de los zombis: su contador baja de 1 en 1 cada ciclo; de noche baja de 2 en 2.
[HarmonyPatch(typeof(Zombie), nameof(Zombie.UpdateGravestone))]
internal static class ParcheTumbasDeNoche
{
    private static void Prefix(Zombie __instance)
    {
        if (__instance.mPhaseCounter > ReglasDeNoche.FactorTumbas && ReglasDeNoche.Activas(__instance.mBoard))
        {
            __instance.mPhaseCounter -= ReglasDeNoche.FactorTumbas - 1;
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}
