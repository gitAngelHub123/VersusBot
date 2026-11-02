// Versus Bot — botón de rival en la pantalla de elegir bando y pantalla de dificultad.
// Un clic abre la dificultad del bot; doble clic cambia entre bot y 2 jugadores.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using Il2CppInterop.Runtime;
using Il2CppReloaded;
using Il2CppTekly.DataModels.Binders;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VersusBot;

internal static class PanelRival
{
    private const float VentanaDobleClic = 0.35f;
    private const float EsperaEntreBusquedas = 0.3f;
    private const string ClaveFondo = "SPR_Almanac_BGPaper.png";
    private const string NombreBotonFila = "BotonModoJugadores";
    private static readonly Color FondoRespaldo = new Color(0.82f, 0.74f, 0.55f, 1f);
    private static readonly Color Tinta = new Color(0.2f, 0.13f, 0.06f, 1f);
    private static readonly Color Barra = new Color(0.33f, 0.25f, 0.14f, 1f);
    private static readonly Color Relleno = new Color(0.42f, 0.73f, 0.2f, 1f);
    private static readonly Vector2 MedidaBoton = new Vector2(380, 96);
    private const string NombreBotonMapa = "BotonCambiarMapa";
    private const string ContenedorOculto = "NestedMenuButton";
    private const string NombreBotonMenu = "P_BasicButton02_Menu";
    private const string ContenedorVisible = "Center";
    private const float SeparacionBotonMapa = 12f;
    // Evita que un doble clic cambie el mapa y lo devuelva enseguida.
    private const float EsperaEntreCambiosDeMapa = 0.8f;
    private static readonly Color ColorAviso = new Color(0.45f, 0.08f, 0.05f, 0.92f);
    private static readonly Vector2 MedidaAviso = new Vector2(1500, 100);

    private static GameObject _raiz;
    private static RectTransform _botonFila;
    private static RectTransform _botonFlotante;
    private static GameObject _pantalla;
    private static RectTransform _volver;
    private static RectTransform _barra;
    private static RectTransform _relleno;
    private static TextMeshProUGUI _textoNivel;
    private static bool _arrastrando;
    private static float _horaPrimerClic = -1f;
    private static float _siguienteBusqueda;
    private static Iluminado _iluminadoFila;
    private static Iluminado _iluminadoFlotante;
    private static RectTransform _botonMapa;
    private static RectTransform _botonMenu;
    private static Iluminado _iluminadoMapa;
    private static float _siguienteCambioDeMapa;

    internal static void Actualizar(bool visible)
    {
        if (!visible)
        {
            if (_raiz != null)
            {
                _raiz.SetActive(false);
                _pantalla.SetActive(false);
            }
            _arrastrando = false;
            _horaPrimerClic = -1f;
            return;
        }
        if (_raiz == null && !Crear())
        {
            return;
        }
        _raiz.SetActive(true);
        AsegurarBotonEnFila();
        AsegurarBotonMapa();
        MapaVersus.Asegurar();
        var filaVisible = _botonFila != null && _botonFila.gameObject.activeInHierarchy;
        // La fila se oculta sin el jugador 2 listo: este botón permite volver al modo de 1 jugador.
        _botonFlotante.gameObject.SetActive(!filaVisible && !VersusBotMod.ModoBot && !_pantalla.activeSelf);
        var pantallaAbierta = _pantalla.activeSelf;
        _iluminadoFila?.Actualizar(!pantallaAbierta);
        _iluminadoFlotante?.Actualizar(!pantallaAbierta);
        ActualizarBotonMapa(!pantallaAbierta);
        if (pantallaAbierta)
        {
            ActualizarPantalla();
        }
        else
        {
            ActualizarBoton(filaVisible ? _botonFila : _botonFlotante);
        }
        Refrescar();
    }

