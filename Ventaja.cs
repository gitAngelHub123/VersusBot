// Versus Bot — ventaja de sol para el bot de plantas en el nivel máximo (1.1.0). © 2026 Angel. MIT.
// Pedido del usuario (2026-11-23): subir la dificultad del bot de plantas hasta una partida pareja.
// En las pruebas de noche la economía zombi ganaba por mucho (16 tumbas, 875 cerebros) y el bot
// pasaba la mayor parte del tiempo sin soles. Cada sol que suma el bot de plantas vale el doble.
using System;
using HarmonyLib;
using Il2CppReloaded.Gameplay;
using UnityEngine;

namespace VersusBot;

[HarmonyPatch(typeof(Board), nameof(Board.AddSunMoney))]
internal static class ParcheVentajaDeSol
{
    // 2 = el doble de sol (+100 %, pedido del usuario). Ajustable si la partida queda despareja.
    internal const float FactorSolDelBot = 2f;

    // Desde el nivel 100 sin ventaja; cada 25 niveles más, un 25 % más de sol (200 = el doble).
    private const int NivelSinVentaja = 100;

    internal static float FactorSegunNivel =>
        1f + (FactorSolDelBot - 1f) * UnityEngine.Mathf.Clamp01((VersusBotMod.Nivel - NivelSinVentaja) / (float)(Bot.NivelMaestro - NivelSinVentaja));

    // Fracción de la ventaja según el nivel: 0 en el 100, 1 en el 200.
    internal static float FraccionSegunNivel =>
        UnityEngine.Mathf.Clamp01((VersusBotMod.Nivel - NivelSinVentaja) / (float)(Bot.NivelMaestro - NivelSinVentaja));

    // Para sumar sol sin pasar por la ventaja (la devolución del café ya viene calculada).
    internal static bool SinVentaja { get; set; }

    private static void Prefix(Board __instance, ref int theAmount, int playerIndex)
    {
        if (SinVentaja || theAmount <= 0 || VersusBotMod.Nivel <= NivelSinVentaja || !VersusBotMod.JugadorEsBot(playerIndex))
        {
            return;
        }
        var versus = __instance.mApp != null && __instance.mApp.IsVersusMode() ? __instance.mApp.VersusMode : null;
        if (versus == null || versus.PlantPlayerIndex != playerIndex)
        {
            return;
        }
        theAmount = Mathf.RoundToInt(theAmount * FactorSegunNivel);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// De día cada seta del bot necesita un Grano de café, y cada seta comida vuelve a pedirlo: en la prueba
// de piscina de día el bot plantó 25 cafés y pasó casi la mitad de la partida sin soles. Para que el día
// sea tan parejo como la noche (donde no hace falta café), al bot de plantas se le devuelve el café
// según el nivel: todo en el 200, nada en el 100.
// Mazo de día rehecho (2026-11-23, pedido del usuario): a la Repetidora y al Guisante de hielo se les
// devuelve la mitad del precio, también escalado por nivel (la mitad en el 200, nada en el 100).
[HarmonyPatch(typeof(Plant), nameof(Plant.PlantInitialize))]
internal static class ParcheCafeDelBot
{
    // Parte del precio que se devuelve en el nivel máximo.
    private static float Devolucion(SeedType tipo) => tipo switch
    {
        SeedType.InstantCoffee => 1f,
        SeedType.Repeater => 0.5f,
        SeedType.Snowpea => 0.5f,
        _ => 0f,
    };

    private static void Postfix(Plant __instance)
    {
        var tablero = __instance.mBoard;
        var parte = Devolucion(__instance.mSeedType);
        if (parte <= 0f || tablero?.mApp == null || !tablero.mApp.IsVersusMode() || VersusBotMod.Nivel <= 100)
        {
            return;
        }
        var versus = tablero.mApp.VersusMode;
        if (versus == null || !VersusBotMod.JugadorEsBot(versus.PlantPlayerIndex))
        {
            return;
        }
        var devolucion = Mathf.RoundToInt(Plant.GetCost(tablero.mApp, __instance.mSeedType, SeedType.None) * parte * ParcheVentajaDeSol.FraccionSegunNivel);
        if (devolucion <= 0)
        {
            return;
        }
        ParcheVentajaDeSol.SinVentaja = true;
        try
        {
            tablero.AddSunMoney(devolucion, versus.PlantPlayerIndex);
        }
        finally
        {
            ParcheVentajaDeSol.SinVentaja = false;
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}
