// Versus Bot — cambio de mapa en Versus (versión de prueba).
// © 2026 Angel. Licencia MIT (ver LICENSE).

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppSource.DataModels;
using Il2CppReloaded.DataModels;
using Il2CppReloaded.Gameplay;
using Il2CppReloaded.TreeStateActivities;
using Il2CppSource.Controllers;
using Il2CppTekly.DataModels.Binders;
using Il2CppTekly.DataModels.Models;
using UnityEngine;

namespace VersusBot;

// El juego elige Versus con mapa de día. Aquí se cambia el fondo por el del primer nivel de
// piscina y el tablero pasa a ser de piscina: tipo de mapa y filas de agua.
internal static class MapaVersus
{
    private const int NivelDePiscina = 21;
    private const int PrimeraFilaDeAgua = 2;
    private const int UltimaFilaDeAgua = 3;
    // Las 3 columnas de la derecha quedan de pasto: es la zona donde entran los zombis.
    internal const int ColumnasConAgua = 6;
    private const float CentroZombi = 75f;
    private const string NombreParche = "ParcheDePasto";
    // Medidas del dibujo de la piscina, desde la esquina inferior izquierda del pasto.
    private const float ParcheIzquierda = 2429f;
    private const float ParcheDerecha = 3140f;
    private const float ParcheAbajo = 509f;
    private const float ParcheArriba = 1063f;
    private const float OrigenPastoAbajo = 1059f;
    private const float OrigenPastoArriba = 1559f;
    // Las losas sobresalen del parche para tapar la unión con el pasto de las líneas 2 y 5.
    private const float LosasSobresalen = 30f;
    private const string NombreLosas = "PastoPiscinaLosas";
    private const string NombreLineaRoja = "LineaRojaPiscina";
    private const string DibujoLineaRoja = "P_WalnutBowlingLine";
    // El campo de la piscina es más alto que el del jardín.
    private const float LineaCentroY = -1320f;
    private const float LineaAlto = 1520f;
    private const int LineaOrdenSobreLosas = 3;
    private const float EsperaRevisionDeCirculo = 0.7f;
    // El juego saca la aspiradora del agua cuando pasa de 660, 100 antes del fin de su piscina.
    // Aquí sale 5 pixeles más adelante, a pedido del usuario.
    private const float SalidaOriginalDeAspiradora = 661f;
    private const float AspiradoraAntesDelFin = 95f;
    private const int ColumnaFinDelAgua = 6;
    // El juego cuenta 100 ciclos por segundo.
    private const int RecargaMinimaDeNenufar = 350;
    private const int RecargaMinimaDeAlga = 1000;
    // Comienzo de la piscina, desde el agua hasta el patio, que se repite en espejo al final de los 6 bloques.
    private const float BordeDesdeElAgua = 1076f;
    private const float BordeHastaElPatio = 934f;
    // Línea donde el juego pone el girasol y la tumba de inicio y línea que les toca en la piscina.
    private const int FilaInicialEnAgua = 3;
    private const int FilaInicialEnPasto = 4;
    private const float DuracionInicio = 1f;
    private const string PiezaFlotador = "Zombie_duckytube";
    private const int GrupoDeDibujoNormal = 0;
    // Reglas propias del mapa de piscina: una carta más por el nenúfar y 6 objetivos. Se gana rompiendo 3, como en el juego.
    private const int CartasExtra = 1;
    private const int FilaDelSextoObjetivo = 5;
    private const float EsperaRevisionDeZombi = 0.3f;
    private const float EsperaEntreRevisiones = 0.5f;
    private const float BordeDerechoDelTablero = 1000f;
    // Disposición interna de un arreglo de 2 dimensiones en IL2CPP.
    private const int DesplazamientoLimites = 0x10;
    private const int DesplazamientoLargo = 0x18;
    private const int DesplazamientoDatos = 0x20;
    private const int TamanoLimite = 16;

    internal static bool Piscina { get; private set; }
    internal static string EtiquetaBoton => Piscina ? Textos.Jardin : Textos.Piscina;

    private static GameplayActivity _partida;
    private static IntPtr _tableroGuardado;
    private static IntPtr _tableroConFondo;
    private static LevelEntryData _datosOriginales;
    private static BackgroundType _fondoOriginal;
    private static PlantRowType[] _filasOriginales;
    private static readonly List<Renderer> _ocultos = new List<Renderer>();
    private static readonly List<UnityEngine.UI.Graphic> _graficosOcultos = new List<UnityEngine.UI.Graphic>();
    private static float _finDelInicio;
    private static readonly HashSet<IntPtr> _conFlotador = new HashSet<IntPtr>();
    private static bool _cuadriculaAvisada;
    private static readonly HashSet<IntPtr> _reparados = new HashSet<IntPtr>();
    private static bool _cartasAvisadas;
    private static Zombie _plantillaObjetivo;
    private static IntPtr _tableroConSextoObjetivo;
    private static bool _contandoCartas;
    private static readonly Dictionary<IntPtr, float> _zombisRevisados = new Dictionary<IntPtr, float>();
    private static VersusMode _versus;
    // En las filas de agua solo entran estos. La tumba no camina: se puede poner en sus 3 bloques de pasto.
    private static readonly HashSet<SeedType> _cartasPermitidasEnAgua = new HashSet<SeedType>
    {
        SeedType.ZombieNormal, SeedType.ZombieFlag, SeedType.ZombieTrafficCone, SeedType.ZombiePail, SeedType.ZombieGravestone, SeedType.ZombieBalloon
    };

    internal static Board TableroActual => Partida?.Board;