    private static void ActualizarBoton(RectTransform boton)
    {
        var raton = Mouse.current;
        if (raton == null || !boton.gameObject.activeInHierarchy)
        {
            return;
        }
        var ahora = Time.unscaledTime;
        if (raton.leftButton.wasPressedThisFrame && RectTransformUtility.RectangleContainsScreenPoint(boton, raton.position.ReadValue(), CamaraDe(boton)))
        {
            if (_horaPrimerClic >= 0f)
            {
                _horaPrimerClic = -1f;
                VersusBotMod.CambiarModo();
                return;
            }
            _horaPrimerClic = ahora;
        }
        if (_horaPrimerClic >= 0f && ahora - _horaPrimerClic > VentanaDobleClic)
        {
            _horaPrimerClic = -1f;
            if (VersusBotMod.ModoBot)
            {
                _pantalla.SetActive(true);
            }
        }
    }

    // El botón de mapa es una copia del de Menú, a su izquierda. Se llama como el mapa al que cambia.
    private static void AsegurarBotonMapa()
    {
        if (_botonMapa != null)
        {
            return;
        }
        // Se espera al Menú visible: copiar el del contenedor oculto no mostraría nada.
        var menu = BuscarBotonMenu();
        if (menu == null || EstaEnContenedorOculto(menu.GetComponent<RectTransform>()))
        {
            return;
        }
        _botonMenu = menu.GetComponent<RectTransform>();
        var copia = Object.Instantiate(menu, menu.transform.parent);
        copia.name = NombreBotonMapa;
        _botonMapa = copia.GetComponent<RectTransform>();
        _iluminadoMapa = new Iluminado(copia);
        Limpiar(copia, _iluminadoMapa.AnimadorUsado);
        copia.AddComponent<LayoutElement>().ignoreLayout = true;
        copia.SetActive(true);
        VersusBotMod.Detalle("Botón de mapa junto a '" + menu.name + "' en '" + menu.transform.parent.name + "', medida " + _botonMenu.rect.size + ", posición " + _botonMenu.anchoredPosition);
    }

    // La primera vez, el juego tiene otro botón Menú en un contenedor que no se muestra.
    private static bool EstaEnContenedorOculto(RectTransform boton)
    {
        return boton != null && boton.parent != null && boton.parent.name.StartsWith(ContenedorOculto);
    }

    private static GameObject BuscarBotonMenu()
    {
        GameObject oculto = null;
        foreach (var objeto in Object.FindObjectsOfType(Il2CppType.Of<TMP_Text>()))
        {
            var etiqueta = objeto.Cast<TMP_Text>().text?.Trim().ToLowerInvariant();
            if (etiqueta == "menú" || etiqueta == "menu")
            {
                var boton = objeto.Cast<TMP_Text>().GetComponentInParent<Button>();
                if (boton != null)
                {
                    if (!EstaEnContenedorOculto(boton.GetComponent<RectTransform>()))
                    {
                        return boton.gameObject;
                    }
                    oculto = boton.gameObject;
                }
            }
        }
        // En otros idiomas el texto cambia: se busca por su nombre, solo dentro del contenedor visible.
        foreach (var objeto in Object.FindObjectsOfType(Il2CppType.Of<Button>()))
        {
            var boton = objeto.Cast<Button>();
            if (boton.name == NombreBotonMenu && boton.transform.parent != null && boton.transform.parent.name == ContenedorVisible)
            {
                return boton.gameObject;
            }
        }
        if (oculto != null)
        {
            return oculto;
        }
        return BuscarBoton(Constants.DataModel.MENU_BUTTON, false) ?? BuscarBoton(Constants.DataModel.BACK, false);
    }

