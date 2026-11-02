// Versus Bot — entendimiento del bot: lee el tablero y puntúa cada jugada posible.
// Solo usa lo que un jugador ve en pantalla: zombis, plantas y sus propias cartas.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using System;
using System.Collections.Generic;
using Il2CppReloaded.Gameplay;

namespace VersusBot;

internal sealed class Casilla
{
    public int Columna;
    public int Fila;
    public float Valor;
    public string Motivo;
}

internal static class Estrategia
{
    private const int Columnas = 9;
    private const int FilasDeDia = 5;
    private const int FilasDePiscina = 6;
    // La sexta fila existe solo en la piscina: en el mapa de día todo sigue con 5.
    private static int Filas => MapaVersus.Piscina ? FilasDePiscina : FilasDeDia;
    private const float CentroZombi = 75f;

    // Escala común de valores: cuanto más alto, antes se juega la carta.
    internal const float ValorSinReserva = 250f;
    internal const float ValorEmergencia = 1000f;
    private const float ValorInstantanea = 400f;
    private const float ValorFilaSinDefensa = 300f;
    private const float ValorMuro = 250f;
    private const float ValorEconomiaBase = 260f;
    private const float ValorEconomia = 200f;
    private const float ValorEconomiaBajoPresion = 50f;
    private const float ValorOfensiva = 130f;
    private const float ValorRelleno = 40f;
    private const float ValorDesconocida = 20f;
    private const float ValorCafe = 350f;
    private const float ValorIman = 300f;
    private const float ValorImanPrevisto = 160f;
    private const float ValorParaguas = 180f;
    private const float ValorPrimerosGirasoles = 450f;
    private const float FraccionDeficitLejano = 0.1f;
    private const int PrimerosGirasoles = 2;
    private const int NivelEconomiaPrioritaria = 51;
    private static bool Maestro => VersusBotMod.Nivel >= Bot.NivelMaestro;
    private const int MargenNenufar = 2;
    private const int ColumnaPapaAnticipada = 3;
    private const int MaximoPapasAnticipadas = 2;
    private const int DistanciaMuroMaestro = 4;
    private const float BonoPresionAtacante = 12f;
    private const float BonoPresionPapa = 8f;
    private const float BonoLineaVacia = 150f;
    // Con esto el alga cuenta como emergencia: un delfín cruza demasiado rápido para esperar a otra jugada.
    private const float BonoAlgaContraBuzo = 1000f;
    private const float BonoAguaAmenazada = 200f;
    private const float FraccionMuroPreventivo = 0.6f;
    private const int MaximoHistorialEnPresion = 5;
    private static readonly int[] HistorialPorFila = new int[FilasDePiscina];
    private static readonly HashSet<IntPtr> ZombisContados = new HashSet<IntPtr>();
    private static IntPtr _tableroLeido;
    private static bool _globoListo;
    private static bool _plantaParaNenufar;
    private static bool _otroAtacante;
    private const int GirasolesPrioritarios = 4;
    private const float ValorContraVolador = 6000f;
    private const float BonoNenufarVacio = 80f;
    private const float VariacionEntreLineas = 10f;
    private static readonly Random AzarDeLineas = new Random();
    internal const float ValorEconomiaPrioritaria = 5000f;
    private const int ColumnaZombiMuyCerca = 2;
    private const int ColumnaUrgenciaEconomia = 4;
    private const int ColumnaDefensaCercana = 5;
    private const int MaximoImanes = 2;
    private const int ColumnaIman = 2;
    private const int ColumnaParaguas = 2;
    private const int FilaParaguas = 2;
    private const float ValorRompeTumbas = 120f;
    private const float ValorAntorcha = 140f;
    private const float ValorSoplador = 400f;
    private const float PenaPorAtacante = 30f;
    private const float BonoFilaSinPodadora = 100f;
    private const float BonoObjetivoDanado = 60f;
    // Sin podadora un zombi puede terminar la partida: pesa más que tres zombis en una fila protegida.
    private const float MultiplicadorSinPodadora = 4f;

    // Cuándo merece gastar una carta instantánea.
    private const int VidaZombiFuerte = 300;
    private const int VidaZombiMuyFuerte = 800;
    private static int VidaGrupo => VersusBotMod.Nivel >= Bot.NivelAnticipa ? 480 : 800;
    private static int VidaFilaQuemable => VersusBotMod.Nivel >= Bot.NivelAnticipa ? 600 : 1000;
    private const int ColumnaUrgente = 2;
    private const int ColumnaAlcanceCartaFuerte = 6;
    private const int DistanciaMuro = 3;
    private const int TurnosParaArmarPapa = 3;
    private const int ColumnaPeligroGirasol = 3;
    private const int ColumnaSoplador = 4;
    private const int MinimoPeashootersAntorcha = 2;

    // Daño por segundo de un guisante y tiempos aproximados del juego original.
    private const float DanoGuisante = 13.3f;
    private const float SegundosMuro = 35f;
    private const float SegundosPorColumnaNormal = 4.7f;

    // Lado zombi.
    private const float ValorTumbaBase = 350f;
    private const float PenaDefensaZombi = 30f;
    private const float BonoPocasTumbas = 40f;
    // Cada zombi en una fila la hace menos atractiva: el ataque se reparte por varias líneas.
    private const float PenaZombiEnFila = 40f;
    private const float PenaVidaEnFila = 0.15f;
    // Una fila vacía con atacantes deja que disparen al objetivo: un zombi les bloquea los disparos.
    private const float BonoBloquearObjetivo = 80f;
    private const float BonoObjetivoEnRiesgo = 120f;
    private const float BonoSaltaMuros = 100f;
    private const float PenaGloboContraCactus = 200f;
    private const float PenaFilaCaida = 1000f;
    private const float FraccionPesoSinDefensa = 0.25f;
    private const float ValorBungee = 200f;
    private const int EconomiaBase = 4;

    private static readonly Dictionary<IntPtr, Zombie> Zombis = new Dictionary<IntPtr, Zombie>();
    private static readonly List<Zombie> Vivos = new List<Zombie>();
    private static readonly List<IntPtr> Muertos = new List<IntPtr>();
    private static readonly Dictionary<IntPtr, LawnMower> Podadoras = new Dictionary<IntPtr, LawnMower>();
    private static readonly int[] TumbasPorFila = new int[FilasDePiscina];
    private static readonly bool[] ObjetivoPorFila = new bool[FilasDePiscina];
    private static readonly bool[] PodadoraPorFila = new bool[FilasDePiscina];
    private static readonly int[] VidaObjetivoPorFila = new int[FilasDePiscina];
    private static readonly int[] MaxVidaObjetivoPorFila = new int[FilasDePiscina];
    private static readonly float[] DanoPorFila = new float[FilasDePiscina];
    private static readonly int[] FrenteAtacante = new int[FilasDePiscina];
    private static readonly bool[] MuroPorFila = new bool[FilasDePiscina];
    private static readonly float[] DeficitPorFila = new float[FilasDePiscina];
    private static readonly List<Zombie> ZombisDeFila = new List<Zombie>();
    private static readonly HashSet<SeedType> CartasPropias = new HashSet<SeedType>();
    private static readonly HashSet<SeedType> CartasRival = new HashSet<SeedType>();
    private static bool _esDeNoche;
    private static int _tumbas;
    private static bool _hayObjetivos;
    private static string _ultimoEstado = "";

