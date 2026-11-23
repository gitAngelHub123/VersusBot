// Versus Bot — ventaja de sol para el bot de plantas en el nivel máximo (1.1.0). © 2026 Angel. MIT.
// Pedido del usuario (2026-11-23): subir la dificultad del bot de plantas hasta una partida pareja.
// En las pruebas de noche la economía zombi ganaba por mucho (16 tumbas, 875 cerebros) y el bot
// pasaba la mayor parte del tiempo sin soles. Cada sol que suma el bot de plantas vale un 50 % más.
using System;
using HarmonyLib;
using Il2CppReloaded.Gameplay;
using UnityEngine;

namespace VersusBot;

[HarmonyPatch(typeof(Board), nameof(Board.AddSunMoney))]
internal static class ParcheVentajaDeSol
{
    // 1,5 = un 50 % más de sol. Ajustable si la partida queda despareja.
    internal const float FactorSolDelBot = 1.5f;

    private static void Prefix(Board __instance, ref int theAmount, int playerIndex)
    {
        if (theAmount <= 0 || VersusBotMod.Nivel < Bot.NivelMaestro || !VersusBotMod.JugadorEsBot(playerIndex))
        {
            return;
        }
        var versus = __instance.mApp != null && __instance.mApp.IsVersusMode() ? __instance.mApp.VersusMode : null;
        if (versus == null || versus.PlantPlayerIndex != playerIndex)
        {
            return;
        }
        theAmount = Mathf.RoundToInt(theAmount * FactorSolDelBot);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}
