// Versus Bot — losas de piedra de la piscina.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VersusBot;

// Las losas se dibujan por código, a mano, imitando las del patio de la casa. El comienzo de la
// piscina se copia del fondo del juego en espejo. El borde recortado deja ver el pasto real.
internal static class TexturaLosas
{
    // Tamaño de diseño: cada unidad de diseño equivale a un pixel de pantalla a 1080p.
    private const int Ancho = 356;
    private const int Alto = 307;
    private const int Definicion = 2;
    private const float AnchoSombra = 5f;
    private const float AnchoContorno = 1.2f;
    private const float AlfaMinima = 0.9f;
    // Tamaños en unidades del fondo. La textura cubre 711 de ancho; del borde se copian 142.
    private const float UnidadesPorPixel = 711f / Ancho;
    private const float UnidadesDelBorde = 142f;
    // Hasta dónde llega el borde copiado en esta textura y el ancho de su junta con las losas.
    private const float AnchoTira = 62f;
    private const float JuntaTira = 2.2f;
    private const float AnchoLabio = 5f;
    private const float AnchoLineaFina = 0.65f;
    private const float AnchoBrilloFino = 1.8f;
    private const float AnchoBrilloGrieta = 1.6f;

    private struct Punto
    {
        public float X;
        public float Y;

