// Versus Bot — botón de velocidad x2, solo durante una partida Bot vs Bot, en cualquier mapa.
// El dibujo se copia en vivo del botón de velocidad del juego: el mod no incluye arte propio.
// Se copian solo imágenes y textos, nunca sus enlaces: clonar interfaz con enlaces vivos cierra el juego.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VersusBot;

internal static class BotonVelocidad
{
    private const float VelocidadRapida = 2f;
    private const float VelocidadNormal = 1f;
    private const float EscalaDeAudio = 1f;
    // La cámara lenta del final usa 0,5: solo se deshace lo que puso este botón.
    private const float LimiteVelocidadPropia = 1.5f;
    private const int TamanoTexto = 26;
    private const float AlturaBoton = 42f;
    private static readonly Vector2 MedidaRespaldo = new Vector2(66, AlturaBoton);
    // Dentro del tablero, a la izquierda de los objetivos: en pantallas más anchas que 16:9 los lados tapan la esquina.
    private static readonly Vector2 AnclaCentroAbajo = new Vector2(0.5f, 0f);
    private static readonly Vector2 PivoteDerechoAbajo = new Vector2(1f, 0f);
    private static readonly Vector2 PosicionJuntoAObjetivos = new Vector2(465, 14);
    private const string IndicadorNormal = "Speed_01";
    private const string IndicadorRapido = "Speed_02";
    private const string NombreBotonMenu = "P_BasicButton02_Menu";
    private static readonly Color ColorApagado = new Color(0.33f, 0.25f, 0.14f, 0.95f);
    private static readonly Color ColorEncendido = new Color(0.42f, 0.73f, 0.2f, 0.95f);

    private static GameObject _raiz;
    private static RectTransform _boton;
    private static Image _fondoRespaldo;
    private static TMP_Text _texto;
    private static GameObject _indicadorNormal;
    private static GameObject _indicadorRapido;
    private static bool _disenoDelJuego;
    private static bool _rapido;

    internal static void Actualizar()
    {
        if (!VersusBotMod.EnPartidaBotVsBot)
        {
            if (_raiz != null)
            {
                _raiz.SetActive(false);
                // El panel del juego puede cargarse más tarde: se vuelve a buscar en la próxima partida.
                if (!_disenoDelJuego)
                {
                    Object.Destroy(_raiz);
                    _raiz = null;
                }
            }
            if (Il2Cpp.TimeUtil.GetFlowingTimeScale() > LimiteVelocidadPropia)
            {
                Il2Cpp.TimeUtil.SetFlowingTimeScale(VelocidadNormal, EscalaDeAudio);
            }
            return;
        }
        if (_raiz == null && !Crear())
        {
            return;
        }
        var pausado = Il2Cpp.TimeUtil.IsTimePaused();
        _raiz.SetActive(!pausado);
        if (pausado)
        {
            return;
        }
        var raton = Mouse.current;
        if (raton != null && raton.leftButton.wasPressedThisFrame && RectTransformUtility.RectangleContainsScreenPoint(_boton, raton.position.ReadValue(), null))
        {
            _rapido = !_rapido;
            VersusBotMod.Log.Msg("Velocidad de Bot vs Bot: " + (_rapido ? "x2" : "x1"));
        }
        var deseada = _rapido ? VelocidadRapida : VelocidadNormal;
        if (!Mathf.Approximately(Il2Cpp.TimeUtil.GetFlowingTimeScale(), deseada))
        {
            Il2Cpp.TimeUtil.SetFlowingTimeScale(deseada, EscalaDeAudio);
        }
        MostrarEstado();
    }

    private static void MostrarEstado()
    {
        if (_fondoRespaldo != null)
        {
            _fondoRespaldo.color = _rapido ? ColorEncendido : ColorApagado;
        }
        if (_texto != null)
        {
            _texto.text = _rapido ? "x2" : "x1";
        }
        _indicadorNormal?.SetActive(!_rapido);
        _indicadorRapido?.SetActive(_rapido);
    }

