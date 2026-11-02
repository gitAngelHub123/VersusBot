// Versus Bot — decisiones del bot durante la partida.
// El bot juega con el mismo camino que un mando: activa una carta y la coloca en una casilla,
// así el juego le cobra el costo y le aplica la recarga como a cualquier jugador.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using System;
using System.Collections.Generic;
using Il2CppReloaded.Gameplay;

namespace VersusBot;

internal sealed class TipoDeMazo
{
    public readonly string Nombre;
    public readonly SeedType[] Nucleo;
    public readonly SeedType[] Apoyo;

    public TipoDeMazo(string nombre, SeedType[] nucleo, SeedType[] apoyo)
    {
        Nombre = nombre;
        Nucleo = nucleo;
        Apoyo = apoyo;
    }
}

// Hay un cerebro por jugador: en Bot vs Bot cada uno lleva sus propias cartas y sus tiempos.
internal sealed class Bot
{
    private const int Columnas = 9;
    private const int FilasDeDia = 5;
    private const int FilasDePiscina = 6;
    private static int Filas => MapaVersus.Piscina ? FilasDePiscina : FilasDeDia;
    private const int IntentosPorCarta = 30;
    // Cada curva da su valor en estos niveles; entre dos puntos se interpola.
    private static readonly int[] NivelesCurva = { 1, 25, 50, 75, 100, 150, 200 };
    private static readonly float[] CurvaEspera = { 14f, 7f, 3.5f, 2f, 1f, 0.5f, 0.25f };
    private static readonly float[] CurvaRecogida = { 5f, 3f, 1.6f, 0.9f, 0.5f, 0.25f, 0.1f };
    private static readonly float[] CurvaColocar = { 1f, 0.8f, 0.6f, 0.5f, 0.4f, 0.25f, 0.15f };
    private static readonly float[] CurvaInteligencia = { 0f, 0.15f, 0.55f, 0.85f, 1f, 1f, 1f };
    private static readonly float[] CurvaPerderTurno = { 0.5f, 0.3f, 0.1f, 0f, 0f, 0f, 0f };
    // Soles que las plantas guardan para emergencias, desde el nivel indicado.
    private const int NivelReserva1 = 100;
    private const int NivelReserva2 = 150;
    private const int NivelReserva3 = 200;
    private const int Reserva1 = 50;
    private const int Reserva2 = 100;
    private const int Reserva3 = 150;
    private const int MitadCasillaX = 40;
    private const int MitadCasillaY = 50;

    private const float EsperaEntreElecciones = 0.8f;
    private const float EsperaPrimeraEleccion = 3f;

    // Probabilidad de que una carta del mazo sea cualquiera, sin importar el tipo de mazo.
    private const double ProbabilidadCartaAlAzar = 0.05;
    private const int NucleoPlantas = 2;
    private const int NucleoZombis = 1;
    // Cartas que el jugador elige; la de economía ya viene puesta.
    private const int CartasDeMazo = 5;
    // El segundo ataque cuesta hasta este valor; desde el valor caro solo se usa contra zombis muy fuertes.
    private const int CostoAtaqueMedio = 175;
    private const int CostoAtaqueCaro = 200;
    private const int MaximoCartasCaras = 2;
    // Desde este nivel el bot mira el mazo del rival y recuerda el de la partida anterior.
    internal const int NivelAnticipa = 100;
    // El nivel extremo arma un mazo barato y fijo, sin cartas de este precio o más.
    internal const int NivelMaestro = 200;
    private const int CostoMedio = 150;
    private const int CostoQueNoRetrasa = 50;
    private const float EsperaAvisoSinCasilla = 5f;
    private float _siguienteAvisoSinCasilla;
    private const int PasoDeSoles = 25;
    private const int TopeDeSoles = 3000;

    private static readonly HashSet<SeedType> MazoRivalAnterior = new HashSet<SeedType>();

    // Cada partida el bot sortea un tipo de mazo: primero toma cartas del núcleo y después de apoyo.
    private static readonly TipoDeMazo[] MazosPlanta =
    {
        new TipoDeMazo("guisantes",
            new[] { SeedType.Peashooter, SeedType.Repeater, SeedType.Snowpea, SeedType.Threepeater, SeedType.Splitpea },
            new[] { SeedType.Wallnut, SeedType.Squash, SeedType.Cherrybomb, SeedType.Potatomine, SeedType.Torchwood, SeedType.Jalapeno }),
        new TipoDeMazo("catapultas",
            new[] { SeedType.Cabbagepult, SeedType.Kernelpult, SeedType.Melonpult },
            new[] { SeedType.Wallnut, SeedType.Tallnut, SeedType.Squash, SeedType.Cherrybomb, SeedType.Jalapeno }),
        new TipoDeMazo("explosivas",
            new[] { SeedType.Peashooter, SeedType.Snowpea, SeedType.Repeater },
            new[] { SeedType.Cherrybomb, SeedType.Jalapeno, SeedType.Squash, SeedType.Potatomine, SeedType.Chomper }),
        new TipoDeMazo("muralla",
            new[] { SeedType.Peashooter, SeedType.Repeater, SeedType.Threepeater, SeedType.Snowpea },
            new[] { SeedType.Wallnut, SeedType.Tallnut, SeedType.Pumpkinshell, SeedType.Spikeweed, SeedType.Squash }),
        new TipoDeMazo("estrellas",
            new[] { SeedType.Starfruit, SeedType.Cactus, SeedType.Splitpea },
            new[] { SeedType.Wallnut, SeedType.Pumpkinshell, SeedType.Squash, SeedType.Cherrybomb, SeedType.Potatomine })
    };

