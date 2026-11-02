// Versus Bot — mod para PvZ Replanted (MelonLoader).
// Permite jugar el modo Versus contra un bot. El bot usa un mando virtual como
// segundo jugador, así el juego lo trata igual que a una persona.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppReloaded;
using Il2CppReloaded.Binders;
using Il2CppReloaded.DataModels;
using Il2CppReloaded.Gameplay;
using Il2CppReloaded.TreeStateActivities;
using Il2CppSource.DataModels;
using Il2CppTekly.DataModels.Binders;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(VersusBot.VersusBotMod), "Versus Bot", "0.11.18", "Angel")]
[assembly: MelonGame("PopCap Games", "PvZ Replanted")]

namespace VersusBot;

internal enum ModoJuego
{
    UnJugador,
    DosJugadores,
    BotVsBot
}

public class VersusBotMod : MelonMod
{
    private const string NombreMandoJ1 = "mando_virtual_bot_1";
    private const string NombreMandoJ2 = "mando_virtual_bot_2";
    private const float EsperaCrearMando = 3f;
    private const float EsperaEntrePasos = 0.6f;
    private const int PosicionPlantas = -1;
    private const int PosicionZombis = 1;
    private const int IndiceBot = 1;
    private const int NivelInicial = 50;
    internal const int NivelMaximo = 200;
    private const float VigenciaTick = 0.5f;
    private const float EsperaRecogidaJugador = 0.5f;
    private const string EnsambladoIncompatible = "PvZReAutoCollectDropsDll";
    // Sin ticks durante este tiempo se considera que el jugador salió de la pantalla de Versus.
    private const float PausaDeSalida = 2f;

    internal static MelonLogger.Instance Log;
    internal static VersusDataModel Datos;

    private static InputDevice _mando;
    private static InputDevice _mandoJ1;
    private static int _ladoBotJ1;
    private static string _ultimoEstado = "";
    private static float _siguientePaso;
    private static bool _invitadoPedido;
    private static MelonPreferences_Entry<bool> _recogerActivo;
    private static MelonPreferences_Entry<int> _modo;
    private static MelonPreferences_Entry<int> _nivel;
    private static MelonPreferences_Entry<bool> _detalle;

