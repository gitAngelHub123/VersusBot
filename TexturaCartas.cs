// Versus Bot — cartas de zombi con estilo propio, dibujadas desde la animación de cada zombi.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppReloaded.DataModels;
using Il2CppReloaded.Gameplay;
using Il2CppSource.Controllers;
using Il2CppReloaded.TreeStateActivities;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace VersusBot;

// Estilo A aprobado por el usuario: marco y fondo lila, igual para todas las cartas. El zombi se
// fotografía con una cámara propia usando la silueta que el juego crea al colocar cartas.
internal static class TexturaCartas
{
    private const int Ancho = 434;
    private const int Alto = 591;
    private const int Borde = 6;
    private const int MargenArte = 25;
    private const int ArteArriba = 62;
    private const int ArteAbajo = 134;
    private const int PlacaAbajo = 23;
    private const int PlacaArriba = 118;
    private const int AltoLetrero = 58;
    private const int LadoCerebro = 88;
    private const int MargenCerebro = 14;
    private const int LadoFoto = 1024;
    private const int CapaFoto = 31;
    private const float MedioCuadro = 420f;
    private const float CentroX = 150f;
    private const float CentroY = -250f;
    private const int AlfaMinima = 20;
    private const int FramesDeEspera = 4;
    private const int FramesMaximos = 120;
    private const float EsperaImagenesPlanta = 3f;
    private const string PrefijoPlanta = "SPR_Almanac_Seedpackets_";
    private static readonly Vector3 Lugar = new Vector3(0f, 100000f, 0f);

    private static readonly Color32 Linea = new Color32(70, 44, 120, 255);
    private static readonly Color32 Marco = new Color32(222, 212, 248, 255);
    private static readonly Color32 Brillo = new Color32(245, 240, 255, 255);
    private static readonly Color32 Placa = new Color32(244, 240, 252, 255);
    private static readonly Color32 FondoArriba = new Color32(196, 182, 240, 255);
    private static readonly Color32 FondoAbajo = new Color32(150, 128, 214, 255);
    private static readonly Color32 Titulo = new Color32(84, 48, 148, 255);

    private static readonly Dictionary<IntPtr, SeedType> Modelos = new Dictionary<IntPtr, SeedType>();
    private static readonly Dictionary<IntPtr, AddressableSpriteValueModel> Vivos = new Dictionary<IntPtr, AddressableSpriteValueModel>();
    private static readonly Dictionary<SeedType, Sprite> Cartas = new Dictionary<SeedType, Sprite>();
    private static bool _generando;
    private static bool _generadas;
    private static Color32[] _letrero;
    private static int _anchoLetrero;
    private static int _altoLetrero;
    private static Color32[] _cerebro;
    private static int _ladoCerebroLeido;

    internal static bool EsCartaDeZombi(SeedType carta)
    {
        return carta == MapaVersus.CartaBuzo || (carta >= SeedType.ZombieNormal && carta <= SeedType.ZombieGravestone);
    }

    internal static void Marcar(AddressableSpriteValueModel modelo, SeedType carta)
    {
        if (modelo == null)
        {
            return;
        }
        if (!EsCartaDeZombi(carta))
        {
            Modelos.Remove(modelo.Pointer);
            Vivos.Remove(modelo.Pointer);
            return;
        }
        Modelos[modelo.Pointer] = carta;
        Vivos[modelo.Pointer] = modelo;
        Aplicar(modelo);
    }

    internal static void Aplicar(AddressableSpriteValueModel modelo)
    {
        if (modelo == null || !Modelos.TryGetValue(modelo.Pointer, out var carta) || !Cartas.TryGetValue(carta, out var sprite) || sprite == null)
        {
            return;
        }
        if (modelo.Value == null || modelo.Value.Pointer != sprite.Pointer)
        {
            modelo.Value = sprite;
        }
    }

