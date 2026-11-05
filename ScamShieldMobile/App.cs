namespace ScamShieldMobile;

public sealed class App : Application
{
    public App()
    {
        UserAppTheme = AppTheme.Unspecified;
        MainPage = new MainPage();
    }
}