    internal static void Detalle(string texto)
    {
        if (_detalle != null && _detalle.Value)
        {
            Log.Msg(texto);
        }
    }
    private static float _ultimoTick;
    private static VersusPhase _faseAnterior = VersusPhase.Invalid;
    private static float _fueraDeVersusDesde = -1f;
    private static readonly List<Recogida> _pendientes = new List<Recogida>();

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        var categoria = MelonPreferences.CreateCategory("VersusBot");
        _recogerActivo = categoria.CreateEntry("RecogerAutomaticamente", false, "Recoger automáticamente soles, monedas y regalos");
        var conBotAntes = categoria.CreateEntry("JugarConBot", true, "Ajuste antiguo; ahora se usa Modo");
        _modo = categoria.CreateEntry("Modo", (int)(conBotAntes.Value ? ModoJuego.UnJugador : ModoJuego.DosJugadores), "0 = Jugador vs Bot, 1 = Jugador vs Jugador, 2 = Bot vs Bot");
        _detalle = categoria.CreateEntry("RegistroDetallado", false, "Anota en el registro los mensajes de diagnóstico");
        Log.Msg("Versión del juego: " + Application.version + " (el mod se probó con la 1.5.1469)");
        _nivel = categoria.CreateEntry("Dificultad", NivelInicial, "Dificultad del bot, de 1 a 200");
        _modo.Value = (int)ModoJuego.UnJugador;
    }

    // Se mira aquí porque todos los mods ya están cargados.
    public override void OnLateInitializeMelon()
    {
        foreach (var mod in RegisteredMelons)
        {
            if (mod.MelonAssembly.Assembly.GetName().Name == EnsambladoIncompatible)
            {
                ModIncompatibleInstalado = true;
                Log.Warning("Quick Collect está instalado y no es compatible: en Versus le da todos los soles al jugador 1. Conviene quitarlo.");
            }
        }
    }

    public override void OnLateUpdate()
    {
        try
        {
            PanelRival.AntesDeDibujar();
        }
        catch (Exception error)
        {
            Log.Warning("Botones personalizados: " + error.Message);
        }
    }

    public override void OnUpdate()
    {
        if (_mando == null && Time.realtimeSinceStartup > EsperaCrearMando)
        {
            _mando = InputSystem.AddDevice("Gamepad", NombreMandoJ2, null);
            _mandoJ1 = InputSystem.AddDevice("Gamepad", NombreMandoJ1, null);
            Log.Msg(_mando != null && _mandoJ1 != null ? "Mandos virtuales creados." : "No se pudieron crear los mandos virtuales.");
        }
        VigilarSalidaDeVersus();
        OpcionRecoger.Actualizar();
        // Un fallo del botón no debe impedir que funcionen los paneles de abajo.
        try
        {
            BotonVelocidad.Actualizar();
        }
        catch (Exception e)
        {
            Detalle("Botón de velocidad: " + e.Message);
        }
        PanelRival.Actualizar(EnPantallaDeBando());
        ProcesarRecogidas();
    }

    internal static bool RecogerActivo => _recogerActivo.Value;
    internal static bool ModIncompatibleInstalado { get; private set; }
    internal static ModoJuego Modo => (ModoJuego)_modo.Value;
    internal static bool ModoBot => Modo != ModoJuego.DosJugadores;

    internal static bool JugadorEsBot(int indice)
    {
        return indice == IndiceBot ? ModoBot : Modo == ModoJuego.BotVsBot;
    }

    // En Bot vs Bot el ratón no debe jugar por el jugador 1.
    // Al terminar la partida el ratón vuelve a funcionar: la copa se recoge con un clic en el tablero.
    internal static bool EnPartidaBotVsBot
    {
        get
        {
            if (Modo != ModoJuego.BotVsBot || Datos == null || !Datos.m_gameplayService.IsVSMode)
            {
                return false;
            }
            var fase = Datos.m_versusMode?.Phase;
            return fase == VersusPhase.Gameplay || fase == VersusPhase.SuddenDeath;
        }
    }

    internal static int Nivel
    {
        get => Mathf.Clamp(_nivel.Value, 1, NivelMaximo);
        set => _nivel.Value = value;
    }

    internal static void GuardarAjustes()
    {
        MelonPreferences.Save();
    }

    private static bool EnPantallaDeBando()
    {
        if (Datos == null || Time.unscaledTime - _ultimoTick > VigenciaTick)
        {
            return false;
        }
        var versus = Datos.m_versusMode;
        return versus != null && Datos.m_gameplayService.IsVSMode && versus.Phase == VersusPhase.PickSides
            && !OpcionRecoger.OpcionesAbiertas;
    }

    // Rota el modo. Si el jugador 2 cambia de bot a persona o al revés, se suelta el actual:
    // con 2 jugadores el juego vuelve a esperar Start en un mando; con bot, Tick conecta el virtual.
    internal static void CambiarModo()
    {
        if (Datos == null)
        {
            return;
        }
        var anterior = Modo;
        FijarModo(anterior == ModoJuego.UnJugador ? ModoJuego.DosJugadores
            : anterior == ModoJuego.DosJugadores ? ModoJuego.BotVsBot : ModoJuego.UnJugador);
    }

    private static void FijarModo(ModoJuego nuevo)
    {
        var anterior = Modo;
        var j1 = Datos.m_player1Model;
        var j2 = Datos.m_player2Model;
        if (j2 != null && j2.m_hasSelectedModel.Value)
        {
            j2.Cancel();
        }
        if (anterior == ModoJuego.BotVsBot && j1 != null && j1.m_hasSelectedModel.Value)
        {
            j1.Cancel();
        }
        if (anterior == ModoJuego.DosJugadores || nuevo == ModoJuego.DosJugadores)
        {
            var entrada = Datos.m_inputService;
            entrada.DisableGuestPlayerInput();
            if (nuevo == ModoJuego.DosJugadores)
            {
                entrada.BeginListeningForGuestInputDevice(true);
            }
            _invitadoPedido = false;
        }
        _ladoBotJ1 = 0;
        _modo.Value = (int)nuevo;
        GuardarAjustes();
        Log.Msg("Modo: " + nuevo);
    }

    internal static void CambiarRecoger(bool activo)
    {
        if (_recogerActivo.Value != activo)
        {
            _recogerActivo.Value = activo;
            MelonPreferences.Save();
            Log.Msg("Recoger automáticamente " + (activo ? "activado." : "desactivado."));
        }
    }

    // Fuera de Versus se recoge lo que cae al tablero; los premios de fin de nivel se dejan
    // para el jugador, porque recogerlos termina el nivel.
    private static bool SeRecogeFueraDeVersus(CoinType tipo)
    {
        return EsSol(tipo) || tipo == CoinType.Silver || tipo == CoinType.Gold || tipo == CoinType.Diamond
            || tipo == CoinType.PresentPlant || tipo == CoinType.Chocolate;
    }

    private static bool EsSol(CoinType tipo)
    {
        return tipo == CoinType.Sun || tipo == CoinType.SmallSun || tipo == CoinType.LargeSun || tipo == CoinType.DoubleSun;
    }

    internal static void MonedaCreada(GameplayActivity partida, Board tablero, Coin moneda, CoinType tipo)
    {
        if (partida == null || moneda == null)
        {
            return;
        }
        var enVersus = partida.IsVersusMode();
        int jugador;
        float espera;
        if (enVersus)
        {
            // En Versus cada recurso va a su dueño: soles al lado de plantas y cerebros al de zombis.
            // El bot siempre recoge lo suyo; lo tuyo se recoge solo si la casilla está marcada.
            var versus = partida.VersusMode;
            if (versus == null)
            {
                return;
            }
            jugador = tipo == CoinType.Brain ? versus.ZombiePlayerIndex : EsSol(tipo) ? versus.PlantPlayerIndex : -1;
            if (jugador < 0)
            {
                return;
            }
            var delBot = JugadorEsBot(jugador);
            if (!delBot && !_recogerActivo.Value)
            {
                return;
            }
            espera = delBot ? Bot.EsperaRecogida() : EsperaRecogidaJugador;
        }
        else
        {
            if (!_recogerActivo.Value || !SeRecogeFueraDeVersus(tipo))
            {
                return;
            }
            jugador = 0;
            espera = EsperaRecogidaJugador;
        }
        _pendientes.Add(new Recogida
        {
            Partida = partida,
            Tablero = tablero,
            Moneda = moneda,
            Tipo = tipo,
            Jugador = jugador,
            EnVersus = enVersus,
            Hora = Time.time + espera
        });
    }

    private static void ProcesarRecogidas()
    {
        // Al terminar la partida de Versus las monedas pendientes ya no son válidas.
        var fase = Datos?.m_versusMode?.Phase;
        var versusEnJuego = fase == VersusPhase.Gameplay || fase == VersusPhase.SuddenDeath;
        for (var i = _pendientes.Count - 1; i >= 0; i--)
        {
            var recogida = _pendientes[i];
            if (recogida.EnVersus && !versusEnJuego)
            {
                _pendientes.RemoveAt(i);
                continue;
            }
            if (Time.time < recogida.Hora)
            {
                continue;
            }
            _pendientes.RemoveAt(i);
            var moneda = recogida.Moneda;
            if (TableroSigueActivo(recogida) && !moneda.mDead && !moneda.mIsBeingCollected && moneda.mType == recogida.Tipo)
            {
                moneda.Collect(recogida.Jugador, true, CoinVacuumStyle.None);
            }
        }
    }

    // Al salir del nivel la moneda guardada ya no pertenece al tablero en juego.
    private static bool TableroSigueActivo(Recogida recogida)
    {
        return recogida.Partida != null && recogida.Partida.Board != null
            && recogida.Partida.Board.Pointer == recogida.Tablero.Pointer;
    }

    private class Recogida
    {
        public GameplayActivity Partida;
        public Board Tablero;
        public Coin Moneda;
        public CoinType Tipo;
        public int Jugador;
        public bool EnVersus;
        public float Hora;
    }

    internal static void Tick(VersusDataModel datos)
    {
        Datos = datos;
        var sinTicks = Time.unscaledTime - _ultimoTick;
        _ultimoTick = Time.unscaledTime;
        var versus = datos.m_versusMode;
        if (versus == null)
        {
            _invitadoPedido = false;
            _faseAnterior = VersusPhase.Invalid;
            return;
        }
        VolverAModoPrincipal(versus.Phase, sinTicks);

        var entrada = datos.m_inputService;
        var yo = datos.m_player1Model;
        var bot = datos.m_player2Model;
        Registrar(versus, datos, yo, bot);

        if (versus.Phase == VersusPhase.ChoosePlantPacket || versus.Phase == VersusPhase.ChooseZombiePacket)
        {
            if (ModoBot)
            {
                Bot.ElegirCartasIniciales(versus);
            }
            return;
        }
        Bot.ReiniciarElecciones();

        if (!ModoBot || versus.Phase != VersusPhase.PickSides || _mando == null || Time.realtimeSinceStartup < _siguientePaso)
        {
            return;
        }
        _siguientePaso = Time.realtimeSinceStartup + EsperaEntrePasos;

        if (!entrada.IsGuestPlayerEnabledAndReady())
        {
            if (!_invitadoPedido)
            {
                _invitadoPedido = true;
                Log.Msg("Conectando el mando virtual como jugador 2.");
                entrada.EnableGuestPlayerInput(_mando);
            }
            return;
        }

        if (yo == null || bot == null)
        {
            return;
        }
        if (Modo == ModoJuego.BotVsBot && !yo.m_hasSelectedModel.Value)
        {
            ElegirLadoJ1(yo);
            return;
        }
        ElegirLado(yo, bot);
    }

    // Al entrar a Versus desde el menú siempre se empieza en Jugador vs Bot.
    // La revancha tras una partida conserva el modo: así Bot vs Bot sigue jugando rondas.
    private static void VolverAModoPrincipal(VersusPhase fase, float sinTicks)
    {
        var anterior = _faseAnterior;
        _faseAnterior = fase;
        if (fase != VersusPhase.PickSides || anterior == VersusPhase.PickSides && sinTicks < PausaDeSalida)
        {
            return;
        }
        var esRevancha = anterior == VersusPhase.PlantsWin || anterior == VersusPhase.ZombiesWin;
        var entroDesdeMenu = anterior == VersusPhase.Invalid || (!esRevancha && sinTicks >= PausaDeSalida);
        if (entroDesdeMenu && Modo != ModoJuego.UnJugador)
        {
            FijarModo(ModoJuego.UnJugador);
        }
    }

    // La revancha puede dejar el modo Versus por un instante: solo cuenta una salida que dure.
    private static void VigilarSalidaDeVersus()
    {
        if (Datos == null || Datos.m_gameplayService.IsVSMode)
        {
            _fueraDeVersusDesde = -1f;
        }
        else if (_fueraDeVersusDesde < 0f)
        {
            _fueraDeVersusDesde = Time.unscaledTime;
        }
        else if (Time.unscaledTime - _fueraDeVersusDesde >= PausaDeSalida)
        {
            _faseAnterior = VersusPhase.Invalid;
        }
    }

    // En Bot vs Bot, el bot del jugador 1 toma un lado al azar.
    private static void ElegirLadoJ1(VersusPlayerModel yo)
    {
        if (_ladoBotJ1 == 0)
        {
            _ladoBotJ1 = UnityEngine.Random.value < 0.5f ? PosicionPlantas : PosicionZombis;
        }
        if (Math.Sign(yo.m_iconPositionModel.Value) != _ladoBotJ1)
        {
            yo.Move(_ladoBotJ1);
        }
        else
        {
            yo.Confirm();
            _ladoBotJ1 = 0;
        }
    }

    // El bot no hace nada hasta que el jugador 1 confirma su lado, y entonces toma el otro.
    // Si el jugador 1 cancela o cambia de lado, el bot también deshace su elección.
    private static void ElegirLado(VersusPlayerModel yo, VersusPlayerModel bot)
    {
        var botListo = bot.m_hasSelectedModel.Value;
        if (!yo.m_hasSelectedModel.Value)
        {
            if (botListo)
            {
                bot.Cancel();
                Log.Msg("El jugador 1 canceló: el bot también.");
            }
            return;
        }

        var objetivo = yo.m_isPlantsModel.Value ? PosicionZombis : PosicionPlantas;
        if (Math.Sign(bot.m_iconPositionModel.Value) != objetivo)
        {
            if (botListo)
            {
                bot.Cancel();
            }
            else
            {
                bot.Move(objetivo);
            }
        }
        else if (!botListo)
        {
            bot.Confirm();
            Log.Msg("El bot confirmó su lado: " + (objetivo == PosicionZombis ? "zombis." : "plantas."));
        }
    }

    private static void Registrar(VersusMode versus, VersusDataModel datos, VersusPlayerModel yo, VersusPlayerModel bot)
    {
        var entrada = datos.m_inputService;
        var estado = "fase=" + versus.Phase
            + " minMandos=" + entrada.HasMinimumGamepadsForMultiplayer()
            + " invitado=" + entrada.IsGuestPlayerEnabledAndReady()
            + " J1[" + Describir(yo, entrada.GetPlayer(0)) + "]"
            + " J2[" + Describir(bot, entrada.GetPlayer(1)) + "]"
            + " plantas=J" + (versus.PlantPlayerIndex + 1) + " zombis=J" + (versus.ZombiePlayerIndex + 1)
            + " vidaZombis=" + versus.ZombieLife;
        if (estado != _ultimoEstado)
        {
            _ultimoEstado = estado;
            Log.Msg(estado);
        }
    }

    private static string Describir(VersusPlayerModel jugador, Il2CppReloaded.Input.ReloadedPlayerInput entrada)
    {
        var control = entrada == null ? "sin entrada" : entrada.CurrentControlType + "/" + entrada.Paired;
        if (jugador == null)
        {
            return control;
        }
        return control + " pos=" + jugador.m_iconPositionModel.Value + " listo=" + jugador.m_hasSelectedModel.Value
            + " P=" + jugador.m_isPlantsModel.Value + " Z=" + jugador.m_isZombiesModel.Value;
    }
}