        public Punto(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    private static readonly Punto[] Arriba = { new Punto(55, -12), new Punto(152, -12), new Punto(250, -12), new Punto(365, -12) };
    private static readonly Punto[] Linea1 = Puntos(55, 70, 100, 73, 148, 76, 212, 71, 258, 68, 300, 70, 365, 74);
    private static readonly Punto[] Linea2 = Puntos(55, 150, 110, 146, 150, 149, 205, 154, 255, 151, 305, 147, 365, 152);
    private static readonly Punto[] Linea3 = Puntos(55, 226, 105, 229, 158, 232, 215, 228, 262, 224, 310, 227, 365, 230);
    private static readonly Punto[] Abajo = Puntos(55, 322, 100, 322, 222, 322, 318, 322, 365, 322);

    private static readonly Punto[][] Piedras =
    {
        new[] { Arriba[0], Arriba[1], Linea1[2], Linea1[1], Linea1[0] },
        new[] { Arriba[1], Arriba[2], Linea1[4], Linea1[3], Linea1[2] },
        new[] { Arriba[2], Arriba[3], Linea1[6], Linea1[5], Linea1[4] },
        new[] { Linea1[0], Linea1[1], Linea2[1], Linea2[0] },
        new[] { Linea1[1], Linea1[2], Linea1[3], Linea2[3], Linea2[2], Linea2[1] },
        new[] { Linea1[3], Linea1[4], Linea1[5], Linea2[5], Linea2[4], Linea2[3] },
        new[] { Linea1[5], Linea1[6], Linea2[6], Linea2[5] },
        new[] { Linea2[0], Linea2[1], Linea2[2], Linea3[2], Linea3[1], Linea3[0] },
        new[] { Linea2[2], Linea2[3], Linea2[4], Linea3[4], Linea3[3], Linea3[2] },
        new[] { Linea2[4], Linea2[5], Linea2[6], Linea3[6], Linea3[5], Linea3[4] },
        new[] { Linea3[0], Linea3[1], Abajo[1], Abajo[0] },
        new[] { Linea3[1], Linea3[2], Linea3[3], Abajo[2], Abajo[1] },
        new[] { Linea3[3], Linea3[4], Linea3[5], Abajo[3], Abajo[2] },
        new[] { Linea3[5], Linea3[6], Abajo[4], Abajo[3] },
    };

    // Tres tonos medidos en el patio: claro, oscuro y medio.
    private static readonly int[] TonoDePiedra = { 0, 2, 1, 1, 0, 2, 0, 2, 1, 0, 1, 0, 2, 1 };
    private static readonly float[][] Tonos =
    {
        new[] { 214f, 198f, 154f },
        new[] { 170f, 154f, 113f },
        new[] { 198f, 184f, 146f },
    };

    private static readonly Punto[][] GrietasGruesas =
    {
        Puntos(258, 68, 238, 92, 214, 99),
        Puntos(238, 92, 246, 116),
        Puntos(110, 146, 128, 178, 158, 186),
        Puntos(128, 178, 120, 205),
        Puntos(305, 147, 332, 172, 360, 176),
        Puntos(205, 154, 190, 128),
        Puntos(215, 228, 238, 262, 282, 270),
        Puntos(238, 262, 232, 290),
        Puntos(148, 76, 124, 44, 90, 38),
    };

    private static readonly Punto[][] GrietasFinas =
    {
        Puntos(176, 28, 205, 24, 226, 34),
        Puntos(300, 30, 322, 40),
        Puntos(70, 110, 88, 118),
        Puntos(160, 108, 184, 102),
        Puntos(280, 110, 296, 122),
        Puntos(82, 186, 100, 194),
        Puntos(180, 190, 204, 184),
        Puntos(290, 194, 312, 186),
        Puntos(120, 270, 148, 262),
        Puntos(70, 270, 84, 290),
        Puntos(280, 300, 305, 290),
    };

    private static readonly Punto[] Picaduras = Puntos(112, 20, 205, 50, 320, 18, 85, 126, 175, 128, 270, 88, 335, 120, 95, 200, 232, 200, 322, 206, 150, 290, 300, 262);

    private static Punto[][] _juntas;
    private static Texture2D _textura;
    private static Color32[] _borde;
    private static int _anchoBorde;
    private static int _altoBorde;

    internal static Texture2D Obtener(Texture fondo, Rect borde)
    {
        if (_textura == null)
        {
            _borde = Leer(fondo, borde, "Borde de la piscina", out _anchoBorde, out _altoBorde);
            _juntas = Juntas();
            _textura = Crear();
            _borde = null;
        }
        return _textura;
    }

    private static Punto[] Puntos(params float[] v)
    {
        var puntos = new Punto[v.Length / 2];
        for (var i = 0; i < puntos.Length; i++)
        {
            puntos[i] = new Punto(v[2 * i], v[2 * i + 1]);
        }
        return puntos;
    }

    // Cada juntura entre dos piedras se guarda una sola vez.
    private static Punto[][] Juntas()
    {
        var juntas = new List<Punto[]>();
        var vistas = new HashSet<string>();
        foreach (var piedra in Piedras)
        {
            for (var i = 0; i < piedra.Length; i++)
            {
                var a = piedra[i];
                var b = piedra[(i + 1) % piedra.Length];
                var clave = a.X < b.X || (a.X == b.X && a.Y < b.Y) ? a.X + "," + a.Y + "," + b.X + "," + b.Y : b.X + "," + b.Y + "," + a.X + "," + a.Y;
                if (vistas.Add(clave))
                {
                    juntas.Add(new[] { a, b });
                }
            }
        }
        return juntas.ToArray();
    }

    // El dibujo del juego no se puede leer directo: se copia a una textura temporal.
    private static Color32[] Leer(Texture fondo, Rect zona, string nombre, out int ancho, out int alto)
    {
        ancho = Mathf.RoundToInt(zona.width);
        alto = Mathf.RoundToInt(zona.height);
        RenderTexture destino = null;
        var anterior = RenderTexture.active;
        try
        {
            destino = RenderTexture.GetTemporary(fondo.width, fondo.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(fondo, destino);
            RenderTexture.active = destino;
            var lectura = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
            lectura.ReadPixels(new Rect(zona.x, zona.y, ancho, alto), 0, 0);
            lectura.Apply();
            Color32[] pixeles = lectura.GetPixels32();
            UnityEngine.Object.Destroy(lectura);
            long r = 0, g = 0, b = 0, a = 0;
            foreach (var pixel in pixeles)
            {
                r += pixel.r;
                g += pixel.g;
                b += pixel.b;
                a += pixel.a;
            }
            var total = Math.Max(1, pixeles.Length);
            var valida = a / (float)total / 255f >= AlfaMinima;
            VersusBotMod.Log.Msg(nombre + ": " + ancho + "x" + alto + " color medio " + r / total + "," + g / total + "," + b / total + " alfa " + a / total + (valida ? "" : " (no sirve)"));
            return valida ? pixeles : null;
        }
        catch (Exception error)
        {
            VersusBotMod.Log.Warning("No se pudo leer " + nombre + ": " + error.Message);
            return null;
        }
        finally
        {
            RenderTexture.active = anterior;
            if (destino != null)
            {
                RenderTexture.ReleaseTemporary(destino);
            }
        }
    }

    private static Texture2D Crear()
    {
        var inicio = Time.realtimeSinceStartup;
        var ancho = Ancho * Definicion;
        var alto = Alto * Definicion;
        var pixeles = new Color32[ancho * alto];
        for (var y = 0; y < alto; y++)
        {
            for (var x = 0; x < ancho; x++)
            {
                pixeles[y * ancho + x] = Pixel(x / (float)Definicion, y / (float)Definicion);
            }
        }
        var textura = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        textura.wrapMode = TextureWrapMode.Clamp;
        textura.SetPixels32(pixeles);
        textura.Apply();
        textura.hideFlags = HideFlags.DontUnloadUnusedAsset;
        VersusBotMod.Detalle("Losas dibujadas en " + (int)((Time.realtimeSinceStartup - inicio) * 1000f) + " ms");
        return textura;
    }

    private static Color32 Pixel(float x, float y)
    {
        float r, g, b;
        var limiteTira = AnchoTira + 5f * (Ruido(x, y, 20f, 6) - 0.5f) + 2f * (Ruido(x, y, 6f, 8) - 0.5f);
        if (_borde != null && x < limiteTira)
        {
            ColorDelBorde(x, y, out r, out g, out b);
            if (x > limiteTira - JuntaTira)
            {
                r = 88f;
                g = 76f;
                b = 62f;
            }
        }
        else
        {
            // Las losas se diseñaron mirando la pantalla, con la Y hacia abajo.
            ColorDeLosa(x, Alto - 1 - y, out r, out g, out b);
        }
        return ConBordeDePasto(x, y, r, g, b);
    }

    // El borde se copia en espejo: la primera columna de esta textura es la que toca el agua.
    private static void ColorDelBorde(float x, float y, out float r, out float g, out float b)
    {
        var u = (_anchoBorde - 1) * (1f - x * UnidadesPorPixel / UnidadesDelBorde);
        var v = y / (Alto - 1) * (_altoBorde - 1);
        Muestrear(_borde, _anchoBorde, _altoBorde, u, v, out r, out g, out b);
    }

    private static void ColorDeLosa(float x, float y, out float r, out float g, out float b)
    {
        var wx = x + 5f * (Ruido(x, y, 38f, 1) - 0.5f) + 1.6f * (Ruido(x, y, 6f, 2) - 0.5f);
        var wy = y + 5f * (Ruido(x, y, 38f, 3) - 0.5f) + 1.6f * (Ruido(x, y, 6f, 4) - 0.5f);
        var piedra = PiedraEn(wx, wy);
        var tono = Tonos[TonoDePiedra[piedra]];
        r = tono[0];
        g = tono[1];
        b = tono[2];

        // Manchas suaves y redondas como las del patio.
        var mancha = Ruido(x + piedra * 90f, y, 30f, 13);
        if (mancha > 0.62f)
        {
            Teñir(ref r, ref g, ref b, 232f, 216f, 172f, Math.Min(1f, (mancha - 0.62f) / 0.05f) * 0.25f);
        }
        var grano = 0.97f + 0.05f * Ruido(x, y, 12f, 14) + 0.012f * (Ruido(x, y, 1.3f, 15) - 0.5f);
        r *= grano;
        g *= grano;
        b *= grano;

        var distancia = float.MaxValue;
        var masCercana = _juntas[0];
        foreach (var junta in _juntas)
        {
            var d = DistanciaASegmento(wx, wy, junta[0], junta[1], out _);
            if (d < distancia)
            {
                distancia = d;
                masCercana = junta;
            }
        }

        // Cada piedra lleva un labio claro abajo y a la derecha de su juntura, y sombra al otro lado.
        var a = masCercana[0];
        var c = masCercana[1];
        var nx = c.Y - a.Y;
        var ny = a.X - c.X;
        if (nx * 0.4f + ny * 0.92f < 0f)
        {
            nx = -nx;
            ny = -ny;
        }
        var lado = (wx - a.X) * nx + (wy - a.Y) * ny;
        var ancho = 1.3f + 1.1f * Ruido(x, y, 19f, 16);
        if (distancia < ancho + AnchoLabio)
        {
            var t = Math.Max(0f, distancia - ancho) / AnchoLabio;
            if (lado > 0f)
            {
                Teñir(ref r, ref g, ref b, 240f, 224f, 180f, MathF.Pow(1f - t, 1.6f) * 0.78f);
            }
            else
            {
                var sombra = 1f - MathF.Pow(1f - t, 1.5f) * 0.28f;
                r *= sombra;
                g *= sombra;
                b *= sombra;
            }
        }

        var grieta = float.MaxValue;
        var anchoGrieta = 0f;
        foreach (var linea in GrietasGruesas)
        {
            for (var i = 0; i < linea.Length - 1; i++)
            {
                var d = DistanciaASegmento(wx, wy, linea[i], linea[i + 1], out var avance);
                var tramo = (i + avance) / (linea.Length - 1);
                var anchoAqui = 2.4f * MathF.Pow(1f - tramo, 0.8f) + 0.35f;
                if (d - anchoAqui < grieta)
                {
                    grieta = d - anchoAqui;
                    anchoGrieta = anchoAqui;
                }
            }
        }
        if (grieta < AnchoBrilloGrieta)
        {
            Teñir(ref r, ref g, ref b, 240f, 224f, 180f, (1f - Math.Max(0f, grieta) / AnchoBrilloGrieta) * 0.52f);
        }
        if (distancia < ancho || grieta < 0f)
        {
            var enJunta = distancia < ancho ? distancia / ancho : 1f;
            var enGrieta = grieta < 0f ? -grieta / Math.Max(anchoGrieta, 0.01f) : 1f;
            var fondoDeLinea = MathF.Pow(Saturar(Math.Min(enJunta, enGrieta)), 1.3f);
            r = 52f + (116f - 52f) * fondoDeLinea;
            g = 40f + (96f - 40f) * fondoDeLinea;
            b = 34f + (78f - 34f) * fondoDeLinea;
        }

        foreach (var linea in GrietasFinas)
        {
            for (var i = 0; i < linea.Length - 1; i++)
            {
                var d = DistanciaASegmento(wx, wy, linea[i], linea[i + 1], out _);
                if (d < AnchoLineaFina)
                {
                    Teñir(ref r, ref g, ref b, 72f, 58f, 48f, (1f - d / AnchoLineaFina) * 0.8f);
                }
                else if (d < AnchoBrilloFino && wy < (linea[i].Y + linea[i + 1].Y) / 2f)
                {
                    Teñir(ref r, ref g, ref b, 240f, 224f, 180f, (1f - (d - AnchoLineaFina) / (AnchoBrilloFino - AnchoLineaFina)) * 0.38f);
                }
            }
        }

        for (var i = 0; i < Picaduras.Length; i++)
        {
            var radio = 0.9f + 1.1f * Azar(i, 0, 34);
            var dx = (x - Picaduras[i].X) / 1.5f;
            var dy = y - Picaduras[i].Y;
            var d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < radio)
            {
                Teñir(ref r, ref g, ref b, 66f, 52f, 44f, 0.85f);
            }
            else if (d < radio + 1f && y > Picaduras[i].Y)
            {
                Teñir(ref r, ref g, ref b, 242f, 226f, 182f, 0.6f);
            }
        }
    }

    private static int PiedraEn(float x, float y)
    {
        for (var i = 0; i < Piedras.Length; i++)
        {
            var piedra = Piedras[i];
            var dentro = false;
            for (var j = 0; j < piedra.Length; j++)
            {
                var a = piedra[j];
                var b = piedra[j == 0 ? piedra.Length - 1 : j - 1];
                if (a.Y > y != b.Y > y && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    dentro = !dentro;
                }
            }
            if (dentro)
            {
                return i;
            }
        }
        return 0;
    }

    private static float DistanciaASegmento(float x, float y, Punto a, Punto b, out float avance)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        avance = Saturar(((x - a.X) * dx + (y - a.Y) * dy) / (dx * dx + dy * dy));
        var ex = x - a.X - avance * dx;
        var ey = y - a.Y - avance * dy;
        return MathF.Sqrt(ex * ex + ey * ey);
    }

