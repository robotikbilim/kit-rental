using KitRental.Web.Mvc.Services;

namespace KitRental.Web.Tests;

public sealed class QrCodeValueTests
{
    [Theory]
    [InlineData("QR-0001", "QR-0001")]
    [InlineData("  QR-0001  ", "QR-0001")]
    [InlineData("https://atolye.example/ariza/QR-0001", "QR-0001")]
    [InlineData("https://atolye.example/ariza/KIT%2D2026%2D001", "KIT-2026-001")]
    public void Normalize_extracts_identifier_from_public_fault_url(string value, string expected) =>
        Assert.Equal(expected, QrCodeValue.Normalize(value));
}
