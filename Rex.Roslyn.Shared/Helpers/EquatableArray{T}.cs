// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Taken from https://github.com/CommunityToolkit/dotnet/blob/ecd1711b740f4f88d2bb943ce292ae4fc90df1bc/src/CommunityToolkit.Mvvm.SourceGenerators/Helpers/EquatableArray%7BT%7D.cs

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Rex.Roslyn.Shared.Helpers;

// ReSharper disable once RedundantNullableDirective
#nullable enable

/// <summary>Convenience wrappers for <see cref="EquatableArray{T}"/>.</summary>
public static class EquatableArray
{
    /// <summary>Value-equality view over <paramref name="array"/>.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="array">Source immutable array.</param>
    /// <returns>Wrapped copy.</returns>
    public static EquatableArray<T> AsEquatableArray<T>(this ImmutableArray<T> array)
        where T : IEquatable<T>
    {
        return new EquatableArray<T>(array);
    }
}

/// <summary>Immutable sequence with value equality, backed like <see cref="ImmutableArray{T}"/>.</summary>
/// <typeparam name="T">Element type, must implement <see cref="IEquatable{T}"/>.</typeparam>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    /// <summary>Storage laid out like <see cref="ImmutableArray{T}"/>.</summary>
    private readonly T[]? array;

    /// <summary>Wraps <paramref name="array"/> without allocating a new buffer.</summary>
    /// <param name="array">Immutable array to alias.</param>
    public EquatableArray(ImmutableArray<T> array)
    {
        this.array = Unsafe.As<ImmutableArray<T>, T[]?>(ref array);
    }

    /// <summary>ref readonly access at <paramref name="index"/>.</summary>
    /// <param name="index">Element position.</param>
    /// <returns>ref readonly to the element.</returns>
    public ref readonly T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref AsImmutableArray().ItemRef(index);
    }

    /// <summary>True when the backing array has length zero.</summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => AsImmutableArray().IsEmpty;
    }

    /// <inheritdoc/>
    public bool Equals(EquatableArray<T> array)
    {
        return AsSpan().SequenceEqual(array.AsSpan());
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is EquatableArray<T> array && Equals(this, array);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (this.array is not T[] array)
        {
            return 0;
        }

        HashCode hashCode = default;

        foreach (var item in array)
        {
            hashCode.Add(item);
        }

        return hashCode.ToHashCode();
    }

    /// <summary>ImmutableArray view over the same memory.</summary>
    /// <returns>Alias, not a copy.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableArray<T> AsImmutableArray()
    {
        return Unsafe.As<T[]?, ImmutableArray<T>>(ref Unsafe.AsRef(in array));
    }

    /// <summary>Factory equivalent to the constructor.</summary>
    /// <param name="array">Source immutable array.</param>
    /// <returns>Wrapped value.</returns>
    public static EquatableArray<T> FromImmutableArray(ImmutableArray<T> array)
    {
        return new EquatableArray<T>(array);
    }

    /// <summary>Span over the live buffer.</summary>
    /// <returns>Read only span over the elements.</returns>
    public ReadOnlySpan<T> AsSpan()
    {
        return AsImmutableArray().AsSpan();
    }

    /// <summary>Allocates a new mutable array with the same elements.</summary>
    /// <returns>Fresh heap array.</returns>
    public T[] ToArray()
    {
        return AsImmutableArray().ToArray();
    }

    /// <summary>Enumerator over the wrapped items.</summary>
    /// <returns><see cref="ImmutableArray{T}.Enumerator"/> from the backing array.</returns>
    public ImmutableArray<T>.Enumerator GetEnumerator()
    {
        return AsImmutableArray().GetEnumerator();
    }

    /// <inheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)AsImmutableArray()).GetEnumerator();
    }

    /// <summary>Wraps an immutable array.</summary>
    /// <returns>EquatableArray alias.</returns>
    public static implicit operator EquatableArray<T>(ImmutableArray<T> array)
    {
        return FromImmutableArray(array);
    }

    /// <summary>Unwraps to ImmutableArray.</summary>
    /// <returns>Shared storage view.</returns>
    public static implicit operator ImmutableArray<T>(EquatableArray<T> array)
    {
        return array.AsImmutableArray();
    }

    /// <summary>Sequence equality.</summary>
    /// <param name="left">First operand.</param>
    /// <param name="right">Second operand.</param>
    /// <returns>True when spans match element-wise.</returns>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>Negated sequence equality.</summary>
    /// <param name="left">First operand.</param>
    /// <param name="right">Second operand.</param>
    /// <returns>False when spans match element-wise.</returns>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }
}