    private static readonly TipoDeMazo[] MazosZombi =
    {
        new TipoDeMazo("tanques",
            new[] { SeedType.ZombieNormal, SeedType.ZombieTrafficCone },
            new[] { SeedType.ZombiePail, SeedType.ZombieScreenDoor, SeedType.ZombieFootball, SeedType.ZombieTrashCan, SeedType.ZombieGargantuar }),
        new TipoDeMazo("rápidos",
            new[] { SeedType.ZombieNormal, SeedType.ZombieNewspaper },
            new[] { SeedType.ZombiePolevaulter, SeedType.ZombieFootball, SeedType.ZombiePogo, SeedType.ZombieJackInTheBox, SeedType.ZombieDigger }),
        new TipoDeMazo("saltadores",
            new[] { SeedType.ZombieTrafficCone, SeedType.ZombieNormal },
            new[] { SeedType.ZombiePolevaulter, SeedType.ZombiePogo, SeedType.ZombieLadder, SeedType.ZombieBalloon, SeedType.ZombieDigger }),
        new TipoDeMazo("sorpresa",
            new[] { SeedType.ZombieNormal, SeedType.ZombieNewspaper },
            new[] { SeedType.ZombieDigger, SeedType.ZombieBalloon, SeedType.ZombieBungee, SeedType.ZombieCatapult, SeedType.ZombieDancer })
    };

    private static readonly Random Azar = new Random();
    private static readonly Bot[] Cerebros = { new Bot(0), new Bot(1) };

    private readonly int _indice;
    private float _siguienteJugada;
    private IntPtr _tableroActual;
    private SeedPacket _cartaPreparada;
    private float _horaDeColocar;
    private float _siguienteEleccion;
    private bool _jugadaPensada;
    private TipoDeMazo _mazo;

    private Bot(int indice)
    {
        _indice = indice;
    }

    internal static void ActualizarTodos(Board tablero)
    {
        foreach (var cerebro in Cerebros)
        {
            if (VersusBotMod.JugadorEsBot(cerebro._indice))
            {
                cerebro.Actualizar(tablero);
            }
        }
    }

    internal static void ElegirCartasIniciales(VersusMode versus)
    {
        foreach (var cerebro in Cerebros)
        {
            if (VersusBotMod.JugadorEsBot(cerebro._indice))
            {
                cerebro.ElegirCartaInicial(versus);
            }
        }
    }

    // La pantalla de selección tarda en abrirse: si el bot elige de inmediato, no alcanza a cambiar de bando.
    internal static void ReiniciarElecciones()
    {
        foreach (var cerebro in Cerebros)
        {
            cerebro._siguienteEleccion = UnityEngine.Time.unscaledTime + EsperaPrimeraEleccion;
            cerebro._mazo = null;
        }
    }

    private static float SegunNivel(float[] curva)
    {
        var nivel = VersusBotMod.Nivel;
        for (var i = 1; i < NivelesCurva.Length; i++)
        {
            if (nivel <= NivelesCurva[i])
            {
                return UnityEngine.Mathf.Lerp(curva[i - 1], curva[i], (nivel - NivelesCurva[i - 1]) / (float)(NivelesCurva[i] - NivelesCurva[i - 1]));
            }
        }
        return curva[curva.Length - 1];
    }

    internal static float EsperaRecogida()
    {
        return SegunNivel(CurvaRecogida);
    }

    private static int EconomiaDeseada(bool esZombi)
    {
        var nivel = VersusBotMod.Nivel;
        if (nivel <= 15) return esZombi ? 2 : 1;
        if (nivel <= 40) return esZombi ? 3 : 2;
        if (nivel <= 50) return esZombi ? 4 : 3;
        if (nivel <= 65) return 4;
        if (nivel <= 100) return 5;
        if (nivel <= 150) return 6;
        if (nivel >= NivelMaestro) return 5;
        return 7;
    }

    // En la muerte súbita no se guarda nada: hay que gastarlo todo.
    private static int ReservaDeSoles(VersusPhase fase)
    {
        if (fase == VersusPhase.SuddenDeath) return 0;
        var nivel = VersusBotMod.Nivel;
        if (nivel >= NivelReserva3) return Reserva3;
        if (nivel >= NivelReserva2) return Reserva2;
        return nivel >= NivelReserva1 ? Reserva1 : 0;
    }