    internal static void ZombiCreado(Zombie zombi)
    {
        // En el mapa de piscina hay una sexta fila que el bot todavía no sabe jugar.
        if (zombi.mRow < Filas)
        {
            Zombis[zombi.Pointer] = zombi;
            // El juego reutiliza los zombis: cada creación cuenta como una jugada nueva del rival.
            ZombisContados.Remove(zombi.Pointer);
        }
    }

    internal static void PodadoraCreada(LawnMower podadora)
    {
        Podadoras[podadora.Pointer] = podadora;
    }

    // Lee el tablero actual: zombis que avanzan, tumbas, objetivos y podadoras de cada fila.
    // Lo creado en tableros anteriores se descarta.
    internal static void LeerTablero(Board tablero)
    {
        if (tablero.Pointer != _tableroLeido)
        {
            _tableroLeido = tablero.Pointer;
            Array.Clear(HistorialPorFila, 0, HistorialPorFila.Length);
            ZombisContados.Clear();
        }
        _esDeNoche = tablero.StageIsNight();
        Vivos.Clear();
        Muertos.Clear();
        Array.Clear(TumbasPorFila, 0, TumbasPorFila.Length);
        Array.Clear(ObjetivoPorFila, 0, ObjetivoPorFila.Length);
        Array.Clear(PodadoraPorFila, 0, PodadoraPorFila.Length);
        _tumbas = 0;
        _hayObjetivos = false;
        foreach (var par in Zombis)
        {
            var zombi = par.Value;
            if (zombi.mDead || zombi.mBoard == null || zombi.mBoard.Pointer != tablero.Pointer)
            {
                Muertos.Add(par.Key);
            }
            else if (zombi.IsDeadOrDying() || zombi.mRow >= Filas)
            {
                continue;
            }
            else if (zombi.mZombieType == ZombieType.Gravestone)
            {
                TumbasPorFila[zombi.mRow]++;
                _tumbas++;
            }
            else if (zombi.mZombieType == ZombieType.Target)
            {
                ObjetivoPorFila[zombi.mRow] = true;
                VidaObjetivoPorFila[zombi.mRow] = VidaDe(zombi);
                MaxVidaObjetivoPorFila[zombi.mRow] = Math.Max(MaxVidaObjetivoPorFila[zombi.mRow], VidaDe(zombi));
                _hayObjetivos = true;
            }
            else
            {
                Vivos.Add(zombi);
                if (ZombisContados.Add(par.Key))
                {
                    HistorialPorFila[zombi.mRow]++;
                }
            }
        }
        foreach (var clave in Muertos)
        {
            Zombis.Remove(clave);
        }

        Muertos.Clear();
        foreach (var par in Podadoras)
        {
            var podadora = par.Value;
            if (podadora.mDead || podadora.mBoard == null || podadora.mBoard.Pointer != tablero.Pointer)
            {
                Muertos.Add(par.Key);
            }
            else if (podadora.Row < Filas && (podadora.mMowerState == LawnMowerState.Ready || podadora.mMowerState == LawnMowerState.RollingIn))
            {
                PodadoraPorFila[podadora.Row] = true;
            }
        }
        foreach (var clave in Muertos)
        {
            Podadoras.Remove(clave);
        }

        CalcularDefensa(tablero);

        var estado = "podadoras=" + Marcas(PodadoraPorFila) + " objetivos=" + Marcas(ObjetivoPorFila) + " tumbas=" + string.Join("", TumbasPorFila);
        if (estado != _ultimoEstado)
        {
            _ultimoEstado = estado;
            VersusBotMod.Log.Msg("Filas: " + estado);
        }
    }

    // Déficit de una fila: la vida de zombis que sus plantas no alcanzan a matar antes de que lleguen.
    private static void CalcularDefensa(Board tablero)
    {
        for (var fila = 0; fila < Filas; fila++)
        {
            DanoPorFila[fila] = 0f;
            FrenteAtacante[fila] = -1;
            MuroPorFila[fila] = false;
            for (var columna = 0; columna < Columnas; columna++)
            {
                var planta = tablero.GetTopPlantAt(columna, fila, PlantPriority.Any);
                if (planta == null)
                {
                    continue;
                }
                // Un hongo dormido no dispara.
                var dano = planta.mIsAsleep ? 0f : DanoDe(planta.mSeedType);
                if (dano > 0f)
                {
                    DanoPorFila[fila] += dano;
                    FrenteAtacante[fila] = columna;
                }
                MuroPorFila[fila] |= EsMuro(planta.mSeedType);
            }

            ZombisDeFila.Clear();
            foreach (var zombi in Vivos)
            {
                if (zombi.mRow == fila)
                {
                    ZombisDeFila.Add(zombi);
                }
            }
            ZombisDeFila.Sort((a, b) => a.mPosX.CompareTo(b.mPosX));

            var linea = Math.Max(FrenteAtacante[fila], 0);
            var acumulada = 0f;
            var deficit = 0f;
            foreach (var zombi in ZombisDeFila)
            {
                acumulada += VidaDe(zombi);
                var tiempo = Math.Max(0, ColumnaDe(tablero, zombi) - linea) * SegundosPorColumna(zombi) + (MuroPorFila[fila] ? SegundosMuro : 0f);
                deficit = Math.Max(deficit, acumulada - DanoPorFila[fila] * tiempo);
            }
            DeficitPorFila[fila] = deficit;
        }
    }

    private static float DanoDe(SeedType tipo)
    {
        if (tipo == SeedType.Repeater || tipo == SeedType.Melonpult || tipo == SeedType.Wintermelon) return DanoGuisante * 2f;
        if (tipo == SeedType.Gatlingpea) return DanoGuisante * 4f;
        return EsAtacante(tipo) ? DanoGuisante : 0f;
    }

    private static float SegundosPorColumna(Zombie zombi)
    {
        switch (zombi.mZombieType)
        {
            case ZombieType.Football:
            case ZombieType.Polevaulter:
            case ZombieType.Digger:
            case ZombieType.JackInTheBox:
                return 2.5f;
            case ZombieType.Pogo:
            case ZombieType.Zamboni:
                return 2f;
            case ZombieType.Bobsled:
                return 1.5f;
            // Medidos en partida: el delfín cruza en unos 14 s y el buzo en unos 32.
            case ZombieType.DolphinRider:
                return 1.6f;
            case ZombieType.Snorkel:
                return 3.5f;
            case ZombieType.Balloon:
            case ZombieType.Imp:
                return 3f;
            case ZombieType.Gargantuar:
            case ZombieType.RedeyeGargantuar:
                return 6f;
            default:
                return SegundosPorColumnaNormal;
        }
    }

    private static float DeficitPonderado(int fila)
    {
        return DeficitPorFila[fila] * (PodadoraPorFila[fila] ? 1f : MultiplicadorSinPodadora);
    }

    // Solo un zombi cerca de una fila débil justifica frenar la economía.
    private static bool HayUrgencia(Board tablero)
    {
        foreach (var zombi in Vivos)
        {
            if (DeficitPorFila[zombi.mRow] > 0f && ColumnaDe(tablero, zombi) <= ColumnaUrgenciaEconomia)
            {
                return true;
            }
        }
        return false;
    }

    internal static bool HayZombiMuyFuerte()
    {
        foreach (var zombi in Vivos)
        {
            if (VidaDe(zombi) >= VidaZombiMuyFuerte)
            {
                return true;
            }
        }
        return false;
    }

