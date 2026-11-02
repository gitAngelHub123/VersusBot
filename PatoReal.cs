// Versus Bot — toma el dibujo real del flotador de pato desde los zombis del juego.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using System;
using Il2CppReloaded.Gameplay;
using Il2CppSpine;
using Il2CppSpine.Unity;
using UnityEngine;

namespace VersusBot;

// El flotador es una pieza de la animación de los zombis normales. El globo no la trae, pero la imagen
// está en el mismo atlas del juego: se lee de ahí la primera vez que aparece un zombi que la tenga.
internal static class PatoReal
{
    private const string Prefijo = "Zombie_duckytube";
    private const int ParteBajoElAgua = 36;
    internal static Sprite Dibujo { get; private set; }
    private static bool _buscado;

    internal static void Buscar(Zombie zombi)
    {
        if (_buscado || zombi.mController == null)
        {
            return;
        }
        var render = zombi.mController.GetComponentInChildren<SkeletonRenderer>(true);
        var esqueleto = render?.skeleton;
        if (esqueleto == null)
        {
            return;
        }
        _buscado = true;
        try
        {
            var piel = esqueleto.Data.DefaultSkin;
            var espacios = esqueleto.Data.Slots;
            for (var i = 0; i < espacios.Count; i++)
            {
                var espacio = espacios.Items[i];
                if (!espacio.Name.StartsWith(Prefijo, StringComparison.Ordinal) || string.IsNullOrEmpty(espacio.AttachmentName))
                {
                    continue;
                }
                var pieza = piel.GetAttachment(i, espacio.AttachmentName);
                var region = pieza?.TryCast<MeshAttachment>()?.Region ?? pieza?.TryCast<RegionAttachment>()?.Region;
                var atlas = region?.TryCast<AtlasRegion>();
                VersusBotMod.Detalle("Pato real: espacio '" + espacio.Name + "' pieza '" + espacio.AttachmentName + "' tipo " + pieza?.GetIl2CppType().Name + (atlas != null ? " región " + atlas.packedWidth + "x" + atlas.packedHeight + " en " + atlas.x + "," + atlas.y + " rotada=" + atlas.rotate : " sin región"));
                if (atlas != null && Dibujo == null)
                {
                    Dibujo = Crear(atlas);
                }
            }
            if (Dibujo == null)
            {
                VersusBotMod.Log.Warning("No se encontró el flotador real: se usa el dibujo propio.");
            }
        }
        catch (Exception error)
        {
            VersusBotMod.Log.Warning("No se pudo leer el flotador real: " + error.Message);
        }
    }

    private static Sprite Crear(AtlasRegion region)
    {
        var material = region.page.rendererObject.Cast<Material>();
        var textura = material.mainTexture;
        var rotada = region.rotate;
        var anchoEnHoja = rotada ? region.packedHeight : region.packedWidth;
        var altoEnHoja = rotada ? region.packedWidth : region.packedHeight;
        var anterior = RenderTexture.active;
        var temporal = RenderTexture.GetTemporary(textura.width, textura.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Color32[] leidos;
        try
        {
            Graphics.Blit(textura, temporal);
            RenderTexture.active = temporal;
            var lectura = new Texture2D(anchoEnHoja, altoEnHoja, TextureFormat.RGBA32, false);
            lectura.ReadPixels(new Rect(region.x, textura.height - region.y - altoEnHoja, anchoEnHoja, altoEnHoja), 0, 0);
            lectura.Apply();
            leidos = lectura.GetPixels32();
            UnityEngine.Object.Destroy(lectura);
        }
        finally
        {
            RenderTexture.active = anterior;
            RenderTexture.ReleaseTemporary(temporal);
        }
        var ancho = region.packedWidth;
        var alto = region.packedHeight;
        var pixeles = new Color32[ancho * alto];
        for (var y = 0; y < alto; y++)
        {
            for (var x = 0; x < ancho; x++)
            {
                pixeles[y * ancho + x] = rotada ? leidos[(anchoEnHoja - 1 - y) + x * anchoEnHoja] : leidos[y * ancho + x];
            }
        }
        // El juego corta el flotador en la línea del agua; en este dibujo, girado, esa parte queda a la derecha.
        for (var y = 0; y < alto; y++)
        {
            for (var x = ancho - ParteBajoElAgua; x < ancho; x++)
            {
                pixeles[y * ancho + x].a = 0;
            }
        }
        var final = new Texture2D(ancho, alto, TextureFormat.RGBA32, true);
        final.wrapMode = TextureWrapMode.Clamp;
        final.SetPixels32(pixeles);
        final.Apply(true);
        final.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var sprite = Sprite.Create(final, new Rect(0f, 0f, ancho, alto), new Vector2(0.5f, 0.5f), 1f);
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }
}