    internal static void Generar(IEnumerable<SeedType> tipos, Il2CppReloaded.Services.IDataService datos, GameplayActivity partida)
    {
        if (_generando || _generadas)
        {
            return;
        }
        _generando = true;
        MelonLoader.MelonCoroutines.Start(GenerarLuego(new List<SeedType>(tipos), datos, partida));
    }

    private static IEnumerator GenerarLuego(List<SeedType> tipos, Il2CppReloaded.Services.IDataService datos, GameplayActivity partida)
    {
        var inicio = Time.unscaledTime;
        // Las imágenes de planta traen el letrero en alta resolución; se cargan al abrir el selector.
        var hora = Time.unscaledTime + EsperaImagenesPlanta;
        while (_letrero == null && Time.unscaledTime < hora)
        {
            Proteger(BuscarLetreroYCerebro);
            yield return null;
        }
        Proteger(BuscarLetreroYCerebro);
        foreach (var tipo in tipos)
        {
            if (!Cartas.ContainsKey(tipo))
            {
                Proteger(() => CrearDesdeElJuego(tipo, datos));
                foreach (var modelo in Vivos.Values)
                {
                    Proteger(() => Aplicar(modelo));
                }
                yield return null;
            }
        }
        // Si alguna no se pudo leer directamente, se fotografía cuando ya hay partida.
        if (partida == null)
        {
            _generando = false;
            yield break;
        }
        var rt = RenderTexture.GetTemporary(LadoFoto, LadoFoto, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var objetoCamara = new GameObject("CamaraCartas");
        var camara = objetoCamara.AddComponent<Camera>();
        camara.enabled = false;
        camara.orthographic = true;
        camara.orthographicSize = MedioCuadro;
        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camara.cullingMask = 1 << CapaFoto;
        camara.nearClipPlane = 0.1f;
        camara.farClipPlane = 5000f;
        camara.targetTexture = rt;
        foreach (var tipo in tipos)
        {
            if (Cartas.ContainsKey(tipo))
            {
                continue;
            }
            PreviewController vista = null;
            Proteger(() => vista = partida.CreatePreviewController(TipoDeZombi(tipo)));
            if (vista == null)
            {
                VersusBotMod.Log.Warning("Carta " + tipo + ": no se creó la silueta.");
                continue;
            }
            var capas = new List<KeyValuePair<GameObject, int>>();
            var espera = 0;
            var listos = 0;
            while (espera < FramesMaximos && listos < FramesDeEspera)
            {
                espera++;
                Proteger(() => Preparar(vista, camara, capas));
                if (vista.GetComponentsInChildren<Renderer>(true).Length > 0)
                {
                    listos++;
                }
                yield return null;
            }
            Proteger(() =>
            {
                Preparar(vista, camara, capas);
                camara.Render();
                Fotografiar(tipo, rt);
            });
            Proteger(() =>
            {
                foreach (var par in capas)
                {
                    if (par.Key != null)
                    {
                        par.Key.layer = par.Value;
                    }
                }
                vista.Die();
            });
        }
        camara.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        UnityEngine.Object.Destroy(objetoCamara);
        _generando = false;
        _generadas = true;
        VersusBotMod.Detalle("Cartas de zombi listas: " + Cartas.Count + " de " + tipos.Count + " en " + (int)((Time.unscaledTime - inicio) * 1000f) + " ms");
        foreach (var modelo in Vivos.Values)
        {
            Proteger(() => Aplicar(modelo));
        }
    }

    // La imagen de cada zombi se lee de los archivos del propio juego: el mod no incluye arte.
    private static void CrearDesdeElJuego(SeedType tipo, Il2CppReloaded.Services.IDataService datos)
    {
        var definicion = datos.GetZombieDefinition(TipoDeZombi(tipo));
        var referencia = definicion?.m_previewSprite;
        if (referencia == null || !referencia.RuntimeKeyIsValid())
        {
            VersusBotMod.Detalle("Carta " + tipo + ": sin imagen en su definición.");
            return;
        }
        var sprite = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Sprite>(referencia.RuntimeKey).WaitForCompletion();
        if (sprite == null)
        {
            return;
        }
        var area = sprite.textureRect;
        var ancho = Mathf.RoundToInt(area.width);
        var alto = Mathf.RoundToInt(area.height);
        var pixeles = Leer(sprite.texture, area, ancho, alto);
        int xMin = ancho, xMax = -1, yMin = alto, yMax = -1;
        for (var y = 0; y < alto; y++)
        {
            for (var x = 0; x < ancho; x++)
            {
                if (pixeles[y * ancho + x].a > AlfaMinima)
                {
                    xMin = Math.Min(xMin, x);
                    xMax = Math.Max(xMax, x);
                    yMin = Math.Min(yMin, y);
                    yMax = Math.Max(yMax, y);
                }
            }
        }
        if (xMax < 0)
        {
            return;
        }
        var anchoFigura = xMax - xMin + 1;
        var altoFigura = yMax - yMin + 1;
        var figura = new Color32[anchoFigura * altoFigura];
        for (var y = 0; y < altoFigura; y++)
        {
            for (var x = 0; x < anchoFigura; x++)
            {
                figura[y * anchoFigura + x] = pixeles[(y + yMin) * ancho + x + xMin];
            }
        }
        Cartas[tipo] = Componer(tipo, figura, anchoFigura, altoFigura);
        VersusBotMod.Detalle("Carta " + tipo + ": imagen '" + sprite.name + "' " + anchoFigura + "x" + altoFigura);
    }

    private static ZombieType TipoDeZombi(SeedType carta)
    {
        if (carta == MapaVersus.CartaDelfin)
        {
            return ZombieType.DolphinRider;
        }
        return carta == MapaVersus.CartaBuzo ? ZombieType.Snorkel : Challenge.IZombieSeedTypeToZombieType(carta);
    }

    private static void Preparar(PreviewController vista, Camera camara, List<KeyValuePair<GameObject, int>> capas)
    {
        var raiz = vista.transform;
        raiz.position = Lugar;
        vista.gameObject.SetActive(true);
        camara.transform.position = new Vector3(Lugar.x + CentroX, Lugar.y + CentroY, Lugar.z - 1000f);
        foreach (var parte in vista.GetComponentsInChildren<Transform>(true))
        {
            var objeto = parte.gameObject;
            if (objeto.layer != CapaFoto)
            {
                capas.Add(new KeyValuePair<GameObject, int>(objeto, objeto.layer));
                objeto.layer = CapaFoto;
            }
        }
    }

    private static void Fotografiar(SeedType tipo, RenderTexture rt)
    {
        var anterior = RenderTexture.active;
        RenderTexture.active = rt;
        var lectura = new Texture2D(LadoFoto, LadoFoto, TextureFormat.RGBA32, false);
        lectura.ReadPixels(new Rect(0f, 0f, LadoFoto, LadoFoto), 0, 0);
        lectura.Apply();
        RenderTexture.active = anterior;
        Color32[] foto = lectura.GetPixels32();
        UnityEngine.Object.Destroy(lectura);

        // La silueta es semitransparente: se recupera el color y se deja opaca.
        var maximo = 0;
        int xMin = LadoFoto, xMax = -1, yMin = LadoFoto, yMax = -1;
        for (var i = 0; i < foto.Length; i++)
        {
            if (foto[i].a > maximo)
            {
                maximo = foto[i].a;
            }
        }
        var umbral = Math.Max(AlfaMinima * maximo / 255, 1);
        for (var y = 0; y < LadoFoto; y++)
        {
            for (var x = 0; x < LadoFoto; x++)
            {
                if (foto[y * LadoFoto + x].a > umbral)
                {
                    xMin = Math.Min(xMin, x);
                    xMax = Math.Max(xMax, x);
                    yMin = Math.Min(yMin, y);
                    yMax = Math.Max(yMax, y);
                }
            }
        }
        VersusBotMod.Detalle("Foto de " + tipo + ": alfa máxima " + maximo + ", figura " + (xMax - xMin + 1) + "x" + (yMax - yMin + 1));
        if (xMax < 0 || maximo < AlfaMinima)
        {
            return;
        }
        var ancho = xMax - xMin + 1;
        var alto = yMax - yMin + 1;
        var figura = new Color32[ancho * alto];
        for (var y = 0; y < alto; y++)
        {
            for (var x = 0; x < ancho; x++)
            {
                var c = foto[(y + yMin) * LadoFoto + x + xMin];
                if (c.a > 0)
                {
                    var r = Math.Min(255, c.r * 255 / c.a);
                    var g = Math.Min(255, c.g * 255 / c.a);
                    var b = Math.Min(255, c.b * 255 / c.a);
                    c = new Color32((byte)r, (byte)g, (byte)b, (byte)Math.Min(255, c.a * 255 / maximo));
                }
                figura[y * ancho + x] = c;
            }
        }
        Cartas[tipo] = Componer(tipo, figura, ancho, alto);
    }

    private static Sprite Componer(SeedType tipo, Color32[] figura, int anchoFigura, int altoFigura)
    {
        var p = new Color32[Ancho * Alto];
        Redondeado(p, 0, 0, Ancho, Alto, 20, Linea);
        Redondeado(p, Borde, Borde, Ancho - 2 * Borde, Alto - 2 * Borde, 16, Brillo);
        Redondeado(p, Borde + 3, Borde + 3, Ancho - 2 * (Borde + 3), Alto - 2 * (Borde + 3), 14, Marco);
        // Arte.
        int ax = MargenArte, ay = ArteAbajo, aw = Ancho - 2 * MargenArte, ah = Alto - ArteAbajo - ArteArriba;
        Redondeado(p, ax - 4, ay - 4, aw + 8, ah + 8, 15, Linea);
        for (var y = 0; y < ah; y++)
        {
            var t = y / (float)ah;
            var color = new Color32((byte)(FondoAbajo.r + (FondoArriba.r - FondoAbajo.r) * t), (byte)(FondoAbajo.g + (FondoArriba.g - FondoAbajo.g) * t), (byte)(FondoAbajo.b + (FondoArriba.b - FondoAbajo.b) * t), 255);
            for (var x = 0; x < aw; x++)
            {
                if (DentroDeEsquina(x, y, aw, ah, 12))
                {
                    p[(y + ay) * Ancho + x + ax] = color;
                }
            }
        }
        var escala = Math.Min((aw - 16f) / anchoFigura, (ah - 12f) / altoFigura);
        var w = (int)(anchoFigura * escala);
        var h = (int)(altoFigura * escala);
        var x0 = ax + (aw - w) / 2;
        var y0 = ay + 5;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                Mezclar(p, x0 + x, y0 + y, Muestra(figura, anchoFigura, altoFigura, (x + 0.5f) / escala, (y + 0.5f) / escala));
            }
        }
        // Precio: el número lo escribe el juego encima.
        Redondeado(p, MargenArte - 4, PlacaAbajo - 4, Ancho - 2 * MargenArte + 8, PlacaArriba - PlacaAbajo + 8, 15, Linea);
        Redondeado(p, MargenArte, PlacaAbajo, Ancho - 2 * MargenArte, PlacaArriba - PlacaAbajo, 12, Placa);
        if (_cerebro != null)
        {
            var bx = Ancho - MargenArte - LadoCerebro - MargenCerebro;
            var by = PlacaAbajo + (PlacaArriba - PlacaAbajo - LadoCerebro) / 2;
            for (var y = 0; y < LadoCerebro; y++)
            {
                for (var x = 0; x < LadoCerebro; x++)
                {
                    Mezclar(p, bx + x, by + y, Muestra(_cerebro, _ladoCerebroLeido, _ladoCerebroLeido, (x + 0.5f) * _ladoCerebroLeido / LadoCerebro, (y + 0.5f) * _ladoCerebroLeido / LadoCerebro));
                }
            }
        }
        // Letrero tomado de una carta de planta, en morado.
        if (_letrero != null)
        {
            for (var y = 0; y < _altoLetrero; y++)
            {
                for (var x = 0; x < _anchoLetrero; x++)
                {
                    var c = _letrero[y * _anchoLetrero + x];
                    var claridad = (c.r + c.g + c.b) / 3f;
                    var alfa = Math.Max(0f, Math.Min(1f, (170f - claridad) / 90f)) * (c.a / 255f);
                    if (alfa > 0f)
                    {
                        Mezclar(p, x * Ancho / _anchoLetrero, Alto - AltoLetrero + y, new Color32(Titulo.r, Titulo.g, Titulo.b, (byte)(alfa * 255f)));
                    }
                }
            }
        }
        var textura = new Texture2D(Ancho, Alto, TextureFormat.RGBA32, true);
        textura.wrapMode = TextureWrapMode.Clamp;
        textura.SetPixels32(p);
        textura.Apply(true);
        textura.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var sprite = Sprite.Create(textura, new Rect(0f, 0f, Ancho, Alto), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "CartaZombi_" + tipo;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    private static Color32 Muestra(Color32[] datos, int ancho, int alto, float x, float y)
    {
        x = Math.Max(0f, Math.Min(x - 0.5f, ancho - 1f));
        y = Math.Max(0f, Math.Min(y - 0.5f, alto - 1f));
        var ix = (int)x;
        var iy = (int)y;
        var fx = x - ix;
        var fy = y - iy;
        var ix2 = Math.Min(ix + 1, ancho - 1);
        var iy2 = Math.Min(iy + 1, alto - 1);
        var a = datos[iy * ancho + ix];
        var b = datos[iy * ancho + ix2];
        var c = datos[iy2 * ancho + ix];
        var d = datos[iy2 * ancho + ix2];
        // Se pondera por alfa para que el fondo del recorte no manche los bordes.
        float pa = a.a * (1 - fx) * (1 - fy), pb = b.a * fx * (1 - fy), pc = c.a * (1 - fx) * fy, pd = d.a * fx * fy;
        var suma = pa + pb + pc + pd;
        if (suma <= 0f)
        {
            return new Color32(0, 0, 0, 0);
        }
        return new Color32((byte)((a.r * pa + b.r * pb + c.r * pc + d.r * pd) / suma), (byte)((a.g * pa + b.g * pb + c.g * pc + d.g * pd) / suma), (byte)((a.b * pa + b.b * pb + c.b * pc + d.b * pd) / suma), (byte)suma);
    }

    private static void Mezclar(Color32[] p, int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= Ancho || y >= Alto || c.a == 0)
        {
            return;
        }
        var i = y * Ancho + x;
        var a = c.a / 255f;
        var d = p[i];
        p[i] = new Color32((byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a), (byte)(d.b + (c.b - d.b) * a), 255);
    }

