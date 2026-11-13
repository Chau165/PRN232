namespace PRN232.Application.Interfaces;

/// <summary>
/// Deterministic keyed hash ("blind index") of a CCCD number (A9). The number itself is stored
/// encrypted with randomized Data Protection, so equality checks such as "is this CCCD already
/// used by another account" compare this hash instead.
/// </summary>
public interface ICccdHasher
{
    string Hash(string cccdNumber);
}