    private static void Teñir(ref float r, ref float g, ref float b, float cr, float cg, float cb, float cuanto)
    {
        r += (cr - r) * cuanto;
        g += (cg - g) * cuanto;
        b += (cb - b) * cuanto;
    }

    // Las losas y el borde se recortan igual contra el pasto de las líneas 2 y 5.
    private static Color32 ConBordeDePasto(float x, float y, float r, float g, float b)
    {
        var abajo = y < Alto / 2;
        var borde = Math.Min(y, Alto - 1 - y);
        var limite = (11f + 6f * MathF.Sin(x * 0.34f + (abajo ? 1.3f : 4f)) + 4f * MathF.Sin(x * 0.94f) + 4f * Azar((int)x / 3, abajo ? 0 : 1, 3)) / 2f;
        var haciaDentro = borde - limite;
        var factor = 1f;
        if (haciaDentro < AnchoSombra)
        {
            factor = 0.65f + 0.35f * Math.Max(0f, haciaDentro) / AnchoSombra;
        }
        if (haciaDentro > 0f && haciaDentro < AnchoContorno)
        {
            factor *= 0.5f;
        }
        var alfa = Saturar(haciaDentro / 0.9f);
        return new Color32((byte)Acotar(r * factor, 0f, 255f), (byte)Acotar(g * factor, 0f, 255f), (byte)Acotar(b * factor, 0f, 255f), (byte)(alfa * 255f));
    }

