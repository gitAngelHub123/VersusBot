// Versus Bot — registro de las jugadas de las personas.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using System;
using HarmonyLib;
using Il2CppReloaded.Gameplay;
using UnityEngine;

namespace VersusBot;

// Anota en el registro lo que hacen las personas en Versus. Solo lee: no cambia ninguna jugada.
internal static class RegistroJugadas
{
    private const float EsperaParaLeerMazos = 2f;

    private static int _profundidadClic;
    private static bool _colocoEnEsteClic;
    private static int _clicX;
    private static int _clicY;
    private static int _clicJugador;
    private static readonly SeedType[] CartaEnMano = { SeedType.None, SeedType.None };
    private static IntPtr _tableroConMazos;
    private static float _leerMazosEn = -1f;

    private static bool EsPersonaEnVersus(int jugador)
    {
        var datos = VersusBotMod.Datos;
        return jugador >= 0 && jugador <= 1 && datos != null && datos.m_gameplayService.IsVSMode && !VersusBotMod.JugadorEsBot(jugador);
    }

    private static void Anotar(string texto)
    {
        VersusBotMod.Log.Msg("Jugador " + texto);
    }

    private static void Proteger(Action accion)
    {
        try
        {
            accion();
        }
        catch (Exception error)
        {
            VersusBotMod.Log.Warning("No se pudo anotar la jugada: " + error.Message);
        }
    }

    internal static void CartaElegida(SeedType carta, int jugador) => Proteger(() =>
    {
        if (EsPersonaEnVersus(jugador))
        {
            Anotar("J" + (jugador + 1) + " eligió: " + carta);
        }
    });

    internal static void CartaQuitada(SeedType carta, int jugador) => Proteger(() =>
    {
        if (EsPersonaEnVersus(jugador))
        {
            Anotar("J" + (jugador + 1) + " quitó del mazo: " + carta);
        }
    });

    internal static void CartaTomada(SeedType carta, int jugador) => Proteger(() =>
    {
        if (EsPersonaEnVersus(jugador))
        {
            CartaEnMano[jugador] = carta;
        }
    });

    internal static void EmpiezaClic(int x, int y, int jugador)
    {
        if (_profundidadClic == 0)
        {
            _colocoEnEsteClic = false;
            _clicX = x;
            _clicY = y;
            _clicJugador = jugador;
        }
        _profundidadClic++;
    }

    internal static void TerminaClic(Board tablero)
    {
        _profundidadClic = Math.Max(0, _profundidadClic - 1);
        if (_profundidadClic > 0 || _colocoEnEsteClic)
        {
            return;
        }
        Proteger(() =>
        {
            if (EsPersonaEnVersus(_clicJugador))
            {
                var carta = CartaEnMano[_clicJugador];
                Anotar("J" + (_clicJugador + 1) + " no pudo colocar " + carta + " en " + Casilla(tablero, carta));
            }
        });
    }

    internal static void CartaColocada(SeedPacket carta, int jugador) => Proteger(() =>
    {
        _colocoEnEsteClic = true;
        if (EsPersonaEnVersus(jugador))
        {
            Anotar("J" + (jugador + 1) + ": " + carta.mPacketType + " en " + Casilla(carta.mBoard, carta.mPacketType));
        }
    });

    internal static void Herramienta(Board tablero, int x, int y, CursorType tipo, int jugador) => Proteger(() =>
    {
        if (EsPersonaEnVersus(jugador))
        {
            Anotar("J" + (jugador + 1) + " usó " + tipo + " en columna " + tablero.PixelToGridX(x, y) + ", fila " + tablero.PixelToGridY(x, y));
        }
    });

    private static string Casilla(Board tablero, SeedType carta)
    {
        return "columna " + tablero.PlantingPixelToGridX(_clicX, _clicY, carta) + ", fila " + tablero.PlantingPixelToGridY(_clicX, _clicY, carta);
    }