[HarmonyPatch(typeof(VersusDataModel), nameof(VersusDataModel.OnTick))]
internal static class ParcheTick
{
    private static void Postfix(VersusDataModel __instance)
    {
        VersusBotMod.Tick(__instance);
    }

    // Un fallo del mod no debe romper el juego: se registra y se sigue.
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception != null)
        {
            VersusBotMod.Log.Error(__exception.ToString());
        }
        return null;
    }
}

// Opción de recoger automáticamente en el panel de los lentes (debajo de zombis retro, bigote...).
// Se clona la fila de bigote como molde y se le quita su conexión con los ajustes del juego:
// la casilla copiada queda independiente y el mod la lee directamente.
internal static class OpcionRecoger
{
    private const string NombreFila = "FilaRecoger";
    private const string Etiqueta = "Recoger automáticamente";
    private const float EsperaEntreBusquedas = 0.1f;
    private const string PrefijoAjustes = "settings.";

    internal static bool OpcionesAbiertas { get; private set; }

    private static Toggle _casilla;
    private static float _siguienteBusqueda;

    internal static void Actualizar()
    {
        if (_casilla != null && _casilla.isOn != VersusBotMod.RecogerActivo)
        {
            VersusBotMod.CambiarRecoger(_casilla.isOn);
        }
        if (Time.unscaledTime < _siguienteBusqueda)
        {
            return;
        }
        _siguienteBusqueda = Time.unscaledTime + EsperaEntreBusquedas;

        var claveBigote = Constants.DataModel.IS_MUSTACHE_MODE;
        var opciones = false;
        UnityToggleBinder bigote = null;
        foreach (var objeto in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<UnityToggleBinder>()))
        {
            var binder = objeto.Cast<UnityToggleBinder>();
            if (binder.m_key == null)
            {
                continue;
            }
            var ruta = binder.m_key.Path;
            opciones |= ruta.StartsWith(PrefijoAjustes);
            if (ruta.EndsWith(claveBigote))
            {
                bigote = binder;
            }
        }
        OpcionesAbiertas = opciones;
        if (bigote != null)
        {
            AsegurarFila(bigote);
        }
    }

    private static void AsegurarFila(UnityToggleBinder bigote)
    {
        var fila = bigote.transform;
        while (fila.parent != null && fila.parent.GetComponentsInChildren<UnityToggleBinder>(true).Length < 2)
        {
            fila = fila.parent;
        }
        var contenedor = fila.parent;
        if (contenedor == null)
        {
            return;
        }
        var existente = contenedor.Find(NombreFila);
        if (existente != null)
        {
            _casilla = existente.GetComponentInChildren<Toggle>(true);
            return;
        }

        var copia = UnityEngine.Object.Instantiate(fila.gameObject, contenedor);
        copia.name = NombreFila;
        copia.SetActive(true);
        foreach (var componente in copia.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (componente.TryCast<Binder>() != null || componente.GetIl2CppType().Name.Contains("Locali"))
            {
                UnityEngine.Object.DestroyImmediate(componente);
            }
        }
        foreach (var texto in copia.GetComponentsInChildren<TMP_Text>(true))
        {
            texto.text = Etiqueta;
        }
        _casilla = copia.GetComponentInChildren<Toggle>(true);
        _casilla.onValueChanged.RemoveAllListeners();
        _casilla.SetIsOnWithoutNotify(VersusBotMod.RecogerActivo);
        VersusBotMod.Log.Msg("Opción de recoger automáticamente creada.");
    }
}