    private static void Muestrear(Color32[] datos, int ancho, int alto, float u, float v, out float r, out float g, out float b)
    {
        u = Acotar(u, 0f, ancho - 1f);
        v = Acotar(v, 0f, alto - 1f);
        var iu = (int)u;
        var iv = (int)v;
        var fu = u - iu;
        var fv = v - iv;
        var siguienteU = Math.Min(iu + 1, ancho - 1);
        var siguienteV = Math.Min(iv + 1, alto - 1);
        var p00 = datos[iv * ancho + iu];
        var p10 = datos[iv * ancho + siguienteU];
        var p01 = datos[siguienteV * ancho + iu];
        var p11 = datos[siguienteV * ancho + siguienteU];
        r = Mezcla(p00.r, p10.r, p01.r, p11.r, fu, fv);
        g = Mezcla(p00.g, p10.g, p01.g, p11.g, fu, fv);
        b = Mezcla(p00.b, p10.b, p01.b, p11.b, fu, fv);
    }

    // Las funciones de Mathf pasan por el juego y son muy lentas para llamarlas por cada pixel.
    private static float Saturar(float valor) => valor < 0f ? 0f : valor > 1f ? 1f : valor;

    private static float Acotar(float valor, float minimo, float maximo) => valor < minimo ? minimo : valor > maximo ? maximo : valor;

    private static float Mezclar(float a, float b, float t) => a + (b - a) * t;

    private static float Mezcla(float a, float b, float c, float d, float fu, float fv)
    {
        return Mezclar(Mezclar(a, b, fu), Mezclar(c, d, fu), fv);
    }

    private static float Azar(int a, int b, int semilla)
    {
        unchecked
        {
            var h = (uint)a * 374761393u + (uint)b * 668265263u + (uint)semilla * 2147483647u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffffu) / (float)0xffffffu;
        }
    }

    private static float Ruido(float x, float y, float escala, int semilla)
    {
        var fx = x / escala;
        var fy = y / escala;
        var ix = (int)Math.Floor(fx);
        var iy = (int)Math.Floor(fy);
        var tx = fx - ix;
        var ty = fy - iy;
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);
        var abajo = Mezclar(Azar(ix, iy, semilla), Azar(ix + 1, iy, semilla), tx);
        var arriba = Mezclar(Azar(ix, iy + 1, semilla), Azar(ix + 1, iy + 1, semilla), tx);
        return Mezclar(abajo, arriba, ty);
    }
}