    // Selección de cartas: los jugadores eligen por turnos y el juego cambia de turno con cada carta.
    private void ElegirCartaInicial(VersusMode versus)
    {
        var esZombi = versus.ZombiePlayerIndex == _indice;
        var turnoDelBot = versus.Phase == (esZombi ? VersusPhase.ChooseZombiePacket : VersusPhase.ChoosePlantPacket);
        if (!turnoDelBot)
        {
            _siguienteEleccion = UnityEngine.Time.unscaledTime + EsperaEntreElecciones;
            return;
        }
        if (UnityEngine.Time.unscaledTime < _siguienteEleccion)
        {
            return;
        }
        _siguienteEleccion = UnityEngine.Time.unscaledTime + EsperaEntreElecciones;

        var pantalla = versus.m_app == null ? null : versus.m_app.SeedChooserScreen;
        if (pantalla == null)
        {
            return;
        }
        var lista = esZombi ? pantalla.mChosenZombies : pantalla.mChosenSeeds;
        if (_mazo == null)
        {
            var mazos = esZombi ? MazosZombi : MazosPlanta;
            _mazo = mazos[Azar.Next(mazos.Length)];
            VersusBotMod.Log.Msg("Bot J" + (_indice + 1) + " arma un mazo de tipo: " + _mazo.Nombre);
        }
        var carta = ElegirCartaDeMazo(pantalla, lista, esZombi);
        if (carta != null)
        {
            pantalla.ClickedSeedInChooser(carta, _indice);
            VersusBotMod.Log.Msg("Bot J" + (_indice + 1) + " eligió: " + carta.mSeedType);
        }
    }

    private static bool Disponible(SeedChooserScreen pantalla, ChosenSeed carta)
    {
        return carta != null && carta.mSeedState == ChosenSeedState.SeedInChooser
            && carta.mSeedType != SeedType.Imitater && (MapaVersus.Piscina || carta.mSeedType != SeedType.ZombieBalloon) && !pantalla.SeedNotAllowedToPick(carta.mSeedType);
    }

    // La carta de economía va siempre. Las plantas llevan el ataque más barato y otro de precio bajo o medio;
    // uno caro solo si el rival lleva zombis muy fuertes. Toda carta
    // que dependa de otra entra solo si queda espacio para las dos.
    private ChosenSeed ElegirCartaDeMazo(SeedChooserScreen pantalla, Il2CppSystem.Collections.Generic.List<ChosenSeed> lista, bool esZombi)
    {
        var economia = esZombi ? SeedType.ZombieGravestone : SeedType.Sunflower;
        var propias = new HashSet<SeedType>();
        for (var i = 0; i < lista.Count; i++)
        {
            var carta = lista[i];
            if (carta == null)
            {
                continue;
            }
            if (carta.mSeedType == economia && Disponible(pantalla, carta))
            {
                return carta;
            }
            if (carta.mSeedState != ChosenSeedState.SeedInChooser && carta.mSeedType != economia && carta.mSeedType != SeedType.Lilypad)
            {
                propias.Add(carta.mSeedType);
            }
        }
        // En la piscina el nenúfar es obligatorio y ocupa la carta extra: el resto del mazo no cambia.
        if (!esZombi && MapaVersus.Piscina)
        {
            for (var i = 0; i < lista.Count; i++)
            {
                var carta = lista[i];
                if (carta != null && carta.mSeedType == SeedType.Lilypad && Disponible(pantalla, carta))
                {
                    return carta;
                }
            }
        }
        var libres = CartasDeMazo - propias.Count;
        var elegidasRival = CartasElegidas(esZombi ? pantalla.mChosenSeeds : pantalla.mChosenZombies);
        var rival = CartasConocidasDelRival(elegidasRival);

        if (!esZombi)
        {
            foreach (var tipo in propias)
            {
                var requisito = Cartas.Requisito(tipo);
                if (requisito != null && requisito != economia && !propias.Contains(requisito.Value))
                {
                    var requerida = CartaAlAzar(pantalla, lista, t => t == requisito.Value);
                    if (requerida != null)
                    {
                        return requerida;
                    }
                }
            }
        }

        var caras = 0;
        if (!esZombi)
        {
            foreach (var tipo in propias)
            {
                caras += Costo(pantalla, tipo) >= CostoAtaqueCaro ? 1 : 0;
            }
        }
        Func<SeedType, bool> cabe = tipo =>
        {
            if (esZombi)
            {
                return true;
            }
            var requisito = Cartas.Requisito(tipo);
            var faltaRequisito = requisito != null && requisito != economia && !propias.Contains(requisito.Value);
            return (!faltaRequisito || libres >= 2) && (caras < MaximoCartasCaras || Costo(pantalla, tipo) < CostoAtaqueCaro);
        };

        if (VersusBotMod.Nivel < NivelAnticipa && Azar.NextDouble() < ProbabilidadCartaAlAzar)
        {
            var alAzar = CartaAlAzar(pantalla, lista, t => cabe(t) && !Cartas.NoSirve(t));
            if (alAzar != null)
            {
                VersusBotMod.Log.Msg("Bot J" + (_indice + 1) + ": carta al azar");
                return alAzar;
            }
        }

        if (!esZombi && VersusBotMod.Nivel >= NivelMaestro)
        {
            return ElegirPlantaMaestra(pantalla, lista, propias, rival);
        }

        return esZombi
            ? ElegirZombi(pantalla, lista, propias, rival)
            : ElegirPlanta(pantalla, lista, propias, rival, cabe, Cartas.HayZombiMuyFuerte(elegidasRival) || Cartas.HayZombiMuyFuerte(rival));
    }

