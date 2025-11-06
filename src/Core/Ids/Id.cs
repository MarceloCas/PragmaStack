using System;
using System.Collections.Generic;
using System.Text;

namespace PragmaStack.Core.Ids;

public readonly struct Id
    : IEquatable<Id>
{
    // Properties
    public Guid Value { get; }

    // Constructors
    private Id(Guid value)
    {
        Value = value;
    }

    // Public Methods
    public static Id GenerateNewId()
    {
        return new Id(Guid.CreateVersion7());
    }
    public static Id FromGuid(Guid guid)
    {
        return new Id(guid);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
    public override bool Equals(object? obj)
    {
        if (obj is not Id id)
            return false;

        return Equals(id);
    }
    public bool Equals(Id other)
    {
        return Value == other.Value;
    }

    // Operators
    public static implicit operator Guid(Id id) => id.Value;
    public static implicit operator Id(Guid guid) => FromGuid(guid);

    public static bool operator ==(Id left, Id right) => left.Value == right.Value;
    public static bool operator !=(Id left, Id right) => left.Value != right.Value;
    public static bool operator <(Id left, Id right) => left.Value.CompareTo(right.Value) < 0;
    public static bool operator >(Id left, Id right) => left.Value.CompareTo(right.Value) > 0;
    public static bool operator <=(Id left, Id right) => left.Value.CompareTo(right.Value) <= 0;
    public static bool operator >=(Id left, Id right) => left.Value.CompareTo(right.Value) >= 0;
}