    // Las cartas extra se reparten al empezar, así que el mazo se lee poco después.
    internal static void RevisarMazos(Board tablero) => Proteger(() =>
    {
        var datos = VersusBotMod.Datos;
        if (datos == null || !datos.m_gameplayService.IsVSMode || datos.m_versusMode?.Phase != VersusPhase.Gameplay || tablero.Pointer == _tableroConMazos)
        {
            return;
        }
        if (_leerMazosEn < 0f)
        {
            _leerMazosEn = Time.unscaledTime + EsperaParaLeerMazos;
            return;
        }
        if (Time.unscaledTime < _leerMazosEn)
        {
            return;
        }
        _tableroConMazos = tablero.Pointer;
        _leerMazosEn = -1f;
        for (var jugador = 0; jugador < 2; jugador++)
        {
            if (!EsPersonaEnVersus(jugador))
            {
                continue;
            }
            var banco = tablero.SeedBanks[jugador];
            var texto = "";
            for (var i = 0; banco != null && i < banco.mNumPackets; i++)
            {
                texto += banco.mSeedPackets[i].mPacketType + " ";
            }
            Anotar("J" + (jugador + 1) + " empieza con: " + texto);
        }
    });

    internal static void TableroNuevo()
    {
        _tableroConMazos = IntPtr.Zero;
        _leerMazosEn = -1f;
        _profundidadClic = 0;
    }
}

[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.ClickedSeedInChooser))]
internal static class ParcheJugadaEleccion
{
    private static void Prefix(ChosenSeed theChosenSeed, int playerIndex)
    {
        RegistroJugadas.CartaElegida(theChosenSeed.mSeedType, playerIndex);
    }
}

[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.ClickedSeedInBank), new[] { typeof(SeedPacket), typeof(int) })]
internal static class ParcheJugadaQuitarCarta
{
    private static void Prefix(SeedPacket thePacket, int playerIndex)
    {
        RegistroJugadas.CartaQuitada(thePacket.mPacketType, playerIndex);
    }
}

[HarmonyPatch(typeof(SeedPacket), nameof(SeedPacket.Activated))]
internal static class ParcheJugadaTomarCarta
{
    private static void Postfix(SeedPacket __instance, int playerIndex)
    {
        RegistroJugadas.CartaTomada(__instance.mPacketType, playerIndex);
    }
}

[HarmonyPatch(typeof(Board), nameof(Board.MouseDownWithPlant))]
internal static class ParcheJugadaClicConCarta
{
    private static void Prefix(int x, int y, int playerIndex)
    {
        RegistroJugadas.EmpiezaClic(x, y, playerIndex);
    }

    private static void Postfix(Board __instance)
    {
        RegistroJugadas.TerminaClic(__instance);
    }
}

[HarmonyPatch(typeof(Challenge), nameof(Challenge.IZombieMouseDownWithZombie))]
internal static class ParcheJugadaClicConZombi
{
    private static void Prefix(int x, int y, int playerIndex)
    {
        RegistroJugadas.EmpiezaClic(x, y, playerIndex);
    }

    private static void Postfix(Challenge __instance)
    {
        RegistroJugadas.TerminaClic(__instance.mBoard);
    }
}

[HarmonyPatch(typeof(SeedPacket), nameof(SeedPacket.WasPlanted))]
internal static class ParcheJugadaColocada
{
    private static void Prefix(SeedPacket __instance, int playerIndex)
    {
        RegistroJugadas.CartaColocada(__instance, playerIndex);
    }
}

[HarmonyPatch(typeof(Board), nameof(Board.MouseDownWithTool))]
internal static class ParcheJugadaHerramienta
{
    private static void Prefix(Board __instance, int x, int y, CursorType theCursorType, int playerIndex)
    {
        RegistroJugadas.Herramienta(__instance, x, y, theCursorType, playerIndex);
    }
}

[HarmonyPatch(typeof(VersusMode), nameof(VersusMode.InitializeGameplay))]
internal static class ParcheJugadaPartidaNueva
{
    private static void Prefix()
    {
        RegistroJugadas.TableroNuevo();
    }
}

[HarmonyPatch(typeof(Board), nameof(Board.Update))]
internal static class ParcheJugadaMazos
{
    private static void Postfix(Board __instance)
    {
        RegistroJugadas.RevisarMazos(__instance);
    }
}