    private ChosenSeed ElegirZombi(SeedChooserScreen pantalla, Il2CppSystem.Collections.Generic.List<ChosenSeed> lista, HashSet<SeedType> propias, HashSet<SeedType> rival)
    {
        var nucleoElegidas = 0;
        foreach (var tipo in propias)
        {
            nucleoElegidas += Array.IndexOf(_mazo.Nucleo, tipo) >= 0 ? 1 : 0;
        }
        var faltaNucleo = nucleoElegidas < NucleoZombis;
        Func<SeedType, bool> util = t => !Cartas.EstaContrarrestado(t, rival) && !Cartas.NoSirve(t);
        if (MapaVersus.Piscina && !faltaNucleo)
        {
            var dePiscina = 0;
            foreach (var tipo in propias)
            {
                dePiscina += Array.IndexOf(ZombisDePiscina, tipo) >= 0 ? 1 : 0;
            }
            if (dePiscina < (VersusBotMod.Nivel >= NivelAnticipa ? ZombisDePiscinaNivelAlto : ZombisDePiscinaBase))
            {
                var nueva = CartaAlAzar(pantalla, lista, t => util(t) && Array.IndexOf(ZombisDePiscina, t) >= 0);
                if (nueva != null)
                {
                    return nueva;
                }
            }
        }
        var primero = faltaNucleo ? _mazo.Nucleo : _mazo.Apoyo;
        var segundo = faltaNucleo ? _mazo.Apoyo : _mazo.Nucleo;
        return CartaAlAzar(pantalla, lista, t => util(t) && Array.IndexOf(primero, t) >= 0)
            ?? CartaAlAzar(pantalla, lista, t => util(t) && Array.IndexOf(segundo, t) >= 0)
            ?? CartaAlAzar(pantalla, lista, util)
            ?? CartaAlAzar(pantalla, lista, t => true);
    }

    // Zombis nuevos que el bot lleva en la piscina: desde el nivel alto, dos en vez de uno.
    private static readonly SeedType[] ZombisDePiscina = { SeedType.ZombieBalloon, MapaVersus.CartaDelfin, MapaVersus.CartaBuzo };
    private const int ZombisDePiscinaBase = 1;
    private const int ZombisDePiscinaNivelAlto = 2;

    // Las catapultas son los únicos atacantes baratos cuyo disparo alcanza a un buzo sumergido.
    private static readonly SeedType[] AtaquesQueAlcanzanAlBuzo = { SeedType.Cabbagepult, SeedType.Kernelpult };

    private static readonly SeedType[][] CartasClave =
    {
        new[] { SeedType.Potatomine },
        new[] { SeedType.Cherrybomb, SeedType.Jalapeno },
        new[] { SeedType.Wallnut },
    };