    // Si el juego no avisó al preparar el tablero, se busca la partida en la escena.
    private static GameplayActivity Partida
    {
        get
        {
            if (_partida == null)
            {
                _partida = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<GameplayActivity>())?.TryCast<GameplayActivity>();
            }
            return _partida;
        }
    }

    internal static void TableroPreparado(Board tablero)
    {
        _partida = tablero.mApp;
        Guardar(tablero);
        if (Piscina)
        {
            AplicarReglas(tablero);
        }
    }

    internal static void Cambiar()
    {
        Piscina = !Piscina;
        try
        {
            var tablero = TableroActual;
            if (tablero != null)
            {
                Guardar(tablero);
                AplicarReglas(tablero);
                PonerFondo(tablero);
            }
            VersusBotMod.Log.Msg("Mapa: " + (Piscina ? "piscina" : "jardín") + (tablero == null ? " (se aplicará al crear el tablero)" : ""));
        }
        catch (Exception e)
        {
            Piscina = !Piscina;
            VersusBotMod.Log.Error("No se pudo cambiar el mapa: " + e);
        }
    }

    // Una revancha crea otro tablero: hay que volver a poner la piscina.
    internal static void Asegurar()
    {
        var tablero = TableroActual;
        if (!Piscina || tablero == null)
        {
            return;
        }
        Guardar(tablero);
        AplicarReglas(tablero);
        if (_tableroConFondo != tablero.Pointer)
        {
            PonerFondo(tablero);
        }
    }

    internal static void Diagnostico(Board tablero)
    {
        var filas = "";
        foreach (var fila in tablero.mPlantRow)
        {
            filas += fila + " ";
        }
        VersusBotMod.Log.Msg("Nivel del bot " + VersusBotMod.Nivel + ". Mapa al empezar: fondo=" + tablero.mBackground + " filasDelTablero=" + tablero.GetNumRows()
            + " filasDeVersus=" + VersusMode.k_numRows + " piscina=" + tablero.StageHasPool() + " seisFilas=" + tablero.StageHas6Rows() + " tipos=" + filas);
    }

    private static void Guardar(Board tablero)
    {
        if (_tableroGuardado == tablero.Pointer)
        {
            return;
        }
        _tableroGuardado = tablero.Pointer;
        _datosOriginales = _partida.LevelData;
        _fondoOriginal = tablero.mBackground;
        var filas = tablero.mPlantRow;
        _filasOriginales = new PlantRowType[filas.Length];
        for (var i = 0; i < filas.Length; i++)
        {
            _filasOriginales[i] = filas[i];
        }
    }

    private static void AplicarReglas(Board tablero)
    {
        tablero.mBackground = Piscina ? BackgroundType.Pool : _fondoOriginal;
        var filas = tablero.mPlantRow;
        for (var i = 0; i < filas.Length; i++)
        {
            filas[i] = !Piscina ? _filasOriginales[i]
                : i >= PrimeraFilaDeAgua && i <= UltimaFilaDeAgua ? PlantRowType.Pool : PlantRowType.Normal;
        }
        EscribirCuadricula(tablero);
    }

    // El juego decide dónde se puede plantar con esta cuadrícula, no con el tipo de fila.
    private static void EscribirCuadricula(Board tablero)
    {
        var cuadricula = tablero.mGridSquareType;
        if (cuadricula == null)
        {
            return;
        }
        var puntero = cuadricula.Pointer;
        var limites = Marshal.ReadIntPtr(puntero, DesplazamientoLimites);
        var largo = Marshal.ReadInt64(puntero, DesplazamientoLargo);
        if (limites == IntPtr.Zero)
        {
            AvisarCuadricula("sin límites, largo " + largo);
            return;
        }
        var columnas = (int)Marshal.ReadInt64(limites, 0);
        var filas = (int)Marshal.ReadInt64(limites, TamanoLimite);
        if (columnas * filas != largo || columnas < ColumnasConAgua || filas <= UltimaFilaDeAgua)
        {
            AvisarCuadricula(columnas + "x" + filas + ", largo " + largo);
            return;
        }
        var filasDelTablero = tablero.mPlantRow;
        for (var columna = 0; columna < columnas; columna++)
        {
            for (var fila = 0; fila < filas && fila < filasDelTablero.Length; fila++)
            {
                var desplazamiento = DesplazamientoDatos + (columna * filas + fila) * sizeof(int);
                Marshal.WriteInt32(puntero, desplazamiento, (int)TipoDeCasilla(filasDelTablero[fila], columna));
            }
        }
    }

    private static GridSquareType TipoDeCasilla(PlantRowType fila, int columna)
    {
        switch (fila)
        {
            case PlantRowType.Pool:
                return columna < ColumnasConAgua ? GridSquareType.Pool : GridSquareType.Grass;
            case PlantRowType.Dirt:
                return GridSquareType.Dirt;
            case PlantRowType.HighGround:
                return GridSquareType.HighGround;
            default:
                return GridSquareType.Grass;
        }
    }

    private static void AvisarCuadricula(string detalle)
    {
        if (!_cuadriculaAvisada)
        {
            _cuadriculaAvisada = true;
            VersusBotMod.Log.Warning("La cuadrícula no tiene la forma esperada: " + detalle);
        }
    }

    // k_numPackets es una constante del juego: escribirla pisa otra variable y termina cerrando el juego.
    private static int CartasDelMapa => Piscina ? VersusMode.k_numPackets + CartasExtra : VersusMode.k_numPackets;

    internal static int CartasEnBanco(Board tablero, int cartas)
    {
        return Piscina && tablero.mApp != null && tablero.mApp.IsVersusMode() ? CartasDelMapa : cartas;
    }

    // La pantalla de elegir guarda su propia cuenta de cartas y la de cada banco.
    internal static void AjustarSelector(SeedChooserScreen selector)
    {
        var tablero = selector.mBoard;
        if (tablero == null || tablero.mApp == null || !tablero.mApp.IsVersusMode())
        {
            return;
        }
        var cartas = CartasDelMapa;
        selector.mNumSeedsToChoose = cartas;
        var bancos = selector.m_seedBankInfos;
        for (var i = 0; bancos != null && i < bancos.Count; i++)
        {
            var banco = bancos[i].mSeedBank;
            if (banco == null || banco.mNumPackets == cartas)
            {
                continue;
            }
            var capacidad = banco.mSeedPackets?.Length ?? 0;
            if (cartas <= capacidad)
            {
                banco.mNumPackets = cartas;
                banco.UpdateWidth();
            }
            if (!_cartasAvisadas)
            {
                _cartasAvisadas = true;
                VersusBotMod.Detalle("Cartas por jugador: " + cartas + ", capacidad del banco " + capacidad + ", k_packetMax " + VersusMode.k_packetMax);
            }
        }
    }

    internal static bool ZombiProhibidoEnFila(Board tablero, int fila, SeedType carta)
    {
        if (EsCartaSoloDeAgua(carta))
        {
            return tablero.mApp != null && tablero.mApp.IsVersusMode() && (!Piscina || !EsFilaDeAgua(tablero, fila));
        }
        return Piscina && carta >= SeedType.ZombieNormal && carta <= SeedType.ZombieGravestone && !_cartasPermitidasEnAgua.Contains(carta) && EsFilaDeAgua(tablero, fila);
    }

    // El juego termina de elegir cuando entre los dos bancos hay 12 cartas, número fijo en su código.
    // Mientras lo revisa, a cada banco se le descuenta la carta extra para que espere las 14.
    internal static void EmpezarConteoDeCartas(VersusMode versus)
    {
        _contandoCartas = Piscina && (versus.Phase == VersusPhase.ChoosePlantPacket || versus.Phase == VersusPhase.ChooseZombiePacket)
            && (versus.SelectionSet == SelectionSet.Custom || versus.SelectionSet == SelectionSet.CustomAll);
    }

    internal static void TerminarConteoDeCartas()
    {
        _contandoCartas = false;
    }

    internal static int CartasParaElConteo(int cartas)
    {
        return _contandoCartas ? cartas - CartasExtra : cartas;
    }

    // Las partidas sin elección llegan con 6 cartas: el espacio extra se llena para que nadie quede sin nenúfar.
    internal static void CompletarBancos(VersusMode versus, Board tablero)
    {
        _versus = versus;
        PonerLineaRoja();
        var selector = tablero.mApp?.SeedChooserScreen;
        if (!Piscina)
        {
            QuitarEspacioExtra(selector);
            return;
        }
        if (selector == null)
        {
            return;
        }
        LlenarEspacioExtra(selector, tablero.SeedBanks[versus.PlantPlayerIndex], versus.PlantPlayerIndex, SeedType.Lilypad, SeedType.Tanglekelp, SeedType.Wallnut);
        LlenarEspacioExtra(selector, tablero.SeedBanks[versus.ZombiePlayerIndex], versus.ZombiePlayerIndex, SeedType.ZombieTrafficCone, SeedType.ZombieNormal, SeedType.ZombiePail);
    }

    private static void QuitarEspacioExtra(SeedChooserScreen selector)
    {
        if (selector != null)
        {
            selector.mNumSeedsToChoose = VersusMode.k_numPackets;
        }
    }

    // La barra de cartas conserva el dibujo de cada espacio que tuvo: en vez de borrarlo, lo esconde
    // y queda un hueco vacío. Fuera de la piscina se quita el que sobra.
    internal static void QuitarDibujoDeEspacioExtra(SeedBankDataModel modelo)
    {
        var banco = modelo.m_seedBank;
        if (Piscina || banco == null || banco.mNumPackets < VersusMode.k_numPackets || VersusBotMod.Datos == null || !VersusBotMod.Datos.m_gameplayService.IsVSMode)
        {
            return;
        }
        var espacios = modelo.m_entriesModel?.m_models;
        if (espacios == null || espacios.Count <= banco.mNumPackets)
        {
            return;
        }
        for (var i = espacios.Count - 1; i >= banco.mNumPackets; i--)
        {
            modelo.m_entriesModel.RemoveModel(espacios[i].Key);
        }
        VersusBotMod.Detalle("Se quitó de la barra el espacio extra de la piscina.");
    }

    private static void LlenarEspacioExtra(SeedChooserScreen selector, SeedBank banco, int jugador, params SeedType[] preferidas)
    {
        if (banco == null || banco.GetPacketCount() >= CartasDelMapa)
        {
            return;
        }
        var paquetes = banco.mSeedPackets;
        foreach (var carta in preferidas)
        {
            var yaLaTiene = false;
            for (var i = 0; i < banco.mNumPackets && !yaLaTiene; i++)
            {
                yaLaTiene = paquetes[i].mPacketType == carta;
            }
            if (!yaLaTiene)
            {
                selector.AddChosenSeedToBank(selector.GetChosenSeedFromType(carta), jugador);
                VersusBotMod.Detalle("Carta extra sin elegir: " + carta + " (ahora " + banco.GetPacketCount() + ")");
                return;
            }
        }
    }

    internal static bool EsFilaDeAgua(Board tablero, int fila)
    {
        var filas = tablero?.mPlantRow;
        return filas != null && fila >= 0 && fila < filas.Length && filas[fila] == PlantRowType.Pool;
    }

    // El juego levanta un objetivo por cada una de sus 5 filas de Versus: falta el de la sexta.
    internal static void ObjetivoCreado(Zombie objetivo, int fila)
    {
        if (!Piscina)
        {
            return;
        }
        if (fila == FilaDelSextoObjetivo)
        {
            _tableroConSextoObjetivo = objetivo.mBoard?.Pointer ?? IntPtr.Zero;
            _plantillaObjetivo = null;
        }
        else if (fila == FilaDelSextoObjetivo - 1)
        {
            _plantillaObjetivo = objetivo;
        }
    }

    internal static void AsegurarSextoObjetivo(Board tablero)
    {
        var plantilla = _plantillaObjetivo;
        if (!Piscina || plantilla == null || plantilla.mBoard == null || plantilla.mBoard.Pointer != tablero.Pointer || _tableroConSextoObjetivo == tablero.Pointer)
        {
            return;
        }
        _plantillaObjetivo = null;
        _tableroConSextoObjetivo = tablero.Pointer;
        var columna = tablero.PixelToGridXKeepOnBoard(plantilla.mPosX + CentroZombi, plantilla.mPosY);
        var nuevo = tablero.AddZombieAtCell(ZombieType.Target, columna, FilaDelSextoObjetivo);
        if (nuevo == null)
        {
            VersusBotMod.Log.Warning("No se pudo crear el sexto objetivo.");
            return;
        }
        nuevo.mPosX = plantilla.mPosX;
        VersusBotMod.Detalle("Sexto objetivo creado: fila " + nuevo.mRow + ", columna " + columna + ", vida " + nuevo.mBodyHealth + " (el de la fila 5 tiene " + plantilla.mBodyHealth + ")");
    }

    // El juego hace nacer del agua a todo zombi que sube del suelo si el mapa tiene piscina.
    // Mientras nace, el mapa cuenta como el original. Devuelve la fila a restaurar, o -2 si no aplica.
    internal static int EmpezarNacimiento(Zombie zombi, int fila)
    {
        var tablero = zombi.mBoard;
        if (!Piscina || tablero == null || tablero.mApp == null || !tablero.mApp.IsVersusMode())
        {
            return -2;
        }
        tablero.mBackground = _fondoOriginal;
        return FilaComoPasto(fila);
    }

    internal static void TerminarNacimiento(Zombie zombi, int filaARestaurar)
    {
        if (filaARestaurar == -2)
        {
            return;
        }
        RestaurarFila(filaARestaurar);
        var tablero = zombi.mBoard;
        if (tablero != null)
        {
            tablero.mBackground = BackgroundType.Pool;
        }
        if (zombi.mInPool)
        {
            zombi.mInPool = false;
            VersusBotMod.Log.Warning("Un " + zombi.mZombieType + " nació en el agua sobre tierra: se corrigió.");
        }
    }

    // El juego reutiliza los zombis: cada uno se vuelve a revisar al crearse otra vez.
    internal static void ZombiNuevo(Zombie zombi)
    {
        _zombisRevisados.Remove(zombi.Pointer);
        _reparados.Remove(zombi.Pointer);
        // El juego reutiliza los zombis: el que vuelve a nadar necesita su flotador otra vez.
        _conFlotador.Remove(zombi.Pointer);
    }

    // Un zombi que camina fuera del agua no lleva recorte: conservarlo lo dejaría sin cuerpo.
    internal static void RevisarZombi(Zombie zombi)
    {
        if (VersusBotMod.Datos == null || !VersusBotMod.Datos.m_gameplayService.IsVSMode || zombi.mZombieType == ZombieType.Target || zombi.mZombieType == ZombieType.Gravestone)
        {
            return;
        }
        ActualizarFlotadorDeGlobo(zombi);
        var controlador = zombi.mController;
        var efecto = controlador?.m_materialEffectController;
        if (Piscina && efecto != null && efecto.m_enableClipRect && !zombi.mInPool && zombi.mZombiePhase == ZombiePhase.ZombieNormal && zombi.mZombieHeight == ZombieHeight.ZombieNormal)
        {
            efecto.SetEnableClipRect(false);
        }
        var ahora = Time.unscaledTime;
        if (!_zombisRevisados.TryGetValue(zombi.Pointer, out var proxima))
        {
            _zombisRevisados[zombi.Pointer] = ahora + EsperaRevisionDeZombi;
            return;
        }
        if (ahora < proxima || controlador == null || zombi.mPosX >= BordeDerechoDelTablero)
        {
            return;
        }
        _zombisRevisados[zombi.Pointer] = ahora + EsperaEntreRevisiones;
        RevisarPartes(zombi, controlador);
    }

    // El cuerpo se dibuja en las partes "0" y "1". Si faltan solo queda la pieza vacía "Offset".
    private static void RevisarPartes(Zombie zombi, ZombieController controlador)
    {
        var estado = "";
        var hayParte = false;
        var apagadas = new List<Renderer>();
        foreach (var dibujo in controlador.GetComponentsInChildren<Renderer>(true))
        {
            var activo = dibujo.enabled && dibujo.gameObject.activeInHierarchy;
            var malla = dibujo.GetComponent<MeshFilter>()?.sharedMesh;
            estado += dibujo.name + ":" + (dibujo.enabled ? "on" : "off") + "/" + (dibujo.gameObject.activeInHierarchy ? "act" : "inact") + "/v" + (malla != null ? malla.vertexCount : -1) + " ";
            if (dibujo.name.Length == 1 && char.IsDigit(dibujo.name[0]))
            {
                if (activo)
                {
                    hayParte = true;
                }
                else
                {
                    apagadas.Add(dibujo);
                }
            }
        }
        if (_reparados.Remove(zombi.Pointer))
        {
            VersusBotMod.Detalle("Normal después de encender sus partes: fila " + zombi.mRow + " [" + estado + "]");
        }
        if (hayParte || apagadas.Count == 0 || zombi.mZombieType != ZombieType.Normal)
        {
            return;
        }
        _reparados.Add(zombi.Pointer);
        VersusBotMod.Detalle("Normal sin cuerpo: fila " + zombi.mRow + " x=" + zombi.mPosX + " fase=" + zombi.mZombiePhase + " agua=" + zombi.mInPool
            + " separador=" + controlador.m_materialEffectController?.m_enableRenderSeparator + " [" + estado + "]");
        foreach (var parte in apagadas)
        {
            parte.gameObject.SetActive(true);
            parte.enabled = true;
        }
    }

    // Las podadoras no tocan objetivos ni tumbas; la aspiradora de la piscina sí, y eso no debe pasar.
    internal static bool AspiradoraNoLoTraga(LawnMower maquina, Zombie zombi)
    {
        return Piscina && maquina.mMowerType == LawnMowerType.Pool && (zombi.mZombieType == ZombieType.Target || zombi.mZombieType == ZombieType.Gravestone);
    }

    internal static bool EnTierra(Zombie zombi)
    {
        return Piscina && zombi.mBoard != null && (!EsFilaDeAgua(zombi.mBoard, zombi.mRow) || EnPastoDeFilaConAgua(zombi));
    }

    internal static void EmpiezaPartida()
    {
        _finDelInicio = Time.unscaledTime + DuracionInicio;
        _conFlotador.Clear();
        TerminarCamaraLenta();
        _zombisRevisados.Clear();
    }

    // El juego pone el inicio en las líneas 2 y 4, y la 4 ahora es agua: pasa a la línea 5.
    internal static int FilaDeInicio(int fila)
    {
        return Piscina && fila == FilaInicialEnAgua && Time.unscaledTime < _finDelInicio ? FilaInicialEnPasto : fila;
    }

    // Sobre el pasto de una fila con agua, el zombi camina como en tierra firme.
    internal static bool EnPastoDeFilaConAgua(Zombie zombi)
    {
        var tablero = zombi.mBoard;
        return Piscina && tablero != null && tablero.mApp != null && tablero.mApp.IsVersusMode()
            && zombi.mRow >= 0 && zombi.mRow < tablero.mPlantRow.Length && tablero.mPlantRow[zombi.mRow] == PlantRowType.Pool
            && tablero.PixelToGridXKeepOnBoard(zombi.mPosX + CentroZombi, zombi.mPosY) >= ColumnasConAgua;
    }

    internal static bool ZombiSigueEnPasto(Zombie zombi)
    {
        return !zombi.mInPool && EnPastoDeFilaConAgua(zombi);
    }

    // El juego pone el flotador al crear al zombi según su fila. Aquí se pone al llegar al agua.
    internal static void MostrarFlotadorEnAgua(Zombie zombi)
    {
        if (Piscina && zombi.mInPool && _conFlotador.Add(zombi.Pointer))
        {
            zombi.ReanimShowPrefix(PiezaFlotador, GrupoDeDibujoNormal);
        }
    }

    // Mientras el juego prepara al zombi, su fila cuenta como pasto. Devuelve la fila a restaurar.
    internal static int FilaComoPasto(int fila)
    {
        var filas = Piscina ? TableroActual?.mPlantRow : null;
        if (filas == null || fila < 0 || fila >= filas.Length || filas[fila] != PlantRowType.Pool)
        {
            return -1;
        }
        filas[fila] = PlantRowType.Normal;
        return fila;
    }

    internal static void RestaurarFila(int fila)
    {
        if (fila >= 0 && TableroActual != null)
        {
            TableroActual.mPlantRow[fila] = PlantRowType.Pool;
        }
    }

    internal static Exception Registrar(Exception error)
    {
        if (error != null)
        {
            VersusBotMod.Log.Error(error.ToString());
        }
        return null;
    }

    // El fondo anterior sigue recibiendo los clics del tablero: se esconde sin desactivarlo.
    private static void MostrarSoloFondoActual()
    {
        foreach (var dibujo in _ocultos)
        {
            if (dibujo != null)
            {
                dibujo.enabled = true;
            }
        }
        foreach (var grafico in _graficosOcultos)
        {
            if (grafico != null)
            {
                grafico.canvasRenderer.SetAlpha(1f);
            }
        }
        _ocultos.Clear();
        _graficosOcultos.Clear();
        var actual = _partida.LevelBackground;
        foreach (var objeto in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<BackgroundController>()))
        {
            var fondo = objeto.Cast<BackgroundController>().gameObject;
            if (actual != null && fondo.Pointer == actual.Pointer)
            {
                continue;
            }
            foreach (var dibujo in fondo.GetComponentsInChildren<Renderer>(true))
            {
                if (dibujo.enabled && !EsPiezaDeJuego(dibujo.gameObject))
                {
                    dibujo.enabled = false;
                    _ocultos.Add(dibujo);
                }
            }
            foreach (var grafico in fondo.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                if (!EsPiezaDeJuego(grafico.gameObject))
                {
                    grafico.canvasRenderer.SetAlpha(0f);
                    _graficosOcultos.Add(grafico);
                }
            }
        }
    }

    // El juego cuelga del fondo la pala, los cursores y el círculo de victoria: solo se esconde el paisaje.
    private static bool EsPiezaDeJuego(GameObject objeto)
    {
        return objeto.GetComponentInParent<ReloadedController>(true) != null || objeto.GetComponentInParent<FocusCircleController>(true) != null;
    }

    // El agua es una capa sobre el pasto pintado. Sus 3 últimas columnas se tapan con pasto del
    // mismo dibujo, tomado de encima de la piscina, y el corte lleva el mismo borde de piedra.
    private static void CrearParcheDePasto(GameObject fondo)
    {
        SpriteRenderer pasto = null;
        SpriteRenderer borde = null;
        foreach (var dibujo in fondo.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (dibujo.name == "Background Sprite")
            {
                pasto = dibujo;
            }
            else if (dibujo.name == "PoolEdgeOL")
            {
                borde = dibujo;
            }
        }
        if (pasto == null || borde == null)
        {
            VersusBotMod.Log.Warning("No se encontró el dibujo de la piscina para recortarla.");
            return;
        }
        var padre = pasto.transform.parent;
        if (padre.Find(NombreParche) != null)
        {
            return;
        }
        var limites = pasto.bounds;
        var recorte = pasto.sprite.textureRect;
        var pixelesPorUnidadX = recorte.width / limites.size.x;
        var pixelesPorUnidadY = recorte.height / limites.size.y;
        var origen = new Rect(
            recorte.x + ParcheIzquierda * pixelesPorUnidadX,
            recorte.y + OrigenPastoAbajo * pixelesPorUnidadY,
            (ParcheDerecha - ParcheIzquierda) * pixelesPorUnidadX,
            (OrigenPastoArriba - OrigenPastoAbajo) * pixelesPorUnidadY);
        var trozo = Sprite.Create(pasto.sprite.texture, origen, new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        trozo.name = NombreParche;

        var parche = new GameObject(NombreParche);
        parche.transform.SetParent(padre, false);
        var dibujoParche = parche.AddComponent<SpriteRenderer>();
        dibujoParche.sprite = trozo;
        dibujoParche.sortingLayerID = pasto.sortingLayerID;
        dibujoParche.sortingOrder = borde.sortingOrder + 1;
        var escala = padre.lossyScale;
        parche.transform.position = new Vector3(limites.min.x + (ParcheIzquierda + ParcheDerecha) / 2f, limites.min.y + (ParcheAbajo + ParcheArriba) / 2f, pasto.transform.position.z);
        parche.transform.localScale = new Vector3((ParcheDerecha - ParcheIzquierda) / origen.width / escala.x, (ParcheArriba - ParcheAbajo) / origen.height / escala.y, 1f);

        CrearLosas(padre, limites, pasto, dibujoParche.sortingOrder + 1, escala);
        VersusBotMod.Detalle("Piscina recortada: " + ColumnasConAgua + " columnas de agua, con losas.");
    }

    private static void CrearLosas(Transform padre, Bounds limites, SpriteRenderer pasto, int orden, Vector3 escala)
    {
        var recorte = pasto.sprite.textureRect;
        var pixelesPorUnidadX = recorte.width / limites.size.x;
        var pixelesPorUnidadY = recorte.height / limites.size.y;
        var borde = new Rect(
            recorte.x + BordeHastaElPatio * pixelesPorUnidadX,
            recorte.y + (ParcheAbajo - LosasSobresalen) * pixelesPorUnidadY,
            (BordeDesdeElAgua - BordeHastaElPatio) * pixelesPorUnidadX,
            (ParcheArriba - ParcheAbajo + 2f * LosasSobresalen) * pixelesPorUnidadY);
        var textura = TexturaLosas.Obtener(pasto.sprite.texture, borde);
        var sprite = Sprite.Create(textura, new Rect(0f, 0f, textura.width, textura.height), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        sprite.name = NombreLosas;
        var losas = new GameObject(NombreLosas);
        losas.transform.SetParent(padre, false);
        var dibujo = losas.AddComponent<SpriteRenderer>();
        dibujo.sprite = sprite;
        dibujo.sortingLayerID = pasto.sortingLayerID;
        dibujo.sortingOrder = orden;
        var ancho = ParcheDerecha - ParcheIzquierda;
        var alto = ParcheArriba - ParcheAbajo + 2f * LosasSobresalen;
        losas.transform.position = new Vector3(limites.min.x + (ParcheIzquierda + ParcheDerecha) / 2f, limites.min.y + (ParcheAbajo + ParcheArriba) / 2f, pasto.transform.position.z);
        losas.transform.localScale = new Vector3(ancho / textura.width / escala.x, alto / textura.height / escala.y, 1f);
    }

    // El fondo nuevo no recibe clics hasta conectarlo con el tablero como el anterior.
    private static void ConectarClics(BackgroundController anterior, BackgroundController nuevo, Board tablero)
    {
        if (anterior == null || nuevo == null || anterior.Pointer == nuevo.Pointer)
        {
            return;
        }
        var sinTablero = nuevo.m_board == null;
        if (sinTablero)
        {
            nuevo.Init(tablero);
        }
        var sinClic = nuevo.MouseDown == null;
        if (sinClic)
        {
            nuevo.MouseDown = anterior.MouseDown;
        }
        if (nuevo.MouseUp == null)
        {
            nuevo.MouseUp = anterior.MouseUp;
        }
        VersusBotMod.Detalle("Clics del fondo '" + nuevo.name + "': sinTablero=" + sinTablero + " sinClic=" + sinClic + " desde '" + anterior.name + "' (tenía clic=" + (anterior.MouseDown != null) + ")");
    }

    // La línea roja es un dibujo apagado del fondo del jardín, que la piscina no trae. Se copia en
    // el corte del agua: 6 bloques para las plantas y 3 para los zombis.
    private static void PonerLineaRoja()
    {
        var fondo = _partida?.LevelBackground;
        if (!Piscina || fondo == null || fondo.transform.Find(NombreLineaRoja) != null)
        {
            return;
        }
        SpriteRenderer referencia = null;
        var ordenBorde = int.MinValue;
        foreach (var dibujo in fondo.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (dibujo.name == "Background Sprite")
            {
                referencia = dibujo;
            }
            else if (dibujo.name == "PoolEdgeOL")
            {
                ordenBorde = dibujo.sortingOrder;
            }
        }
        var linea = BuscarLineaDelJardin(fondo);
        if (referencia == null || linea == null || linea.sprite == null)
        {
            VersusBotMod.Log.Warning("No se encontró la línea roja del jardín.");
            return;
        }
        var limites = referencia.bounds;
        var copia = new GameObject(NombreLineaRoja);
        copia.transform.SetParent(fondo.transform, false);
        copia.transform.position = new Vector3(limites.min.x + ParcheIzquierda, LineaCentroY, referencia.transform.position.z);
        var escalaPadre = fondo.transform.lossyScale;
        var altoDibujo = linea.sprite.bounds.size.y;
        copia.transform.localScale = new Vector3(linea.transform.lossyScale.x / escalaPadre.x, LineaAlto / altoDibujo / escalaPadre.y, 1f);
        var nueva = copia.AddComponent<SpriteRenderer>();
        nueva.sprite = linea.sprite;
        nueva.color = new Color(linea.color.r, linea.color.g, linea.color.b, 1f);
        nueva.sharedMaterial = linea.sharedMaterial;
        nueva.maskInteraction = SpriteMaskInteraction.None;
        nueva.sortingLayerID = referencia.sortingLayerID;
        nueva.sortingOrder = ordenBorde + LineaOrdenSobreLosas;
        VersusBotMod.Detalle("Línea roja copiada a la piscina: dibujo '" + linea.sprite.name + "' color " + linea.color + " medida " + nueva.bounds.size + " centro " + nueva.bounds.center + " orden " + nueva.sortingOrder);
    }

    private static SpriteRenderer BuscarLineaDelJardin(GameObject piscina)
    {
        foreach (var objeto in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<BackgroundController>()))
        {
            var fondo = objeto.Cast<BackgroundController>();
            if (fondo.gameObject.Pointer == piscina.Pointer)
            {
                continue;
            }
            foreach (var dibujo in fondo.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (dibujo.name == DibujoLineaRoja)
                {
                    return dibujo;
                }
            }
        }
        return null;
    }

    // En Versus el nenúfar no trae recarga: se podría plantar sin pausa.
    internal static void RecargaDeNenufar(SeedPacket carta)
    {
        var minima = carta.mPacketType == SeedType.Lilypad ? RecargaMinimaDeNenufar : carta.mPacketType == SeedType.Tanglekelp ? RecargaMinimaDeAlga : 0;
        if (!Piscina || carta.mRefreshTime >= minima || VersusBotMod.Datos == null || !VersusBotMod.Datos.m_gameplayService.IsVSMode)
        {
            return;
        }
        carta.mRefreshTime = minima;
        carta.mRefreshCounter = 0;
        carta.mRefreshing = true;
    }

    // En Versus el zombi ganador entra a la casa y, al pasar cierto punto, muere y sale la copa.
    // Ese tramo dura cerca de 1 segundo: se juega en cámara lenta para que dure unos 2,5 segundos.
    private const float VelocidadVictoriaZombi = 0.5f;
    // El zombi no desaparece antes de esto ni antes de que el círculo termine de cerrarse,
    // porque el círculo también va más lento. Sigue caminando: no se detiene frente a la casa.
    private const float DuracionMinimaVictoria = 3.5f;
    private const float DuracionMaximaVictoria = 6f;
    private const float MargenCirculoCerrado = 1.1f;
    private static VersusMode _versusGanado;
    private static float _inicioVictoria;
    private const float LimiteCamaraLenta = 8f;
    private static IntPtr _zombiGanador;

    internal static void CirculoPedido(VersusMode versus, GameObject objetivo)
    {
        if (versus.Phase != VersusPhase.ZombiesWin || objetivo == null)
        {
            return;
        }
        _zombiGanador = objetivo.Pointer;
        _versusGanado = versus;
        _inicioVictoria = Time.unscaledTime;
        Il2Cpp.TimeUtil.SetFlowingTimeScale(VelocidadVictoriaZombi, 1f);
        MelonLoader.MelonCoroutines.Start(TerminarCamaraLentaLuego());
    }

    // Devuelve si el zombi ya puede desaparecer.
    internal static bool PuedeDesaparecer(Zombie zombi)
    {
        if (_zombiGanador == IntPtr.Zero || zombi.mController == null || zombi.mController.gameObject.Pointer != _zombiGanador)
        {
            return true;
        }
        var pasado = Time.unscaledTime - _inicioVictoria;
        var control = _versusGanado?.m_focusCircleController;
        var circulo = control?.m_focusCircle;
        var cerrado = circulo == null || circulo.transform.lossyScale.x <= control.m_minScale * MargenCirculoCerrado;
        if (pasado < DuracionMaximaVictoria && (pasado < DuracionMinimaVictoria || !cerrado))
        {
            return false;
        }
        VersusBotMod.Detalle("Final de zombis: " + pasado + " s, círculo cerrado=" + cerrado);
        TerminarCamaraLenta();
        return true;
    }

    private static void TerminarCamaraLenta()
    {
        if (_zombiGanador != IntPtr.Zero)
        {
            _zombiGanador = IntPtr.Zero;
            Il2Cpp.TimeUtil.SetFlowingTimeScale(1f, 1f);
        }
    }

    // Por si la partida se abandona a mitad de la animación.
    private static System.Collections.IEnumerator TerminarCamaraLentaLuego()
    {
        var hora = Time.unscaledTime + LimiteCamaraLenta;
        while (Time.unscaledTime < hora)
        {
            yield return null;
        }
        TerminarCamaraLenta();
    }

    // Cartas nuevas. El juego no tiene carta de delfín ni de buzo en Versus: el delfín usa el espacio
    // libre del Imp y el buzo el del minijuego Zombiquarium, con sus reglas propias.
    internal const SeedType CartaDelfin = SeedType.ZombieImp;
    internal const SeedType CartaBuzo = SeedType.ZombiquariumSnorkel;
    private const int CostoDelfin = 150;
    private const int CostoBuzo = 100;
    private const int RecargaDelfin = 1500;
    private const int RecargaBuzo = 1200;
    private const int RecargaDeGlobo = 1000;
    // Puntos del tablero donde entran al agua: el delfín en el tercer bloque zombi, el buzo en la línea roja.
    private const float EntradaDelfinTrasElFin = 20f;
    private const float EntradaBuzoAntesDelFin = 40f;
    private const float PosicionDeEntradaDelJuego = 710f;
    private const float PosicionAntesDeEntrar = 800f;
    private const float FinDeLaZonaDeEntrada = 300f;

    internal static bool EsCartaSoloDeAgua(SeedType carta) => carta == CartaDelfin || carta == CartaBuzo;

    // El juego trata la carta del buzo como una que va en el agua: pedía justo el lado contrario.
    // Va en el pasto del bando zombi, en las líneas 3 y 4.
    internal static bool ReglaDeBuzo(Board tablero, int columna, int fila, SeedType carta, ref PlantingReason resultado)
    {
        if (carta != CartaBuzo || tablero == null || !EsVersus(tablero.mApp))
        {
            return false;
        }
        resultado = Piscina && columna >= ColumnaFinDelAgua && fila >= 0 && EsFilaDeAgua(tablero, fila) ? PlantingReason.Ok : PlantingReason.VSNotHere;
        return true;
    }

    private static readonly SeedType[] CartaEnMano = { SeedType.None, SeedType.None };
    private static SeedType _colocando = SeedType.None;

    internal static void CartaTomada(SeedType carta, int jugador)
    {
        if (jugador >= 0 && jugador < CartaEnMano.Length)
        {
            CartaEnMano[jugador] = carta;
        }
    }

    internal static bool EnviarClicDeBuzo(Board tablero, int x, int y, int clics, int jugador)
    {
        if (!Piscina || jugador < 0 || jugador >= CartaEnMano.Length || CartaEnMano[jugador] != CartaBuzo || tablero.mChallenge == null)
        {
            return true;
        }
        tablero.mChallenge.IZombieMouseDownWithZombie(x, y, clics, jugador);
        return false;
    }

    internal static void EmpezarColocacion(int jugador)
    {
        _colocando = Piscina && jugador >= 0 && jugador < CartaEnMano.Length ? CartaEnMano[jugador] : SeedType.None;
    }

    internal static void TerminarColocacion()
    {
        _colocando = SeedType.None;
    }

    internal static ZombieType TipoAColocar(ZombieType tipo)
    {
        return _colocando == CartaDelfin ? ZombieType.DolphinRider : _colocando == CartaBuzo ? ZombieType.Snorkel : tipo;
    }

    internal static bool EsVersus(GameplayActivity partida) => partida != null && partida.IsVersusMode();

    private static void DefinirCarta(PlantDefinition definicion, int costo, int recarga)
    {
        definicion.m_versusCost = costo;
        definicion.m_versusBaseRefreshTime = recarga;
        definicion.m_versusSuddenDeathRefreshTime = recarga;
    }

    // Versus usa una imagen de carta de poca resolución. Se prueba con la otra que trae cada zombi,
    // como la que usa la carta del buzo.
    private static bool _imagenesCambiadas;

    internal static void UsarImagenesNitidas(SeedChooserDataModel modelo)
    {
        // El juego agrega solo esta carta, si ya está en la lista cuando arma las entradas.
        var lista = modelo.m_seedChooserScreen?.mChosenZombies;
        if (lista != null)
        {
            AsegurarCartaDeBuzo(lista);
        }
        if (_imagenesCambiadas)
        {
            return;
        }
        _imagenesCambiadas = true;
    }

    // El juego salta estas cartas al armar la lista de zombis para elegir: se agregan sus entradas.
    internal static void AgregarCartasAlSelector(SeedChooserDataModel modelo)
    {
        var pantalla = modelo.m_seedChooserScreen;
        var zombis = pantalla?.mChosenZombies;
        var entradas = modelo.m_zombieEntriesModel;
        if (zombis == null || entradas == null)
        {
            return;
        }
        var datos = modelo.m_dataService;
        var cactus = datos.GetPlantDefinition(SeedType.Cactus);
        var globo = datos.GetPlantDefinition(SeedType.ZombieBalloon);
        var delfin = datos.GetPlantDefinition(CartaDelfin);
        var buzo = datos.GetPlantDefinition(CartaBuzo);
        if (cactus == null || globo == null || delfin == null || buzo == null)
        {
            VersusBotMod.Log.Warning("Falta una definición de carta: globo=" + (globo != null) + " delfín=" + (delfin != null) + " buzo=" + (buzo != null));
            return;
        }
        DefinirCarta(globo, cactus.m_versusCost, RecargaDeGlobo);
        DefinirCarta(delfin, CostoDelfin, RecargaDelfin);
        DefinirCarta(buzo, CostoBuzo, RecargaBuzo);
        CorregirSiluetaDelDelfin(datos);
        for (var i = 0; i < zombis.Count; i++)
        {
            var carta = zombis[i];
            var definicion = carta.mSeedType == SeedType.ZombieBalloon ? globo : carta.mSeedType == CartaDelfin ? delfin : null;
            if (definicion != null)
            {
                entradas.Add(i, new SeedChooserEntryModel(definicion, carta, pantalla, modelo, false, i).Cast<Il2CppTekly.DataModels.Models.IModel>());
                VersusBotMod.Detalle("Carta agregada a la lista: " + carta.mSeedType + ", posición " + i + ", costo " + definicion.m_versusCost);
            }
        }
        OrdenarCartas(modelo);
    }

    // El juego no tiene textos de carta para estos dos: su almanaque trae claves sin traducir.
    internal static void PonerTextosDeCarta(StringValueModel nombre, StringValueModel texto, SeedType carta)
    {
        if (!EsCartaSoloDeAgua(carta) || nombre == null || texto == null)
        {
            return;
        }
        var titulo = carta == CartaDelfin ? Textos.NombreDelfin : Textos.NombreBuzo;
        var descripcion = carta == CartaDelfin ? Textos.TextoDelfin : Textos.TextoBuzo;
        nombre.m_needsLocalization = false;
        texto.m_needsLocalization = false;
        if (nombre.Value != titulo)
        {
            nombre.Value = titulo;
        }
        if (texto.Value != descripcion)
        {
            texto.Value = descripcion;
        }
    }

    // La silueta es una imagen fija con un desplazamiento propio. El juego nunca la usó con el
    // delfín, y su valor deja la silueta corrida. Medido a ojo en capturas: ajustar si no calza.
    private static readonly Vector2 CorreccionSiluetaDelfin = new Vector2(120f, -140f);
    private static bool _siluetaCorregida;

    private static void CorregirSiluetaDelDelfin(Il2CppReloaded.Services.IDataService datos)
    {
        if (_siluetaCorregida)
        {
            return;
        }
        _siluetaCorregida = true;
        var delfin = datos.GetZombieDefinition(ZombieType.DolphinRider);
        var buzo = datos.GetZombieDefinition(ZombieType.Snorkel);
        var normal = datos.GetZombieDefinition(ZombieType.Normal);
        if (delfin == null)
        {
            return;
        }
        VersusBotMod.Detalle("Desplazamiento de silueta: delfín " + delfin.m_previewSpriteOffset + ", buzo " + buzo?.m_previewSpriteOffset + ", normal " + normal?.m_previewSpriteOffset);
        delfin.m_previewSpriteOffset += CorreccionSiluetaDelfin;
    }

    private static readonly SeedType[] OrdenDeCartas =
    {
        SeedType.ZombieGravestone, SeedType.ZombieNormal, SeedType.ZombieTrafficCone, SeedType.ZombiePail, SeedType.ZombieFlag,
        SeedType.ZombiePolevaulter, SeedType.ZombieNewspaper, SeedType.ZombieScreenDoor, SeedType.ZombieTrashCan, SeedType.ZombieFootball,
        SeedType.ZombieDancer, SeedType.ZombieBalloon, CartaDelfin, CartaBuzo, SeedType.ZombieDigger,
        SeedType.ZombiePogo, SeedType.ZombieLadder, SeedType.ZombieJackInTheBox, SeedType.ZombieBungee, SeedType.ZombieCatapult,
        SeedType.Zomboni, SeedType.ZombieGargantuar,
    };

    // La lista de cartas no se reordena porque el juego la consulta por posición: se cambia el orden de lo que se muestra.
    private static void OrdenarCartas(SeedChooserDataModel modelo)
    {
        var modelos = modelo.m_zombieEntriesModel?.m_models;
        var zombis = modelo.m_seedChooserScreen?.mChosenZombies;
        if (modelos == null || zombis == null)
        {
            return;
        }
        var porCarta = new Dictionary<SeedType, Il2CppTekly.DataModels.Models.ModelReference>();
        var resto = new List<Il2CppTekly.DataModels.Models.ModelReference>();
        for (var i = 0; i < modelos.Count; i++)
        {
            if (int.TryParse(modelos[i].Key, out var indice) && indice >= 0 && indice < zombis.Count)
            {
                porCarta[zombis[indice].mSeedType] = modelos[i];
            }
            else
            {
                resto.Add(modelos[i]);
            }
        }
        var usados = new HashSet<SeedType>();
        modelos.Clear();
        foreach (var carta in OrdenDeCartas)
        {
            if (porCarta.TryGetValue(carta, out var entrada) && usados.Add(carta))
            {
                modelos.Add(entrada);
            }
        }
        foreach (var par in porCarta)
        {
            if (!usados.Contains(par.Key))
            {
                modelos.Add(par.Value);
            }
        }
        foreach (var entrada in resto)
        {
            modelos.Add(entrada);
        }
        modelo.m_zombieEntriesModel.EmitModified();
        TexturaCartas.Generar(porCarta.Keys, modelo.m_dataService, TableroActual?.mApp);
    }

    private static void AsegurarCartaDeBuzo(Il2CppSystem.Collections.Generic.List<ChosenSeed> zombis)
    {
        ChosenSeed modelo = null;
        for (var i = 0; i < zombis.Count; i++)
        {
            if (zombis[i].mSeedType == CartaBuzo)
            {
                return;
            }
            if (zombis[i].mSeedType == CartaDelfin)
            {
                modelo = zombis[i];
            }
        }
        if (modelo == null)
        {
            return;
        }
        var nueva = new ChosenSeed();
        nueva.mSeedType = CartaBuzo;
        nueva.mSeedState = modelo.mSeedState;
        nueva.mImitaterType = modelo.mImitaterType;
        nueva.mSeedIndexInBank = modelo.mSeedIndexInBank;
        nueva.mPlayerIndex = modelo.mPlayerIndex;
        nueva.mRefreshing = modelo.mRefreshing;
        nueva.mRefreshCounter = modelo.mRefreshCounter;
        nueva.mCrazyDavePicked = modelo.mCrazyDavePicked;
        nueva.mNotSuggested = modelo.mNotSuggested;
        nueva.mTimeStartMotion = modelo.mTimeStartMotion;
        nueva.mTimeEndMotion = modelo.mTimeEndMotion;
        nueva.mDurationOfMotion = modelo.mDurationOfMotion;
        nueva.mX = modelo.mX;
        nueva.mY = modelo.mY;
        nueva.mStartX = modelo.mStartX;
        nueva.mStartY = modelo.mStartY;
        nueva.mEndX = modelo.mEndX;
        nueva.mEndY = modelo.mEndY;
        zombis.Add(nueva);
    }

    // El juego decide la entrada al agua con una posición fija, para una piscina de 9 bloques.
    internal static float EmpezarEntradaAlAgua(Zombie zombi, ZombiePhase faseQueCamina, float ajuste)
    {
        var tablero = zombi.mBoard;
        if (!Piscina || tablero == null || zombi.mZombiePhase != faseQueCamina)
        {
            return float.MinValue;
        }
        // El juego lo haría saltar apenas pasa por 720, recién colocado: hasta el punto pedido se le
        // muestra una posición fuera de esa zona.
        // Al otro lado de la piscina ya salió del agua: no debe volver a entrar.
        var real = zombi.mX;
        if (real < FinDeLaZonaDeEntrada)
        {
            return float.MinValue;
        }
        zombi.mX = real > tablero.GridToPixelX(ColumnaFinDelAgua, zombi.mRow) + ajuste ? PosicionAntesDeEntrar : PosicionDeEntradaDelJuego;
        return real;
    }

    internal static void TerminarEntradaAlAgua(Zombie zombi, float real)
    {
        if (real != float.MinValue)
        {
            zombi.mX = real;
            if (zombi.mZombiePhase == ZombiePhase.DolphinIntoPool || zombi.mZombiePhase == ZombiePhase.SnorkelIntoPool)
            {
                VersusBotMod.Log.Msg(zombi.mZombieType + " entra al agua en x=" + real + ", fila " + zombi.mRow);
            }
        }
    }

    internal static float AjusteDelfin => EntradaDelfinTrasElFin;
    internal static float AjusteBuzo => -EntradaBuzoAntesDelFin;

    // El dibujo del zombi globo no trae flotador: se le pone uno propio cuando nada.
    private const string NombreFlotador = "FlotadorDeGlobo";
    // Medidos desde el punto del zombi, en unidades del fondo (2 por pixel de pantalla a 1080p).
    // Medidos comparando el dibujo real con el pato de un zombi con cono, en una captura.
    private const float EscalaPatoReal = 0.56f;
    private const float GiroPatoReal = -90f;
    private const float PatoRealDesvioX = 153f;
    private const float PatoRealDesvioY = -377f;

    private static void ActualizarFlotadorDeGlobo(Zombie zombi)
    {
        if (zombi.mZombieType == ZombieType.Normal || zombi.mZombieType == ZombieType.TrafficCone || zombi.mZombieType == ZombieType.Pail)
        {
            PatoReal.Buscar(zombi);
        }
        if (zombi.mZombieType != ZombieType.Balloon || zombi.mController == null)
        {
            return;
        }
        var raiz = zombi.mController.transform;
        var existente = raiz.Find(NombreFlotador);
        var nadando = Piscina && zombi.mInPool && !zombi.IsDeadOrDying() && zombi.mZombiePhase != ZombiePhase.BalloonPopping && zombi.mZombiePhase != ZombiePhase.BalloonFlying;
        if (!nadando)
        {
            if (existente != null)
            {
                existente.gameObject.SetActive(false);
            }
            return;
        }
        Renderer cuerpo = null;
        foreach (var dibujo in zombi.mController.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (dibujo.name == "0" || cuerpo == null)
            {
                cuerpo = dibujo;
            }
        }
        if (cuerpo == null)
        {
            return;
        }
        var figura = existente?.GetComponent<SpriteRenderer>();
        if (figura == null)
        {
            var objeto = new GameObject(NombreFlotador);
            objeto.transform.SetParent(raiz, false);
            figura = objeto.AddComponent<SpriteRenderer>();
            existente = objeto.transform;
        }
        existente.gameObject.SetActive(true);
        // El dibujo se lee del juego en cuanto aparece un zombi que lo trae.
        var real = PatoReal.Dibujo;
        if (real == null)
        {
            existente.gameObject.SetActive(false);
            return;
        }
        if (figura.sprite == null || figura.sprite.Pointer != real.Pointer)
        {
            figura.sprite = real;
            var escala = raiz.lossyScale;
            existente.localScale = new Vector3(EscalaPatoReal / escala.x, EscalaPatoReal / escala.y, 1f);
            existente.rotation = Quaternion.Euler(0f, 0f, GiroPatoReal);
        }
        // Los límites del cuerpo no sirven: su malla mide cientos de millones. Se usa el punto del zombi.
        figura.sortingLayerID = cuerpo.sortingLayerID;
        figura.sortingOrder = cuerpo.sortingOrder + 1;
        existente.position = new Vector3(raiz.position.x + PatoRealDesvioX, raiz.position.y + PatoRealDesvioY, raiz.position.z);
    }

    private static float _desvioDeAspiradora;

    // El juego decide la salida del agua con una posición fija, para una piscina de 9 bloques.
    // Mientras decide, se le muestra esa posición; los efectos se corrigen para que salgan donde está.
    internal static float EmpezarSalidaDeAspiradora(LawnMower maquina)
    {
        var tablero = maquina.mBoard;
        if (!Piscina || tablero == null || maquina.mMowerType != LawnMowerType.Pool)
        {
            return -1f;
        }
        var posicion = maquina.mX;
        if (posicion <= tablero.GridToPixelX(ColumnaFinDelAgua, maquina.Row) - AspiradoraAntesDelFin || posicion >= SalidaOriginalDeAspiradora)
        {
            return -1f;
        }
        _desvioDeAspiradora = SalidaOriginalDeAspiradora - posicion;
        maquina.mX = SalidaOriginalDeAspiradora;
        return posicion;
    }

    internal static void TerminarSalidaDeAspiradora(LawnMower maquina, float posicion)
    {
        if (posicion >= 0f)
        {
            maquina.mX = posicion;
            _desvioDeAspiradora = 0f;
        }
    }

    internal static float DesvioDeAspiradora => _desvioDeAspiradora;

    private static void PonerFondo(Board tablero)
    {
        var datos = Piscina ? BuscarDatosDePiscina() : _datosOriginales;
        if (datos == null)
        {
            VersusBotMod.Log.Warning("No se encontraron los datos del mapa de piscina.");
            return;
        }
        var anterior = _partida.BackgroundController;
        _partida.ChangeLevelBackground(datos, true);
        _tableroConFondo = tablero.Pointer;
        ConectarClics(anterior, _partida.BackgroundController, tablero);
        MostrarSoloFondoActual();
        if (Piscina)
        {
            CrearParcheDePasto(_partida.LevelBackground);
            PonerLineaRoja();
        }
        var fondos = "";
        foreach (var fondo in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<BackgroundController>()))
        {
            fondos += fondo.name + " ";
        }
        VersusBotMod.Detalle("Fondo cambiado con los datos de '" + datos.LevelName + "' (zona " + datos.GameArea + "). Activo: "
            + _partida.LevelBackground?.name + ". Fondos en escena: " + fondos);
    }

    private static LevelEntryData BuscarDatosDePiscina()
    {
        var datos = _partida.GetLevelEntryData(NivelDePiscina);
        if (datos != null && datos.GameArea == GameArea.Pool)
        {
            return datos;
        }
        foreach (var objeto in Resources.FindObjectsOfTypeAll(Il2CppType.Of<LevelEntryData>()))
        {
            var candidato = objeto.Cast<LevelEntryData>();
            if (candidato.GameArea == GameArea.Pool)
            {
                return candidato;
            }
        }
        return null;
    }
}