    internal static void LeerCartas(Board tablero, SeedBank propio, SeedBank rival, int indice)
    {
        CartasPropias.Clear();
        CartasRival.Clear();
        AgregarCartas(propio, CartasPropias);
        if (VersusBotMod.Nivel >= Bot.NivelAnticipa)
        {
            AgregarCartas(rival, CartasRival);
        }
        _globoListo = false;
        _plantaParaNenufar = false;
        _otroAtacante = false;
        if (!Maestro)
        {
            return;
        }
        for (var i = 0; rival != null && i < rival.NumPackets; i++)
        {
            var carta = rival.SeedPackets[i];
            _globoListo |= carta != null && carta.mPacketType == SeedType.ZombieBalloon && !carta.mRefreshing;
        }
        var costoNenufar = 0;
        var hayAtacanteParaEncima = false;
        for (var i = 0; propio != null && i < propio.NumPackets; i++)
        {
            var carta = propio.SeedPackets[i];
            if (carta == null)
            {
                continue;
            }
            if (carta.mPacketType == SeedType.Lilypad)
            {
                costoNenufar = carta.GetCost();
            }
            else if (EsAtacante(carta.mPacketType))
            {
                _otroAtacante |= carta.mPacketType != SeedType.Cactus;
                // Un cactus guardado para el globo no iría encima del nenúfar.
                var guardado = carta.mPacketType == SeedType.Cactus && _globoListo;
                hayAtacanteParaEncima |= !guardado;
            }
        }
        // Vale aunque el atacante esté recargando: con pocas cartas, esperar a tenerlo listo deja el agua vacía.
        _plantaParaNenufar = hayAtacanteParaEncima && tablero.CanTakeSunMoney(costoNenufar, indice);
    }

    // Por dónde viene jugando el rival: sus tumbas y los zombis que ya mandó por esa línea.
    private static int Presion(int fila)
    {
        return TumbasPorFila[fila] + Math.Min(HistorialPorFila[fila], MaximoHistorialEnPresion);
    }

    private static bool HayVolador()
    {
        foreach (var zombi in Vivos)
        {
            if (zombi.IsFlying())
            {
                return true;
            }
        }
        return false;
    }

    private static void AgregarCartas(SeedBank banco, HashSet<SeedType> cartas)
    {
        if (banco == null)
        {
            return;
        }
        for (var i = 0; i < banco.NumPackets; i++)
        {
            var carta = banco.SeedPackets[i];
            if (carta != null)
            {
                cartas.Add(carta.mPacketType);
            }
        }
    }

    internal static void RecordarCartasRival(HashSet<SeedType> destino)
    {
        if (CartasRival.Count == 0)
        {
            return;
        }
        destino.Clear();
        destino.UnionWith(CartasRival);
    }

    internal static bool Contrarrestado(SeedType zombi)
    {
        return Cartas.EstaContrarrestado(zombi, CartasRival);
    }