    // Atacante barato, papa, calabaza aplastadora, nuez, y el último espacio se decide tras ver casi todo el mazo del rival.
    // En la piscina la catapulta va primero y el alga reemplaza a la nuez: lo único barato que alcanza a un buzo nadando.
    // Sin explosivas: ahorrar para ellas dejaba al bot sin soles para los atacantes y casi nunca llegaba a usarlas.
    private ChosenSeed ElegirPlantaMaestra(SeedChooserScreen pantalla, Il2CppSystem.Collections.Generic.List<ChosenSeed> lista, HashSet<SeedType> propias, HashSet<SeedType> rival)
    {
        var ataques = 0;
        foreach (var tipo in propias)
        {
            ataques += Cartas.EsAtaqueIndependiente(tipo) ? 1 : 0;
        }
        Func<SeedType, bool> barata = t => Costo(pantalla, t) < CostoMedio && t != SeedType.Jalapeno;
        var respuestas = Cartas.RespuestasMaestras(rival, propias);
        if (ataques == 0)
        {
            var bombeado = MapaVersus.Piscina ? AtaqueMasBarato(pantalla, lista, AtaquesQueAlcanzanAlBuzo) : null;
            if (bombeado != null)
            {
                return bombeado;
            }
            foreach (var respuesta in respuestas)
            {
                var doble = CartaAlAzar(pantalla, lista, t => barata(t) && Cartas.EsAtaqueIndependiente(t) && Array.IndexOf(respuesta, t) >= 0);
                if (doble != null)
                {
                    VersusBotMod.Log.Msg("Bot J" + (_indice + 1) + " se anticipa al mazo del rival: " + doble.mSeedType);
                    return doble;
                }
            }
            var primero = AtaqueMasBarato(pantalla, lista, null);
            if (primero != null)
            {
                return primero;
            }
        }
        if (!propias.Contains(SeedType.Potatomine))
        {
            var papa = CartaAlAzar(pantalla, lista, t => t == SeedType.Potatomine);
            if (papa != null)
            {
                return papa;
            }
        }
        if (!propias.Contains(SeedType.Squash))
        {
            var calabaza = CartaAlAzar(pantalla, lista, t => t == SeedType.Squash);
            if (calabaza != null)
            {
                return calabaza;
            }
        }
        if (MapaVersus.Piscina)
        {
            var alga = propias.Contains(SeedType.Tanglekelp) ? null : CartaAlAzar(pantalla, lista, t => t == SeedType.Tanglekelp);
            if (alga != null)
            {
                return alga;
            }
        }
        else if (!propias.Contains(SeedType.Wallnut))
        {
            var nuez = CartaAlAzar(pantalla, lista, t => t == SeedType.Wallnut);
            if (nuez != null)
            {
                return nuez;
            }
        }
        foreach (var respuesta in respuestas)
        {
            var contra = CartaAlAzar(pantalla, lista, t => barata(t) && Array.IndexOf(respuesta, t) >= 0);
            if (contra != null)
            {
                VersusBotMod.Log.Msg("Bot J" + (_indice + 1) + " se anticipa al mazo del rival: " + contra.mSeedType);
                return contra;
            }
        }
        return CartaAlAzar(pantalla, lista, t => barata(t) && Cartas.EsAtaqueIndependiente(t))
            ?? CartaAlAzar(pantalla, lista, t => barata(t) && !Cartas.NoSirve(t) && Cartas.Requisito(t) == null)
            ?? CartaAlAzar(pantalla, lista, t => true);
    }

    private ChosenSeed ElegirPlanta(SeedChooserScreen pantalla, Il2CppSystem.Collections.Generic.List<ChosenSeed> lista, HashSet<SeedType> propias, HashSet<SeedType> rival, Func<SeedType, bool> cabe, bool rivalMuyFuerte)
    {
        var ataquesAccesibles = 0;
        var tieneCaro = false;
        foreach (var tipo in propias)
        {
            if (Cartas.EsAtaqueIndependiente(tipo))
            {
                var esCaro = Costo(pantalla, tipo) >= CostoAtaqueCaro;
                tieneCaro |= esCaro;
                ataquesAccesibles += esCaro ? 0 : 1;
            }
        }
        // En nivel alto, tras el primer ataque: respuestas al rival y luego papa, explosiva y nuez.
        if (VersusBotMod.Nivel >= NivelAnticipa && ataquesAccesibles >= 1)
        {
            foreach (var respuesta in Cartas.RespuestasPendientes(rival, propias))
            {
                var contra = CartaAlAzar(pantalla, lista, t => cabe(t) && Array.IndexOf(respuesta, t) >= 0);
                if (contra != null)
                {
                    VersusBotMod.Log.Msg("Bot J" + (_indice + 1) + " se anticipa al mazo del rival: " + contra.mSeedType);
                    return contra;
                }
            }
            foreach (var grupo in CartasClave)
            {
                var yaLaLleva = false;
                foreach (var tipo in grupo)
                {
                    yaLaLleva |= propias.Contains(tipo);
                }
                var clave = yaLaLleva ? null : CartaAlAzar(pantalla, lista, t => cabe(t) && Array.IndexOf(grupo, t) >= 0);
                if (clave != null)
                {
                    return clave;
                }
            }
        }
        if (ataquesAccesibles == 0)
        {
            var carta = AtaqueMasBarato(pantalla, lista, _mazo.Nucleo) ?? AtaqueMasBarato(pantalla, lista, null);
            if (carta != null)
            {
                return carta;
            }
        }
        else if (ataquesAccesibles == 1)
        {
            Func<SeedType, bool> ataque = t => Cartas.EsAtaqueIndependiente(t) && Costo(pantalla, t) <= CostoAtaqueMedio;
            var carta = CartaAlAzar(pantalla, lista, t => ataque(t) && Array.IndexOf(_mazo.Nucleo, t) >= 0)
                ?? CartaAlAzar(pantalla, lista, ataque);
            if (carta != null)
            {
                return carta;
            }
        }
        else if (rivalMuyFuerte && !tieneCaro)
        {
            Func<SeedType, bool> ataque = t => Cartas.EsAtaqueIndependiente(t) && Costo(pantalla, t) >= CostoAtaqueCaro;
            var carta = CartaAlAzar(pantalla, lista, t => ataque(t) && Array.IndexOf(_mazo.Nucleo, t) >= 0)
                ?? CartaAlAzar(pantalla, lista, ataque);
            if (carta != null)
            {
                VersusBotMod.Log.Msg("Bot J" + (_indice + 1) + " lleva ataque caro contra zombis muy fuertes: " + carta.mSeedType);
                return carta;
            }
        }

        foreach (var respuesta in Cartas.RespuestasPendientes(rival, propias))
        {
            var carta = CartaAlAzar(pantalla, lista, t => cabe(t) && Array.IndexOf(respuesta, t) >= 0);
            if (carta != null)
            {
                VersusBotMod.Log.Msg("Bot J" + (_indice + 1) + " se anticipa al mazo del rival: " + carta.mSeedType);
                return carta;
            }
        }

        Func<SeedType, bool> util = t => cabe(t) && !Cartas.NoSirve(t)
            && !(Cartas.EsAtaqueIndependiente(t) && Costo(pantalla, t) >= CostoAtaqueCaro);
        return CartaAlAzar(pantalla, lista, t => util(t) && Array.IndexOf(_mazo.Apoyo, t) >= 0)
            ?? CartaAlAzar(pantalla, lista, t => util(t) && Array.IndexOf(_mazo.Nucleo, t) >= 0)
            ?? CartaAlAzar(pantalla, lista, util)
            ?? CartaAlAzar(pantalla, lista, t => true);
    }

