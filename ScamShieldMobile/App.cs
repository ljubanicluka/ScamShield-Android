namespace ScamShieldMobile;

public sealed class App : Application
{
    public App()
    {
        UserAppTheme = AppTheme.Unspecified;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage());
    }
}
