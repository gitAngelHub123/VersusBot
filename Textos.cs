// Versus Bot — textos del mod según el idioma del juego.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace VersusBot;

internal static class Textos
{
    private const float SegundosEntreLecturas = 2f;
    private static bool _espanol;
    private static float _proximaLectura;
    private static IntPtr _campoInstancia;
    private static IntPtr _metodoLocalize;

    // El juego traduce "$LOCALE" al idioma activo: "Spanish_Spain", "English_United States", etc.
    private static bool Espanol
    {
        get
        {
            if (Time.unscaledTime >= _proximaLectura)
            {
                _proximaLectura = Time.unscaledTime + SegundosEntreLecturas;
                try
                {
                    var idioma = LeerIdioma();
                    _espanol = idioma.Length > 0 ? idioma.StartsWith("Spanish", StringComparison.Ordinal) : Application.systemLanguage == SystemLanguage.Spanish;
                }
                catch (Exception)
                {
                    _espanol = Application.systemLanguage == SystemLanguage.Spanish;
                }
            }
            return _espanol;
        }
    }

    internal static string JugadorVsBot => Espanol ? "Jugador vs Bot" : "Player vs Bot";
    internal static string JugadorVsJugador => Espanol ? "Jugador vs Jugador" : "Player vs Player";
    internal static string BotVsBot => "Bot vs Bot";
    internal static string Batallas => Espanol ? "Batallas Personalizadas" : "Custom Battles";
    internal static string Piscina => Espanol ? "Piscina" : "Pool";
    internal static string Jardin => Espanol ? "Jardín" : "Garden";
    internal static string TituloDificultad => Espanol ? "Dificultad del bot" : "Bot difficulty";
    internal static string Volver => Espanol ? "Volver" : "Back";
    internal static string NombreDelfin => Espanol ? "Zombi delfín" : "Dolphin Rider Zombie";
    internal static string NombreBuzo => Espanol ? "Zombi buceador" : "Snorkel Zombie";
    internal static string TextoDelfin => Espanol ? "Usa a su delfín para aprovechar las debilidades de tu defensa en la piscina." : "Uses his dolphin to exploit weaknesses in your pool defense.";
    internal static string TextoBuzo => Espanol ? "Nada bajo el agua para evitar los ataques." : "Swims underwater to avoid attacks.";
    internal static string AvisoQuickCollect => Espanol
        ? "Quick Collect está instalado y no es compatible con este mod: conviene quitarlo."
        : "Quick Collect is installed and is not compatible with this mod: you should remove it.";

    internal static string Nivel(int nivel)
    {
        if (nivel <= 15) return Espanol ? "bebé" : "baby";
        if (nivel <= 40) return Espanol ? "fácil" : "easy";
        if (nivel <= 65) return "normal";
        if (nivel <= 100) return Espanol ? "difícil" : "hard";
        if (nivel <= 150) return Espanol ? "muy difícil" : "very hard";
        return Espanol ? "extremo" : "extreme";
    }

    // El tipo Localizer no se puede usar desde C#: Il2CppInterop no genera su interfaz. Va por la API nativa.
    private static unsafe string LeerIdioma()
    {
        if (_campoInstancia == IntPtr.Zero || _metodoLocalize == IntPtr.Zero)
        {
            var localizer = IL2CPP.GetIl2CppClass("Tekly.Localizations.Runtime.dll", "Tekly.Localizations", "Localizer");
            if (localizer == IntPtr.Zero)
            {
                return "";
            }
            IntPtr interfaz = IntPtr.Zero, iterador = IntPtr.Zero, actual;
            while ((actual = IL2CPP.il2cpp_class_get_interfaces(localizer, ref iterador)) != IntPtr.Zero)
            {
                if (Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(actual)) == "ILocalizer")
                {
                    interfaz = actual;
                    break;
                }
            }
            if (interfaz == IntPtr.Zero)
            {
                return "";
            }
            var singleton = IL2CPP.GetIl2CppClass("Tekly.Common.Runtime.dll", "Tekly.Common.Utils", "Singleton`2");
            var tipo = Il2CppSystem.Type.internal_from_handle(IL2CPP.il2cpp_class_get_type(singleton))
                .MakeGenericType(new Il2CppReferenceArray<Il2CppSystem.Type>(new[]
                {
                    Il2CppSystem.Type.internal_from_handle(IL2CPP.il2cpp_class_get_type(localizer)),
                    Il2CppSystem.Type.internal_from_handle(IL2CPP.il2cpp_class_get_type(interfaz)),
                }));
            var clase = IL2CPP.il2cpp_class_from_type(tipo.TypeHandle.value);
            IL2CPP.il2cpp_runtime_class_init(clase);
            _campoInstancia = IL2CPP.GetIl2CppField(clase, "<Instance>k__BackingField");
            _metodoLocalize = IL2CPP.il2cpp_class_get_method_from_name(localizer, "Localize", 1);
            if (_campoInstancia == IntPtr.Zero || _metodoLocalize == IntPtr.Zero)
            {
                return "";
            }
        }
        var instancia = IntPtr.Zero;
        IL2CPP.il2cpp_field_static_get_value(_campoInstancia, &instancia);
        if (instancia == IntPtr.Zero)
        {
            return "";
        }
        void** argumentos = stackalloc void*[1];
        argumentos[0] = (void*)IL2CPP.ManagedStringToIl2Cpp("$LOCALE");
        var excepcion = IntPtr.Zero;
        var resultado = IL2CPP.il2cpp_runtime_invoke(_metodoLocalize, instancia, argumentos, ref excepcion);
        return excepcion != IntPtr.Zero ? "" : IL2CPP.Il2CppStringToManaged(resultado) ?? "";
    }
}