    // El ataque de menor precio; si hay empate, uno de ellos al azar. Con filtro, solo entre esas cartas.
    private static ChosenSeed AtaqueMasBarato(SeedChooserScreen pantalla, Il2CppSystem.Collections.Generic.List<ChosenSeed> lista, SeedType[] filtro)
    {
        var menor = int.MaxValue;
        for (var i = 0; i < lista.Count; i++)
        {
            var carta = lista[i];
            if (Disponible(pantalla, carta) && Cartas.EsAtaqueIndependiente(carta.mSeedType) && (filtro == null || Array.IndexOf(filtro, carta.mSeedType) >= 0))
            {
                menor = Math.Min(menor, Costo(pantalla, carta.mSeedType));
            }
        }
        return CartaAlAzar(pantalla, lista, t => Cartas.EsAtaqueIndependiente(t) && (filtro == null || Array.IndexOf(filtro, t) >= 0) && Costo(pantalla, t) == menor);
    }

    private static int Costo(SeedChooserScreen pantalla, SeedType tipo)
    {
        return Plant.GetCost(pantalla.mApp, tipo, SeedType.None);
    }

    private static HashSet<SeedType> CartasElegidas(Il2CppSystem.Collections.Generic.List<ChosenSeed> lista)
    {
        var elegidas = new HashSet<SeedType>();
        for (var i = 0; i < lista.Count; i++)
        {
            var carta = lista[i];
            if (carta != null && carta.mSeedState != ChosenSeedState.SeedInChooser)
            {
                elegidas.Add(carta.mSeedType);
            }
        }
        return elegidas;
    }

    // Lo que el rival ya eligió en esta partida, más lo que llevó en la anterior. Solo en niveles altos.
    private static HashSet<SeedType> CartasConocidasDelRival(HashSet<SeedType> elegidasRival)
    {
        var conocidas = new HashSet<SeedType>();
        if (VersusBotMod.Nivel >= NivelAnticipa)
        {
            conocidas.UnionWith(MazoRivalAnterior);
            conocidas.UnionWith(elegidasRival);
        }
        return conocidas;
    }

    // Elige con igual probabilidad entre las cartas disponibles que cumplan la condición.
    private static ChosenSeed CartaAlAzar(SeedChooserScreen pantalla, Il2CppSystem.Collections.Generic.List<ChosenSeed> lista, Func<SeedType, bool> condicion)
    {
        ChosenSeed elegida = null;
        var disponibles = 0;
        for (var i = 0; i < lista.Count; i++)
        {
            var carta = lista[i];
            if (Disponible(pantalla, carta) && condicion(carta.mSeedType) && Azar.Next(++disponibles) == 0)
            {
                elegida = carta;
            }
        }
        return elegida;
    }

    private void Actualizar(Board tablero)
    {
        var partida = tablero.mApp;
        if (partida == null || !partida.IsVersusMode() || tablero.mPaused)
        {
            return;
        }
        var versus = partida.VersusMode;
        if (versus == null || (versus.Phase != VersusPhase.Gameplay && versus.Phase != VersusPhase.SuddenDeath))
        {
            return;
        }
        if (tablero.Pointer != _tableroActual)
        {
            _tableroActual = tablero.Pointer;
            _cartaPreparada = null;
        }
        var esZombi = versus.ZombiePlayerIndex == _indice;
        if (_cartaPreparada != null)
        {
            if (UnityEngine.Time.time >= _horaDeColocar)
            {
                var preparada = _cartaPreparada;
                _cartaPreparada = null;
                if (preparada.CanPickUp() && tablero.CanTakeSunMoney(preparada.GetCost(), _indice))
                {
                    Colocar(tablero, preparada, esZombi);
                }
            }
            return;
        }
        if (UnityEngine.Time.time < _siguienteJugada)
        {
            return;
        }
        _siguienteJugada = UnityEngine.Time.time + SegunNivel(CurvaEspera);

        // En niveles bajos el bot a veces deja pasar su turno.
        if (Azar.NextDouble() < SegunNivel(CurvaPerderTurno))
        {
            return;
        }

        var banco = tablero.SeedBanks[_indice];
        if (banco == null)
        {
            return;
        }
        Estrategia.LeerTablero(tablero);
        _jugadaPensada = Azar.NextDouble() < SegunNivel(CurvaInteligencia);
        var carta = _jugadaPensada ? ElegirCartaPensada(tablero, banco, esZombi, versus.Phase) : ElegirCarta(tablero, banco, esZombi);
        if (carta != null)
        {
            // El control del banco desmarca la carta anterior y marca esta: el resaltado avanza como con un mando.
            tablero.m_seedBankControls[_indice]?._setSelected(carta.mIndex);
            _cartaPreparada = carta;
            _horaDeColocar = UnityEngine.Time.time + SegunNivel(CurvaColocar);
        }
    }