    private static void ActualizarBotonMapa(bool permitido)
    {
        if (_botonMapa == null || _botonMenu == null)
        {
            return;
        }
        _botonMapa.anchorMin = _botonMenu.anchorMin;
        _botonMapa.anchorMax = _botonMenu.anchorMax;
        _botonMapa.pivot = _botonMenu.pivot;
        _botonMapa.sizeDelta = _botonMenu.sizeDelta;
        _botonMapa.anchoredPosition = _botonMenu.anchoredPosition - new Vector2(_botonMenu.rect.width + SeparacionBotonMapa, 0);
        PonerTexto(_botonMapa, MapaVersus.EtiquetaBoton);
        _iluminadoMapa?.Actualizar(permitido);
        var raton = Mouse.current;
        if (permitido && raton != null && raton.leftButton.wasPressedThisFrame && Time.unscaledTime >= _siguienteCambioDeMapa
            && _botonMapa.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(_botonMapa, raton.position.ReadValue(), CamaraDe(_botonMapa)))
        {
            _siguienteCambioDeMapa = Time.unscaledTime + EsperaEntreCambiosDeMapa;
            MapaVersus.Cambiar();
        }
    }

    private static Camera CamaraDe(RectTransform rect)
    {
        var lienzo = rect.GetComponentInParent<Canvas>();
        if (lienzo == null || lienzo.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }
        return lienzo.rootCanvas.worldCamera;
    }

    // Inserta el botón entre Batalla personalizada y Personaliza todo, con el mismo aspecto.
    // Dos botones en uno: se conserva Personalizar todo, que muestra todas las cartas, con otro nombre
    // y en el lugar de Batalla personalizada, que se esconde. Quedan 4 botones.
    // El juego avisa al enlazar cada botón: se esconde ahí, antes de que la pantalla llegue a dibujarse.
    private const string FilaDeBotones = "SelectionSets";
    private static GameObject _botonTodo;
    private static GameObject _botonPersonalizada;
    private static bool _ordenados;

    internal static void BotonEnlazado(string ruta, GameObject boton)
    {
        if (ruta == null || boton == null || boton.transform.parent == null || boton.transform.parent.name != FilaDeBotones)
        {
            return;
        }
        if (TerminaEnClave(ruta, Constants.DataModel.CUSTOM))
        {
            _botonPersonalizada = boton;
            _ordenados = false;
            boton.SetActive(false);
        }
        else if (TerminaEnClave(ruta, Constants.DataModel.CUSTOM_ALL))
        {
            _botonTodo = boton;
            _ordenados = false;
        }
        UnirBotonesPersonalizados();
    }

    // Se repite antes de dibujar cada cuadro, por si el juego vuelve a mostrar el botón.
    internal static void AntesDeDibujar()
    {
        if (_botonTodo != null && _botonTodo.activeInHierarchy && _botonFila == null)
        {
            AsegurarBotonEnFila();
            PonerTexto(_botonFila, EtiquetaModo());
        }
        // Se revisa en cada cuadro mientras se ve la fila, y se deja en su lugar antes de dibujarlo.
        if (_botonTodo != null && _botonTodo.activeInHierarchy && _botonMapa == null)
        {
            AsegurarBotonMapa();
            ActualizarBotonMapa(false);
        }
        UnirBotonesPersonalizados();
    }

    private static string EtiquetaModo()
    {
        return VersusBotMod.Modo == ModoJuego.UnJugador ? Textos.JugadorVsBot
            : VersusBotMod.Modo == ModoJuego.DosJugadores ? Textos.JugadorVsJugador : Textos.BotVsBot;
    }

    private static void UnirBotonesPersonalizados()
    {
        if (_botonPersonalizada != null && _botonPersonalizada.activeSelf)
        {
            _botonPersonalizada.SetActive(false);
        }
        if (_botonTodo == null)
        {
            return;
        }
        PonerTexto(_botonTodo.GetComponent<RectTransform>(), Textos.Batallas);
        // El botón de modo es una copia de este: se reordena después de crearla, para que quede a su izquierda.
        if (!_ordenados && _botonPersonalizada != null && _botonFila != null)
        {
            _ordenados = true;
            _botonTodo.transform.SetSiblingIndex(_botonPersonalizada.transform.GetSiblingIndex());
        }
    }