    internal static bool HayZombiSinContrarrestar(SeedBank banco)
    {
        for (var i = 0; i < banco.NumPackets; i++)
        {
            var carta = banco.SeedPackets[i];
            if (carta != null && carta.mPacketType != SeedType.ZombieGravestone && !Contrarrestado(carta.mPacketType))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Urgente(Board tablero, Zombie zombi)
    {
        return ColumnaDe(tablero, zombi) <= ColumnaUrgente && DeficitPorFila[zombi.mRow] > 0f;
    }

    private static string Marcas(bool[] filas)
    {
        var texto = "";
        foreach (var marcada in filas)
        {
            texto += marcada ? "X" : "-";
        }
        return texto;
    }

    internal static int Tumbas => _tumbas;

    private static bool FilaCaida(int fila)
    {
        return _hayObjetivos && !ObjetivoPorFila[fila];
    }

    private static bool HayZombiEnFila(int fila)
    {
        foreach (var zombi in Vivos)
        {
            if (zombi.mRow == fila)
            {
                return true;
            }
        }
        return false;
    }

    // En la piscina no se defiende una línea sin objetivo mientras no se vea un zombi en ella.
    private static bool FilaSinInteres(int fila)
    {
        return MapaVersus.Piscina && FilaCaida(fila) && !HayZombiEnFila(fila);
    }

    // Filas para una jugada al azar en la piscina, de mayor a menor prioridad.
    internal static List<int> FilasParaAzar(bool esZombi, bool prioridadAlta)
    {
        var filas = new List<int>();
        for (var fila = 0; fila < Filas; fila++)
        {
            var conObjetivo = ObjetivoPorFila[fila];
            if (esZombi ? conObjetivo : prioridadAlta ? HayZombiEnFila(fila) : conObjetivo)
            {
                filas.Add(fila);
            }
        }
        return filas;
    }

    private static int ColumnaDe(Board tablero, Zombie zombi)
    {
        return Math.Clamp(tablero.PixelToGridXKeepOnBoard(zombi.mPosX + CentroZombi, zombi.mPosY), 0, Columnas - 1);
    }

    private static int VidaDe(Zombie zombi)
    {
        return zombi.mBodyHealth + zombi.mHelmHealth + zombi.mShieldHealth + zombi.mFlyingHealth;
    }

    // De 0,04 en el borde derecho a 1 junto a la casa, creciendo cada vez más rápido,
    // y mucho mayor si la fila ya no tiene podadora.
    private static float Peligro(Board tablero, Zombie zombi)
    {
        var cercania = (Columnas + 1 - ColumnaDe(tablero, zombi)) / (float)(Columnas + 1);
        var peligro = VidaDe(zombi) * cercania * cercania;
        return PodadoraPorFila[zombi.mRow] ? peligro : peligro * MultiplicadorSinPodadora;
    }

    private static bool Aplastable(Zombie zombi)
    {
        return !zombi.IsFlying() && zombi.mZombieType != ZombieType.Digger && zombi.mZombieType != ZombieType.Bungee;
    }

    private static bool EsAtacante(SeedType tipo)
    {
        return tipo == SeedType.Peashooter || tipo == SeedType.Snowpea || tipo == SeedType.Repeater
            || tipo == SeedType.Threepeater || tipo == SeedType.Splitpea || tipo == SeedType.Cactus
            || tipo == SeedType.Starfruit || tipo == SeedType.Cabbagepult || tipo == SeedType.Kernelpult
            || tipo == SeedType.Melonpult || tipo == SeedType.Gatlingpea || tipo == SeedType.Fumeshroom
            || tipo == SeedType.Puffshroom || tipo == SeedType.Scaredyshroom || tipo == SeedType.Spikeweed
            || tipo == SeedType.Cattail || tipo == SeedType.Wintermelon;
    }

    private static bool EsMuro(SeedType tipo)
    {
        return tipo == SeedType.Wallnut || tipo == SeedType.Tallnut || tipo == SeedType.GiantWallnut;
    }

    private static bool EsApoyoSinUso(SeedType tipo)
    {
        return tipo == SeedType.Plantern || tipo == SeedType.Flowerpot || tipo == SeedType.Lilypad || tipo == SeedType.Imitater;
    }

    // Puntúa la mejor casilla para una carta. Devuelve null si ahora no conviene usarla.
    internal static Casilla MejorCasilla(Board tablero, SeedType tipo, bool esZombi, int economiaDeseada)
    {
        return esZombi
            ? CasillaDeZombi(tablero, tipo, economiaDeseada)
            : CasillaDePlanta(tablero, tipo, economiaDeseada);
    }

    private static Casilla CasillaDePlanta(Board tablero, SeedType tipo, int girasolesDeseados)
    {
        // De día un hongo sin café se queda dormido: no se gasta.
        if (Cartas.EsHongo(tipo) && !_esDeNoche && !CartasPropias.Contains(SeedType.InstantCoffee)) return null;
        if (tipo == SeedType.InstantCoffee) return DespertarHongo(tablero, tipo);
        if (tipo == SeedType.Magnetshroom) return PonerIman(tablero, tipo);
        if (tipo == SeedType.Hypnoshroom) return Aplastar(tablero, tipo, 1, "hipnotizar al zombi");
        if (tipo == SeedType.Umbrella) return PonerParaguas(tablero, tipo);
        if (tipo == SeedType.Squash) return Aplastar(tablero, tipo, 0, "aplastar al zombi más peligroso");
        if (tipo == SeedType.Chomper) return Aplastar(tablero, tipo, 1, "comerse al zombi más peligroso");
        if (tipo == SeedType.Potatomine)
        {
            return Aplastar(tablero, tipo, TurnosParaArmarPapa, "papa en el camino del zombi") ?? (Maestro ? PapaAnticipada(tablero, tipo) : null);
        }
        if (tipo == SeedType.Jalapeno) return QuemarFila(tablero, tipo);
        if (tipo == SeedType.Cherrybomb) return Explotar(tablero, tipo);
        if (tipo == SeedType.Gravebuster) return RomperTumba(tablero, tipo);
        if (tipo == SeedType.Sunflower) return PonerGirasol(tablero, tipo, girasolesDeseados);
        if (tipo == SeedType.Twinsunflower) return PonerGirasolDoble(tablero, tipo);
        if (tipo == SeedType.Torchwood) return PonerAntorcha(tablero, tipo);
        if (tipo == SeedType.Pumpkinshell) return PonerCalabaza(tablero, tipo);
        if (tipo == SeedType.Blover) return Soplar(tablero, tipo);
        if (tipo == SeedType.Cactus && VersusBotMod.Nivel >= NivelEconomiaPrioritaria)
        {
            var contraGlobo = CactusContraVolador(tablero, tipo);
            if (contraGlobo != null) return contraGlobo;
            // El rival tiene el globo listo: el cactus se guarda para responder sin esperar su recarga.
            if (Maestro && _globoListo && _otroAtacante && !HayVolador() && !HayUrgencia(tablero)) return null;
        }
        if (tipo == SeedType.Lilypad && MapaVersus.Piscina) return PonerNenufar(tablero, tipo);
        if (tipo == SeedType.Tanglekelp && MapaVersus.Piscina && Maestro) return PonerAlga(tablero, tipo);
        if (EsAtacante(tipo)) return PonerAtacante(tablero, tipo);
        if (EsMuro(tipo)) return PonerMuro(tablero, tipo);
        return EsApoyoSinUso(tipo) ? null : PonerDesconocida(tablero, tipo);
    }

    // Una carta instantánea se guarda para zombis fuertes al alcance, o para salvar una fila que va a caer.
    private static Casilla Aplastar(Board tablero, SeedType tipo, int distancia, string motivo)
    {
        Casilla mejor = null;
        foreach (var zombi in Vivos)
        {
            if (!Aplastable(zombi))
            {
                continue;
            }
            var vida = VidaDe(zombi);
            var columnaZombi = ColumnaDe(tablero, zombi);
            var alAlcance = columnaZombi <= ColumnaAlcanceCartaFuerte;
            // La papa es barata: desde el nivel alto se usa contra cualquier zombi que viene, una por línea.
            var papaLibre = alAlcance && tipo == SeedType.Potatomine && VersusBotMod.Nivel >= Bot.NivelAnticipa
                && ContarEnFila(tablero, zombi.mRow, t => t == SeedType.Potatomine) == 0;
            var fuerte = papaLibre || alAlcance && (vida >= VidaZombiMuyFuerte || (vida >= VidaZombiFuerte && DeficitPorFila[zombi.mRow] > 0f));
            var urgente = Urgente(tablero, zombi);
            if (!fuerte && !urgente)
            {
                continue;
            }
            var valor = ValorInstantanea + Peligro(tablero, zombi) + (urgente ? ValorEmergencia : 0f);
            if (mejor != null && valor <= mejor.Valor)
            {
                continue;
            }
            var columna = columnaZombi - distancia;
            // En el nivel extremo la papa busca más atrás si esa casilla es agua o está ocupada.
            while (Maestro && tipo == SeedType.Potatomine && columna > 0 && tablero.CanPlantAt(columna, zombi.mRow, tipo) != PlantingReason.Ok)
            {
                columna--;
            }
            if (columna >= 0 && tablero.CanPlantAt(columna, zombi.mRow, tipo) == PlantingReason.Ok)
            {
                mejor = new Casilla { Columna = columna, Fila = zombi.mRow, Valor = valor, Motivo = motivo + " (" + zombi.mZombieType + ", vida " + vida + DescribirFila(zombi.mRow) + ")" };
            }
        }
        return mejor;
    }

    // El alga va delante del zombi que nada, una por línea. El buzo primero: los guisantes y el cactus no le dan bajo el agua.
    private static Casilla PonerAlga(Board tablero, SeedType tipo)
    {
        Casilla mejor = null;
        foreach (var zombi in Vivos)
        {
            if (!zombi.mInPool || zombi.IsFlying() || ContarEnFila(tablero, zombi.mRow, t => t == tipo) > 0)
            {
                continue;
            }
            var valor = ValorInstantanea + Peligro(tablero, zombi) + (zombi.mZombieType == ZombieType.Snorkel || zombi.mZombieType == ZombieType.DolphinRider ? BonoAlgaContraBuzo : 0f) + (Urgente(tablero, zombi) ? ValorEmergencia : 0f);
            if (mejor != null && valor <= mejor.Valor)
            {
                continue;
            }
            var columna = ColumnaDe(tablero, zombi) - 1;
            while (columna > 0 && tablero.CanPlantAt(columna, zombi.mRow, tipo) != PlantingReason.Ok)
            {
                columna--;
            }
            if (columna >= 0 && tablero.CanPlantAt(columna, zombi.mRow, tipo) == PlantingReason.Ok)
            {
                mejor = new Casilla { Columna = columna, Fila = zombi.mRow, Valor = valor, Motivo = "alga contra el zombi que nada (" + zombi.mZombieType + DescribirFila(zombi.mRow) + ")" };
            }
        }
        return mejor;
    }

    // Antes de que llegue el zombi: una papa en la línea por donde más viene jugando el rival.
    private static Casilla PapaAnticipada(Board tablero, SeedType tipo)
    {
        // Primero un atacante en cada línea en pie: si no, estas plantas baratas se comen los soles del nenúfar.
        if (tablero.CountPlantByType(tipo) >= MaximoPapasAnticipadas || HayLineaSinAtacante(tablero))
        {
            return null;
        }
        Casilla mejor = null;
        for (var fila = 0; fila < Filas; fila++)
        {
            var presion = Presion(fila);
            if (presion == 0 || FilaSinInteres(fila) || ContarEnFila(tablero, fila, t => t == SeedType.Potatomine) > 0)
            {
                continue;
            }
            var columna = PrimeraLibre(tablero, tipo, fila, ColumnaPapaAnticipada);
            if (columna < 0 || columna + TurnosParaArmarPapa > ZombiMasCercano(tablero, fila))
            {
                continue;
            }
            var valor = ValorOfensiva + presion * BonoPresionPapa;
            if (mejor == null || valor > mejor.Valor)
            {
                mejor = new Casilla { Columna = columna, Fila = fila, Valor = valor, Motivo = "papa anticipada, presión " + presion };
            }
        }
        return mejor;
    }

    private static string DescribirFila(int fila)
    {
        return PodadoraPorFila[fila] ? "" : ", fila sin podadora";
    }

    private static Casilla QuemarFila(Board tablero, SeedType tipo)
    {
        Casilla mejor = null;
        for (var fila = 0; fila < Filas; fila++)
        {
            var vida = 0;
            var peligro = 0f;
            var urgente = false;
            foreach (var zombi in Vivos)
            {
                if (zombi.mRow == fila && ColumnaDe(tablero, zombi) <= ColumnaAlcanceCartaFuerte + 1)
                {
                    vida += VidaDe(zombi);
                    peligro += Peligro(tablero, zombi);
                    urgente |= Urgente(tablero, zombi);
                }
            }
            if (vida < VidaFilaQuemable && !urgente)
            {
                continue;
            }
            var valor = ValorInstantanea + peligro + (urgente ? ValorEmergencia : 0f);
            var columna = PrimeraLibre(tablero, tipo, fila, 0);
            if (columna >= 0 && (mejor == null || valor > mejor.Valor))
            {
                mejor = new Casilla { Columna = columna, Fila = fila, Valor = valor, Motivo = "quemar la fila con vida " + vida + DescribirFila(fila) };
            }
        }
        return mejor;
    }

    private static Casilla Explotar(Board tablero, SeedType tipo)
    {
        Casilla mejor = null;
        for (var fila = 0; fila < Filas; fila++)
        {
            for (var columna = 0; columna < Columnas; columna++)
            {
                var vida = 0;
                var cuenta = 0;
                var peligro = 0f;
                var urgente = false;
                foreach (var zombi in Vivos)
                {
                    if (Math.Abs(zombi.mRow - fila) <= 1 && Math.Abs(ColumnaDe(tablero, zombi) - columna) <= 1)
                    {
                        vida += VidaDe(zombi);
                        cuenta++;
                        peligro += Peligro(tablero, zombi);
                        urgente |= Urgente(tablero, zombi);
                    }
                }
                var conviene = columna <= ColumnaAlcanceCartaFuerte + 1
                    && ((vida >= VidaGrupo && cuenta >= 2) || vida >= VidaFilaQuemable);
                if (!conviene && !urgente)
                {
                    continue;
                }
                var valor = ValorInstantanea + peligro + (urgente ? ValorEmergencia : 0f);
                if ((mejor == null || valor > mejor.Valor) && tablero.CanPlantAt(columna, fila, tipo) == PlantingReason.Ok)
                {
                    mejor = new Casilla { Columna = columna, Fila = fila, Valor = valor, Motivo = "explotar " + cuenta + " zombis con vida " + vida };
                }
            }
        }
        return mejor;
    }

    private static Casilla RomperTumba(Board tablero, SeedType tipo)
    {
        Casilla mejor = null;
        var masTumbas = 0;
        for (var columna = 0; columna < Columnas; columna++)
        {
            for (var fila = 0; fila < Filas; fila++)
            {
                if (TumbasPorFila[fila] > masTumbas && tablero.GetVSGravestoneAt(columna, fila) != null && tablero.CanPlantAt(columna, fila, tipo) == PlantingReason.Ok)
                {
                    masTumbas = TumbasPorFila[fila];
                    mejor = new Casilla { Columna = columna, Fila = fila, Valor = ValorRompeTumbas, Motivo = "romper una tumba de la fila con más tumbas" };
                }
            }
        }
        return mejor;
    }

    private static Casilla PonerGirasol(Board tablero, SeedType tipo, int deseados)
    {
        var girasoles = tablero.CountSunFlowers();
        if (girasoles >= deseados)
        {
            return null;
        }
        var urgencia = HayUrgencia(tablero);
        var valor = girasoles < PrimerosGirasoles && !urgencia ? ValorPrimerosGirasoles
            : urgencia ? ValorEconomiaBajoPresion
            : girasoles < EconomiaBase ? ValorEconomiaBase : ValorEconomia;
        // Desde el nivel 51 la economía básica no se abandona por un zombi lejano: sin soles no hay defensa.
        // Pasada esa base vuelve a competir con la defensa: 7 girasoles seguidos pierden la partida.
        if (VersusBotMod.Nivel >= NivelEconomiaPrioritaria)
        {
            if (girasoles < (Maestro ? GirasolesPrioritarios : PrimerosGirasoles) ? !HayZombiMuyCerca(tablero) : girasoles < GirasolesPrioritarios && !urgencia)
            {
                valor = ValorEconomiaPrioritaria;
            }
            // Con la base lista, un girasol más solo quita soles a la defensa si todavía falta defender.
            else if (girasoles >= GirasolesPrioritarios && (HayLineaSinAtacante(tablero) || HayDeficit()))
            {
                return null;
            }
        }
        for (var columna = 0; columna < 2; columna++)
        {
            for (var fila = 0; fila < Filas; fila++)
            {
                // Un nenúfar sirve más con un atacante encima que con un girasol.
                if (!MapaVersus.EsFilaDeAgua(tablero, fila) && !FilaAmenazada(tablero, fila) && tablero.CanPlantAt(columna, fila, tipo) == PlantingReason.Ok)
                {
                    return new Casilla { Columna = columna, Fila = fila, Valor = valor, Motivo = "economía" };
                }
            }
        }
        return null;
    }

    private static bool HayDeficit()
    {
        foreach (var deficit in DeficitPorFila)
        {
            if (deficit > 0f)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HayLineaSinAtacante(Board tablero)
    {
        for (var fila = 0; fila < Filas; fila++)
        {
            if (ObjetivoPorFila[fila] && ContarEnFila(tablero, fila, EsAtacante) == 0)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HayZombiMuyCerca(Board tablero)
    {
        foreach (var zombi in Vivos)
        {
            // En el nivel extremo un zombi que aún tiene máquina delante no frena la economía.
            if (ColumnaDe(tablero, zombi) <= ColumnaZombiMuyCerca && (!Maestro || !PodadoraPorFila[zombi.mRow]))
            {
                return true;
            }
        }
        return false;
    }

    private static bool FilaAmenazada(Board tablero, int fila)
    {
        foreach (var zombi in Vivos)
        {
            if (zombi.mRow == fila && ColumnaDe(tablero, zombi) <= ColumnaPeligroGirasol)
            {
                return true;
            }
        }
        return false;
    }

    private static Casilla PonerGirasolDoble(Board tablero, SeedType tipo)
    {
        for (var columna = 0; columna < Columnas; columna++)
        {
            for (var fila = 0; fila < Filas; fila++)
            {
                if (tablero.CanPlantAt(columna, fila, tipo) == PlantingReason.Ok)
                {
                    return new Casilla { Columna = columna, Fila = fila, Valor = HayUrgencia(tablero) ? ValorEconomiaBajoPresion : ValorEconomia, Motivo = "economía doble" };
                }
            }
        }
        return null;
    }

    private static int ZombiMasCercano(Board tablero, int fila)
    {
        var masCerca = Columnas;
        foreach (var zombi in Vivos)
        {
            if (zombi.mRow == fila)
            {
                masCerca = Math.Min(masCerca, ColumnaDe(tablero, zombi));
            }
        }
        return masCerca;
    }

    // Defensa primero, en la fila que más vida deja pasar; sin presión, ataca los objetivos en pie:
    // las plantas ganan rompiéndolos, no solo aguantando.
    private static Casilla PonerAtacante(Board tablero, SeedType tipo, float valorRelleno = ValorRelleno)
    {
        Casilla mejor = null;
        for (var fila = 0; fila < Filas; fila++)
        {
            if (FilaSinInteres(fila))
            {
                continue;
            }
            var columna = PrimeraLibre(tablero, tipo, fila, 0);
            if (columna < 0 || columna >= ZombiMasCercano(tablero, fila))
            {
                continue;
            }
            var atacantes = ContarEnFila(tablero, fila, EsAtacante);
            var sinPodadora = !PodadoraPorFila[fila];
            float valor;
            string motivo;
            if (DeficitPorFila[fila] > 0f)
            {
                // Un zombi todavía lejano no justifica dejar de armar la economía.
                var cerca = ZombiMasCercano(tablero, fila) <= ColumnaDefensaCercana;
                valor = ValorFilaSinDefensa + DeficitPonderado(fila) * (cerca ? 1f : FraccionDeficitLejano) + DanoDe(tipo);
                motivo = "defender: faltan " + (int)DeficitPorFila[fila] + " de daño";
            }
            else if (ObjetivoPorFila[fila])
            {
                var danado = MaxVidaObjetivoPorFila[fila] > 0 ? 1f - VidaObjetivoPorFila[fila] / (float)MaxVidaObjetivoPorFila[fila] : 0f;
                valor = ValorOfensiva - atacantes * PenaPorAtacante + danado * BonoObjetivoDanado;
                motivo = "atacar el objetivo";
            }
            else
            {
                valor = valorRelleno - atacantes * PenaPorAtacante;
                motivo = "reforzar la fila";
            }
            if (sinPodadora)
            {
                valor += BonoFilaSinPodadora;
                motivo += ", fila sin podadora";
            }
            if (VersusBotMod.Nivel >= NivelEconomiaPrioritaria)
            {
                // En una línea de agua, poder plantar significa que hay un nenúfar vacío esperando.
                if (MapaVersus.EsFilaDeAgua(tablero, fila))
                {
                    valor += BonoNenufarVacio;
                    motivo += ", sobre el nenúfar";
                    // Un delfín o un buzo cruzan el agua antes de que llegue el resto: esa línea no puede esperar.
                    if (Maestro && atacantes == 0 && (CartasRival.Contains(MapaVersus.CartaDelfin) || CartasRival.Contains(MapaVersus.CartaBuzo)))
                    {
                        valor += BonoAguaAmenazada;
                    }
                }
                if (Maestro && DeficitPorFila[fila] <= 0f)
                {
                    valor += Presion(fila) * BonoPresionAtacante;
                }
                // Cubrir una línea vacía vale más que reforzar otra: así el agua no queda sin defensa.
                if (Maestro && atacantes == 0 && ObjetivoPorFila[fila])
                {
                    valor += BonoLineaVacia;
                }
                // Sin esto, entre líneas iguales siempre ganaba la de más arriba.
                valor += (float)AzarDeLineas.NextDouble() * VariacionEntreLineas;
            }
            if (valor > 0f && (mejor == null || valor > mejor.Valor))
            {
                mejor = new Casilla { Columna = columna, Fila = fila, Valor = valor, Motivo = motivo };
            }
        }
        return mejor;
    }

    // Un zombi que vuela pasa sobre toda la defensa: el cactus de su línea es lo primero.
    private static Casilla CactusContraVolador(Board tablero, SeedType tipo)
    {
        foreach (var zombi in Vivos)
        {
            if (!zombi.IsFlying() || ContarEnFila(tablero, zombi.mRow, t => t == SeedType.Cactus) > 0)
            {
                continue;
            }
            var columna = PrimeraLibre(tablero, tipo, zombi.mRow, 0);
            if (columna >= 0 && columna < ColumnaDe(tablero, zombi))
            {
                return new Casilla { Columna = columna, Fila = zombi.mRow, Valor = ValorContraVolador, Motivo = "cactus contra el globo" };
            }
        }
        return null;
    }

    // Si el cactus no se puede poner porque el globo va por el agua, primero hace falta un nenúfar.
    private static Casilla NenufarContraVolador(Board tablero, SeedType tipo)
    {
        if (!CartasPropias.Contains(SeedType.Cactus))
        {
            return null;
        }
        foreach (var zombi in Vivos)
        {
            if (zombi.IsFlying() && MapaVersus.EsFilaDeAgua(tablero, zombi.mRow) && ContarEnFila(tablero, zombi.mRow, t => t == SeedType.Cactus) == 0
                && PrimeraLibre(tablero, SeedType.Cactus, zombi.mRow, 0) < 0)
            {
                var columna = PrimeraLibre(tablero, tipo, zombi.mRow, 0);
                if (columna >= 0 && columna < ColumnaDe(tablero, zombi))
                {
                    return new Casilla { Columna = columna, Fila = zombi.mRow, Valor = ValorContraVolador, Motivo = "nenúfar para el cactus contra el globo" };
                }
            }
        }
        return null;
    }

    // Sin nenúfar no se puede plantar en el agua. Se pone uno a la vez: el siguiente espera a que
    // el anterior tenga una planta encima.
    private static Casilla PonerNenufar(Board tablero, SeedType tipo)
    {
        if (VersusBotMod.Nivel >= NivelEconomiaPrioritaria)
        {
            var contraGlobo = NenufarContraVolador(tablero, tipo);
            if (contraGlobo != null) return contraGlobo;
        }
        // En el nivel extremo solo se pone si el mazo tiene una planta para ponerle encima.
        if (Maestro && !_plantaParaNenufar)
        {
            return null;
        }
        var hayNenufarVacio = false;
        for (var fila = 0; fila < Filas; fila++)
        {
            hayNenufarVacio |= MapaVersus.EsFilaDeAgua(tablero, fila) && ContarEnFila(tablero, fila, t => t == SeedType.Lilypad) > 0;
        }
        if (hayNenufarVacio && !Maestro)
        {
            return null;
        }
        Casilla mejor = null;
        for (var fila = 0; fila < Filas; fila++)
        {
            if (!MapaVersus.EsFilaDeAgua(tablero, fila) || FilaSinInteres(fila))
            {
                continue;
            }
            // Un nenúfar vacío en otra línea no puede dejar sin defensa a la que ya tiene un zombi en el agua.
            if (hayNenufarVacio && (!HayZombiEnFila(fila) || ContarEnFila(tablero, fila, t => t == SeedType.Lilypad || EsAtacante(t)) > 0))
            {
                continue;
            }
            var columna = PrimeraLibre(tablero, tipo, fila, 0);
            // Un nenúfar pegado al zombi se lo comen antes de que reciba su planta.
            if (columna < 0 || columna >= ZombiMasCercano(tablero, fila) - (Maestro ? MargenNenufar : 0))
            {
                continue;
            }
            var atacantes = ContarEnFila(tablero, fila, EsAtacante);
            var valor = DeficitPorFila[fila] > 0f
                ? ValorFilaSinDefensa + DeficitPonderado(fila)
                : atacantes == 0 ? ValorOfensiva + (Maestro ? BonoNenufarVacio + Presion(fila) * BonoPresionAtacante : 0f) : ValorRelleno - atacantes * PenaPorAtacante;
            if (valor > 0f && (mejor == null || valor > mejor.Valor))
            {
                mejor = new Casilla { Columna = columna, Fila = fila, Valor = valor, Motivo = "nenúfar para plantar en el agua" };
            }
        }
        return mejor;
    }

    // Saltadores, voladores, excavadores y el zamboni no se detienen ante un muro.
    private static bool PrimeroSeFrenaConMuro(Board tablero, int fila)
    {
        Zombie primero = null;
        foreach (var zombi in Vivos)
        {
            if (zombi.mRow == fila && (primero == null || zombi.mPosX < primero.mPosX))
            {
                primero = zombi;
            }
        }
        return primero != null && !primero.IsFlying() && primero.mZombieType != ZombieType.Polevaulter && primero.mZombieType != ZombieType.Pogo
            && primero.mZombieType != ZombieType.Digger && primero.mZombieType != ZombieType.Zamboni;
    }

    // Un muro solo sirve frente a un zombi que se acerca a una fila que no da abasto.
    // En el nivel extremo, al costar poco, se pone también por adelantado.
    private static Casilla PonerMuro(Board tablero, SeedType tipo)
    {
        Casilla mejor = null;
        for (var fila = 0; fila < Filas; fila++)
        {
            var preventivo = Maestro && DeficitPorFila[fila] <= 0f;
            if (preventivo && HayLineaSinAtacante(tablero))
            {
                continue;
            }
            if ((DeficitPorFila[fila] <= 0f && !preventivo) || MuroPorFila[fila] || FrenteAtacante[fila] < 0 || (Maestro && !PrimeroSeFrenaConMuro(tablero, fila)))
            {
                continue;
            }
            var masCerca = ZombiMasCercano(tablero, fila);
            var columna = ColumnaFrente(tablero, fila) + 1;
            if (columna >= masCerca || masCerca - columna > (Maestro ? DistanciaMuroMaestro : DistanciaMuro) || columna >= Columnas || tablero.CanPlantAt(columna, fila, tipo) != PlantingReason.Ok)
            {
                continue;
            }
            var valor = preventivo ? ValorMuro * FraccionMuroPreventivo : ValorMuro + DeficitPonderado(fila) * 0.5f;
            if (mejor == null || valor > mejor.Valor)
            {
                mejor = new Casilla { Columna = columna, Fila = fila, Valor = valor, Motivo = "frenar al zombi" + DescribirFila(fila) };
            }
        }
        return mejor;
    }

    private static Casilla DespertarHongo(Board tablero, SeedType tipo)
    {
        Casilla mejor = null;
        for (var fila = 0; fila < Filas; fila++)
        {
            for (var columna = 0; columna < Columnas; columna++)
            {
                if (tablero.CanPlantAt(columna, fila, tipo) != PlantingReason.Ok)
                {
                    continue;
                }
                var planta = tablero.GetTopPlantAt(columna, fila, PlantPriority.Any);
                var esIman = planta != null && planta.mSeedType == SeedType.Magnetshroom;
                if (mejor == null || esIman)
                {
                    mejor = new Casilla { Columna = columna, Fila = fila, Valor = ValorCafe, Motivo = "despertar un hongo" };
                }
            }
        }
        return mejor;
    }

    private static int MetalEnFila(int fila)
    {
        var total = 0;
        foreach (var zombi in Vivos)
        {
            if (zombi.mRow == fila && zombi.mHelmHealth + zombi.mShieldHealth > 0)
            {
                total++;
            }
        }
        return total;
    }

    // El imán se pone contra el metal que ya está en el tablero, o por adelantado si el rival lleva zombis de metal.
    private static Casilla PonerIman(Board tablero, SeedType tipo)
    {
        if (tablero.CountPlantByType(tipo) >= MaximoImanes)
        {
            return null;
        }
        var rivalConMetal = false;
        foreach (var carta in CartasRival)
        {
            rivalConMetal |= Cartas.EsDeMetal(carta);
        }
        Casilla mejor = null;
        for (var fila = 0; fila < Filas; fila++)
        {
            var metal = MetalEnFila(fila);
            if (metal == 0 && !rivalConMetal)
            {
                continue;
            }
            var columna = PrimeraLibre(tablero, tipo, fila, ColumnaIman);
            if (columna < 0 || columna >= ZombiMasCercano(tablero, fila))
            {
                continue;
            }
            var valor = metal > 0 ? ValorIman + metal * PenaPorAtacante : ValorImanPrevisto;
            if (mejor == null || valor > mejor.Valor)
            {
                mejor = new Casilla { Columna = columna, Fila = fila, Valor = valor, Motivo = metal > 0 ? "quitarle el metal a los zombis" : "anticiparse a los zombis de metal" };
            }
        }
        return mejor;
    }

    private static Casilla PonerParaguas(Board tablero, SeedType tipo)
    {
        if (tablero.CountPlantByType(tipo) > 0 || (!CartasRival.Contains(SeedType.ZombieBungee) && !CartasRival.Contains(SeedType.ZombieCatapult))
            || tablero.CanPlantAt(ColumnaParaguas, FilaParaguas, tipo) != PlantingReason.Ok)
        {
            return null;
        }
        return new Casilla { Columna = ColumnaParaguas, Fila = FilaParaguas, Valor = ValorParaguas, Motivo = "cubrirse de bungees y catapultas" };
    }

    private static Casilla PonerDesconocida(Board tablero, SeedType tipo)
    {
        for (var fila = 0; fila < Filas; fila++)
        {
            var columna = PrimeraLibre(tablero, tipo, fila, 0);
            if (!FilaSinInteres(fila) && columna >= 0 && columna < ZombiMasCercano(tablero, fila))
            {
                return new Casilla { Columna = columna, Fila = fila, Valor = ValorDesconocida, Motivo = "usar la carta" };
            }
        }
        return null;
    }

    private static int ColumnaFrente(Board tablero, int fila)
    {
        for (var columna = Columnas - 1; columna >= 0; columna--)
        {
            if (tablero.GetTopPlantAt(columna, fila, PlantPriority.Any) != null)
            {
                return columna;
            }
        }
        return -1;
    }

    private static Casilla PonerAntorcha(Board tablero, SeedType tipo)
    {
        for (var fila = 0; fila < Filas; fila++)
        {
            var columna = FrenteAtacante[fila] + 1;
            if (FrenteAtacante[fila] >= 0 && ContarEnFila(tablero, fila, EsAtacante) >= MinimoPeashootersAntorcha && columna < Columnas
                && tablero.CanPlantAt(columna, fila, tipo) == PlantingReason.Ok)
            {
                return new Casilla { Columna = columna, Fila = fila, Valor = ValorAntorcha, Motivo = "potenciar los guisantes" };
            }
        }
        return null;
    }

    private static Casilla PonerCalabaza(Board tablero, SeedType tipo)
    {
        Casilla mejor = null;
        for (var fila = 0; fila < Filas; fila++)
        {
            if (DeficitPorFila[fila] > 0f && FrenteAtacante[fila] >= 0 && tablero.CanPlantAt(FrenteAtacante[fila], fila, tipo) == PlantingReason.Ok)
            {
                var valor = ValorMuro + DeficitPonderado(fila) * 0.5f;
                if (mejor == null || valor > mejor.Valor)
                {
                    mejor = new Casilla { Columna = FrenteAtacante[fila], Fila = fila, Valor = valor, Motivo = "proteger al atacante" };
                }
            }
        }
        return mejor;
    }

    private static Casilla Soplar(Board tablero, SeedType tipo)
    {
        foreach (var zombi in Vivos)
        {
            if (zombi.IsFlying() && ColumnaDe(tablero, zombi) <= ColumnaSoplador)
            {
                var columna = PrimeraLibre(tablero, tipo, zombi.mRow, 0);
                if (columna >= 0)
                {
                    return new Casilla { Columna = columna, Fila = zombi.mRow, Valor = VersusBotMod.Nivel >= NivelEconomiaPrioritaria ? ValorContraVolador : ValorSoplador, Motivo = "soplar al zombi volador" };
                }
            }
        }
        return null;
    }

    private static int ContarEnFila(Board tablero, int fila, Func<SeedType, bool> condicion)
    {
        var total = 0;
        for (var columna = 0; columna < Columnas; columna++)
        {
            var planta = tablero.GetTopPlantAt(columna, fila, PlantPriority.Any);
            if (planta != null && condicion(planta.mSeedType))
            {
                total++;
            }
        }
        return total;
    }

    private static int PrimeraLibre(Board tablero, SeedType tipo, int fila, int desde)
    {
        for (var columna = desde; columna < Columnas; columna++)
        {
            if (tablero.CanPlantAt(columna, fila, tipo) == PlantingReason.Ok)
            {
                return columna;
            }
        }
        return -1;
    }

    // Cuánto aporta cada zombi frente a una fila defendida. En una fila vacía conviene el barato.
    private static float PesoDeZombi(SeedType tipo)
    {
        switch (tipo)
        {
            case SeedType.ZombieTrafficCone:
            case SeedType.ZombieNewspaper:
                return 40f;
            case SeedType.ZombiePolevaulter:
            case SeedType.ZombieLadder:
            case SeedType.ZombieDigger:
            case SeedType.ZombieTrashCan:
            case SeedType.ZombieJackInTheBox:
            case SeedType.ZombieCatapult:
                return 60f;
            case MapaVersus.CartaBuzo:
            case SeedType.ZombieScreenDoor:
            case SeedType.ZombieBalloon:
            case SeedType.ZombiePogo:
            case SeedType.ZombiePail:
                return 80f;
            case MapaVersus.CartaDelfin:
            case SeedType.ZombieDancer:
                return 100f;
            case SeedType.ZombieFootball:
                return 120f;
            case SeedType.Zomboni:
                return 150f;
            case SeedType.ZombieGargantuar:
                return 200f;
            default:
                return 0f;
        }
    }

    private static bool SaltaMuros(SeedType tipo)
    {
        return tipo == SeedType.ZombiePolevaulter || tipo == SeedType.ZombiePogo || tipo == SeedType.ZombieLadder || tipo == MapaVersus.CartaDelfin;
    }

    private static float SaturacionDeFila(int fila)
    {
        var total = 0f;
        foreach (var zombi in Vivos)
        {
            if (zombi.mRow == fila)
            {
                total += PenaZombiEnFila + VidaDe(zombi) * PenaVidaEnFila;
            }
        }
        return total;
    }

    private static float BonoBloqueo(int fila)
    {
        if (!ObjetivoPorFila[fila] || FrenteAtacante[fila] < 0 || SaturacionDeFila(fila) > 0f)
        {
            return 0f;
        }
        var danado = MaxVidaObjetivoPorFila[fila] > 0 ? 1f - VidaObjetivoPorFila[fila] / (float)MaxVidaObjetivoPorFila[fila] : 0f;
        return BonoBloquearObjetivo + danado * BonoObjetivoEnRiesgo;
    }

    // Lado zombi: las tumbas van a las filas en pie con menos tumbas. Los ataques se reparten por varias
    // filas, prefiriendo las que se defienden peor o ya no tienen podadora, con zombis fuertes si hay defensa.
    // Una fila que ya perdió su objetivo deja de importar; solo se usa si no queda otra.
    private static Casilla CasillaDeZombi(Board tablero, SeedType tipo, int tumbasDeseadas)
    {
        if (tipo == SeedType.ZombieBungee)
        {
            return SoltarBungee(tablero, tipo);
        }
        var esTumba = tipo == SeedType.ZombieGravestone;
        if (esTumba && _tumbas >= tumbasDeseadas)
        {
            return null;
        }
        var maxTumbas = 0;
        foreach (var cantidad in TumbasPorFila)
        {
            maxTumbas = Math.Max(maxTumbas, cantidad);
        }
        Casilla mejor = null;
        for (var fila = 0; fila < Filas; fila++)
        {
            var caida = FilaCaida(fila);
            if (esTumba && caida)
            {
                continue;
            }
            var columna = -1;
            for (var c = Columnas - 1; c >= 0 && columna < 0; c--)
            {
                if (tablero.CanPlantAt(c, fila, tipo) == PlantingReason.Ok)
                {
                    columna = c;
                }
            }
            if (columna < 0)
            {
                continue;
            }
            var defensa = ContarEnFila(tablero, fila, EsAtacante) * 2 + ContarEnFila(tablero, fila, EsMuro);
            float valor;
            string motivo;
            if (esTumba)
            {
                valor = (_tumbas < EconomiaBase ? ValorTumbaBase : ValorEconomia) + (maxTumbas - TumbasPorFila[fila]) * BonoPocasTumbas;
                motivo = "reforzar la fila con menos tumbas";
            }
            else
            {
                var peso = PesoDeZombi(tipo);
                valor = ValorFilaSinDefensa - defensa * PenaDefensaZombi
                    - SaturacionDeFila(fila) + BonoBloqueo(fila)
                    + (defensa > 0 ? peso : -peso * FraccionPesoSinDefensa)
                    + (MuroPorFila[fila] && SaltaMuros(tipo) ? BonoSaltaMuros : 0f)
                    + (PodadoraPorFila[fila] ? 0f : BonoFilaSinPodadora)
                    - (caida ? PenaFilaCaida : 0f);
                // El globo vale por pasar sobre la defensa: conviene donde no hay un cactus esperándolo.
                if (tipo == SeedType.ZombieBalloon)
                {
                    valor += ContarEnFila(tablero, fila, t => t == SeedType.Cactus) > 0 ? -PenaGloboContraCactus : BonoSaltaMuros;
                }
                motivo = "fila en pie con " + TumbasPorFila[fila] + " tumbas y defensa " + defensa + DescribirFila(fila);
            }
            if (mejor == null || valor > mejor.Valor)
            {
                mejor = new Casilla { Columna = columna, Fila = fila, Valor = valor, Motivo = motivo };
            }
        }
        return mejor;
    }

    // El bungee se lleva la planta atacante más adelantada.
    private static Casilla SoltarBungee(Board tablero, SeedType tipo)
    {
        Casilla mejor = null;
        for (var fila = 0; fila < Filas; fila++)
        {
            var columna = FrenteAtacante[fila];
            if (columna >= 0 && tablero.CanPlantAt(columna, fila, tipo) == PlantingReason.Ok && (mejor == null || columna > mejor.Columna))
            {
                mejor = new Casilla { Columna = columna, Fila = fila, Valor = ValorBungee, Motivo = "robar al atacante más adelantado" };
            }
        }
        return mejor;
    }
}
