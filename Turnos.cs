// Versus Bot — orden de elección de cartas (1.1.0). © 2026 Angel. MIT.
// En el juego los bandos eligen por turnos y los zombis siempre eligen la última carta: así pueden
// guardarse una carta ganadora (por ejemplo el globo) que las plantas ya no alcanzan a responder.
// Pedido del usuario (2026-11-23), en todos los mapas y también con el bot zombi: los zombis eligen
// su penúltima y su última carta en el mismo turno, y las plantas eligen al final.
//   Antes: P Z P Z P Z P Z P Z      Ahora: P Z P Z P Z P Z Z P
using System;
using HarmonyLib;
using Il2CppReloaded.Gameplay;

namespace VersusBot;

[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.ClickedSeedInChooser))]
internal static class ParcheDobleTurnoZombi
{
    private static void Prefix(SeedChooserScreen __instance, out bool __state)
    {
        var versus = Versus(__instance);
        __state = versus != null && versus.Phase == VersusPhase.ChooseZombiePacket;
    }

    private static void Postfix(SeedChooserScreen __instance, bool __state)
    {
        var versus = Versus(__instance);
        // Solo si eligieron los zombis y el juego ya pasó el turno a las plantas.
        if (!__state || versus == null || versus.Phase != VersusPhase.ChoosePlantPacket)
        {
            return;
        }
        var tablero = __instance.mBoard;
        var banco = tablero?.SeedBanks?[versus.ZombiePlayerIndex];
        if (banco == null || __instance.mChosenZombies == null)
        {
            return;
        }
        // Cartas zombi ya elegidas (también la tumba fija y las que van volando al banco).
        var elegidas = 0;
        for (var i = 0; i < __instance.mChosenZombies.Count; i++)
        {
            var carta = __instance.mChosenZombies[i];
            elegidas += carta != null && carta.mSeedState != ChosenSeedState.SeedInChooser ? 1 : 0;
        }
        // Falta exactamente una: los zombis eligen otra vez en este mismo turno.
        if (elegidas == banco.mNumPackets - 1)
        {
            versus.Phase = VersusPhase.ChooseZombiePacket;
            VersusBotMod.Log.Msg("Los zombis eligen su penúltima y su última carta seguidas (" + elegidas + "/" + banco.mNumPackets + ")");
        }
    }

    private static VersusMode Versus(SeedChooserScreen pantalla)
    {
        var partida = pantalla?.mApp;
        return partida != null && partida.IsVersusMode() ? partida.VersusMode : null;
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}
