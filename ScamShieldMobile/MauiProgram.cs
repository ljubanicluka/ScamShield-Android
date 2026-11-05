using ZXing.Net.Maui.Controls;

namespace ScamShieldMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader();

        return builder.Build();
    }
}