    private static bool DentroDeEsquina(int x, int y, int w, int h, int r)
    {
        var dx = Math.Max(Math.Max(r - x, x - (w - 1 - r)), 0);
        var dy = Math.Max(Math.Max(r - y, y - (h - 1 - r)), 0);
        return dx * dx + dy * dy <= r * r;
    }

    private static void Redondeado(Color32[] p, int x0, int y0, int w, int h, int r, Color32 color)
    {
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (DentroDeEsquina(x, y, w, h, r))
                {
                    p[(y + y0) * Ancho + x + x0] = color;
                }
            }
        }
    }

    private static void BuscarLetreroYCerebro()
    {
        Sprite planta = null;
        Sprite cerebro = null;
        var mejor = 0f;
        foreach (var objeto in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>()))
        {
            var sprite = objeto.Cast<Sprite>();
            var nombre = sprite.name;
            if (planta == null && nombre.StartsWith(PrefijoPlanta, StringComparison.Ordinal) && !nombre.EndsWith("Empty", StringComparison.Ordinal))
            {
                planta = sprite;
            }
            if (nombre.IndexOf("brain", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var r = sprite.rect;
                var proporcion = r.width / Math.Max(1f, r.height);
                if (proporcion > 0.75f && proporcion < 1.35f && r.width * r.height > mejor)
                {
                    mejor = r.width * r.height;
                    cerebro = sprite;
                }
            }
        }
        if (_letrero == null && planta != null)
        {
            var area = planta.textureRect;
            _anchoLetrero = Mathf.RoundToInt(area.width);
            _altoLetrero = Mathf.RoundToInt(area.height * AltoLetrero / Alto);
            _letrero = Leer(planta.texture, new Rect(area.x, area.yMax - _altoLetrero, _anchoLetrero, _altoLetrero), _anchoLetrero, _altoLetrero);
            VersusBotMod.Detalle("Letrero tomado de '" + planta.name + "'.");
        }
        if (_cerebro == null && cerebro != null)
        {
            var area = cerebro.textureRect;
            var lado = Mathf.RoundToInt(Math.Max(area.width, area.height));
            var ancho = Mathf.RoundToInt(area.width);
            var alto = Mathf.RoundToInt(area.height);
            var leidos = Leer(cerebro.texture, area, ancho, alto);
            var cuadrado = new Color32[lado * lado];
            for (var y = 0; y < alto; y++)
            {
                for (var x = 0; x < ancho; x++)
                {
                    cuadrado[(y + (lado - alto) / 2) * lado + x + (lado - ancho) / 2] = leidos[y * ancho + x];
                }
            }
            _cerebro = cuadrado;
            _ladoCerebroLeido = lado;
            VersusBotMod.Detalle("Cerebro tomado de '" + cerebro.name + "' " + ancho + "x" + alto + ".");
        }
    }

    private static Color32[] Leer(Texture textura, Rect area, int ancho, int alto)
    {
        var anterior = RenderTexture.active;
        var temporal = RenderTexture.GetTemporary(textura.width, textura.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        try
        {
            Graphics.Blit(textura, temporal);
            RenderTexture.active = temporal;
            var lectura = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
            lectura.ReadPixels(new Rect(area.x, area.y, ancho, alto), 0, 0);
            lectura.Apply();
            Color32[] pixeles = lectura.GetPixels32();
            UnityEngine.Object.Destroy(lectura);
            return pixeles;
        }
        finally
        {
            RenderTexture.active = anterior;
            RenderTexture.ReleaseTemporary(temporal);
        }
    }

    private static void Proteger(Action accion)
    {
        try
        {
            accion();
        }
        catch (Exception error)
        {
            VersusBotMod.Log.Warning("Cartas de zombi: " + error.Message);
        }
    }
}

