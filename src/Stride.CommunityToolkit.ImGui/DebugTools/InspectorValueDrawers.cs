using System.Diagnostics.CodeAnalysis;
using static Hexa.NET.ImGui.ImGui;
using static Stride.CommunityToolkit.ImGui.ImGuiExtension;

namespace Stride.CommunityToolkit.ImGui.DebugTools;

/// <summary>
/// The leaf-value widgets <see cref="Inspector"/> draws: one ImGui control per primitive type, the
/// enum combo, and the bit twiddling enum editing needs. Split out of <see cref="Inspector"/>;
/// everything here is stateless.
/// </summary>
internal static class InspectorValueDrawers
{
    /// <summary>
    /// Draws the editor for a primitive scalar value. Returns <see langword="true"/> when
    /// <paramref name="value"/> is of a type handled here - whether or not it changed;
    /// <paramref name="valueChanged"/> says whether it did.
    /// </summary>
    internal static bool TryDrawScalar(ref object? value, out bool valueChanged)
    {
        switch (value)
        {
            // if(valueChanged) => to cast / generate garbage only when the value changed
            case bool v: valueChanged = Checkbox("", ref v); if (valueChanged) { value = v; } return true;
            case string v: valueChanged = InputText("", ref v, 99); if (valueChanged) { value = v; } return true;
            case float v: valueChanged = DragFloat("", ref v, RelativeDragSpeed(v)); if (valueChanged) { value = v; } return true;
            case double v: valueChanged = InputDouble("", ref v); if (valueChanged) { value = v; } return true;
            case int v: valueChanged = InputInt("", ref v); if (valueChanged) { value = v; } return true;
            default: return TryDrawIntegerScalar(ref value, out valueChanged);
        }
    }

    /// <summary>
    /// The scalar types ImGui has no native widget for: edited as int and cast back afterwards.
    /// </summary>
    private static bool TryDrawIntegerScalar(ref object? value, out bool valueChanged)
    {
        // Every case follows the same shape: edit through the closest type ImGui implements
        // natively (int), then cast back so the boxed value keeps its original type.
        switch (value)
        {
            // c = closest type that ImGui implements natively, manually cast it to the right type afterward
            case uint v: { int c = (int)v; valueChanged = InputInt("", ref c); if (valueChanged) { value = (uint)c; } return true; }
            case long v: { int c = (int)v; valueChanged = InputInt("", ref c); if (valueChanged) { value = (long)c; } return true; }
            case ulong v: { int c = (int)v; valueChanged = InputInt("", ref c); if (valueChanged) { value = (ulong)c; } return true; }
            case short v: { int c = (int)v; valueChanged = InputInt("", ref c); if (valueChanged) { value = (short)c; } return true; }
            case ushort v: { int c = (int)v; valueChanged = InputInt("", ref c); if (valueChanged) { value = (ushort)c; } return true; }
            case byte v: { int c = (int)v; valueChanged = InputInt("", ref c); if (valueChanged) { value = (byte)c; } return true; }
            case sbyte v: { int c = (int)v; valueChanged = InputInt("", ref c); if (valueChanged) { value = (sbyte)c; } return true; }
            default: valueChanged = false; return false;
        }
    }

    /// <summary>
    /// Draws the combo for an enum value, multi-select when the enum carries <see cref="FlagsAttribute"/>.
    /// </summary>
    internal static bool DrawEnum((bool flags, Array values) enumInfo, ref object? value)
    {
        bool valueChanged = false;
        (bool flags, Array values) = enumInfo;
        using (UCombo("", value?.ToString() ?? string.Empty, out bool open))
        {
            if (open)
            {
                foreach (object o in values)
                {
                    ulong fieldValue = GetEnumBits(value!);
                    ulong compValue = GetEnumBits(o);
                    bool selected;
                    if (flags)
                        selected = (fieldValue & compValue) == compValue;
                    else
                        selected = fieldValue == compValue;

                    if (Selectable(o.ToString(), selected))
                    {
                        if (flags)
                        {
                            if (selected) // unselect this value
                                compValue = fieldValue & ~compValue;
                            else // select new value
                                compValue = fieldValue | compValue;
                        }
                        value = GetEnumValueFromBits(compValue, value!.GetType());
                        valueChanged = true;
                    }
                }
            }
            return valueChanged;
        }
    }

    private static ulong GetEnumBits(object enumValue)
    {
        var valueType = enumValue.GetType();
        if (valueType.IsEnum)
            valueType = Enum.GetUnderlyingType(valueType);

        if (valueType == typeof(int)) return (ulong)(int)enumValue;
        if (valueType == typeof(uint)) return (ulong)(uint)enumValue;
        if (valueType == typeof(long)) return (ulong)(long)enumValue;
        if (valueType == typeof(ulong)) return (ulong)enumValue;
        if (valueType == typeof(short)) return (ulong)(short)enumValue;
        if (valueType == typeof(ushort)) return (ulong)(ushort)enumValue;
        if (valueType == typeof(byte)) return (ulong)(byte)enumValue;
        if (valueType == typeof(sbyte)) return (ulong)(sbyte)enumValue;

        throw new ArgumentException(valueType.ToString());
    }

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "The method exists to box an arbitrary enum's underlying value; there is no single concrete return type.")]
    private static object GetEnumValueFromBits(ulong bits, Type enumType)
    {
        if (enumType.IsEnum)
        {
            var valueType = Enum.GetUnderlyingType(enumType);
            if (valueType == typeof(int)) return (int)bits;
            if (valueType == typeof(uint)) return (uint)bits;
            if (valueType == typeof(long)) return (long)bits;
            if (valueType == typeof(ulong)) return bits;
            if (valueType == typeof(short)) return (short)bits;
            if (valueType == typeof(ushort)) return (ushort)bits;
            if (valueType == typeof(byte)) return (byte)bits;
            if (valueType == typeof(sbyte)) return (sbyte)bits;
        }

        throw new ArgumentException(enumType.ToString());
    }

    private static float RelativeDragSpeed(in float currentValue)
    {
        float finalSpeed = currentValue < 0f ? -currentValue : currentValue;
        finalSpeed *= 0.1f;
        return finalSpeed < 0.001f ? 0.001f : finalSpeed;
    }
}