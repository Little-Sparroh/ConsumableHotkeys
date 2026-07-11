using System;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

/// <summary>
/// Soft dependency on ModSettingsMenu HudRepositionAPI (no hard assembly reference required).
/// Retries resolution if ModSettingsMenu was not loaded yet on first call.
/// </summary>
public static class HudRepositionClient
{
    private const string ApiTypeName = "HudRepositionAPI";
    private static Type _apiType;
    private static MethodInfo _register;
    private static MethodInfo _unregister;
    private static bool _available;

    public static bool IsAvailable
    {
        get
        {
            EnsureResolved();
            return _available;
        }
    }

    public static void Register(
        string id,
        string displayName,
        RectTransform rect,
        ConfigEntry<float> anchorX,
        ConfigEntry<float> anchorY)
    {
        if (!_available)
            EnsureResolved(force: true);

        if (!_available || rect == null || anchorX == null || anchorY == null)
            return;

        try
        {
            _register.Invoke(null, new object[] { id, displayName, rect, anchorX, anchorY });
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HudRepositionClient] Register failed: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public static void Unregister(string id)
    {
        if (!_available)
            EnsureResolved(force: true);

        if (!_available || string.IsNullOrEmpty(id))
            return;

        try
        {
            _unregister.Invoke(null, new object[] { id });
        }
        catch (Exception)
        {
        }
    }

    private static void EnsureResolved(bool force = false)
    {
        if (_available && !force)
            return;

        _apiType = null;
        _register = null;
        _unregister = null;
        _available = false;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = assembly.GetType(ApiTypeName, false);
                if (t != null)
                {
                    _apiType = t;
                    break;
                }
            }
            catch
            {
            }
        }

        if (_apiType == null)
            return;

        foreach (var m in _apiType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var ps = m.GetParameters();
            if (m.Name == "Register" && ps.Length == 5 &&
                ps[0].ParameterType == typeof(string) &&
                ps[1].ParameterType == typeof(string) &&
                typeof(RectTransform).IsAssignableFrom(ps[2].ParameterType))
            {
                _register = m;
            }
            else if (m.Name == "Unregister" && ps.Length == 1 && ps[0].ParameterType == typeof(string))
            {
                _unregister = m;
            }
        }

        _available = _register != null && _unregister != null;
    }
}