    private static void AsegurarBotonEnFila()
    {
        // Si el juego ya avisó del botón no hay que buscarlo ni esperar: se crea antes de dibujar la fila.
        var conocido = _botonTodo != null && _botonTodo.activeInHierarchy;
        if (_botonFila != null || (!conocido && Time.unscaledTime < _siguienteBusqueda))
        {
            return;
        }
        if (!conocido)
        {
            _siguienteBusqueda = Time.unscaledTime + EsperaEntreBusquedas;
        }
        var personalizaTodo = conocido ? _botonTodo : BuscarBoton(Constants.DataModel.CUSTOM_ALL, false);
        if (personalizaTodo == null)
        {
            return;
        }
        var original = personalizaTodo.transform;
        var padre = original.parent;
        var copia = Object.Instantiate(personalizaTodo, padre);
        copia.name = NombreBotonFila;
        copia.transform.SetSiblingIndex(original.GetSiblingIndex());
        _botonFila = copia.GetComponent<RectTransform>();
        _iluminadoFila = new Iluminado(copia);
        Limpiar(copia, _iluminadoFila.AnimadorUsado);
        copia.SetActive(true);
        var conDistribucion = padre.GetComponent<HorizontalOrVerticalLayoutGroup>() != null;
        if (!conDistribucion)
        {
            RepartirFila(padre);
        }
        VersusBotMod.Detalle("Botón agregado a '" + padre.name + "' (distribución automática: " + conDistribucion + "): " + DescribirFila(padre));
    }

    // Sin distribución automática, los botones se reparten a lo ancho de la misma línea.
    private static void RepartirFila(Transform padre)
    {
        var botones = new System.Collections.Generic.List<RectTransform>();
        for (var i = 0; i < padre.childCount; i++)
        {
            var hijo = padre.GetChild(i);
            if (hijo.gameObject.activeSelf && (hijo.name == NombreBotonFila || hijo.GetComponent<Button>() != null))
            {
                botones.Add(hijo.GetComponent<RectTransform>());
            }
        }
        if (botones.Count < 3)
        {
            return;
        }
        var primero = botones[0].anchoredPosition;
        var ultimo = botones[botones.Count - 1].anchoredPosition;
        if (botones[botones.Count - 1] == _botonFila)
        {
            ultimo = botones[botones.Count - 2].anchoredPosition;
        }
        for (var i = 0; i < botones.Count; i++)
        {
            botones[i].anchoredPosition = Vector2.Lerp(primero, ultimo, i / (float)(botones.Count - 1));
        }
    }

    private static string DescribirFila(Transform padre)
    {
        var texto = "";
        for (var i = 0; i < padre.childCount; i++)
        {
            var hijo = padre.GetChild(i).GetComponent<RectTransform>();
            if (hijo != null)
            {
                texto += hijo.name + hijo.anchoredPosition + hijo.rect.size + " ";
            }
        }
        return texto;
    }

    private static GameObject BuscarBoton(string clave, bool incluirInactivos)
    {
        var binders = incluirInactivos
            ? Resources.FindObjectsOfTypeAll(Il2CppType.Of<UnityButtonBinder>())
            : Object.FindObjectsOfType(Il2CppType.Of<UnityButtonBinder>());
        foreach (var objeto in binders)
        {
            var binder = objeto.Cast<UnityButtonBinder>();
            if (binder.m_key != null && binder.m_button != null && TerminaEnClave(binder.m_key.Path, clave))
            {
                return binder.m_button.gameObject;
            }
        }
        binders = incluirInactivos
            ? Resources.FindObjectsOfTypeAll(Il2CppType.Of<ButtonBinder>())
            : Object.FindObjectsOfType(Il2CppType.Of<ButtonBinder>());
        foreach (var objeto in binders)
        {
            var binder = objeto.Cast<ButtonBinder>();
            if (binder.m_key != null && binder.m_button != null && TerminaEnClave(binder.m_key.Path, clave))
            {
                return binder.m_button.gameObject;
            }
        }
        return null;
    }

    private static bool TerminaEnClave(string ruta, string clave)
    {
        return ruta == clave || ruta.EndsWith("." + clave);
    }