    // Puntúa todas las cartas disponibles y usa la de mayor valor. Si ninguna conviene, espera.
    private SeedPacket ElegirCartaPensada(Board tablero, SeedBank banco, bool esZombi, VersusPhase fase)
    {
        // Guardar soles solo tiene sentido con la economía ya armada.
        var reserva = esZombi || tablero.CountSunFlowers() < EconomiaDeseada(false) ? 0 : ReservaDeSoles(fase);
        var bancoRival = tablero.SeedBanks[1 - _indice];
        Estrategia.LeerCartas(tablero, banco, bancoRival, _indice);
        Estrategia.RecordarCartasRival(MazoRivalAnterior);
        var hayAlternativa = esZombi && Estrategia.HayZombiSinContrarrestar(banco);
        var ahorrarAtaqueCaro = !esZombi && !Estrategia.HayZombiMuyFuerte() && TieneAtaqueAccesible(banco);
        SeedPacket elegida = null;
        var mejorValor = float.MinValue;
        var mejorSinPagar = float.MinValue;
        for (var i = 0; i < banco.NumPackets; i++)
        {
            var carta = banco.SeedPackets[i];
            if (carta == null || !carta.CanPickUp())
            {
                continue;
            }
            if (!tablero.CanTakeSunMoney(carta.GetCost(), _indice))
            {
                // Lo mejor que aún no se puede pagar: en nivel alto se ahorra para ello.
                if (!esZombi && VersusBotMod.Nivel >= NivelAnticipa)
                {
                    var soñada = Estrategia.MejorCasilla(tablero, carta.mPacketType, esZombi, EconomiaDeseada(esZombi));
                    if (soñada != null && soñada.Valor > mejorSinPagar)
                    {
                        mejorSinPagar = soñada.Valor;
                    }
                }
                continue;
            }
            if (ahorrarAtaqueCaro && Cartas.EsAtaqueIndependiente(carta.mPacketType) && carta.GetCost() >= CostoAtaqueCaro)
            {
                continue;
            }
            // Con otra opción disponible, el bot no gasta un zombi que el rival puede anular.
            if (hayAlternativa && Estrategia.Contrarrestado(carta.mPacketType))
            {
                continue;
            }
            var casilla = Estrategia.MejorCasilla(tablero, carta.mPacketType, esZombi, EconomiaDeseada(esZombi));
            // Un valor negativo es una fila caída: se usa solo si no hay nada mejor que hacer con la carta.
            // Las jugadas sin urgencia no pueden tocar la reserva para emergencias.
            if (casilla != null && casilla.Valor < Estrategia.ValorSinReserva && !tablero.CanTakeSunMoney(carta.GetCost() + reserva, _indice))
            {
                continue;
            }
            if (casilla != null && (elegida == null || casilla.Valor > mejorValor))
            {
                mejorValor = casilla.Valor;
                elegida = carta;
            }
        }
        // Una emergencia que sí se puede pagar nunca espera a una jugada más cara.
        var esperar = mejorSinPagar > mejorValor && mejorValor < Estrategia.ValorEmergencia;
        // Una carta muy barata casi no retrasa lo que se está ahorrando, y evita perder el turno.
        // Salvo si se ahorra para un girasol: las cartas baratas se comían esos 50 soles y el bot se quedaba en 2 girasoles.
        if (esperar && VersusBotMod.Nivel >= NivelMaestro && elegida != null && elegida.GetCost() <= CostoQueNoRetrasa && mejorSinPagar < Estrategia.ValorEconomiaPrioritaria)
        {
            esperar = false;
        }
        return esperar ? null : elegida;
    }

    private static bool TieneAtaqueAccesible(SeedBank banco)
    {
        for (var i = 0; i < banco.NumPackets; i++)
        {
            var carta = banco.SeedPackets[i];
            if (carta != null && Cartas.EsAtaqueIndependiente(carta.mPacketType) && carta.GetCost() < CostoAtaqueCaro)
            {
                return true;
            }
        }
        return false;
    }