[HarmonyPatch(typeof(Zombie), nameof(Zombie.CheckForPool))]
internal static class ParcheZombiEnPasto
{
    private static bool Prefix(Zombie __instance)
    {
        return !MapaVersus.ZombiSigueEnPasto(__instance);
    }

    private static void Postfix(Zombie __instance)
    {
        MapaVersus.MostrarFlotadorEnAgua(__instance);
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

// Sin chapoteo sobre el pasto.
[HarmonyPatch(typeof(Zombie), nameof(Zombie.PoolSplash))]
internal static class ParcheChapoteo
{
    private static bool Prefix(Zombie __instance)
    {
        return !MapaVersus.EnTierra(__instance);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// Los zombis que suben del suelo nacerían dentro del agua aunque estén en tierra.
[HarmonyPatch(typeof(Zombie), nameof(Zombie.RiseFromGrave))]
internal static class ParcheNacerEnTierra
{
    private static void Prefix(Zombie __instance, int theRow, out int __state)
    {
        __state = MapaVersus.EmpezarNacimiento(__instance, theRow);
    }

    private static Exception Finalizer(Exception __exception, Zombie __instance, int __state)
    {
        MapaVersus.TerminarNacimiento(__instance, __state);
        return MapaVersus.Registrar(__exception);
    }
}

// En las filas de agua solo entran los zombis que saben nadar.
[HarmonyPatch(typeof(Board), nameof(Board.CanPlantAt))]
internal static class ParcheZombisEnAgua
{
    private static void Postfix(Board __instance, int theGridX, int theGridY, SeedType theType, ref PlantingReason __result)
    {
        if (MapaVersus.ReglaDeBuzo(__instance, theGridX, theGridY, theType, ref __result))
        {
            return;
        }
        if (__result == PlantingReason.Ok && MapaVersus.ZombiProhibidoEnFila(__instance, theGridY, theType))
        {
            __result = PlantingReason.VSNotHere;
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// Los zombis que coloca una persona en Versus se revisan en Challenge, no en el tablero.
[HarmonyPatch(typeof(Challenge), nameof(Challenge.CanPlantAt))]
internal static class ParcheZombisEnAguaAlColocar
{
    private static void Postfix(int theGridX, int theGridY, SeedType theType, ref PlantingReason __result)
    {
        if (MapaVersus.ReglaDeBuzo(MapaVersus.TableroActual, theGridX, theGridY, theType, ref __result))
        {
            return;
        }
        if (__result == PlantingReason.Ok && MapaVersus.ZombiProhibidoEnFila(MapaVersus.TableroActual, theGridY, theType))
        {
            __result = PlantingReason.VSNotHere;
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// La revisión de cartas está incluida dentro de Update en el código compilado del juego.
[HarmonyPatch(typeof(VersusMode), nameof(VersusMode.Update))]
internal static class ParcheEsperarCartaExtra
{
    private static void Prefix(VersusMode __instance)
    {
        MapaVersus.EmpezarConteoDeCartas(__instance);
    }

    private static Exception Finalizer(Exception __exception)
    {
        MapaVersus.TerminarConteoDeCartas();
        return MapaVersus.Registrar(__exception);
    }
}

[HarmonyPatch(typeof(SeedBank), nameof(SeedBank.GetPacketCount))]
internal static class ParcheConteoDeCartas
{
    private static void Postfix(ref int __result)
    {
        __result = MapaVersus.CartasParaElConteo(__result);
    }
}

// Los trineos de la muerte súbita tampoco entran a las filas de agua.
[HarmonyPatch(typeof(VersusMode), nameof(VersusMode.CanTrySpawnOnRow))]
internal static class ParcheTrineosEnAgua
{
    private static void Postfix(int row, ref bool __result)
    {
        if (__result && MapaVersus.Piscina && MapaVersus.EsFilaDeAgua(MapaVersus.TableroActual, row))
        {
            __result = false;
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(SeedPacket), nameof(SeedPacket.WasPlanted))]
internal static class ParcheRecargaDeNenufar
{
    private static void Postfix(SeedPacket __instance)
    {
        MapaVersus.RecargaDeNenufar(__instance);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// IZombieSeedTypeToZombieType e IsZombieSeedType empiezan con saltos condicionales y NO se pueden
// parchear con Harmony: el salto queda mal copiado y el juego se cierra sin error.
[HarmonyPatch(typeof(SeedPacket), nameof(SeedPacket.Activated))]
internal static class ParcheCartaTomada
{
    private static void Postfix(SeedPacket __instance, int playerIndex)
    {
        MapaVersus.CartaTomada(__instance.mPacketType, playerIndex);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// El juego llega aquí solo con cartas de zombi en su rango; el buzo se envía directo.
[HarmonyPatch(typeof(Board), nameof(Board.MouseDownWithPlant))]
internal static class ParcheClicConBuzo
{
    private static bool Prefix(Board __instance, int x, int y, int theClickCount, int playerIndex)
    {
        return MapaVersus.EnviarClicDeBuzo(__instance, x, y, theClickCount, playerIndex);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(Challenge), nameof(Challenge.IZombieMouseDownWithZombie))]
internal static class ParcheClicDeZombiNuevo
{
    private static void Prefix(int playerIndex)
    {
        MapaVersus.EmpezarColocacion(playerIndex);
    }

    private static void Postfix()
    {
        MapaVersus.TerminarColocacion();
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(Challenge), nameof(Challenge.IZombiePlaceZombie))]
internal static class ParcheTipoDeZombiNuevo
{
    private static void Prefix(ref ZombieType theZombieType)
    {
        theZombieType = MapaVersus.TipoAColocar(theZombieType);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(Board), nameof(Board.Update))]
internal static class ParcheRangoDeCartas
{
    private static void Postfix(Board __instance)
    {
        CodigoDeCartas.PrepararSalidaDeZombis(MapaVersus.EsVersus(__instance.mApp));
        CodigoDeCartas.Ajustar(MapaVersus.Piscina && MapaVersus.EsVersus(__instance.mApp));
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// La pantalla de elegir busca la carta en la lista por su número y el del buzo queda fuera de rango.
[HarmonyPatch(typeof(ChosenSeed), nameof(ChosenSeed.Selected))]
internal static class ParcheElegirBuzo
{
    private static bool Prefix(ChosenSeed __instance, SeedChooserScreen seedChooserScreen, int playerIndex)
    {
        if (__instance.mSeedType != MapaVersus.CartaBuzo)
        {
            return true;
        }
        seedChooserScreen.ClickedSeedInChooser(__instance, playerIndex);
        return false;
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// Datos para investigar la copa trabada de una partida ganada con el delfín.
[HarmonyPatch(typeof(Board), nameof(Board.AddCoin))]
internal static class ParcheAvisoDeCopa
{
    private static void Postfix(Coin __result, CoinType theCoinType, float theX, float theY)
    {
        if (theCoinType == CoinType.VersusTrophyPlant || theCoinType == CoinType.VersusTrophyZombie)
        {
            VersusBotMod.Detalle("Copa creada: " + theCoinType + " en x=" + theX + ", y=" + theY);
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(VersusMode), nameof(VersusMode.SetUnfocus))]
internal static class ParcheAvisoDeCirculoQuitado
{
    private static void Postfix()
    {
        VersusBotMod.Detalle("Círculo quitado.");
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(Zombie), nameof(Zombie.UpdateZombieDolphinRider))]
internal static class ParcheEntradaDelfin
{
    private static void Prefix(Zombie __instance, out float __state)
    {
        __state = MapaVersus.EmpezarEntradaAlAgua(__instance, ZombiePhase.DolphinWalking, MapaVersus.AjusteDelfin);
    }

    private static void Postfix(Zombie __instance, float __state)
    {
        MapaVersus.TerminarEntradaAlAgua(__instance, __state);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(Zombie), nameof(Zombie.UpdateZombieSnorkel))]
internal static class ParcheEntradaBuzo
{
    private static void Prefix(Zombie __instance, out float __state)
    {
        __state = MapaVersus.EmpezarEntradaAlAgua(__instance, ZombiePhase.SnorkelWalking, MapaVersus.AjusteBuzo);
    }

    private static void Postfix(Zombie __instance, float __state)
    {
        MapaVersus.TerminarEntradaAlAgua(__instance, __state);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(SeedChooserDataModel), nameof(SeedChooserDataModel.UpdateZombieEntries))]
internal static class ParcheGloboEnSelector
{
    private static void Prefix(SeedChooserDataModel __instance)
    {
        MapaVersus.UsarImagenesNitidas(__instance);
    }

    private static void Postfix(SeedChooserDataModel __instance)
    {
        MapaVersus.AgregarCartasAlSelector(__instance);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// El juego mata al que cae del globo si su fila tiene agua, aunque caiga en el pasto. Mientras decide,
// la fila cuenta como pasto; después el zombi camina y, si está sobre el agua, entra con flotador.
[HarmonyPatch(typeof(Zombie), nameof(Zombie.LandFlyer))]
internal static class ParcheCaidaDeGlobo
{
    private static void Prefix(Zombie __instance, out int __state)
    {
        __state = MapaVersus.Piscina && __instance.mZombieType == ZombieType.Balloon ? MapaVersus.FilaComoPasto(__instance.mRow) : -1;
    }

    private static void Postfix(int __state)
    {
        MapaVersus.RestaurarFila(__state);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(Zombie), nameof(Zombie.DieNoLoot))]
internal static class ParcheVictoriaZombi
{
    private static bool Prefix(Zombie __instance)
    {
        return MapaVersus.PuedeDesaparecer(__instance);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(LawnMower), nameof(LawnMower.UpdatePool))]
internal static class ParcheSalidaDeAspiradora
{
    private static void Prefix(LawnMower __instance, out float __state)
    {
        __state = MapaVersus.EmpezarSalidaDeAspiradora(__instance);
    }

    private static void Postfix(LawnMower __instance, float __state)
    {
        MapaVersus.TerminarSalidaDeAspiradora(__instance, __state);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(GameplayActivity), nameof(GameplayActivity.AddReanimation), new[] { typeof(float), typeof(float), typeof(int), typeof(ReanimationType) })]
internal static class ParcheEfectoDeAspiradora
{
    private static void Prefix(ref float __0)
    {
        __0 -= MapaVersus.DesvioDeAspiradora;
    }
}

[HarmonyPatch(typeof(GameplayActivity), nameof(GameplayActivity.AddTodParticle), new[] { typeof(float), typeof(float), typeof(int), typeof(ParticleEffect) })]
internal static class ParcheParticulaDeAspiradora
{
    private static void Prefix(ref float __0)
    {
        __0 -= MapaVersus.DesvioDeAspiradora;
    }
}

[HarmonyPatch(typeof(VersusMode), nameof(VersusMode.SetFocus))]
internal static class ParcheAvisoDeCirculo
{
    private static void Postfix(VersusMode __instance, GameObject focusTarget)
    {
        MapaVersus.CirculoPedido(__instance, focusTarget);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(UnityButtonBinder), nameof(UnityButtonBinder.Bind))]
internal static class ParcheEnlaceBotonUnity
{
    private static void Postfix(UnityButtonBinder __instance)
    {
        PanelRival.BotonEnlazado(__instance.m_key?.Path, __instance.m_button?.gameObject);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(ButtonBinder), nameof(ButtonBinder.Bind))]
internal static class ParcheEnlaceBoton
{
    private static void Postfix(ButtonBinder __instance)
    {
        PanelRival.BotonEnlazado(__instance.m_key?.Path, __instance.m_button?.gameObject);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// Los compañeros del bailarín no aparecen en las líneas de agua: el juego consulta esto antes de crearlos.
[HarmonyPatch(typeof(Board), nameof(Board.RowCanHaveZombieType))]
internal static class ParcheBailarinesEnAgua
{
    private static void Postfix(Board __instance, int theRow, ZombieType theZombieType, ref bool __result)
    {
        if (__result && theZombieType == ZombieType.BackupDancer && MapaVersus.Piscina && MapaVersus.EsVersus(__instance.mApp) && MapaVersus.EsFilaDeAgua(__instance, theRow))
        {
            __result = false;
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(SeedBankDataModel), nameof(SeedBankDataModel.OnTick))]
internal static class ParcheBarraDeCartas
{
    private static void Postfix(SeedBankDataModel __instance)
    {
        MapaVersus.QuitarDibujoDeEspacioExtra(__instance);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// Una carta más por jugador en la piscina.
[HarmonyPatch(typeof(Board), nameof(Board.GetNumSeedsInBank))]
internal static class ParcheCartasEnBanco
{
    private static void Postfix(Board __instance, ref int __result)
    {
        __result = MapaVersus.CartasEnBanco(__instance, __result);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.Update))]
internal static class ParcheCartasEnSelector
{
    private static void Prefix(SeedChooserScreen __instance)
    {
        MapaVersus.AjustarSelector(__instance);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(Board), nameof(Board.Update))]
internal static class ParcheSextoObjetivo
{
    private static void Postfix(Board __instance)
    {
        MapaVersus.AsegurarSextoObjetivo(__instance);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// Evita zombis invisibles que solo muestran la sombra.
[HarmonyPatch(typeof(Zombie), nameof(Zombie.UpdateReanim))]
internal static class ParcheZombiInvisible
{
    private static void Postfix(Zombie __instance)
    {
        MapaVersus.RevisarZombi(__instance);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(LawnMower), nameof(LawnMower.MowZombie))]
internal static class ParcheAspiradora
{
    private static bool Prefix(LawnMower __instance, Zombie theZombie)
    {
        if (MapaVersus.AspiradoraNoLoTraga(__instance, theZombie))
        {
            return false;
        }
        // El juego llama esto en cada cuadro con tumbas y objetivos, y a esos no les hace nada.
        if (MapaVersus.Piscina && theZombie.mZombieType != ZombieType.Target && theZombie.mZombieType != ZombieType.Gravestone)
        {
            VersusBotMod.Log.Msg("Máquina " + __instance.mMowerType + " de la fila " + __instance.Row + " en x=" + __instance.mX + " se lleva a " + theZombie.mZombieType + " (x=" + theZombie.mPosX + ", fase=" + theZombie.mZombiePhase + ")");
        }
        return true;
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// Los pasos suenan según el tipo de fila: sobre el pasto se le hace contar como tierra.
[HarmonyPatch(typeof(Zombie), nameof(Zombie.CheckForZombieStep))]
internal static class ParchePasos
{
    private static void Prefix(Zombie __instance, out int __state)
    {
        __state = MapaVersus.EnPastoDeFilaConAgua(__instance) ? MapaVersus.FilaComoPasto(__instance.mRow) : -1;
    }

    private static void Postfix(int __state)
    {
        MapaVersus.RestaurarFila(__state);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// Sin flotador desde el inicio: el juego lo pondría a todo zombi de una fila con agua.
[HarmonyPatch(typeof(Zombie), nameof(Zombie.ZombieInitialize))]
internal static class ParcheZombiSinFlotador
{
    private static void Prefix(int theRow, out int __state)
    {
        __state = MapaVersus.FilaComoPasto(theRow);
    }

    private static void Postfix(Zombie __instance, int theRow, ZombieType theType, int __state)
    {
        MapaVersus.RestaurarFila(__state);
        MapaVersus.ZombiNuevo(__instance);
        if (theType == ZombieType.Target)
        {
            MapaVersus.ObjetivoCreado(__instance, theRow);
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

// En la piscina hay que poder elegir el nenúfar.
[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.SeedNotAllowedToPick))]
internal static class ParcheNenufarPermitido
{
    private static void Postfix(SeedType theSeedType, ref bool __result)
    {
        if (MapaVersus.EsCartaSoloDeAgua(theSeedType))
        {
            __result = !MapaVersus.Piscina || CodigoDeCartas.NoDisponible;
        }
        else if (theSeedType == SeedType.ZombieBalloon)
        {
            // Sin el cambio en memoria el globo aparece, pero no avanza.
            __result = CodigoDeCartas.NoDisponible;
        }
        else if (MapaVersus.Piscina && (theSeedType == SeedType.Lilypad || theSeedType == SeedType.Tanglekelp))
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.SeedNotRecommendedToPick))]
internal static class ParcheNenufarRecomendado
{
    private static void Postfix(SeedType theSeedType, ref RecommentedFlags __result)
    {
        if (theSeedType == SeedType.ZombieBalloon || (MapaVersus.Piscina && (theSeedType == SeedType.Lilypad || theSeedType == SeedType.Tanglekelp || MapaVersus.EsCartaSoloDeAgua(theSeedType))))
        {
            __result = 0;
        }
    }
}

[HarmonyPatch(typeof(Board), nameof(Board.AddPlant))]
internal static class ParchePlantaInicial
{
    private static void Prefix(ref int theGridY, SeedType theSeedType)
    {
        if (theSeedType == SeedType.Sunflower)
        {
            theGridY = MapaVersus.FilaDeInicio(theGridY);
        }
    }
}

[HarmonyPatch(typeof(Board), nameof(Board.AddZombieAtCell))]
internal static class ParcheTumbaInicialEnCasilla
{
    private static void Prefix(ZombieType theZombieType, ref int y)
    {
        if (theZombieType == ZombieType.Gravestone)
        {
            y = MapaVersus.FilaDeInicio(y);
        }
    }
}

// El zombi con bandera reparte a sus seguidores en un arreglo de 5 líneas: en la sexta falla a medias.
// Se crea en la quinta y después se pasa a la sexta. Sus seguidores siguen en las primeras 5.
[HarmonyPatch(typeof(Zombie), nameof(Zombie.ZombieInitialize))]
internal static class ParcheBanderaEnSextaLinea
{
    private const int SextaLinea = 5;

    private static void Prefix(ref int theRow, ZombieType theType, out bool __state)
    {
        __state = MapaVersus.Piscina && theType == ZombieType.Flag && theRow == SextaLinea;
        if (__state)
        {
            theRow = SextaLinea - 1;
        }
    }

    private static void Postfix(Zombie __instance, bool __state)
    {
        if (__state)
        {
            __instance.SetRow(SextaLinea);
            __instance.mPosY = __instance.GetPosYBasedOnRow(SextaLinea);
            VersusBotMod.Detalle("Zombi con bandera movido a la línea 6.");
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(Zombie), nameof(Zombie.ZombieInitialize))]
internal static class ParcheTumbaInicial
{
    private static void Prefix(ref int theRow, ZombieType theType)
    {
        if (theType == ZombieType.Gravestone)
        {
            theRow = MapaVersus.FilaDeInicio(theRow);
        }
    }
}

[HarmonyPatch(typeof(Board), nameof(Board.PickBackground))]
internal static class ParcheElegirFondo
{
    private static void Postfix(Board __instance)
    {
        if (__instance.mApp != null && __instance.mApp.IsVersusMode())
        {
            MapaVersus.TableroPreparado(__instance);
        }
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

[HarmonyPatch(typeof(VersusMode), nameof(VersusMode.InitializeGameplay))]
internal static class ParcheEmpezarVersus
{
    private static void Prefix(VersusMode __instance)
    {
        MapaVersus.EmpiezaPartida();
        MapaVersus.Asegurar();
    }

    private static void Postfix(VersusMode __instance)
    {
        var tablero = MapaVersus.TableroActual;
        if (tablero != null)
        {
            MapaVersus.CompletarBancos(__instance, tablero);
            MapaVersus.Diagnostico(tablero);
        }
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

// Cambia en memoria los dos valores del rango de IsZombieSeedType, mientras se juega en la piscina,
// para que la carta del buzo cuente como zombi. Si el juego se actualiza y el código es otro, no toca nada.
internal static class CodigoDeCartas
{
    // Rango de IsZombieSeedType, y en IZombieSeedTypeToZombieType la carta del Imp da delfín (14) y
    // las desconocidas, como la del buzo, dan buzo (11). Así la silueta al colocar es la correcta.
    private static readonly (long Direccion, byte[] Original, byte[] Nuevo)[] Cambios =
    {
        (0x6859A0, new byte[] { 0x8D, 0x41, 0xC4, 0x83, 0xF8, 0x12 }, new byte[] { 0x8D, 0x41, 0xC6, 0x83, 0xF8, 0x14 }),
        (0x683DB4, new byte[] { 0xB8, 0x18 }, new byte[] { 0xB8, 0x0E }),
        (0x683D88, new byte[] { 0x33, 0xD2 }, new byte[] { 0xB2, 0x0B }),
    };
    private const uint LecturaEscrituraEjecucion = 0x40;

    private static bool _activo;
    private static bool _noDisponible;
    internal static bool NoDisponible => _noDisponible;

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string nombre);

    [DllImport("kernel32")]
    private static extern bool VirtualProtect(IntPtr direccion, UIntPtr largo, uint proteccion, out uint anterior);

    [DllImport("kernel32")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32")]
    private static extern bool FlushInstructionCache(IntPtr proceso, IntPtr direccion, UIntPtr largo);

    // ZombieRisesFromGround lleva en un número qué zombis NO salen del suelo. Se suman globo, delfín
    // y buzo: al salir del suelo perdían su fase propia y se quedaban quietos. Solo lo usa Versus.
    private const long DireccionSalidaDelSuelo = 0x6D9DA5;
    private static readonly byte[] SalidaOriginal = { 0xB8, 0x02, 0x91, 0x40, 0x00 };
    private static readonly byte[] SalidaNueva = { 0xB8, 0x02, 0xD9, 0x41, 0x00 };
    private static bool _salidaPreparada;

    internal static void PrepararSalidaDeZombis(bool enVersus)
    {
        if (!enVersus || _salidaPreparada)
        {
            return;
        }
        _salidaPreparada = true;
        var modulo = GetModuleHandleW("GameAssembly.dll");
        var direccion = modulo + (nint)DireccionSalidaDelSuelo;
        for (var i = 0; modulo != IntPtr.Zero && i < SalidaOriginal.Length; i++)
        {
            if (Marshal.ReadByte(direccion, i) != SalidaOriginal[i])
            {
                _noDisponible = true;
                VersusBotMod.Log.Warning("Esta versión del juego es distinta a la del mod: se desactivan las cartas de globo, delfín y buzo.");
                return;
            }
        }
        var largo = (UIntPtr)SalidaNueva.Length;
        if (modulo != IntPtr.Zero && VirtualProtect(direccion, largo, LecturaEscrituraEjecucion, out var anterior))
        {
            Marshal.Copy(SalidaNueva, 0, direccion, SalidaNueva.Length);
            VirtualProtect(direccion, largo, anterior, out _);
            FlushInstructionCache(GetCurrentProcess(), direccion, largo);
            VersusBotMod.Log.Msg("Globo, delfín y buzo ya no salen del suelo.");
        }
    }

    internal static void Ajustar(bool activar)
    {
        if (_activo == activar || _noDisponible)
        {
            return;
        }
        var modulo = GetModuleHandleW("GameAssembly.dll");
        if (modulo == IntPtr.Zero)
        {
            _noDisponible = true;
            return;
        }
        foreach (var cambio in Cambios)
        {
            var esperado = activar ? cambio.Original : cambio.Nuevo;
            var direccion = modulo + (nint)cambio.Direccion;
            for (var i = 0; i < esperado.Length; i++)
            {
                if (Marshal.ReadByte(direccion, i) != esperado[i])
                {
                    _noDisponible = true;
                    VersusBotMod.Log.Warning("Esta versión del juego es distinta a la del mod: se desactivan las cartas de globo, delfín y buzo.");
                    return;
                }
            }
        }
        foreach (var cambio in Cambios)
        {
            var nuevo = activar ? cambio.Nuevo : cambio.Original;
            var direccion = modulo + (nint)cambio.Direccion;
            var largo = (UIntPtr)nuevo.Length;
            if (!VirtualProtect(direccion, largo, LecturaEscrituraEjecucion, out var anterior))
            {
                _noDisponible = true;
                VersusBotMod.Log.Warning("No se pudo cambiar la protección de memoria para las cartas nuevas.");
                return;
            }
            Marshal.Copy(nuevo, 0, direccion, nuevo.Length);
            VirtualProtect(direccion, largo, anterior, out _);
            FlushInstructionCache(GetCurrentProcess(), direccion, largo);
        }
        _activo = activar;
        VersusBotMod.Log.Msg("Rango de cartas de zombi " + (activar ? "con buzo." : "original."));
    }
}