    // Deja solo imagen, texto y distribución: el botón original envía eventos al sistema de
    // pantallas del juego, y si la copia los conserva, un clic puede cambiar o romper el menú.
    private static void Limpiar(GameObject copia, Animator conservar)
    {
        foreach (var animador in copia.GetComponentsInChildren<Animator>(true))
        {
            if (animador != conservar)
            {
                Object.DestroyImmediate(animador);
            }
        }
        foreach (var componente in copia.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var nombre = componente.GetIl2CppType().Name;
            if (componente.TryCast<Graphic>() == null && !nombre.Contains("Layout") && !nombre.Contains("Fitter") && !nombre.Contains("Mask"))
            {
                Object.DestroyImmediate(componente);
            }
        }
    }

    private static void ActualizarPantalla()
    {
        var teclado = Keyboard.current;
        var raton = Mouse.current;
        if (raton == null)
        {
            return;
        }
        var pos = raton.position.ReadValue();
        if (raton.leftButton.wasPressedThisFrame)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(_volver, pos, null))
            {
                Cerrar();
                return;
            }
            _arrastrando = RectTransformUtility.RectangleContainsScreenPoint(_barra, pos, null);
        }
        if (teclado != null && teclado[Key.Escape].wasPressedThisFrame)
        {
            Cerrar();
            return;
        }
        if (_arrastrando)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_barra, pos, null, out var local);
            var avance = Mathf.Clamp01((local.x - _barra.rect.xMin) / _barra.rect.width);
            VersusBotMod.Nivel = Mathf.RoundToInt(Mathf.Lerp(1f, VersusBotMod.NivelMaximo, avance));
            if (raton.leftButton.wasReleasedThisFrame)
            {
                _arrastrando = false;
                VersusBotMod.GuardarAjustes();
            }
        }
    }

    private static void Cerrar()
    {
        _arrastrando = false;
        _pantalla.SetActive(false);
        VersusBotMod.GuardarAjustes();
    }

    private static void Refrescar()
    {
        var nivel = VersusBotMod.Nivel;
        var etiqueta = EtiquetaModo();
        PonerTexto(_botonFila, etiqueta);
        PonerTexto(_botonFlotante, etiqueta);
        _relleno.anchorMax = new Vector2((nivel - 1) / (VersusBotMod.NivelMaximo - 1f), 1f);
        _textoNivel.text = nivel + " (" + NombreNivel(nivel) + ")";
    }

    private static void PonerTexto(RectTransform boton, string etiqueta)
    {
        if (boton == null)
        {
            return;
        }
        foreach (var texto in boton.GetComponentsInChildren<TMP_Text>(true))
        {
            texto.text = etiqueta;
        }
    }

    private static string NombreNivel(int nivel) => Textos.Nivel(nivel);

    private static bool Crear()
    {
        var modelo = Object.FindObjectOfType(Il2CppType.Of<TMP_Text>())?.TryCast<TMP_Text>();
        if (modelo == null)
        {
            return false;
        }
        var fuente = modelo.font;

        _raiz = new GameObject("PanelRivalVersusBot");
        Object.DontDestroyOnLoad(_raiz);
        var lienzo = _raiz.AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.sortingOrder = 1000;
        var escala = _raiz.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);
        _raiz.AddComponent<GraphicRaycaster>();

        var plantilla = BuscarBoton(Constants.DataModel.QUICK_PLAY, true);
        _botonFlotante = CrearBoton(plantilla, _raiz.transform, fuente, "BotonFlotante", new Vector2(0, -400));
        if (VersusBotMod.ModIncompatibleInstalado)
        {
            CrearAviso(fuente);
        }
        CrearPantalla(plantilla, fuente);
        return true;
    }

    // Se crea antes que la pantalla de dificultad para que esta lo tape al abrirse.
    private static void CrearAviso(TMP_FontAsset fuente)
    {
        var caja = CrearCaja("AvisoModIncompatible", _raiz.transform, ColorAviso);
        caja.raycastTarget = false;
        var rect = caja.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = MedidaAviso;
        rect.anchoredPosition = new Vector2(0, 480);
        CrearTexto("Texto", rect, fuente, Textos.AvisoQuickCollect, 30, Vector2.zero, MedidaAviso, Color.white);
    }

    private static void CrearPantalla(GameObject plantilla, TMP_FontAsset fuente)
    {
        var fondo = CrearCaja("PantallaDificultad", _raiz.transform, Color.white);
        _pantalla = fondo.gameObject;
        var rect = fondo.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var sprite = CargarFondo();
        if (sprite != null)
        {
            fondo.sprite = sprite;
        }
        else
        {
            fondo.color = FondoRespaldo;
        }

        CrearTexto("Titulo", rect, fuente, Textos.TituloDificultad, 64, new Vector2(0, 260), new Vector2(1200, 90), Tinta);
        _textoNivel = CrearTexto("Nivel", rect, fuente, "", 48, new Vector2(0, 110), new Vector2(1000, 70), Tinta);

        var barra = CrearCaja("Barra", rect, Barra);
        _barra = barra.rectTransform;
        _barra.anchorMin = _barra.anchorMax = new Vector2(0.5f, 0.5f);
        _barra.sizeDelta = new Vector2(1000, 44);
        _barra.anchoredPosition = new Vector2(0, 20);

        var relleno = CrearCaja("Relleno", _barra, Relleno);
        relleno.raycastTarget = false;
        _relleno = relleno.rectTransform;
        _relleno.anchorMin = Vector2.zero;
        _relleno.anchorMax = Vector2.one;
        _relleno.offsetMin = _relleno.offsetMax = Vector2.zero;

        _volver = CrearBoton(plantilla, rect, fuente, "Volver", new Vector2(0, -260));
        foreach (var texto in _volver.GetComponentsInChildren<TMP_Text>(true))
        {
            texto.text = Textos.Volver;
        }
        _pantalla.SetActive(false);
    }

    private static Sprite CargarFondo()
    {
        try
        {
            var clave = new Il2CppSystem.Object(IL2CPP.ManagedStringToIl2Cpp(ClaveFondo));
            return Addressables.LoadAssetAsync<Sprite>(clave).WaitForCompletion();
        }
        catch (System.Exception e)
        {
            VersusBotMod.Log.Warning("No se pudo cargar el fondo del almanaque: " + e.Message);
            return null;
        }
    }

    private static RectTransform CrearBoton(GameObject plantilla, Transform padre, TMP_FontAsset fuente, string nombre, Vector2 posicion)
    {
        RectTransform rect;
        if (plantilla != null)
        {
            var copia = Object.Instantiate(plantilla, padre);
            copia.name = nombre;
            copia.SetActive(true);
            var iluminado = new Iluminado(copia);
            if (nombre == "BotonFlotante")
            {
                _iluminadoFlotante = iluminado;
            }
            Limpiar(copia, iluminado.AnimadorUsado);
            rect = copia.GetComponent<RectTransform>();
        }
        else
        {
            var caja = CrearCaja(nombre, padre, Relleno);
            rect = caja.rectTransform;
            CrearTexto("Texto", rect, fuente, "", 32, new Vector2(0, 0), MedidaBoton, Color.white);
        }
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.sizeDelta = MedidaBoton;
        rect.anchoredPosition = posicion;
        return rect;
    }

    private static Image CrearCaja(string nombre, Transform padre, Color color)
    {
        var objeto = new GameObject(nombre);
        var rect = objeto.AddComponent<RectTransform>();
        rect.SetParent(padre, false);
        var imagen = objeto.AddComponent<Image>();
        imagen.color = color;
        return imagen;
    }

    private static TextMeshProUGUI CrearTexto(string nombre, Transform padre, TMP_FontAsset fuente, string contenido, int tamano, Vector2 posicion, Vector2 medida, Color color)
    {
        var objeto = new GameObject(nombre);
        var rect = objeto.AddComponent<RectTransform>();
        rect.SetParent(padre, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = medida;
        rect.anchoredPosition = posicion;
        var texto = objeto.AddComponent<TextMeshProUGUI>();
        texto.font = fuente;
        texto.fontSize = tamano;
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = color;
        texto.raycastTarget = false;
        texto.text = contenido;
        return texto;
    }

    // Ilumina la copia al pasar el ratón como lo hace el botón original, pero siempre,
    // aunque los bandos no estén listos. La copia ya no es un botón, así que lo hace el mod.
    private sealed class Iluminado
    {
        private const float DuracionTinte = 0.1f;
        private const float EscalaRespaldo = 1.05f;
        private static readonly Color TinteRespaldo = new Color(1.25f, 1.25f, 1.25f, 1f);

        private readonly RectTransform _rect;
        private readonly Selectable.Transition _transicion;
        private readonly Graphic _grafico;
        private readonly Image _imagen;
        private readonly Sprite _spriteEncima;
        private readonly Color _colorNormal = Color.white;
        private readonly Color _colorEncima = TinteRespaldo;
        private readonly string _disparadorNormal;
        private readonly string _disparadorEncima;
        private readonly Vector3 _escalaNormal;
        private bool _encima;

        internal Animator AnimadorUsado { get; }

        internal Iluminado(GameObject copia)
        {
            _rect = copia.GetComponent<RectTransform>();
            _escalaNormal = _rect.localScale;
            var boton = copia.GetComponent<Selectable>();
            if (boton == null)
            {
                _transicion = Selectable.Transition.None;
                _grafico = copia.GetComponentInChildren<Image>(true);
                return;
            }
            _transicion = boton.transition;
            _grafico = boton.targetGraphic ?? copia.GetComponentInChildren<Image>(true);
            _imagen = _grafico?.TryCast<Image>();
            switch (_transicion)
            {
                case Selectable.Transition.SpriteSwap:
                    _spriteEncima = boton.spriteState.highlightedSprite;
                    break;
                case Selectable.Transition.ColorTint:
                    _colorNormal = boton.colors.normalColor;
                    _colorEncima = boton.colors.highlightedColor;
                    break;
                case Selectable.Transition.Animation:
                    AnimadorUsado = boton.animator;
                    _disparadorNormal = boton.animationTriggers.normalTrigger;
                    _disparadorEncima = boton.animationTriggers.highlightedTrigger;
                    break;
            }
            VersusBotMod.Detalle("Iluminado del botón: transición=" + _transicion + " gráfico=" + (_grafico == null ? "ninguno" : _grafico.name)
                + " spriteEncima=" + (_spriteEncima != null) + " animador=" + (AnimadorUsado != null) + " colorEncima=" + _colorEncima);
        }

        internal void Actualizar(bool permitido)
        {
            var raton = Mouse.current;
            var encima = permitido && raton != null && _rect != null && _rect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(_rect, raton.position.ReadValue(), CamaraDe(_rect));
            if (encima == _encima)
            {
                return;
            }
            _encima = encima;
            switch (_transicion)
            {
                case Selectable.Transition.SpriteSwap when _imagen != null && _spriteEncima != null:
                    _imagen.overrideSprite = encima ? _spriteEncima : null;
                    break;
                case Selectable.Transition.ColorTint when _grafico != null:
                    _grafico.CrossFadeColor(encima ? _colorEncima : _colorNormal, DuracionTinte, true, true);
                    break;
                case Selectable.Transition.Animation when AnimadorUsado != null:
                    AnimadorUsado.SetTrigger(encima ? _disparadorEncima : _disparadorNormal);
                    break;
                default:
                    _rect.localScale = encima ? _escalaNormal * EscalaRespaldo : _escalaNormal;
                    _grafico?.CrossFadeColor(encima ? TinteRespaldo : Color.white, DuracionTinte, true, true);
                    break;
            }
        }
    }
}