    // Primero la economía; después cualquier otra carta disponible, al azar.
    private SeedPacket ElegirCarta(Board tablero, SeedBank banco, bool esZombi)
    {
        var economia = esZombi ? SeedType.ZombieGravestone : SeedType.Sunflower;
        var faltaEconomia = esZombi ? Estrategia.Tumbas < EconomiaDeseada(true) : tablero.CountSunFlowers() < EconomiaDeseada(false);
        SeedPacket elegida = null;
        var disponibles = 0;
        for (var i = 0; i < banco.NumPackets; i++)
        {
            var carta = banco.SeedPackets[i];
            if (carta == null || !carta.CanPickUp() || !tablero.CanTakeSunMoney(carta.GetCost(), _indice))
            {
                continue;
            }
            if (carta.mPacketType == economia)
            {
                if (faltaEconomia)
                {
                    return carta;
                }
                continue;
            }
            disponibles++;
            if (Azar.Next(disponibles) == 0)
            {
                elegida = carta;
            }
        }
        return elegida;
    }

    private void Colocar(Board tablero, SeedPacket carta, bool esZombi)
    {
        var tipo = carta.mPacketType;
        if (_jugadaPensada)
        {
            // Se vuelve a mirar el tablero: en medio segundo los zombis ya avanzaron.
            Estrategia.LeerTablero(tablero);
            // Las cartas también: en Bot vs Bot el otro bot pudo dejar leídas las suyas mientras tanto.
            Estrategia.LeerCartas(tablero, tablero.SeedBanks[_indice], tablero.SeedBanks[1 - _indice], _indice);
            var casilla = Estrategia.MejorCasilla(tablero, tipo, esZombi, EconomiaDeseada(esZombi));
            if (casilla != null)
            {
                ColocarEn(tablero, carta, casilla.Columna, casilla.Fila, casilla.Motivo);
            }
            else if (UnityEngine.Time.time >= _siguienteAvisoSinCasilla)
            {
                _siguienteAvisoSinCasilla = UnityEngine.Time.time + EsperaAvisoSinCasilla;
                VersusBotMod.Log.Msg("Bot J" + (_indice + 1) + ": eligió " + tipo + " pero ya no tiene casilla");
            }
            return;
        }
        Estrategia.LeerTablero(tablero);
        for (var intento = 0; intento < IntentosPorCarta; intento++)
        {
            var fila = ElegirFilaAlAzar(tipo, esZombi, intento);
            if (fila < 0)
            {
                continue;
            }
            var columna = ElegirColumna(tipo, esZombi);
            if (tablero.CanPlantAt(columna, fila, tipo) != PlantingReason.Ok)
            {
                continue;
            }

            ColocarEn(tablero, carta, columna, fila, "al azar");
            return;
        }
        VersusBotMod.Log.Msg("Bot J" + (_indice + 1) + ": no encontró casilla para " + tipo);
    }

    // En la piscina primero van las líneas con zombis y después las que aún tienen objetivo.
    // El girasol da soles en cualquier línea. Una línea caída y vacía no recibe nada.
    private static int ElegirFilaAlAzar(SeedType tipo, bool esZombi, int intento)
    {
        if (!MapaVersus.Piscina || tipo == SeedType.Sunflower)
        {
            return Azar.Next(Filas);
        }
        var filas = Estrategia.FilasParaAzar(esZombi, intento < IntentosPorCarta / 2);
        return filas.Count > 0 ? filas[Azar.Next(filas.Count)] : -1;
    }

    private void ColocarEn(Board tablero, SeedPacket carta, int columna, int fila, string motivo)
    {
        var tipo = carta.mPacketType;
        tablero.ClearCursor(true, _indice);
        carta.Activated(_indice, true);
        var x = tablero.GridToPixelX(columna, fila) + MitadCasillaX;
        var y = tablero.GridToPixelY(columna, fila) + MitadCasillaY;
        tablero.MouseDownWithPlant(x, y, 1, _indice);
        tablero.ClearCursor(true, _indice);
        VersusBotMod.Log.Msg("Bot J" + (_indice + 1) + ": " + tipo + " en columna " + columna + ", fila " + fila + " (" + motivo + ", recargando=" + carta.mRefreshing + ", recarga=" + carta.mRefreshTime + ", soles=" + SolesDe(tablero) + ")");
    }

    // Para el registro: lo que le queda, de 25 en 25, preguntándole al juego si le alcanza.
    private int SolesDe(Board tablero)
    {
        var soles = 0;
        while (soles < TopeDeSoles && tablero.CanTakeSunMoney(soles + PasoDeSoles, _indice))
        {
            soles += PasoDeSoles;
        }
        return soles;
    }

    // Plantas: girasoles atrás y el resto repartido. Zombis: desde el borde derecho.
    private static int ElegirColumna(SeedType tipo, bool esZombi)
    {
        if (esZombi)
        {
            return Columnas - 1 - Azar.Next(Columnas / 2);
        }
        return tipo == SeedType.Sunflower ? Azar.Next(2) : Azar.Next(Columnas - 1);
    }
}