// En Bot vs Bot se ignoran los clics del ratón sobre el tablero y las cartas del jugador 1.
[HarmonyPatch(typeof(Board), nameof(Board.MouseDown))]
internal static class ParcheClicTablero
{
    private static bool Prefix(int playerIndex)
    {
        return playerIndex != 0 || !VersusBotMod.EnPartidaBotVsBot;
    }
}

[HarmonyPatch(typeof(SeedPacket), nameof(SeedPacket.Activated))]
internal static class ParcheClicCarta
{
    private static bool Prefix(int playerIndex, bool byGamepad)
    {
        return byGamepad || playerIndex != 0 || !VersusBotMod.EnPartidaBotVsBot;
    }
}

[HarmonyPatch(typeof(LawnMower), nameof(LawnMower.LawnMowerInitialize))]
internal static class ParchePodadora
{
    private static void Postfix(LawnMower __instance)
    {
        Estrategia.PodadoraCreada(__instance);
    }

    // Un fallo del mod no debe romper el juego: se registra y se sigue.
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception != null)
        {
            VersusBotMod.Log.Error(__exception.ToString());
        }
        return null;
    }
}

[HarmonyPatch(typeof(Zombie), nameof(Zombie.ZombieInitialize))]
internal static class ParcheZombi
{
    private static void Postfix(Zombie __instance)
    {
        Estrategia.ZombiCreado(__instance);
    }

    // Un fallo del mod no debe romper el juego: se registra y se sigue.
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception != null)
        {
            VersusBotMod.Log.Error(__exception.ToString());
        }
        return null;
    }
}

[HarmonyPatch(typeof(Board), nameof(Board.Update))]
internal static class ParcheTablero
{
    private static void Postfix(Board __instance)
    {
        Bot.ActualizarTodos(__instance);
    }

    // Un fallo del mod no debe romper el juego: se registra y se sigue.
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception != null)
        {
            VersusBotMod.Log.Error(__exception.ToString());
        }
        return null;
    }
}

[HarmonyPatch(typeof(Board), nameof(Board.AddCoin))]
internal static class ParcheMoneda
{
    private static void Postfix(Board __instance, Coin __result, CoinType theCoinType)
    {
        VersusBotMod.MonedaCreada(__instance.mApp, __instance, __result, theCoinType);
    }

    // Un fallo del mod no debe romper el juego: se registra y se sigue.
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception != null)
        {
            VersusBotMod.Log.Error(__exception.ToString());
        }
        return null;
    }
}
