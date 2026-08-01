using Logistics.BuildingBlocks.Domain;

namespace Logistics.Shipment.Domain;

/// <summary>Địa chỉ — value object, EF sẽ trải thành các cột (owned type).</summary>
public sealed class Address : ValueObject
{
    public string Line { get; }
    public string City { get; }
    public string PostalCode { get; }

    private Address() // EF materialization
    {
        Line = string.Empty;
        City = string.Empty;
        PostalCode = string.Empty;
    }

    public Address(string line, string city, string postalCode)
    {
        if (string.IsNullOrWhiteSpace(line))
            throw new ArgumentException("Address line required.", nameof(line));
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City required.", nameof(city));

        Line = line;
        City = city;
        PostalCode = postalCode ?? string.Empty;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Line;
        yield return City;
        yield return PostalCode;
    }
}
