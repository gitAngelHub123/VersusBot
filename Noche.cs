// Versus Bot — reglas de los mapas de noche (1.1.0). © 2026 Angel. MIT.
// El juego decide todo lo de la noche por el fondo del tablero (Night o Fog):
// setas despiertas, sin caída del cielo, niebla en Fog y tumbas al azar en Night.
// Reglas pedidas para Versus: sin niebla, sin tumbas al azar, cielo al doble de lento
// (sol y cerebros) y tumbas de los zombis que dan cerebros el doble de rápido.
using System;
using HarmonyLib;
using Il2CppReloaded.Gameplay;

namespace VersusBot;

internal static class ReglasDeNoche
{
    // Cuánto más lento cae el cielo de noche y cuánto más rápido producen las tumbas.
    internal const int FactorCielo = 2;
    internal const int FactorTumbas = 2;

    internal static bool Activas(Board tablero) =>
        MapaVersus.Noche && tablero != null && MapaVersus.EsVersus(tablero.mApp);
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
// (jardín o piscina) para que caigan sol y cerebros, y la espera hasta la siguiente se alarga.
[HarmonyPatch(typeof(Board), "UpdateSunSpawning")]
internal static class ParcheCieloDeNoche
{
    private struct Estado
    {
        internal bool Activo;
        internal BackgroundType Fondo;
        internal int Espera;
    }

    private static void Prefix(Board __instance, out Estado __state)
    {
        __state = default;
        if (!ReglasDeNoche.Activas(__instance))
        {
            return;
        }
        __state.Activo = true;
        __state.Fondo = __instance.mBackground;
        __state.Espera = __instance.mSunCountDown;
        __instance.mBackground = MapaVersus.Piscina ? BackgroundType.Pool : BackgroundType.Day;
    }

    private static void Postfix(Board __instance, Estado __state)
    {
        if (!__state.Activo)
        {
            return;
        }
        __instance.mBackground = __state.Fondo;
        // Si el juego programó una nueva caída, la espera subió: se multiplica.
        if (__instance.mSunCountDown > __state.Espera)
        {
            __instance.mSunCountDown *= ReglasDeNoche.FactorCielo;
        }
    }

    // Si algo falla, el fondo vuelve a ser el de noche igual.
    private static Exception Finalizer(Board __instance, Estado __state, Exception __exception)
    {
        if (__state.Activo && __instance != null)
        {
            __instance.mBackground = __state.Fondo;
        }
        return MapaVersus.Registrar(__exception);
    }
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