    private static bool Crear()
    {
        var modelo = Object.FindObjectOfType(Il2CppType.Of<TMP_Text>())?.TryCast<TMP_Text>();
        if (modelo == null)
        {
            return false;
        }
        _raiz = new GameObject("BotonVelocidadVersusBot");
        Object.DontDestroyOnLoad(_raiz);
        var lienzo = _raiz.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.sortingOrder = 1000;
        var escala = _raiz.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);
        // Así la distancia al centro sigue al tablero, que se escala con el alto en pantallas anchas.
        escala.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        _texto = null;
        _indicadorNormal = _indicadorRapido = null;
        _fondoRespaldo = null;
        var delJuego = BuscarBotonDelJuego();
        _disenoDelJuego = delJuego != null;
        if (_disenoDelJuego)
        {
            _boton = CopiarVisual(delJuego, _raiz.transform);
            // El panel original no se ha dibujado nunca y su medida es 0: se toma la de su dibujo mayor.
            var medida = delJuego.rect.size;
            if (medida.y <= 0f)
            {
                medida = MedidaDeDibujos(_boton);
            }
            _boton.gameObject.SetActive(true);
            _boton.sizeDelta = medida;
            _boton.localScale = Vector3.one * (AlturaBoton / medida.y);
            AsegurarVisible(modelo);
            VersusBotMod.Log.Msg("Piezas del botón del juego: " + Describir(delJuego, 0));
        }
        else
        {
            _boton = CrearRespaldo(modelo);
        }
        _boton.anchorMin = _boton.anchorMax = AnclaCentroAbajo;
        _boton.pivot = PivoteDerechoAbajo;
        _boton.anchoredPosition = PosicionJuntoAObjetivos;
        VersusBotMod.Log.Msg("Botón de velocidad: " + (_disenoDelJuego ? "diseño copiado del juego, medida " + _boton.sizeDelta : "el juego no tiene cargado su botón, se usa uno propio"));
        return true;
    }

    private static RectTransform BuscarBotonDelJuego()
    {
        foreach (var objeto in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Il2Cpp.TimeCycler>()))
        {
            var rect = objeto.Cast<Il2Cpp.TimeCycler>().GetComponent<RectTransform>();
            if (rect != null)
            {
                return rect;
            }
        }
        return null;
    }

    private static RectTransform CopiarVisual(RectTransform origen, Transform padre)
    {
        var objeto = new GameObject(origen.name);
        var rect = objeto.AddComponent<RectTransform>();
        rect.SetParent(padre, false);
        rect.anchorMin = origen.anchorMin;
        rect.anchorMax = origen.anchorMax;
        rect.pivot = origen.pivot;
        rect.sizeDelta = origen.sizeDelta;
        rect.anchoredPosition = origen.anchoredPosition;
        rect.localScale = origen.localScale;
        rect.localRotation = origen.localRotation;

        var imagen = origen.GetComponent<Image>();
        if (imagen != null)
        {
            var copia = objeto.AddComponent<Image>();
            copia.sprite = imagen.sprite;
            // Sin dibujarse, una pieza de medida automática queda en 0.
            if (imagen.sprite != null && rect.anchorMin == rect.anchorMax && rect.sizeDelta.y <= 0f)
            {
                rect.sizeDelta = imagen.sprite.rect.size;
            }
            copia.type = imagen.type;
            copia.color = imagen.color;
            copia.preserveAspect = imagen.preserveAspect;
            copia.fillCenter = imagen.fillCenter;
            copia.pixelsPerUnitMultiplier = imagen.pixelsPerUnitMultiplier;
            copia.raycastTarget = false;
        }
        var letras = origen.GetComponent<TextMeshProUGUI>();
        if (letras != null)
        {
            var copia = objeto.AddComponent<TextMeshProUGUI>();
            copia.font = letras.font;
            copia.fontSharedMaterial = letras.fontSharedMaterial;
            copia.fontSize = letras.fontSize;
            copia.enableAutoSizing = letras.enableAutoSizing;
            copia.fontSizeMin = letras.fontSizeMin;
            copia.fontSizeMax = letras.fontSizeMax;
            copia.alignment = letras.alignment;
            copia.color = letras.color;
            copia.text = letras.text;
            copia.raycastTarget = false;
            _texto ??= copia;
        }
        if (origen.name == IndicadorNormal)
        {
            _indicadorNormal = objeto;
        }
        else if (origen.name == IndicadorRapido)
        {
            _indicadorRapido = objeto;
        }
        for (var i = 0; i < origen.childCount; i++)
        {
            var hijo = origen.GetChild(i).GetComponent<RectTransform>();
            if (hijo != null)
            {
                CopiarVisual(hijo, rect);
            }
        }
        objeto.SetActive(origen.gameObject.activeSelf || origen.parent == null);
        return rect;
    }

    private static Vector2 MedidaDeDibujos(RectTransform raiz)
    {
        var mayor = MedidaRespaldo;
        var area = 0f;
        foreach (var imagen in raiz.GetComponentsInChildren<Image>(true))
        {
            var medida = imagen.sprite != null ? imagen.sprite.rect.size : Vector2.zero;
            if (medida.x * medida.y > area)
            {
                area = medida.x * medida.y;
                mayor = medida;
            }
        }
        return mayor;
    }

    // Lo copiado puede depender de estados que aquí no se animan: siempre debe verse un fondo y un texto.
    private static void AsegurarVisible(TMP_Text modelo)
    {
        var hayDibujo = false;
        foreach (var imagen in _boton.GetComponentsInChildren<Image>(false))
        {
            hayDibujo |= imagen.sprite != null && imagen.color.a > 0.1f;
        }
        if (!hayDibujo)
        {
            var fondo = _boton.GetComponent<Image>() ?? _boton.gameObject.AddComponent<Image>();
            _fondoRespaldo = fondo;
            PonerTexturaDeMenu(fondo);
        }
        _texto = null;
        foreach (var letras in _boton.GetComponentsInChildren<TextMeshProUGUI>(false))
        {
            _texto ??= letras;
        }
        // Sin texto visible, los indicadores de velocidad del juego son los que muestran el estado.
        if (_texto == null && (_indicadorNormal == null || _indicadorRapido == null))
        {
            CrearEtiqueta(_boton, modelo, TamanoTexto / _boton.localScale.y);
        }
    }

    private static string Describir(RectTransform pieza, int nivel)
    {
        var imagen = pieza.GetComponent<Image>();
        var texto = pieza.name + (pieza.gameObject.activeSelf ? "" : "(apagado)") + (imagen?.sprite != null ? "[" + imagen.sprite.name + "]" : "");
        if (nivel >= 4)
        {
            return texto;
        }
        for (var i = 0; i < pieza.childCount; i++)
        {
            var hijo = pieza.GetChild(i).GetComponent<RectTransform>();
            if (hijo != null)
            {
                texto += " > " + Describir(hijo, nivel + 1);
            }
        }
        return texto;
    }

    private static void PonerTexturaDeMenu(Image fondo)
    {
        fondo.raycastTarget = false;
        // Al menos con la textura de los botones del juego, si está a la vista.
        foreach (var objeto in Object.FindObjectsOfType(Il2CppType.Of<Button>()))
        {
            var boton = objeto.Cast<Button>();
            var dibujo = boton.name == NombreBotonMenu ? boton.GetComponent<Image>() : null;
            if (dibujo != null && dibujo.sprite != null)
            {
                fondo.sprite = dibujo.sprite;
                fondo.type = dibujo.type;
                break;
            }
        }
    }

    private static void CrearEtiqueta(RectTransform padre, TMP_Text modelo, float tamano)
    {
        var etiqueta = new GameObject("Texto");
        var medidas = etiqueta.AddComponent<RectTransform>();
        medidas.SetParent(padre, false);
        medidas.anchorMin = Vector2.zero;
        medidas.anchorMax = Vector2.one;
        medidas.offsetMin = medidas.offsetMax = Vector2.zero;
        var texto = etiqueta.AddComponent<TextMeshProUGUI>();
        texto.font = modelo.font;
        texto.fontSize = tamano;
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = Color.white;
        texto.raycastTarget = false;
        _texto = texto;
    }

    private static RectTransform CrearRespaldo(TMP_Text modelo)
    {
        var caja = new GameObject("Boton");
        var rect = caja.AddComponent<RectTransform>();
        rect.SetParent(_raiz.transform, false);
        rect.sizeDelta = MedidaRespaldo;
        _fondoRespaldo = caja.AddComponent<Image>();
        PonerTexturaDeMenu(_fondoRespaldo);
        CrearEtiqueta(rect, modelo, TamanoTexto);
        return rect;
    }
}