// El juego vuelve a poner su imagen al actualizar cada carta y al terminar de cargarla.
[HarmonyPatch(typeof(Il2CppSource.DataModels.SeedChooserEntryModel), nameof(Il2CppSource.DataModels.SeedChooserEntryModel.UpdateModelData))]
internal static class ParcheCartaEnSelector
{
    private static void Postfix(Il2CppSource.DataModels.SeedChooserEntryModel __instance)
    {
        if (__instance.m_defData != null)
        {
            TexturaCartas.Marcar(__instance.m_thumbnail, __instance.m_defData.SeedType);
            MapaVersus.PonerTextosDeCarta(__instance.m_nameModel, __instance.m_toolTipModel, __instance.m_defData.SeedType);
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(SeedBankEntryModel), nameof(SeedBankEntryModel.UpdateModelData))]
internal static class ParcheCartaEnBanco
{
    private static void Postfix(SeedBankEntryModel __instance)
    {
        if (__instance.m_seedPacket != null)
        {
            TexturaCartas.Marcar(__instance.m_thumbnail, __instance.m_seedPacket.mPacketType);
        }
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}

[HarmonyPatch(typeof(AddressableSpriteValueModel), nameof(AddressableSpriteValueModel.OnSpriteLoaded))]
internal static class ParcheCartaAlCargar
{
    private static void Postfix(AddressableSpriteValueModel __instance)
    {
        TexturaCartas.Aplicar(__instance);
    }

    private static Exception Finalizer(Exception __exception) => MapaVersus.Registrar(__exception);
}
