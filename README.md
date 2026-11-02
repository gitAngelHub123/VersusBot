# Versus Bot

A [MelonLoader](https://melonwiki.xyz) mod for **Plants vs. Zombies: Replanted** that adds a bot opponent to Versus mode, plus Pool and Garden maps.

*Versión en español más abajo.*

## Features

- **Three modes** on the Versus screen: Player vs Bot, Player vs Player and Bot vs Bot.
- **Keyboard and mouse, plus a gamepad:** Player vs Bot needs no gamepad, and in Player vs Player one player uses keyboard and mouse and the other a gamepad.
- **Bot difficulty from 1 to 200**, with a slider in the game. The bot can play plants or zombies.
- **Pool map:** 6 rows, water in rows 3 and 4, and 7 cards.
- **Garden map:** 5 rows and 6 cards.
- **New zombie cards:** Balloon, Dolphin Rider and Snorkel, built from what the game already ships.
- **x1/x2 speed button** during Bot vs Bot matches.
- **Optional auto-collect** for sun, coins and presents, in the game options.
- Texts in English and Spanish, following the game language.

## Requirements

- MelonLoader 0.7.3 or newer
- .NET 6.0 Desktop Runtime (required by MelonLoader for IL2CPP games)
- PvZ Replanted version 1.5.1469 (Steam)

On another game version, the Balloon, Dolphin Rider and Snorkel cards are disabled, because they rely on addresses specific to this version.

## Installation

1. Install MelonLoader into the game and launch it once.
2. Download `VersusBot-vX.Y.Z.zip` from [Releases](../../releases).
3. Copy `Mods/VersusBot.dll` into the game's `Mods` folder (`...\steamapps\common\PVZ Replanted\Mods\`).
4. Launch the game. The MelonLoader console should show `Versus Bot` and its version.

On Linux with Proton, set the Steam launch options to `WINEDLLOVERRIDES="version=n,b" %command%`.

To uninstall, delete `VersusBot.dll` from the `Mods` folder.

## Known limitations

- **Not compatible with the Quick Collect mod by Draco9990.** If you have it installed, remove it before using this mod. The game shows a warning if it finds it.
- **Tested on Fedora with Proton and in a Windows 11 virtual machine, not on a physical Windows PC.**
- In the VirtualBox virtual machine, the game froze after about 11 to 20 minutes with error `0x887A0005`, even without this mod. It points to the virtual machine's graphics.
- Player vs Player with a real USB gamepad has not been tested on Windows.

## Building from source

The project references the game's MelonLoader assemblies, so the game must be installed with MelonLoader and launched once.

```sh
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\PVZ Replanted"
```

`GameDir` defaults to the Linux Steam path. The output is `bin/Release/net6.0/VersusBot.dll`.

---

## Español

Mod de MelonLoader para **Plants vs. Zombies: Replanted** que añade un bot rival al modo Versus, además de los mapas Piscina y Jardín.

- **Tres modos** en la pantalla de Versus: Jugador vs Bot, Jugador vs Jugador y Bot vs Bot.
- **Teclado y ratón, más mando:** Jugador vs Bot no necesita mando, y en Jugador vs Jugador una persona usa teclado y ratón y la otra un mando.
- **Dificultad del bot de 1 a 200**, con un control en el juego. El bot puede jugar con plantas o con zombis.
- **Mapa Piscina:** 6 líneas, agua en las líneas 3 y 4, y 7 cartas.
- **Mapa Jardín:** 5 líneas y 6 cartas.
- **Cartas nuevas de zombi:** Globo, Delfín y Buzo.
- **Botón de velocidad x1/x2** en partidas Bot vs Bot.
- **Recogida automática opcional** de soles, monedas y regalos, en las opciones del juego.

**Requisitos:** MelonLoader 0.7.3 o superior, .NET 6.0 Desktop Runtime y PvZ Replanted versión 1.5.1469 (Steam). En otra versión del juego se desactivan las cartas de Globo, Delfín y Buzo.

**Instalación:** instala MelonLoader, abre el juego una vez, descarga el zip de [Releases](../../releases) y copia `VersusBot.dll` en la carpeta `Mods` del juego. En Linux con Proton, usa `WINEDLLOVERRIDES="version=n,b" %command%` como opción de lanzamiento en Steam.

**Limitaciones conocidas:**

- Incluye recogida automática opcional. **No es compatible con el mod Quick Collect de Draco9990**; si lo tienes instalado, quítalo antes de usar este mod.
- Se probó en Fedora con Proton y en una máquina virtual con Windows 11, no en un PC con Windows real.
- En la máquina virtual de VirtualBox, el juego se congeló tras unos 11 a 20 minutos con el error `0x887A0005`, incluso sin este mod. Apunta a los gráficos de la máquina virtual.
- No se probó Jugador vs Jugador con un mando USB real en Windows.

## License

[MIT](LICENSE) © 2026 Angel
